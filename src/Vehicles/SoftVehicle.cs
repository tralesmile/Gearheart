using System.Collections.Generic;
using Gearheart.Core;
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

    /// <summary>Parked cars hold the handbrake and ignore all input.</summary>
    public bool Parked { get; set; }

    /// <summary>Drills only cut while this is on.</summary>
    public bool DrillEnabled { get; set; } = true;

    public bool HasDrill => _drills.Count > 0;

    /// <summary>Shows the driver in the cab window.</summary>
    public bool Occupied { get; set; }
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
        public int Sprite;
        public bool Attached = true;
        public bool Grounded;
    }

    private sealed class DrillTip
    {
        public int Node;
        public float Power;
        public Cell Block;
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
    private readonly List<DrillTip> _drills = new();
    private float[] _nodeRadius;
    private float _totalMass;
    private float _engineTorque, _engineMaxSpin, _fuelUse;
    private VehicleInput _input;
    private float _drillContactTime;
    private float _autoStuckTime, _autoReverseTime;
    private int _rearHub = -1, _frontHub = -1;
    private Vector2 _centerAtTickStart;

    public float RadiusOf(int node) => _nodeRadius[node];

    /// <summary>Drill tip positions (for diagnostics and effects).</summary>
    public IEnumerable<Vector2> DrillTips
    {
        get
        {
            foreach (var d in _drills) yield return Pos[d.Node];
        }
    }

    /// <summary>One-line state summary for automated playtest logs.</summary>
    public string DebugSummary()
    {
        var parts = new List<string>();
        foreach (var w in _wheels)
            parts.Add($"wheel(att={w.Attached} spin={w.Spin:0} y={Pos[w.Hub].Y:0})");
        Vector2 c = BodyCenter();
        parts.Add($"body=({c.X:0},{c.Y:0}) nodes={Pos.Length} broken={_beams.FindAll(b => b.Broken).Count}");
        return string.Join(" ", parts);
    }

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

        // Drill tips: a point in front of each drill block's right edge.
        var drillSetups = new List<(int Tip, Cell Cell)>();
        foreach (var cell in _cells)
        {
            if (cell.Part.Category != PartCategory.Drill) continue;
            int tip = positions.Count;
            Vector2 edgeMiddle = (positions[cell.Corners[1]] + positions[cell.Corners[2]]) / 2f;
            positions.Add(edgeMiddle + new Vector2(CellSize * 0.8f, 0f));
            masses.Add(cell.Part.Mass * 0.25f);
            _drills.Add(new DrillTip { Node = tip, Power = cell.Part.DrillPower, Block = cell });
            drillSetups.Add((tip, cell));
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

            _wheels.Add(new Wheel { Hub = hub, Sprite = wheelPart.Sprite, Radius = wheelPart.Radius, Grip = wheelPart.Grip, Mass = Mathf.Max(wheelPart.Mass, MinNodeMass) });
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

        // Rearmost and frontmost wheels, for measuring pitch.
        foreach (var w in _wheels)
        {
            if (_rearHub < 0 || Pos[w.Hub].X < Pos[_rearHub].X) _rearHub = w.Hub;
            if (_frontHub < 0 || Pos[w.Hub].X > Pos[_frontHub].X) _frontHub = w.Hub;
        }

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

        // Drill tips are braced to all four corners of their block.
        foreach (var (tip, cell) in drillSetups)
            foreach (int corner in cell.Corners)
                AddBeam(tip, corner, BodyStiffness, cell.Part.Yield, cell.Part.BreakLimit);

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
        _input = Parked ? new VehicleInput(0f, 0f, true) : PlayerControlled ? VehicleInput.ReadPlayer() : AutoPilot();
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
    public void EndSubstep(float dt, ITerrainCollider terrain, IMineable mineable = null)
    {
        DampSuspension();

        if (mineable != null && DrillEnabled)
        {
            foreach (var drill in _drills)
                if (DrillIntact(drill) && mineable.Mine(Pos[drill.Node], NodeRadius + 6f, drill.Power * dt))
                    _drillContactTime = 0.5f;
        }

        for (int i = 0; i < BodyNodeCount; i++)
        {
            if (!terrain.Collide(Pos[i], NodeRadius + 0.5f, out Vector2 n, out _)) continue;
            Vector2 v = Pos[i] - Prev[i];
            Vector2 tangential = v - n * v.Dot(n);
            Prev[i] = Pos[i] - tangential * (1f - GroundFriction);
        }

        bool drive = Fuel > 0f && !Mathf.IsZeroApprox(_input.Throttle);
        if (drive) Fuel = Mathf.Max(0f, Fuel - _fuelUse * Mathf.Abs(_input.Throttle) * dt);

        // Engine torque goes to attached wheels that touch the ground (like a locked differential),
        // so a wheel spinning in the air does not waste half the power.
        int attached = 0, driven = 0;
        foreach (var w in _wheels)
        {
            if (!w.Attached) continue;
            attached++;
            if (w.Grounded) driven++;
        }
        foreach (var wheel in _wheels)
            UpdateWheel(wheel, dt, terrain, drive, attached, driven);

        ApplyDeformation();
    }

    public void EndTick(float delta)
    {
        _drillContactTime = Mathf.Max(0f, _drillContactTime - delta);
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

    private void UpdateWheel(Wheel wheel, float dt, ITerrainCollider terrain, bool drive, int attachedWheels, int drivenWheels)
    {
        float inertia = 0.5f * wheel.Mass * wheel.Radius * wheel.Radius;

        if (wheel.Attached)
        {
            if (_input.Handbrake)
            {
                wheel.Spin *= 0.8f;
            }
            else if (drive && attachedWheels > 0 && (wheel.Grounded || drivenWheels == 0))
            {
                bool belowTopSpeed = Mathf.Abs(wheel.Spin) < _engineMaxSpin || Mathf.Sign(wheel.Spin) != Mathf.Sign(_input.Throttle);
                float share = drivenWheels > 0 ? drivenWheels : attachedWheels;
                float torque = _engineTorque / share * _input.Throttle;
                if (belowTopSpeed) wheel.Spin += torque / inertia * dt;
            }
        }
        wheel.Spin *= 0.99995f; // rolling resistance

        int h = wheel.Hub;
        wheel.Grounded = terrain.Collide(Pos[h], wheel.Radius + 0.5f, out Vector2 n, out _);
        if (wheel.Grounded)
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

    /// <summary>True while a drill tip has touched ore in the last half second.</summary>
    public bool IsDrilling => _drillContactTime > 0f;

    /// <summary>Body pitch in degrees from the rear to the front wheel; negative = nose up.</summary>
    public float PitchDegrees()
    {
        if (_rearHub < 0 || _frontHub < 0 || _rearHub == _frontHub) return 0f;
        Vector2 d = Pos[_frontHub] - Pos[_rearHub];
        return Mathf.RadToDeg(Mathf.Atan2(d.Y, d.X));
    }

    /// <summary>
    /// Drives like a careful player, for bots and automated playtests: full throttle,
    /// ease off while drilling, and tilt the nose back down if the car starts to flip.
    /// </summary>
    private VehicleInput AutoPilot()
    {
        float throttle = AutoThrottle;
        float tilt = 0f;
        float pitch = PitchDegrees() * Mathf.Sign(AutoThrottle == 0f ? 1f : AutoThrottle);

        // Steep ramps reach about 30 degrees, so only steeper than that counts as flipping.
        if (pitch < -38f)
        {
            tilt = Mathf.Sign(throttle == 0f ? 1f : throttle);   // nose down
            throttle *= pitch < -55f ? 0f : 0.5f;
        }
        else if (pitch > 25f)
        {
            tilt = -Mathf.Sign(throttle == 0f ? 1f : throttle);
        }

        if (IsDrilling) throttle = Mathf.Clamp(throttle, -0.45f, 0.45f);

        // Stuck for 3 s without drilling: back up for a moment, then try again.
        const float tick = 1f / 120f;
        if (_autoReverseTime > 0f)
        {
            _autoReverseTime -= tick;
            return new VehicleInput(-AutoThrottle * 0.8f, 0f, false);
        }
        _autoStuckTime = Speed < 15f && !IsDrilling && AutoThrottle != 0f ? _autoStuckTime + tick : 0f;
        if (_autoStuckTime > 3f)
        {
            _autoStuckTime = 0f;
            _autoReverseTime = 1.2f;
        }

        return new VehicleInput(throttle, tilt, false);
    }

    private bool DrillIntact(DrillTip drill)
    {
        foreach (var beam in _beams)
            if (!beam.Broken && (beam.A == drill.Node || beam.B == drill.Node))
                return true;
        return false;
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
        Texture2D parts = Art.Parts;
        Vector2 partsSize = parts.GetSize();

        // Suspension struts behind everything.
        var strut = new Color(0.16f, 0.16f, 0.2f);
        foreach (var beam in _beams)
            if (beam.Suspension && !beam.Broken)
                DrawLine(ToLocal(Pos[beam.A]), ToLocal(Pos[beam.B]), strut, 3f);

        // Body: every block is a textured quad that bends with the soft body.
        foreach (var cell in _cells)
        {
            int[] c = cell.Corners;
            Rect2 src = Art.PartCell(cell.Part.Sprite);
            Vector2 u0 = (src.Position + new Vector2(0.1f, 0.1f)) / partsSize;
            Vector2 u1 = (src.End - new Vector2(0.1f, 0.1f)) / partsSize;
            TexturedTriangle(parts, Pos[c[0]], Pos[c[1]], Pos[c[2]], u0, new Vector2(u1.X, u0.Y), u1);
            TexturedTriangle(parts, Pos[c[0]], Pos[c[2]], Pos[c[3]], u0, u1, new Vector2(u0.X, u1.Y));

            if (Occupied && cell.Part.Category == PartCategory.Cab)
            {
                // The driver's helmet and face behind the glass.
                Vector2 middle = ToLocal((Pos[c[0]] + Pos[c[1]] + Pos[c[2]] + Pos[c[3]]) / 4f);
                DrawRect(new Rect2(middle + new Vector2(-2f, -3f), new Vector2(6f, 5f)), new Color(0.93f, 0.74f, 0.58f));
                DrawRect(new Rect2(middle + new Vector2(-3f, -7f), new Vector2(8f, 4f)), new Color(0.94f, 0.77f, 0.19f));
            }
        }

        // Drill spikes: a striped cone from the block's front edge to the tip.
        foreach (var drill in _drills)
        {
            if (!DrillIntact(drill)) continue;
            int[] c = drill.Block.Corners;
            Vector2 top = ToLocal(Pos[c[1]]), bottom = ToLocal(Pos[c[2]]), tip = ToLocal(Pos[drill.Node]);
            float spin = _drillContactTime > 0f ? Time.GetTicksMsec() / 60f : 0f;
            const int bands = 5;
            for (int i = 0; i < bands; i++)
            {
                float f0 = i / (float)bands, f1 = (i + 1) / (float)bands;
                Vector2 a0 = top.Lerp(tip, f0), a1 = top.Lerp(tip, f1), b0 = bottom.Lerp(tip, f0), b1 = bottom.Lerp(tip, f1);
                bool light = ((i + (int)spin) & 1) == 0;
                Color color = light ? new Color(0.82f, 0.84f, 0.88f) : new Color(0.5f, 0.52f, 0.58f);
                if (i < bands - 1) FillQuad(a0, a1, b1, b0, color);
                else FillTriangleLocal(a0, tip, b0, color);
            }
            DrawLine(top, tip, new Color(0.13f, 0.11f, 0.16f), 1.5f);
            DrawLine(bottom, tip, new Color(0.13f, 0.11f, 0.16f), 1.5f);
        }

        // Dark outline around the body silhouette.
        var outlineColor = new Color(0.13f, 0.11f, 0.16f);
        foreach (var (a, b) in BoundaryEdges)
            if (Pos[a].DistanceTo(Pos[b]) < CellSize * 2f)
                DrawLine(ToLocal(Pos[a]), ToLocal(Pos[b]), outlineColor, 2f);

        // Wheels on top, spinning.
        Texture2D wheels = Art.Wheels;
        foreach (var wheel in _wheels)
        {
            DrawSetTransform(ToLocal(Pos[wheel.Hub]), wheel.Angle, Vector2.One);
            float half = Art.WheelCell / 2f;
            DrawTextureRectRegion(wheels, new Rect2(-half, -half, Art.WheelCell, Art.WheelCell),
                new Rect2(wheel.Sprite * Art.WheelCell, 0, Art.WheelCell, Art.WheelCell));
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private void TexturedTriangle(Texture2D texture, Vector2 a, Vector2 b, Vector2 c, Vector2 ua, Vector2 ub, Vector2 uc)
    {
        Vector2 la = ToLocal(a), lb = ToLocal(b), lc = ToLocal(c);
        float area = Mathf.Abs((lb - la).Cross(lc - la));
        if (area < 2f) return; // crushed flat
        if (la.DistanceTo(lb) > CellSize * 3f || lb.DistanceTo(lc) > CellSize * 3f || lc.DistanceTo(la) > CellSize * 3f)
            return; // torn apart
        DrawPolygon(new[] { la, lb, lc }, new[] { Colors.White }, new[] { ua, ub, uc }, texture);
    }

    private void FillTriangleLocal(Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        if (Mathf.Abs((b - a).Cross(c - a)) < 1f) return;
        DrawPolygon(new[] { a, b, c }, new[] { color });
    }

    private void FillQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        FillTriangleLocal(a, b, c, color);
        FillTriangleLocal(a, c, d, color);
    }
}
