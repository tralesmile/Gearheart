using System.Collections.Generic;
using Gearheart.Parts;
using Gearheart.Physics;
using Godot;

namespace Gearheart.Vehicles;

/// <summary>
/// Physics approach B (chosen 2026-11-11): node-and-beam soft body, generated from a
/// <see cref="VehicleDesign"/>. Every grid block becomes a square of four mass points with
/// edge and diagonal beams; neighbouring blocks share points and edges. Each beam takes
/// its strength from its part: past its yield it bends permanently (dents), and after too
/// much total bend it snaps. Wheels are hub points on suspension beams under a block.
///
/// The vehicle does not step itself: <see cref="SoftPhysicsWorld"/> steps every soft body
/// together so they can collide with each other and with loose rigid objects.
/// </summary>
public partial class SoftVehicle : Node2D, IVehicle
{
    [Export] public bool PlayerControlled;
    [Export] public float AutoThrottle;
    [Export] public Vector2 Spawn = new(200f, -150f);
    [Export] public float AirControl = 9f;          // rad/s² of tilt

    public VehicleDesign Design { get; set; }
    public SoftPhysicsWorld World { get; set; }

    public string PhysicsName => "B: soft body";
    public Vector2 CenterPosition => BodyCenter();
    public float Speed { get; private set; }
    public float Damage { get; private set; }
    public float Fuel { get; private set; }
    public float FuelCapacity { get; private set; }

    public const float CellSize = 22f;
    public const float NodeRadius = 3f;

    private const float Gravity = 980f;
    private const float MinNodeMass = 0.05f;
    private const float AirDrag = 0.99998f;        // per substep (about 2% per second)
    private const float GroundFriction = 0.3f;     // scraping body nodes
    private const float BodyStiffness = 0.5f;

    private sealed class Beam
    {
        public int A, B;
        public float Rest, OriginalRest, Stiffness, Yield, BreakLimit, Damping;
        public bool Suspension, Broken;
    }

    private sealed class Wheel
    {
        public int Hub;
        public float Radius, Grip, Mass, Spin, Angle;
        public bool Attached = true;
    }

    private sealed class Cell
    {
        public int[] Corners;          // top-left, top-right, bottom-right, bottom-left
        public PartDefinition Part;
    }

    // Node state, shared with SoftPhysicsWorld for collisions.
    public Vector2[] Pos { get; private set; }
    public Vector2[] Prev { get; private set; }
    public float[] InvMass { get; private set; }
    public int BodyNodeCount { get; private set; }

    /// <summary>Edges on the outside of the body, used for car-to-car collisions.</summary>
    public List<(int A, int B)> BoundaryEdges { get; } = new();

    private readonly List<Beam> _beams = new();
    private readonly List<Wheel> _wheels = new();
    private readonly List<Cell> _cells = new();
    private float[] _nodeRadius;
    private float _totalMass;
    private float _engineTorque, _engineMaxSpin, _fuelUse;
    private VehicleInput _input;
    private Vector2 _centerAtTickStart;

    public float RadiusOf(int node) => _nodeRadius[node];

    public IEnumerable<int> HubNodes
    {
        get
        {
            foreach (var w in _wheels) yield return w.Hub;
        }
    }

    public override void _Ready()
    {
        Build(Design ?? VehicleDesign.Default());
        World?.Register(this);
    }

    public override void _ExitTree() => World?.Unregister(this);

    // ---- Building from a design ------------------------------------------------------------

    private void Build(VehicleDesign design)
    {
        var positions = new List<Vector2>();
        var masses = new List<float>();
        var cornerIndex = new Dictionary<(int, int), int>();

        // Centre the body on the spawn point.
        Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
        foreach (var b in design.Blocks)
        {
            min = new Vector2(Mathf.Min(min.X, b.X), Mathf.Min(min.Y, b.Y));
            max = new Vector2(Mathf.Max(max.X, b.X + 1), Mathf.Max(max.Y, b.Y + 1));
        }
        Vector2 gridCenter = design.Blocks.Count > 0 ? (min + max) / 2f : Vector2.Zero;

        int Corner(int gx, int gy)
        {
            if (cornerIndex.TryGetValue((gx, gy), out int index)) return index;
            index = positions.Count;
            positions.Add(Spawn + (new Vector2(gx, gy) - gridCenter) * CellSize);
            masses.Add(0f);
            cornerIndex[(gx, gy)] = index;
            return index;
        }

        // Blocks: four corners each, mass shared out to the corners.
        foreach (var block in design.Blocks)
        {
            var part = PartCatalog.Get(block.Part);
            if (part == null || !part.IsBlock) continue;

            var corners = new[]
            {
                Corner(block.X, block.Y),
                Corner(block.X + 1, block.Y),
                Corner(block.X + 1, block.Y + 1),
                Corner(block.X, block.Y + 1),
            };
            foreach (int c in corners) masses[c] += part.Mass / 4f;
            _cells.Add(new Cell { Corners = corners, Part = part });

            _engineTorque += part.Torque;
            _engineMaxSpin = Mathf.Max(_engineMaxSpin, part.MaxSpin);
            _fuelUse += part.FuelUse;
            FuelCapacity += part.FuelCapacity;
        }

        BodyNodeCount = positions.Count;
        FuelCapacity += VehicleDesign.BaseFuelCapacity;
        Fuel = FuelCapacity;

        // Wheels: a hub point under the block above, hung on suspension beams.
        var wheelSetups = new List<(int Hub, PartDefinition Suspension, int[] Anchors)>();
        foreach (var dw in design.Wheels)
        {
            var wheelPart = PartCatalog.Get(dw.Wheel);
            var suspension = PartCatalog.Get(dw.Suspension);
            if (wheelPart == null || suspension == null) continue;
            if (!cornerIndex.TryGetValue((dw.X, dw.Y), out int left) || !cornerIndex.TryGetValue((dw.X + 1, dw.Y), out int right))
                continue; // no block above: the garage should have caught this

            var anchors = new List<int> { left, right };
            if (cornerIndex.TryGetValue((dw.X - 1, dw.Y), out int farLeft)) anchors.Add(farLeft);
            if (cornerIndex.TryGetValue((dw.X + 2, dw.Y), out int farRight)) anchors.Add(farRight);

            int hub = positions.Count;
            Vector2 attach = (positions[left] + positions[right]) / 2f;
            positions.Add(attach + new Vector2(0f, suspension.RestLength));
            masses.Add(wheelPart.Mass);

            _wheels.Add(new Wheel { Hub = hub, Radius = wheelPart.Radius, Grip = wheelPart.Grip, Mass = Mathf.Max(wheelPart.Mass, MinNodeMass) });
            wheelSetups.Add((hub, suspension, anchors.ToArray()));
        }

        // Node arrays.
        int count = positions.Count;
        Pos = positions.ToArray();
        Prev = positions.ToArray();
        InvMass = new float[count];
        _nodeRadius = new float[count];
        for (int i = 0; i < count; i++)
        {
            float mass = Mathf.Max(masses[i], MinNodeMass);
            InvMass[i] = 1f / mass;
            _totalMass += mass;
            _nodeRadius[i] = NodeRadius;
        }
        foreach (var w in _wheels) _nodeRadius[w.Hub] = w.Radius;

        // Body beams: four edges and both diagonals per block. Shared edges keep the stronger part.
        var edgeBeams = new Dictionary<(int, int), Beam>();
        var edgeUse = new Dictionary<(int, int), int>();
        foreach (var cell in _cells)
        {
            int[] c = cell.Corners;
            for (int e = 0; e < 4; e++)
            {
                var key = EdgeKey(c[e], c[(e + 1) % 4]);
                edgeUse[key] = edgeUse.GetValueOrDefault(key) + 1;
                if (edgeBeams.TryGetValue(key, out var existing))
                {
                    existing.Yield = Mathf.Max(existing.Yield, cell.Part.Yield);
                    existing.BreakLimit = Mathf.Max(existing.BreakLimit, cell.Part.BreakLimit);
                }
                else
                {
                    edgeBeams[key] = AddBeam(key.Item1, key.Item2, BodyStiffness, cell.Part.Yield, cell.Part.BreakLimit);
                }
            }
            AddBeam(c[0], c[2], BodyStiffness, cell.Part.Yield, cell.Part.BreakLimit);
            AddBeam(c[1], c[3], BodyStiffness, cell.Part.Yield, cell.Part.BreakLimit);
        }

        foreach (var (key, uses) in edgeUse)
            if (uses == 1) BoundaryEdges.Add(key);

        // Suspension beams.
        foreach (var (hub, suspension, anchors) in wheelSetups)
        {
            foreach (int anchor in anchors)
            {
                var beam = AddBeam(hub, anchor, suspension.SpringStiffness, suspension.Yield, suspension.BreakLimit);
                beam.Suspension = true;
                beam.Damping = suspension.Damping;
            }
        }
    }

    private static (int, int) EdgeKey(int a, int b) => a < b ? (a, b) : (b, a);

    private Beam AddBeam(int a, int b, float stiffness, float yieldStrain, float breakLimit)
    {
        float rest = Pos[a].DistanceTo(Pos[b]);
        var beam = new Beam
        {
            A = a,
            B = b,
            Rest = rest,
            OriginalRest = rest,
            Stiffness = stiffness,
            Yield = yieldStrain,
            BreakLimit = breakLimit,
        };
        _beams.Add(beam);
        return beam;
    }

    // ---- Stepping, called by SoftPhysicsWorld -------------------------------------------

    public void BeginTick()
    {
        _input = PlayerControlled ? VehicleInput.ReadPlayer() : VehicleInput.Auto(AutoThrottle);
        _centerAtTickStart = BodyCenter();
    }

    public void Refuel() => Fuel = FuelCapacity;

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
            if (terrain.Collide(Pos[i], _nodeRadius[i], out Vector2 n, out float depth))
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

        bool drive = Fuel > 0f && !Mathf.IsZeroApprox(_input.Throttle);
        if (drive) Fuel = Mathf.Max(0f, Fuel - _fuelUse * Mathf.Abs(_input.Throttle) * dt);

        int attached = 0;
        foreach (var w in _wheels) if (w.Attached) attached++;
        foreach (var wheel in _wheels)
            UpdateWheel(wheel, dt, terrain, drive, attached);

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
        if (Pos.Length == 0) return new Rect2(Spawn, Vector2.Zero);
        Vector2 min = Pos[0], max = Pos[0];
        foreach (var p in Pos)
        {
            min = new Vector2(Mathf.Min(min.X, p.X), Mathf.Min(min.Y, p.Y));
            max = new Vector2(Mathf.Max(max.X, p.X), Mathf.Max(max.Y, p.Y));
        }
        float grow = NodeRadius;
        foreach (var w in _wheels) grow = Mathf.Max(grow, w.Radius);
        return new Rect2(min, max - min).Grow(grow);
    }

    /// <summary>True if the point is inside any block of the body.</summary>
    public bool ContainsPoint(Vector2 p)
    {
        foreach (var cell in _cells)
        {
            int[] c = cell.Corners;
            if (InTriangle(p, Pos[c[0]], Pos[c[1]], Pos[c[2]]) || InTriangle(p, Pos[c[0]], Pos[c[2]], Pos[c[3]]))
                return true;
        }
        return false;
    }

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (p - b).Cross(a - b);
        float d2 = (p - c).Cross(b - c);
        float d3 = (p - a).Cross(c - a);
        bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNeg && hasPos);
    }

    // ---- Internals -------------------------------------------------------------------------

    private void SolveBeam(Beam beam)
    {
        Vector2 d = Pos[beam.B] - Pos[beam.A];
        float length = d.Length();
        if (length < 1e-4f) return;

        float wa = InvMass[beam.A], wb = InvMass[beam.B];
        float correction = (length - beam.Rest) / length * beam.Stiffness;
        Pos[beam.A] += d * correction * (wa / (wa + wb));
        Pos[beam.B] -= d * correction * (wb / (wa + wb));
    }

    private void DampSuspension()
    {
        foreach (var beam in _beams)
        {
            if (!beam.Suspension || beam.Broken) continue;
            Vector2 dir = (Pos[beam.B] - Pos[beam.A]).Normalized();
            Vector2 va = Pos[beam.A] - Prev[beam.A];
            Vector2 vb = Pos[beam.B] - Prev[beam.B];
            float relative = (vb - va).Dot(dir) * beam.Damping;
            float wa = InvMass[beam.A], wb = InvMass[beam.B];
            va += dir * relative * (wa / (wa + wb));
            vb -= dir * relative * (wb / (wa + wb));
            Prev[beam.A] = Pos[beam.A] - va;
            Prev[beam.B] = Pos[beam.B] - vb;
        }
    }

    private void UpdateWheel(Wheel wheel, float dt, ITerrainCollider terrain, bool drive, int attachedWheels)
    {
        float inertia = 0.5f * wheel.Mass * wheel.Radius * wheel.Radius;

        if (wheel.Attached)
        {
            if (_input.Handbrake)
            {
                wheel.Spin *= 0.8f;
            }
            else if (drive && attachedWheels > 0)
            {
                bool belowTopSpeed = Mathf.Abs(wheel.Spin) < _engineMaxSpin || Mathf.Sign(wheel.Spin) != Mathf.Sign(_input.Throttle);
                float torque = _engineTorque / attachedWheels * _input.Throttle;
                if (belowTopSpeed) wheel.Spin += torque / inertia * dt;
            }
        }
        wheel.Spin *= 0.99995f; // rolling resistance

        int h = wheel.Hub;
        if (terrain.Collide(Pos[h], wheel.Radius + 0.5f, out Vector2 n, out _))
        {
            Vector2 v = (Pos[h] - Prev[h]) / dt;
            Vector2 tangent = new Vector2(-n.Y, n.X); // along the ground, pointing right on flat ground
            float normalSpeed = v.Dot(n);
            float slip = v.Dot(tangent) - wheel.Spin * wheel.Radius;

            // Tyre friction: an impulse that cancels slip, capped by grip x load.
            float impulse = -slip / (1f / wheel.Mass + wheel.Radius * wheel.Radius / inertia);
            float carried = wheel.Attached && attachedWheels > 0 ? _totalMass / attachedWheels : wheel.Mass;
            float load = carried * Gravity * dt * Mathf.Max(0f, -n.Y) + wheel.Mass * Mathf.Max(0f, -normalSpeed);
            float maxImpulse = wheel.Grip * load;
            impulse = Mathf.Clamp(impulse, -maxImpulse, maxImpulse);

            v += tangent * (impulse / wheel.Mass);
            wheel.Spin -= impulse * wheel.Radius / inertia;
            if (normalSpeed < 0f) v -= n * normalSpeed;
            Prev[h] = Pos[h] - v * dt;
        }

        wheel.Angle += wheel.Spin * dt;
    }

    /// <summary>Permanent bending (dents) and breaking. A beam snaps once its total permanent bend passes its part's limit.</summary>
    private void ApplyDeformation()
    {
        foreach (var beam in _beams)
        {
            if (beam.Broken || beam.OriginalRest <= 0f) continue;
            float length = (Pos[beam.B] - Pos[beam.A]).Length();
            float strain = (length - beam.Rest) / beam.Rest;
            if (Mathf.Abs(strain) <= beam.Yield) continue;

            float newRest = length / (1f + Mathf.Sign(strain) * beam.Yield);
            Damage += Mathf.Abs(newRest - beam.Rest);
            beam.Rest = newRest;

            if (Mathf.Abs(beam.Rest - beam.OriginalRest) / beam.OriginalRest > beam.BreakLimit)
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
        if (Pos == null || BodyNodeCount == 0) return Spawn;
        Vector2 sum = Vector2.Zero;
        for (int i = 0; i < BodyNodeCount; i++) sum += Pos[i];
        return sum / BodyNodeCount;
    }

    // ---- Drawing ---------------------------------------------------------------------------

    public override void _Draw()
    {
        if (Pos == null) return;

        foreach (var cell in _cells)
        {
            int[] c = cell.Corners;
            Color color = cell.Part.GodotColor;
            FillTriangle(Pos[c[0]], Pos[c[1]], Pos[c[2]], color);
            FillTriangle(Pos[c[0]], Pos[c[2]], Pos[c[3]], color);

            if (cell.Part.Category == PartCategory.Drill)
            {
                // Spike on the right-hand edge.
                Vector2 tip = (Pos[c[1]] + Pos[c[2]]) / 2f + (Pos[c[1]] - Pos[c[0]]).Normalized() * CellSize * 0.8f;
                FillTriangle(Pos[c[1]], tip, Pos[c[2]], color.Darkened(0.2f));
            }
            else if (cell.Part.Category == PartCategory.Engine)
            {
                Vector2 middle = (Pos[c[0]] + Pos[c[2]]) / 2f;
                DrawCircle(ToLocal(middle), 5f, color.Darkened(0.4f));
            }
        }

        var beamColor = new Color(0f, 0f, 0f, 0.25f);
        var springColor = new Color(0.85f, 0.85f, 0.85f, 0.9f);
        foreach (var beam in _beams)
        {
            if (beam.Broken) continue;
            DrawLine(ToLocal(Pos[beam.A]), ToLocal(Pos[beam.B]), beam.Suspension ? springColor : beamColor, beam.Suspension ? 2f : 1f);
        }

        var outlineColor = new Color(0f, 0f, 0f, 0.6f);
        foreach (var (a, b) in BoundaryEdges)
            if (Pos[a].DistanceTo(Pos[b]) < CellSize * 2f)
                DrawLine(ToLocal(Pos[a]), ToLocal(Pos[b]), outlineColor, 2f);

        foreach (var wheel in _wheels)
        {
            Vector2 c = ToLocal(Pos[wheel.Hub]);
            DrawCircle(c, wheel.Radius, new Color(0.12f, 0.12f, 0.14f));
            DrawCircle(c, wheel.Radius * 0.55f, new Color(0.6f, 0.6f, 0.65f));
            Vector2 spoke = new Vector2(Mathf.Cos(wheel.Angle), Mathf.Sin(wheel.Angle)) * wheel.Radius * 0.9f;
            DrawLine(c, c + spoke, new Color(0.9f, 0.9f, 0.9f), 3f);
        }
    }

    private void FillTriangle(Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        Vector2 la = ToLocal(a), lb = ToLocal(b), lc = ToLocal(c);
        float area = Mathf.Abs((lb - la).Cross(lc - la));
        if (area < 2f) return; // crushed
        if (la.DistanceTo(lb) > CellSize * 3f || lb.DistanceTo(lc) > CellSize * 3f || lc.DistanceTo(la) > CellSize * 3f)
            return; // torn apart
        DrawPolygon(new[] { la, lb, lc }, new[] { color });
    }
}
