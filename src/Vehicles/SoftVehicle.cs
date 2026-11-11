using System.Collections.Generic;
using Gearheart.Physics;
using Godot;

namespace Gearheart.Vehicles;

/// <summary>
/// Physics approach B (chosen 2026-11-11): node-and-beam soft body, the idea BeamNG uses, in 2D.
/// The chassis is a lattice of mass points joined by beams. Beams that stretch past
/// their yield point bend permanently (dents); after too much total bend they snap.
/// Wheels are hub nodes on soft suspension beams with their own spin and tyre friction.
///
/// The vehicle does not step itself: <see cref="SoftPhysicsWorld"/> steps every soft body
/// together so they can collide with each other and with loose rigid objects.
/// </summary>
public partial class SoftVehicle : Node2D, IVehicle
{
    [Export] public bool PlayerControlled;
    [Export] public float AutoThrottle;
    [Export] public Vector2 Spawn = new(200f, -150f);
    [Export] public Color BodyColor = new(0.25f, 0.55f, 0.85f);
    [Export] public float WheelTorque = 20000f;
    [Export] public float MaxWheelSpin = 60f;       // rad/s
    [Export] public float AirControl = 9f;          // rad/s² of tilt
    [Export] public float TyreGrip = 1.1f;

    public SoftPhysicsWorld World { get; set; }

    public string PhysicsName => "B: soft body";
    public Vector2 CenterPosition => BodyCenter();
    public float Speed { get; private set; }
    public float Damage { get; private set; }

    public const float NodeRadius = 3f;
    public const float WheelRadius = 20f;
    public const int BodyNodeCount = 16;          // 0-6 floor, 7-13 roof line, 14-15 cabin top
    public const int RearHub = 16, FrontHub = 17;

    /// <summary>Body outline, in order, used for car-to-car collisions.</summary>
    public static readonly int[] Outline = { 0, 1, 2, 3, 4, 5, 6, 13, 12, 11, 15, 14, 9, 8, 7 };

    private const float Gravity = 980f;
    private const float BodyNodeMass = 0.2f;
    private const float HubMass = 1f;
    private const float AirDrag = 0.99998f;        // per substep (about 2% per second)
    private const float GroundFriction = 0.3f;     // scraping body nodes
    private const float SuspensionDamping = 0.15f;
    private const float BodyStiffness = 0.5f;
    private const float SuspensionStiffness = 0.03f;
    private const float BodyYield = 0.015f, BodyBreak = 0.18f;         // break = total permanent bend
    private const float SuspensionYield = 0.3f, SuspensionBreak = 0.4f;

    private sealed class Beam
    {
        public int A, B;
        public float Rest, OriginalRest, Stiffness;
        public bool Suspension, Broken;
    }

    private sealed class Wheel
    {
        public int Hub;
        public float Spin, Angle;
        public bool Attached = true;
    }

    // Node state, shared with SoftPhysicsWorld for collisions.
    public Vector2[] Pos { get; private set; }
    public Vector2[] Prev { get; private set; }
    public float[] InvMass { get; private set; }

    private readonly List<Beam> _beams = new();
    private readonly Wheel[] _wheels = { new() { Hub = RearHub }, new() { Hub = FrontHub } };
    private float _totalMass;
    private VehicleInput _input;
    private Vector2 _centerAtTickStart;

    private static readonly int[] BodyTriangles =
    {
        0, 1, 8, 0, 8, 7, 1, 2, 9, 1, 9, 8, 2, 3, 10, 2, 10, 9,
        3, 4, 11, 3, 11, 10, 4, 5, 12, 4, 12, 11, 5, 6, 13, 5, 13, 12,
    };
    private static readonly int[] CabinTriangles = { 9, 10, 14, 10, 15, 14, 10, 11, 15 };

    public static float RadiusOf(int node) => node < BodyNodeCount ? NodeRadius : WheelRadius;

    public override void _Ready()
    {
        float[] xs = { -65f, -43f, -22f, 0f, 22f, 43f, 65f };
        var layout = new List<Vector2>();
        foreach (float x in xs) layout.Add(new Vector2(x, 14f));    // 0-6 floor
        foreach (float x in xs) layout.Add(new Vector2(x, -14f));   // 7-13 roof line
        layout.Add(new Vector2(-12f, -38f));                         // 14 cabin
        layout.Add(new Vector2(10f, -38f));                          // 15 cabin
        layout.Add(new Vector2(-46f, 40f));                          // 16 rear hub
        layout.Add(new Vector2(46f, 40f));                           // 17 front hub

        int count = layout.Count;
        Pos = new Vector2[count];
        Prev = new Vector2[count];
        InvMass = new float[count];
        for (int i = 0; i < count; i++)
        {
            Pos[i] = Prev[i] = Spawn + layout[i];
            float mass = i < BodyNodeCount ? BodyNodeMass : HubMass;
            InvMass[i] = 1f / mass;
            _totalMass += mass;
        }

        // Chassis lattice: floor, roof line, verticals and both diagonals of every cell.
        for (int i = 0; i < 6; i++)
        {
            Link(i, i + 1);
            Link(7 + i, 8 + i);
            Link(i, 8 + i);
            Link(i + 1, 7 + i);
        }
        for (int i = 0; i < 7; i++) Link(i, 7 + i);

        // Cabin truss.
        Link(14, 15); Link(9, 14); Link(10, 14); Link(10, 15); Link(11, 15); Link(9, 15); Link(11, 14);

        // Suspension: each hub hangs from three floor nodes.
        foreach (int n in new[] { 0, 1, 2 }) Link(RearHub, n, SuspensionStiffness, true);
        foreach (int n in new[] { 4, 5, 6 }) Link(FrontHub, n, SuspensionStiffness, true);

        World?.Register(this);
    }

    public override void _ExitTree() => World?.Unregister(this);

    private void Link(int a, int b, float stiffness = BodyStiffness, bool suspension = false)
    {
        float rest = (Pos[b] - Pos[a]).Length();
        _beams.Add(new Beam
        {
            A = a,
            B = b,
            Rest = rest,
            OriginalRest = rest,
            Stiffness = stiffness,
            Suspension = suspension,
        });
    }

    // ---- Stepping, called by SoftPhysicsWorld -------------------------------------------

    public void BeginTick()
    {
        _input = PlayerControlled ? VehicleInput.ReadPlayer() : VehicleInput.Auto(AutoThrottle);
        _centerAtTickStart = BodyCenter();
    }

    /// <summary>Verlet integration with gravity, plus air control.</summary>
    public void Integrate(float dt)
    {
        Vector2 gravityStep = new Vector2(0f, Gravity) * dt * dt;
        for (int i = 0; i < Pos.Length; i++)
        {
            Vector2 velocity = (Pos[i] - Prev[i]) * AirDrag;
            Prev[i] = Pos[i];
            Pos[i] += velocity + gravityStep;
        }

        if (!Mathf.IsZeroApprox(_input.Tilt))
        {
            Vector2 c = BodyCenter();
            float spin = _input.Tilt * AirControl * dt * dt;
            for (int i = 0; i < Pos.Length; i++)
            {
                Vector2 r = Pos[i] - c;
                Pos[i] += new Vector2(-r.Y, r.X) * spin;
            }
        }
    }

    /// <summary>One constraint pass: beams, then push every node out of the terrain.</summary>
    public void SolveIteration(ITerrainCollider terrain)
    {
        foreach (var beam in _beams)
            if (!beam.Broken) SolveBeam(beam);

        for (int i = 0; i < Pos.Length; i++)
        {
            if (terrain.Collide(Pos[i], RadiusOf(i), out Vector2 n, out float depth))
                Pos[i] += n * depth;
        }
    }

    /// <summary>Velocity fixes after the constraint passes: damping, friction, tyres, damage.</summary>
    public void EndSubstep(float dt, ITerrainCollider terrain)
    {
        DampSuspension();

        for (int i = 0; i < BodyNodeCount; i++)
        {
            if (!terrain.Collide(Pos[i], NodeRadius + 0.5f, out Vector2 n, out _)) continue;
            Vector2 v = Pos[i] - Prev[i];
            Vector2 tangential = v - n * v.Dot(n);
            Prev[i] = Pos[i] - tangential * (1f - GroundFriction);
        }

        foreach (var wheel in _wheels)
            UpdateWheel(wheel, dt, terrain);

        ApplyDeformation();
    }

    public void EndTick(float delta)
    {
        Speed = (BodyCenter() - _centerAtTickStart).Length() / delta;
        QueueRedraw();
    }

    /// <summary>Axis-aligned bounds of all nodes including wheel radius, for broad-phase checks.</summary>
    public Rect2 Bounds()
    {
        Vector2 min = Pos[0], max = Pos[0];
        foreach (var p in Pos)
        {
            min = new Vector2(Mathf.Min(min.X, p.X), Mathf.Min(min.Y, p.Y));
            max = new Vector2(Mathf.Max(max.X, p.X), Mathf.Max(max.Y, p.Y));
        }
        return new Rect2(min, max - min).Grow(WheelRadius);
    }

    // ---- Internals -------------------------------------------------------------------------

    private void SolveBeam(Beam beam)
    {
        Vector2 d = Pos[beam.B] - Pos[beam.A];
        float length = d.Length();
        if (length < 1e-4f) return;

        float wa = InvMass[beam.A], wb = InvMass[beam.B];
        float w = wa + wb;
        float correction = (length - beam.Rest) / length * beam.Stiffness;
        Pos[beam.A] += d * correction * (wa / w);
        Pos[beam.B] -= d * correction * (wb / w);
    }

    private void DampSuspension()
    {
        foreach (var beam in _beams)
        {
            if (!beam.Suspension || beam.Broken) continue;
            Vector2 dir = (Pos[beam.B] - Pos[beam.A]).Normalized();
            Vector2 va = Pos[beam.A] - Prev[beam.A];
            Vector2 vb = Pos[beam.B] - Prev[beam.B];
            float relative = (vb - va).Dot(dir) * SuspensionDamping;
            float wa = InvMass[beam.A], wb = InvMass[beam.B];
            float w = wa + wb;
            va += dir * relative * (wa / w);
            vb -= dir * relative * (wb / w);
            Prev[beam.A] = Pos[beam.A] - va;
            Prev[beam.B] = Pos[beam.B] - vb;
        }
    }

    private void UpdateWheel(Wheel wheel, float dt, ITerrainCollider terrain)
    {
        float inertia = 0.5f * HubMass * WheelRadius * WheelRadius;

        if (wheel.Attached)
        {
            if (_input.Handbrake)
            {
                wheel.Spin *= 0.8f;
            }
            else if (!Mathf.IsZeroApprox(_input.Throttle))
            {
                bool belowTopSpeed = Mathf.Abs(wheel.Spin) < MaxWheelSpin || Mathf.Sign(wheel.Spin) != Mathf.Sign(_input.Throttle);
                if (belowTopSpeed) wheel.Spin += WheelTorque * _input.Throttle / inertia * dt;
            }
        }
        wheel.Spin *= 0.99995f; // rolling resistance

        int h = wheel.Hub;
        if (terrain.Collide(Pos[h], WheelRadius + 0.5f, out Vector2 n, out _))
        {
            Vector2 v = (Pos[h] - Prev[h]) / dt;
            Vector2 tangent = new Vector2(-n.Y, n.X); // along the ground, pointing right on flat ground
            float normalSpeed = v.Dot(n);
            float slip = v.Dot(tangent) - wheel.Spin * WheelRadius;

            // Tyre friction: an impulse that cancels slip, capped by grip x load.
            float impulse = -slip / (1f / HubMass + WheelRadius * WheelRadius / inertia);
            float load = (wheel.Attached ? _totalMass * 0.5f : HubMass) * Gravity * dt * Mathf.Max(0f, -n.Y)
                         + HubMass * Mathf.Max(0f, -normalSpeed);
            float maxImpulse = TyreGrip * load;
            impulse = Mathf.Clamp(impulse, -maxImpulse, maxImpulse);

            v += tangent * (impulse / HubMass);
            wheel.Spin -= impulse * WheelRadius / inertia;
            if (normalSpeed < 0f) v -= n * normalSpeed;
            Prev[h] = Pos[h] - v * dt;
        }

        wheel.Angle += wheel.Spin * dt;
    }

    /// <summary>Permanent bending (dents) and breaking. A beam snaps once its total permanent bend passes the break limit.</summary>
    private void ApplyDeformation()
    {
        foreach (var beam in _beams)
        {
            if (beam.Broken) continue;
            float length = (Pos[beam.B] - Pos[beam.A]).Length();
            float strain = (length - beam.Rest) / beam.Rest;
            float yieldStrain = beam.Suspension ? SuspensionYield : BodyYield;
            float breakStrain = beam.Suspension ? SuspensionBreak : BodyBreak;

            if (Mathf.Abs(strain) <= yieldStrain) continue;

            float newRest = length / (1f + Mathf.Sign(strain) * yieldStrain);
            Damage += Mathf.Abs(newRest - beam.Rest);
            beam.Rest = newRest;

            if (Mathf.Abs(beam.Rest - beam.OriginalRest) / beam.OriginalRest > breakStrain)
            {
                beam.Broken = true;
                Damage += 5f;
            }
        }

        foreach (var wheel in _wheels)
        {
            if (!wheel.Attached) continue;
            bool connected = false;
            foreach (var beam in _beams)
                if (beam.Suspension && !beam.Broken && (beam.A == wheel.Hub || beam.B == wheel.Hub))
                    connected = true;
            wheel.Attached = connected;
        }
    }

    private Vector2 BodyCenter()
    {
        if (Pos == null) return Spawn;
        Vector2 sum = Vector2.Zero;
        for (int i = 0; i < BodyNodeCount; i++) sum += Pos[i];
        return sum / BodyNodeCount;
    }

    // ---- Drawing ---------------------------------------------------------------------------

    public override void _Draw()
    {
        if (Pos == null) return;

        DrawTriangles(BodyTriangles, BodyColor);
        DrawTriangles(CabinTriangles, BodyColor.Darkened(0.3f));

        var beamColor = new Color(0f, 0f, 0f, 0.35f);
        var springColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);
        foreach (var beam in _beams)
        {
            if (beam.Broken) continue;
            DrawLine(ToLocal(Pos[beam.A]), ToLocal(Pos[beam.B]), beam.Suspension ? springColor : beamColor, beam.Suspension ? 2f : 1.5f);
        }

        foreach (var wheel in _wheels)
        {
            Vector2 c = ToLocal(Pos[wheel.Hub]);
            DrawCircle(c, WheelRadius, new Color(0.12f, 0.12f, 0.14f));
            DrawCircle(c, WheelRadius * 0.55f, new Color(0.6f, 0.6f, 0.65f));
            Vector2 spoke = new Vector2(Mathf.Cos(wheel.Angle), Mathf.Sin(wheel.Angle)) * WheelRadius * 0.9f;
            DrawLine(c, c + spoke, new Color(0.9f, 0.9f, 0.9f), 3f);
        }
    }

    private void DrawTriangles(int[] indices, Color color)
    {
        var points = new Vector2[3];
        var colors = new[] { color };
        for (int t = 0; t + 2 < indices.Length; t += 3)
        {
            points[0] = ToLocal(Pos[indices[t]]);
            points[1] = ToLocal(Pos[indices[t + 1]]);
            points[2] = ToLocal(Pos[indices[t + 2]]);
            float area = Mathf.Abs((points[1] - points[0]).Cross(points[2] - points[0]));
            if (area < 2f) continue; // skip crushed triangles
            if (points[0].DistanceTo(points[1]) > 80f || points[1].DistanceTo(points[2]) > 80f || points[2].DistanceTo(points[0]) > 80f)
                continue; // skip torn-apart triangles
            DrawPolygon(points, colors);
        }
    }
}
