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

    /// <summary>Bots only: forces a tilt input (-1..1) and switches off the autopilot's anti-flip.</summary>
    public float AutoTilt;
    [Export] public Vector2 Spawn = new(200f, -150f);
    [Export] public float AirControl = 9f;          // rad/s² of tilt

    public VehicleDesign Design { get; set; }

    /// <summary>Parked cars hold the handbrake and ignore all input.</summary>
    public bool Parked { get; set; }

    /// <summary>Drills only cut while this is on.</summary>
    public bool DrillEnabled { get; set; } = true;

    public bool HasDrill => _drills.Count > 0;

    public bool HasBooster => _boosters.Count > 0;

    /// <summary>Bots only: fire the boosters whenever the car is in a safe attitude.</summary>
    public bool AutoBoost;

    /// <summary>Bots only: hold the handbrake (with <see cref="AutoThrottle"/> this arms launch control).</summary>
    public bool AutoHandbrake;

    // ---- Launch control -------------------------------------------------------------------------
    // At a standstill, hold the handbrake and the throttle: the revs climb (the launch charge fills the
    // rev bar). Let go of the handbrake with the throttle still down. In the green window near the top
    // it is a PERFECT START (a big shove), lower a GOOD START (a small one), early a BOGGED start (none).
    // Holding on past the top bounces off the limiter and drops back a little, so you can try again.
    public const float LaunchRiseTime = 1.1f;        // seconds from idle to the top
    public const float LaunchPerfectFrom = 0.75f, LaunchPerfectTo = 0.97f, LaunchGoodFrom = 0.45f;
    public const float LaunchBounceTo = 0.6f;        // the charge drops back to this off the limiter
    public const float LaunchPerfectKick = 320f;     // px/s (18 km/h)
    public const float LaunchGoodKick = 140f;        // px/s (8 km/h)
    private const float LaunchMaxSpeed = 50f;        // px/s (about 3 km/h): faster than this is not a standing start

    /// <summary>True while the car is held on the handbrake with the throttle down, ready to launch.</summary>
    public bool LaunchArmed { get; private set; }
    /// <summary>Launch charge 0..1 while armed (the revs the driver holds).</summary>
    public float LaunchCharge { get; private set; }
    /// <summary>Times the charge has bounced off the limiter during this hold.</summary>
    public int LaunchBounces { get; private set; }
    private float _launchBounceFlash;

    /// <summary>Raised when the handbrake is let go from an armed launch: the grade and the kick (px/s).</summary>
    public event System.Action<LaunchGrade, float> Launched;

    /// <summary>
    /// Raised once per hard hit (landing, slam into the ground, snapped beams, lost wheel), for
    /// screen shake, sparks and crunch sounds. Fired from the physics tick.
    /// </summary>
    public event System.Action<CrashEvent> Crashed;

    // ---- Rock it free -------------------------------------------------------------------------
    // Held the throttle for a second on the ground and the car does not move (a notch, a step, a
    // tunnel lip): it is STUCK. While stuck, Q/E tilt rocks the car on its springs much harder, and
    // every tap of Q or E (best alternating, like rocking a real car) gives a little hop, a shove the
    // way the throttle points and a twist the way you tapped. Taps in a rhythm build a chain that
    // hops higher each time. Moving 1.5 m away from where it got stuck, rolling, is ROCKED FREE.
    public const float RockStuckAfter = 1.0f;     // seconds of pushing without moving
    private const float RockStuckSpeed = 20f;     // px/s (about 1 km/h; 64 px = 1 m)
    private const float RockChainWindow = 0.9f;   // seconds between taps that still build the chain
    private const float RockMinGap = 0.16f;       // seconds between two rocks
    private const float RockTiltBoost = 2.5f;     // ground tilt strength while stuck
    /// <summary>Tilt strength with every wheel off the ground: 9 rad/s² x 1.5 turns a controlled backflip into
    /// about 1.35 s of air (at 1x it needed 1.7 s, longer than nearly every jump). See docs/DECISIONS.md.</summary>
    public const float AirFlipBoost = 1.5f;
    /// <summary>Holding Q or E in the air (rather than tapping) ramps the rotation up by this much more, from
    /// <see cref="AirHoldDelay"/> s into the hold over <see cref="AirHoldRamp"/> s: taps stay gentle for lining up a
    /// landing, a held key commits to a flip (a full turn in about 0.85 s).</summary>
    public const float AirHoldBoost = 0.8f, AirHoldDelay = 0.15f, AirHoldRamp = 0.3f;
    private float _airHold;
    private int _airHoldSign;

    /// <summary>How much stronger tilt is with every wheel off the ground, for this substep.</summary>
    private float AirSpinBoost(float tilt, float dt)
    {
        int sign = tilt > 0.5f ? 1 : tilt < -0.5f ? -1 : 0;
        if (sign == 0 || sign != _airHoldSign)
        {
            _airHoldSign = sign;
            _airHold = 0f;
        }
        else _airHold += dt;
        return AirFlipBoost + AirHoldBoost * Mathf.Clamp((_airHold - AirHoldDelay) / AirHoldRamp, 0f, 1f);
    }
    private const float RockBuffer = 0.4f;        // seconds a tap made in the air waits for touchdown
    public const int RockChainMax = 5;
    private const float RockFreeDistance = 96f;   // px sideways from the stuck point (1.5 m)

    /// <summary>True from a second of pushing without moving until the car gets going again (or the driver gives up).</summary>
    public bool Stuck { get; private set; }
    /// <summary>Rocks in the current rhythm (1..<see cref="RockChainMax"/>).</summary>
    public int RockChain { get; private set; }
    /// <summary>All rocks since the car was built (for the playtest log).</summary>
    public int RockPulses { get; private set; }
    /// <summary>Bots only: rock the car (alternate Q and E) when it gets stuck. Off for a control run.</summary>
    public bool AutoRock = true;

    /// <summary>Raised when the car becomes stuck.</summary>
    public event System.Action StuckStarted;
    /// <summary>Raised on every rock: the chain count and the hop (px/s).</summary>
    public event System.Action<int, float> Rocked;
    /// <summary>Raised when a stuck car gets going again: rocks used and seconds it was stuck.</summary>
    public event System.Action<int, float> RockedFree;

    private float _stuckTime, _stuckFor, _sinceRock = 10f, _giveUp, _autoRockTime;
    private Vector2 _stuckAt;
    private int _rocksThisStuck, _heldTiltSign, _lastRockSign, _bufferedRock;
    private float _bufferedFor;
    private bool _rockPending;
    private Vector2 _rockPush;
    private float _rockSpin;

    /// <summary>True while the rocket boosters are firing this tick.</summary>
    public bool Boosting { get; private set; }

    /// <summary>True while the car is rocking itself back onto its wheels after landing on its roof.</summary>
    public bool SelfRighting => _selfRighting;

    /// <summary>True while no attached wheel touches the ground.</summary>
    public bool Airborne
    {
        get
        {
            foreach (var w in _wheels)
                if (w.Attached && w.Grounded) return false;
            return true;
        }
    }

    /// <summary>Wheels still bolted on (lost wheels do not count).</summary>
    public int WheelsAttached
    {
        get
        {
            int n = 0;
            foreach (var w in _wheels)
                if (w.Attached) n++;
            return n;
        }
    }

    /// <summary>Attached wheels touching the ground right now.</summary>
    public int WheelsGrounded
    {
        get
        {
            int n = 0;
            foreach (var w in _wheels)
                if (w.Attached && w.Grounded) n++;
            return n;
        }
    }

    /// <summary>
    /// Degrees between the car's "up" and the ground under its grounded wheels: 0 when the car sits
    /// flat on the slope it touched, large when it lands nose-first or tail-first. -1 with no wheel down.
    /// </summary>
    public float GroundMismatchDegrees()
    {
        Vector2 n = Vector2.Zero;
        foreach (var w in _wheels)
            if (w.Attached && w.Grounded) n += w.GroundNormal;
        if (n == Vector2.Zero) return -1f;
        Vector2 d = Forward();
        Vector2 up = new Vector2(d.Y, -d.X);
        return Mathf.Abs(Mathf.RadToDeg(up.AngleTo(n.Normalized())));
    }

    /// <summary>
    /// A short shove along the way the car is already rolling (a perfect landing), in px/s.
    /// Spread over <see cref="KickTime"/> seconds (a jolt); the tyres spin up with it
    /// so they do not brake it away.
    /// </summary>
    public void Kick(float speed)
    {
        _kickDir = 0f;
        _kickPending += speed;
        _kickRate = _kickPending / KickTime;
        // Overdrive: the engine may spin past its normal top speed by the kick for a moment,
        // or it would stop pushing and the kick would bleed away at once.
        _overdrive = Mathf.Max(_overdrive, speed);
    }

    /// <summary>A launch shove from a standstill (px/s), to the right for <paramref name="direction"/> > 0.</summary>
    private void LaunchKick(float speed, float direction)
    {
        _kickDir = Mathf.Sign(direction);
        _kickPending = speed;
        _kickRate = speed / KickTime;
        _overdrive = Mathf.Max(_overdrive, speed);
    }

    /// <summary>Extra top speed (px/s at the tyre) the engine is allowed right now after a kick; fades in about 1.5 s.</summary>
    public float Overdrive => _overdrive;
    private const float OverdriveFade = 180f;   // px/s lost per second

    public const float KickTime = 0.05f;   // near-instant: in A/B bot runs a 0.3 s spread was weaker

    /// <summary>Shows the driver in the cab window.</summary>
    public bool Occupied { get; set; }
    public SoftPhysicsWorld World { get; set; }

    public string PhysicsName => "B: soft body";
    public Vector2 CenterPosition => BodyCenter() + Origin;

    // ---- Local origin ---------------------------------------------------------------------------
    // Node positions (Pos, Prev) are kept relative to Origin, a whole multiple of OriginStep pixels
    // along x, so they stay small numbers. Far from x = 0 a 32-bit float can only step 0.002 px, and
    // the Verlet velocity (position minus previous position) then lost every change under about
    // 1.9 px/s per substep: the Scrap 4 pickup could not leave the garage pad. The node itself sits at
    // Origin, so drawing uses Pos directly; the world (terrain, crashes, other cars) gets Pos + Origin.

    /// <summary>World position of the node frame (x only, a multiple of <see cref="OriginStep"/>).</summary>
    public Vector2 Origin { get; private set; }
    public const float OriginStep = 1024f;
    private readonly ShiftedTerrain _localTerrain = new();

    /// <summary>Node drawing: Pos is already relative to this node, which sits at <see cref="Origin"/>.</summary>
    private new Vector2 ToLocal(Vector2 local) => local;

    private ShiftedTerrain Local(ITerrainCollider terrain, IMineable mineable = null)
    {
        _localTerrain.Inner = terrain;
        _localTerrain.InnerMine = mineable;
        _localTerrain.Shift = Origin;
        return _localTerrain;
    }

    /// <summary>Moves the origin when the car has driven far from it (exact: whole multiples of 1,024 px).</summary>
    private void Recenter()
    {
        float x = BodyCenter().X;
        if (Mathf.Abs(x) < OriginStep * 1.5f) return;
        float shift = Mathf.Round(x / OriginStep) * OriginStep;
        for (int i = 0; i < Pos.Length; i++)
        {
            Pos[i].X -= shift;
            Prev[i].X -= shift;
        }
        _centerAtTickStart.X -= shift;
        Origin += new Vector2(shift, 0f);
        Position = Origin;
    }
    public float Speed { get; private set; }
    /// <summary>Velocity of the body centre over the last physics tick (px/s, y down).</summary>
    public Vector2 CenterVelocity { get; private set; }
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
        public float FrozenGrip;          // studs: grip floor on snow and ice (0 = plain tyre)
        public float Slip;                // last tyre slip speed along the ground (px/s), for effects
        public bool OnFrozen;             // touching snow or ice right now
        public bool Biting;               // studs gripping where a plain tyre would slide
        public int Sprite;
        public bool Attached = true;
        public bool Grounded;
        public Vector2 GroundNormal = Vector2.Up;
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
        public int GridX, GridY;
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
    private readonly List<Cell> _boosters = new();
    private float _boostFuelUse;
    private float[] _nodeRadius;
    private float _totalMass;
    private float _engineTorque, _engineMaxSpin, _fuelUse;
    private VehicleInput _input;
    private float _drillContactTime;
    private Cell _frontCell, _rearCell;
    private CpuParticles2D _dust, _exhaust, _boostSmoke, _backfire;
    private float _lastThrottle, _popCooldown;
    private float _upsideDownTime;
    private bool _selfRighting;
    private float _rightingTime;
    private bool _hopPending;
    private float _kickPending, _kickRate, _overdrive, _kickDir;
    private const float AirSpinDamping = 0.003f;   // share of spin removed per substep while airborne
    private const float MaxHeldSpin = 12f;         // rad/s (about 690 deg/s): spin held with Q/E is not bled below this
    private float _autoStuckTime, _autoReverseTime;
    private int _rearHub = -1, _frontHub = -1;
    private Vector2 _centerAtTickStart;

    // Impact detection (see DetectImpact).
    private const float ImpactThreshold = 230f;   // px/s (about 13 km/h): body slams and scrapes
    private const float LandingThreshold = 380f;  // px/s (about 21 km/h): small hops on the suspension are not hits
    private const int ImpactGatherTicks = 4;      // a hit's dents and snaps arrive over a few ticks
    private float _age, _impactCooldown, _impactPeak, _dentThisHit, _subDt = 1f / 960f, _airTimer;
    private float _bodyImpact, _landing;
    private Vector2 _airVelocity;
    private int _impactTicks = -1, _snapsThisTick, _snapsThisHit;
    private bool _firstIteration, _bodyHit, _wheelLostThisTick, _wheelLostThisHit;
    private bool[] _isDrillTip;
    private Vector2 _hitPoint, _hitNormal = Vector2.Up, _snapPoint;

    public float RadiusOf(int node) => _nodeRadius[node];

    /// <summary>Drill tip positions (for diagnostics and effects).</summary>
    public IEnumerable<Vector2> DrillTips
    {
        get
        {
            foreach (var d in _drills) yield return Pos[d.Node] + Origin;
        }
    }

    /// <summary>One-line state summary for automated playtest logs.</summary>
    public string DebugSummary()
    {
        var parts = new List<string>();
        foreach (var w in _wheels)
            parts.Add($"wheel(att={w.Attached} spin={w.Spin:0} y={Pos[w.Hub].Y:0})");
        Vector2 c = CenterPosition;
        parts.Add($"body=({c.X:0},{c.Y:0}) nodes={Pos.Length} broken={_beams.FindAll(b => b.Broken).Count}");
        return string.Join(" ", parts);
    }

    /// <summary>What a wheel is doing on the ground, for tyre effects (ice spray, stud bite).</summary>
    public readonly struct WheelContact
    {
        public readonly Vector2 Point, Normal;
        public readonly float Slip, RimSpeed;
        public readonly bool OnFrozen, Studded, Biting;

        public WheelContact(Vector2 point, Vector2 normal, float slip, float rimSpeed, bool onFrozen, bool studded, bool biting)
        {
            Point = point; Normal = normal; Slip = slip; RimSpeed = rimSpeed;
            OnFrozen = onFrozen; Studded = studded; Biting = biting;
        }
    }

    /// <summary>The grounded, attached wheels and how they meet the ground.</summary>
    public IEnumerable<WheelContact> WheelContacts
    {
        get
        {
            foreach (var w in _wheels)
            {
                if (!w.Attached || !w.Grounded) continue;
                yield return new WheelContact(Pos[w.Hub] + Origin - w.GroundNormal * w.Radius, w.GroundNormal, w.Slip, w.Spin * w.Radius,
                    w.OnFrozen, w.FrozenGrip > 0f, w.Biting);
            }
        }
    }

    /// <summary>World centre and radius of every attached wheel.</summary>
    public IEnumerable<(Vector2 Center, float Radius)> WheelCircles
    {
        get
        {
            foreach (var w in _wheels)
                if (w.Attached) yield return (Pos[w.Hub] + Origin, w.Radius);
        }
    }

    /// <summary>True when at least one attached wheel has studs.</summary>
    public bool HasStuds => _wheels.Exists(w => w.Attached && w.FrozenGrip > 0f);

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
        // Lights go on the frontmost and rearmost body blocks (the lowest one when several tie).
        foreach (var cell in _cells)
        {
            if (cell.Part.Category is PartCategory.Drill or PartCategory.Booster or PartCategory.Turbo or PartCategory.Hydraulics) continue;
            if (_frontCell == null || cell.GridX > _frontCell.GridX || (cell.GridX == _frontCell.GridX && cell.GridY > _frontCell.GridY)) _frontCell = cell;
            if (_rearCell == null || cell.GridX < _rearCell.GridX || (cell.GridX == _rearCell.GridX && cell.GridY > _rearCell.GridY)) _rearCell = cell;
        }

        _dust = new CpuParticles2D
        {
            Emitting = false, Amount = 20, Lifetime = 0.55f, Spread = 35f, Direction = new Vector2(-1f, -0.7f),
            InitialVelocityMin = 30f, InitialVelocityMax = 90f, Gravity = new Vector2(0f, 120f),
            ScaleAmountMin = 2f, ScaleAmountMax = 5f, Color = new Color(0.82f, 0.74f, 0.6f, 0.7f),
        };
        AddChild(_dust);
        _exhaust = new CpuParticles2D
        {
            Emitting = false, Amount = 12, Lifetime = 0.7f, Spread = 25f, Direction = new Vector2(-1f, -0.5f),
            InitialVelocityMin = 20f, InitialVelocityMax = 50f, Gravity = new Vector2(0f, -60f),
            ScaleAmountMin = 2f, ScaleAmountMax = 4f, Color = new Color(0.3f, 0.3f, 0.34f, 0.55f),
        };
        AddChild(_exhaust);
        // Exhaust pop: a quick orange flash and a puff of dark smoke out of the tailpipe.
        _backfire = new CpuParticles2D
        {
            Emitting = false, OneShot = true, Explosiveness = 0.9f, Amount = 20, Lifetime = 0.5f, Spread = 28f,
            Direction = new Vector2(-1f, -0.2f), InitialVelocityMin = 70f, InitialVelocityMax = 170f, Gravity = new Vector2(0f, -50f),
            ScaleAmountMin = 4f, ScaleAmountMax = 9f, LocalCoords = false,
            ColorRamp = new Gradient
            {
                Offsets = new[] { 0f, 0.25f, 1f },
                Colors = new[] { new Color(1f, 0.9f, 0.4f, 1f), new Color(1f, 0.45f, 0.1f, 0.85f), new Color(0.2f, 0.2f, 0.22f, 0f) },
            },
        };
        AddChild(_backfire);
        Gearbox.Shifted += (gear, up) =>
        {
            if (up && Mathf.Abs(Throttle) > 0.5f) Backfire(false);
        };
        // Booster plume: hot sparks that cool into smoke, blown out of the nozzle (local +X = backwards).
        _boostSmoke = new CpuParticles2D
        {
            Emitting = false, Amount = 40, Lifetime = 0.6f, Spread = 12f, Direction = new Vector2(1f, 0f),
            InitialVelocityMin = 160f, InitialVelocityMax = 300f, Gravity = new Vector2(0f, -40f),
            ScaleAmountMin = 3f, ScaleAmountMax = 7f, LocalCoords = false,
            ColorRamp = new Gradient
            {
                Offsets = new[] { 0f, 0.25f, 1f },
                Colors = new[] { new Color(1f, 0.85f, 0.3f, 1f), new Color(0.95f, 0.4f, 0.12f, 0.8f), new Color(0.35f, 0.33f, 0.36f, 0f) },
            },
        };
        AddChild(_boostSmoke);
        CreateTurboEffects();
        CreateHydraulicsEffects();
        CreateLights();

        World?.Register(this);
    }

    /// <summary>The pretend gearbox: gear and revs for the engine sound and the dashboard (no effect on physics).</summary>
    public Gearbox Gearbox { get; private set; } = new(5);
    /// <summary>Engine loop to play (from the strongest engine part), e.g. "engine_v8".</summary>
    public string EngineSound { get; private set; } = "engine";
    /// <summary>Name of the strongest engine on the car, for the log.</summary>
    public string EngineName { get; private set; } = "none";
    /// <summary>Exhaust pops so far (flame puffs on upshifts and when lifting off at high revs).</summary>
    public int Backfires { get; private set; }
    /// <summary>Raised on each exhaust pop, with true for a pop on lifting off the throttle (false: on an upshift).</summary>
    public event System.Action<bool> Backfired;

    /// <summary>Current throttle input, -1..1 (for engine sound and effects).</summary>
    public float Throttle => Parked || Fuel <= 0f ? 0f : _input.Throttle;

    public override void _ExitTree() => World?.Unregister(this);

    // ---- Building from a design ------------------------------------------------------------

    private void Build(VehicleDesign design)
    {
        Origin = new Vector2(Mathf.Round(Spawn.X / OriginStep) * OriginStep, 0f);
        Position = Origin;
        Vector2 spawn = Spawn - Origin;
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
            positions.Add(spawn + (new Vector2(gx, gy) - gridCenter) * CellSize);
            masses.Add(0f);
            cornerIndex[(gx, gy)] = index;
            return index;
        }

        // Blocks: four corners each, mass shared out to the corners.
        float strongest = 0f;
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
            _cells.Add(new Cell { Corners = corners, Part = part, GridX = block.X, GridY = block.Y });

            if (part.Category == PartCategory.Engine && part.Torque > strongest)
            {
                strongest = part.Torque;
                EngineSound = string.IsNullOrEmpty(part.Sound) ? "engine" : part.Sound;
                EngineName = part.Name;
                Gearbox = new Gearbox(part.Gears);
            }
            _engineTorque += part.Torque;
            _engineMaxSpin = Mathf.Max(_engineMaxSpin, part.MaxSpin);
            _fuelUse += part.FuelUse;
            FuelCapacity += part.FuelCapacity;
            _boostFuelUse += part.BoostFuelUse;
        }
        foreach (var cell in _cells)
        {
            if (cell.Part.Category == PartCategory.Booster) _boosters.Add(cell);
            SetUpTurbo(cell);
            SetUpHydraulics(cell);
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

            _wheels.Add(new Wheel { Hub = hub, Sprite = wheelPart.Sprite, Radius = wheelPart.Radius, Grip = wheelPart.Grip, FrozenGrip = wheelPart.FrozenGrip, Mass = Mathf.Max(wheelPart.Mass, MinNodeMass) });
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
        _isDrillTip = new bool[count];
        foreach (var d in _drills) _isDrillTip[d.Node] = true;

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
        if (!Parked && !PlayerControlled && AutoHop) _input = new VehicleInput(_input.Throttle, _input.Tilt, _input.Handbrake, _input.Boost, true);
        _centerAtTickStart = BodyCenter();
        Boosting = _input.Boost && !Parked && Fuel > 0f && HasBooster;
    }

    public void Refuel() => Fuel = FuelCapacity;

    /// <summary>
    /// Takes away part of the car's speed at once (ploughing through junk): every node keeps
    /// <paramref name="keep"/> of its velocity, so the car slows without turning or bending.
    /// </summary>
    public void Slow(float keep)
    {
        keep = Mathf.Clamp(keep, 0f, 1f);
        for (int i = 0; i < Pos.Length; i++) Prev[i] = Pos[i] - (Pos[i] - Prev[i]) * keep;
        foreach (var wheel in _wheels) wheel.Spin *= keep;   // or the tyres would spin it straight back up
    }

    /// <summary>Adds fuel (a can picked up on the road), up to the tank size.</summary>
    public void AddFuel(float amount) => Fuel = Mathf.Min(FuelCapacity, Fuel + amount);

    /// <summary>Verlet integration with gravity, plus air control.</summary>
    public void Integrate(float dt)
    {
        Vector2 gravityStep = new Vector2(0f, Gravity) * dt * dt;
        _subDt = dt;
        _firstIteration = true;
        for (int i = 0; i < Pos.Length; i++)
        {
            Vector2 velocity = (Pos[i] - Prev[i]) * AirDrag;
            Prev[i] = Pos[i];
            Pos[i] += velocity + gravityStep;
        }

        StabiliseInAir();
        if (Boosting) ApplyBoost(dt);
        if (_kickPending > 0f)
        {
            float add = Mathf.Min(_kickPending, _kickRate * dt);
            _kickPending -= add;
            ApplyKick(add, dt);
            if (_kickPending <= 0f) _kickDir = 0f;
        }
        UpdateHydraulics(dt);
        if (_hopPending)
        {
            _hopPending = false;
            for (int i = 0; i < Pos.Length; i++) Prev[i].Y += 240f * dt;   // about 240 px/s upwards
        }

        if (_rockPending)
        {
            _rockPending = false;
            Vector2 rc = BodyCenter();
            for (int i = 0; i < Pos.Length; i++)
            {
                Vector2 r = Pos[i] - rc;
                Prev[i] -= (_rockPush + new Vector2(-r.Y, r.X) * _rockSpin) * dt;
            }
        }

        ApplyWhirl(dt);

        // Upside down and stopped: the car rocks itself back onto its wheels.
        float tilt = _input.Tilt;
        // Flying (no wheel down): rotation is stronger, so a full flip fits in a big jump.
        if (!Stuck && !_selfRighting && Airborne) tilt *= AirSpinBoost(tilt, dt);
        else _airHoldSign = 0;
        if (Stuck && !_selfRighting) tilt *= RockTiltBoost;
        // If it stays stuck (wedged on its roof in a dip), the push grows until it rolls over.
        if (_selfRighting && !Parked) tilt = -Mathf.Sign(PitchDegrees()) * Mathf.Min(8f, 2.5f + 2.2f * _rightingTime);

        if (!Mathf.IsZeroApprox(tilt))
        {
            Vector2 c = BodyCenter();
            float spin = tilt * AirControl * dt * dt;
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
        terrain = Local(terrain);
        foreach (var beam in _beams)
            if (!beam.Broken) SolveBeam(beam);

        for (int i = 0; i < Pos.Length; i++)
        {
            if (terrain.Collide(Pos[i], _nodeRadius[i], out Vector2 n, out float depth))
            {
                // On the first pass after integrating, how deep a body node sank into the ground is
                // how fast it was moving into it: depth / substep = approach speed.
                if (_firstIteration && i < BodyNodeCount && !_isDrillTip[i] && depth / _subDt > _bodyImpact)
                {
                    _bodyImpact = depth / _subDt;
                    _hitPoint = Pos[i] - n * _nodeRadius[i];
                    _hitNormal = n;
                }
                Pos[i] += n * depth;
            }
        }
        _firstIteration = false;
    }

    /// <summary>Velocity fixes after the constraint passes: damping, friction, tyres, damage.</summary>
    public void EndSubstep(float dt, ITerrainCollider terrain, IMineable mineable = null)
    {
        var local = Local(terrain, mineable);
        terrain = local;
        if (mineable != null) mineable = local;
        DampSuspension();

        if (mineable != null && DrillEnabled)
        {
            // The tunnel floor is the level the wheels stand on, unless the nose is tilted well down
            // (then the drill digs downward).
            float floorY = float.NaN;
            if (PitchDegrees() < 14f)
            {
                foreach (var w in _wheels)
                    if (w.Attached) floorY = float.IsNaN(floorY) ? Pos[w.Hub].Y + w.Radius : Mathf.Max(floorY, Pos[w.Hub].Y + w.Radius);
            }

            foreach (var drill in _drills)
                if (DrillIntact(drill) && mineable.Mine(Pos[drill.Node], NodeRadius + 6f, drill.Power * dt, floorY))
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
        if (Boosting) Fuel = Mathf.Max(0f, Fuel - _boostFuelUse * dt);

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

    /// <summary>The car's nose direction: from the rear wheel to the front wheel (or along the body).</summary>
    private Vector2 Forward()
    {
        if (_rearHub >= 0 && _frontHub >= 0 && _rearHub != _frontHub)
            return (Pos[_frontHub] - Pos[_rearHub]).Normalized();
        return new Vector2(1f, 0f);
    }

    private void ApplyKick(float speed, float dt)
    {
        Vector2 forward = Forward();
        Vector2 velocity = Vector2.Zero;
        for (int i = 0; i < BodyNodeCount; i++) velocity += Pos[i] - Prev[i];
        float along = velocity.Dot(forward) / Mathf.Max(1, BodyNodeCount) / dt;
        if (_kickDir != 0f)
        {
            // A launch: from a standstill, in the way the throttle points; the tyres spin up with it.
            Vector2 launchDir = forward * (forward.X >= 0f ? _kickDir : -_kickDir);   // along the body, towards the throttle side
            for (int i = 0; i < Pos.Length; i++) Prev[i] -= launchDir * speed * dt;
            foreach (var wheel in _wheels) wheel.Spin += _kickDir * speed / wheel.Radius;
            return;
        }
        if (Mathf.Abs(along) < 40f) return;   // nearly stopped: no direction to push in
        Vector2 dir = forward * Mathf.Sign(along);
        for (int i = 0; i < Pos.Length; i++) Prev[i] -= dir * speed * dt;
        float ratio = (Mathf.Abs(along) + speed) / Mathf.Abs(along);
        foreach (var wheel in _wheels) wheel.Spin *= ratio;
    }

    private bool BoosterIntact(Cell cell)
    {
        int[] c = cell.Corners;
        return Pos[c[0]].DistanceTo(Pos[c[2]]) < CellSize * 2.2f && Pos[c[1]].DistanceTo(Pos[c[3]]) < CellSize * 2.2f;
    }

    /// <summary>
    /// Rocket thrust along the car's nose direction. The push is spread over the whole car by
    /// mass (so it never tears the soft body apart), plus the turning effect of where the
    /// booster sits: one mounted high pushes the nose down, one mounted low lifts it.
    /// Thrust fades out as the car nears the booster's top speed.
    /// </summary>
    private void ApplyBoost(float dt)
    {
        Vector2 forward = Forward();
        Vector2 c = BodyCenter();
        Vector2 velocity = Vector2.Zero;
        for (int i = 0; i < BodyNodeCount; i++) velocity += Pos[i] - Prev[i];
        float forwardSpeed = velocity.Dot(forward) / Mathf.Max(1, BodyNodeCount) / dt;
        Vector2 force = Vector2.Zero;
        float torque = 0f;
        foreach (var cell in _boosters)
        {
            if (!BoosterIntact(cell)) continue;
            float top = cell.Part.BoostSpeed > 0f ? cell.Part.BoostSpeed : 1600f;
            float fade = Mathf.Clamp((top - forwardSpeed) / (top * 0.3f), 0f, 1f);
            Vector2 f = forward * cell.Part.Thrust * fade;
            int[] k = cell.Corners;
            Vector2 at = (Pos[k[0]] + Pos[k[1]] + Pos[k[2]] + Pos[k[3]]) / 4f;
            force += f;
            torque += (at - c).Cross(f);
        }
        if (force == Vector2.Zero || _totalMass <= 0f) return;

        float inertia = 0f;
        for (int i = 0; i < Pos.Length; i++)
            inertia += (Pos[i] - c).LengthSquared() / InvMass[i];
        float spin = inertia > 1f ? torque / inertia * dt * dt : 0f;
        Vector2 push = force / _totalMass * dt * dt;
        for (int i = 0; i < Pos.Length; i++)
        {
            Vector2 r = Pos[i] - c;
            Pos[i] += push + new Vector2(-r.Y, r.X) * spin;
        }
    }

    /// <summary>
    /// While no wheel touches the ground, bleeds off spin so jumps do not turn into endless tumbles.
    /// Tilt input still rotates the car, so air control keeps working.
    /// </summary>
    private void StabiliseInAir()
    {
        foreach (var w in _wheels)
            if (w.Grounded) return;

        Vector2 c = BodyCenter();
        float angular = 0f, inertia = 0f;
        for (int i = 0; i < Pos.Length; i++)
        {
            Vector2 r = Pos[i] - c;
            angular += r.Cross(Pos[i] - Prev[i]);
            inertia += r.LengthSquared();
        }
        if (inertia < 1f) return;
        // Spin the driver is holding Q/E for is theirs: no bleed until it gets really fast (a flip in progress).
        if (_airHoldSign != 0 && Mathf.Sign(angular) == _airHoldSign && Mathf.Abs(angular / inertia) / _subDt < MaxHeldSpin) return;

        float remove = angular / inertia * AirSpinDamping;
        for (int i = 0; i < Pos.Length; i++)
        {
            Vector2 r = Pos[i] - c;
            Prev[i] += new Vector2(-r.Y, r.X) * remove;
        }
    }

    public void EndTick(float delta)
    {
        _overdrive = Mathf.Max(0f, _overdrive - OverdriveFade * delta);
        // Self-righting kicks in after lying upside down, nearly still, for a moment.
        bool upsideDown = Mathf.Abs(PitchDegrees()) > 110f;
        _upsideDownTime = upsideDown && Speed < 60f ? _upsideDownTime + delta : 0f;
        if (_upsideDownTime > 1.2f) _selfRighting = true;
        if (Mathf.Abs(PitchDegrees()) < 50f) _selfRighting = false;
        float before = _rightingTime;
        _rightingTime = _selfRighting ? _rightingTime + delta : 0f;
        // Still stuck: every 1.5 s give it a little hop off the ground so the roll can start.
        if (Mathf.FloorToInt(_rightingTime / 1.5f) > Mathf.FloorToInt(before / 1.5f)) _hopPending = true;

        _drillContactTime = Mathf.Max(0f, _drillContactTime - delta);
        CenterVelocity = (BodyCenter() - _centerAtTickStart) / delta;
        Speed = CenterVelocity.Length();
        UpdateRock(delta);
        DetectImpact(delta);

        // Dust from the rearmost grounded wheel when moving; exhaust smoke under throttle.
        Wheel dusty = null;
        foreach (var w in _wheels)
            if (w.Attached && w.Grounded && (dusty == null || Pos[w.Hub].X < Pos[dusty.Hub].X)) dusty = w;
        _dust.Emitting = dusty != null && (Speed > 90f || Mathf.Abs(dusty.Spin * dusty.Radius) > Speed + 120f);
        if (dusty != null) _dust.Position = ToLocal(Pos[dusty.Hub] + new Vector2(0f, dusty.Radius - 2f));
        _exhaust.Emitting = _rearCell != null && Mathf.Abs(Throttle) > 0.1f;
        _boostSmoke.Emitting = false;
        foreach (var cell in _boosters)
        {
            if (!BoosterIntact(cell)) continue;
            Vector2 nozzle = (Pos[cell.Corners[0]] + Pos[cell.Corners[3]]) / 2f;
            Vector2 back = (Pos[cell.Corners[0]] - Pos[cell.Corners[1]]).Normalized();
            _boostSmoke.Position = ToLocal(nozzle + back * 6f);
            _boostSmoke.Rotation = back.Angle() - GlobalRotation;
            _boostSmoke.Emitting = Boosting;
            break;
        }
        if (_rearCell != null) _exhaust.Position = ToLocal((Pos[_rearCell.Corners[0]] + Pos[_rearCell.Corners[3]]) / 2f);
        UpdateLaunch(delta);
        UpdateGearbox(delta);
        UpdateTurbo(delta);
        UpdateLights();
        Recenter();
        QueueRedraw();
    }

    /// <summary>
    /// Launch control (see the constants at the top): arms with the handbrake and the throttle held at
    /// a standstill on the ground, fills the charge, bounces off the limiter, and grades the start when
    /// the handbrake is let go with the throttle still down. Letting go of the throttle first cancels it.
    /// </summary>
    private void UpdateLaunch(float delta)
    {
        _launchBounceFlash = Mathf.Max(0f, _launchBounceFlash - delta);
        bool grounded = false;
        foreach (var w in _wheels) grounded |= w.Attached && w.Grounded;
        bool holding = !Parked && Fuel > 0f && _input.Handbrake && Mathf.Abs(_input.Throttle) > 0.5f && grounded;

        if (!LaunchArmed)
        {
            if (holding && Speed < LaunchMaxSpeed)
            {
                LaunchArmed = true;
                LaunchCharge = 0f;
                LaunchBounces = 0;
            }
            return;
        }

        if (holding)
        {
            LaunchCharge += delta / LaunchRiseTime;
            if (LaunchCharge >= 1f)
            {
                LaunchCharge = LaunchBounceTo;   // off the limiter: the revs drop back and climb again
                LaunchBounces++;
                _launchBounceFlash = 0.12f;
            }
            return;
        }

        LaunchArmed = false;
        // Let go of the throttle (or ran dry, or got knocked into the air): no launch.
        if (Parked || Fuel <= 0f || Mathf.Abs(_input.Throttle) <= 0.5f || _input.Handbrake || !grounded) return;

        float charge = LaunchCharge;
        LaunchGrade grade = charge >= LaunchPerfectFrom && charge <= LaunchPerfectTo ? LaunchGrade.Perfect
                          : charge >= LaunchGoodFrom ? LaunchGrade.Good : LaunchGrade.Bogged;
        float kick = grade == LaunchGrade.Perfect ? LaunchPerfectKick : grade == LaunchGrade.Good ? LaunchGoodKick : 0f;
        if (kick > 0f) LaunchKick(kick, _input.Throttle);
        if (grade == LaunchGrade.Perfect) Backfire(false);
        Launched?.Invoke(grade, kick);
    }

    /// <summary>
    /// Feeds the pretend gearbox: the fastest attached wheel's spin against the engine's top spin.
    /// Lifting off the throttle at high revs pops the exhaust (at most every 0.6 s).
    /// </summary>
    private void UpdateGearbox(float delta)
    {
        float spin = 0f, radius = 0f;
        int attached = 0;
        foreach (var w in _wheels)
        {
            if (!w.Attached) continue;
            spin = Mathf.Max(spin, Mathf.Abs(w.Spin));
            radius += w.Radius;
            attached++;
        }
        float throttle = Mathf.Abs(Throttle);
        float topSpeed = attached > 0 ? _engineMaxSpin * radius / attached : 0f;   // px/s at the tyre
        Gearbox.Update(delta, _engineMaxSpin > 0f ? spin / _engineMaxSpin : 0f, topSpeed > 0f ? Speed / topSpeed : 0f, throttle, Airborne);
        if (LaunchArmed) Gearbox.HoldRevs(Gearbox.Idle + (1.02f - Gearbox.Idle) * LaunchCharge, _launchBounceFlash > 0f || LaunchCharge > 0.95f);

        _popCooldown = Mathf.Max(0f, _popCooldown - delta);
        if (_lastThrottle > 0.6f && throttle < 0.1f && Gearbox.Rpm > 0.7f) Backfire(true);
        _lastThrottle = throttle;
    }

    private void Backfire(bool liftOff)
    {
        if (_popCooldown > 0f || _rearCell == null || _backfire == null) return;
        _popCooldown = 0.6f;
        Backfires++;
        Vector2 a = Pos[_rearCell.Corners[0]], b = Pos[_rearCell.Corners[3]];
        Vector2 back = (Pos[_rearCell.Corners[0]] - Pos[_rearCell.Corners[1]]).Normalized();
        _backfire.Position = ToLocal((a + b) / 2f + back * 4f);
        _backfire.Rotation = back.Angle() - GlobalRotation + Mathf.Pi;   // particles' Direction is -X: point it out the back
        _backfire.Restart();
        _backfire.Emitting = true;
        Backfired?.Invoke(liftOff);
    }

    /// <summary>Axis-aligned bounds of all nodes including wheel radius, for broad-phase checks.</summary>
    public Rect2 Bounds()
    {
        Rect2 local = LocalBounds();
        return new Rect2(local.Position + Origin, local.Size);
    }

    /// <summary><see cref="Bounds"/> in the car's own frame (relative to <see cref="Origin"/>).</summary>
    public Rect2 LocalBounds()
    {
        if (Pos.Length == 0) return new Rect2(Spawn - Origin, Vector2.Zero);
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
    public bool ContainsPoint(Vector2 p) => ContainsLocalPoint(p - Origin);

    /// <summary><see cref="ContainsPoint"/> for a point in the car's own frame.</summary>
    public bool ContainsLocalPoint(Vector2 p)
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
                bool belowTopSpeed = Mathf.Abs(wheel.Spin) < _engineMaxSpin + TurboExtraSpin + _overdrive / wheel.Radius || Mathf.Sign(wheel.Spin) != Mathf.Sign(_input.Throttle);
                float share = drivenWheels > 0 ? drivenWheels : attachedWheels;
                float torque = _engineTorque * TurboTorqueFactor / share * _input.Throttle;
                if (belowTopSpeed) wheel.Spin += torque / inertia * dt;
            }
        }
        wheel.Spin *= 0.99995f; // rolling resistance

        int h = wheel.Hub;
        wheel.Grounded = terrain.Collide(Pos[h], wheel.Radius + 0.5f, out Vector2 n, out _);
        if (wheel.Grounded)
        {
            wheel.GroundNormal = n;
            Vector2 v = (Pos[h] - Prev[h]) / dt;
            Vector2 tangent = new Vector2(-n.Y, n.X); // along the ground, pointing right on flat ground
            float normalSpeed = v.Dot(n);
            float slip = v.Dot(tangent) - wheel.Spin * wheel.Radius;

            // Tyre friction: an impulse that cancels slip, capped by grip x load.
            float impulse = -slip / (1f / wheel.Mass + wheel.Radius * wheel.Radius / inertia);
            float carried = wheel.Attached && attachedWheels > 0 ? _totalMass / attachedWheels : wheel.Mass;
            float load = carried * Gravity * dt * Mathf.Max(0f, -n.Y) + wheel.Mass * Mathf.Max(0f, -normalSpeed);
            Vector2 under = Pos[h] - n * (wheel.Radius + 4f);
            float surface = 1f;
            wheel.OnFrozen = false;
            wheel.Biting = false;
            if (terrain is ISurfaceInfo info)
            {
                surface = info.GripAt(under);
                wheel.OnFrozen = surface < 1f && info.FrozenAt(under);
                if (wheel.OnFrozen && wheel.FrozenGrip > surface)
                {
                    surface = wheel.FrozenGrip;   // the studs bite into snow and ice
                    wheel.Biting = true;
                }
            }
            float maxImpulse = wheel.Grip * surface * load;
            impulse = Mathf.Clamp(impulse, -maxImpulse, maxImpulse);
            wheel.Slip = slip + impulse * (1f / wheel.Mass + wheel.Radius * wheel.Radius / inertia);   // what is left sliding

            v += tangent * (impulse / wheel.Mass);
            wheel.Spin -= impulse * wheel.Radius / inertia;
            if (normalSpeed < 0f) v -= n * normalSpeed;
            Prev[h] = Pos[h] - v * dt;
        }

        wheel.Angle += wheel.Spin * dt;
    }

    /// <summary>Stuck detection, rock taps and the ROCKED FREE moment (see the constants at the top).</summary>
    private void UpdateRock(float dt)
    {
        _sinceRock += dt;
        bool grounded = !Airborne;
        bool pushing = Mathf.Abs(_input.Throttle) > 0.3f && !_input.Handbrake && !Parked && !_selfRighting && !IsDrilling && !LaunchArmed;

        if (!Stuck)
        {
            _stuckTime = pushing && grounded && Speed < RockStuckSpeed ? _stuckTime + dt : 0f;
            if (_stuckTime >= RockStuckAfter)
            {
                Stuck = true;
                _stuckAt = CenterPosition;   // world frame: Recenter() may move the node frame meanwhile
                _stuckFor = 0f;
                _rocksThisStuck = 0;
                _giveUp = 0f;
                RockChain = 0;
                StuckStarted?.Invoke();
            }
        }
        else
        {
            _stuckFor += dt;
            if (Mathf.Abs(CenterPosition.X - _stuckAt.X) > RockFreeDistance && Speed > 64f)
            {
                RockedFree?.Invoke(_rocksThisStuck, _stuckFor);
                EndStuck();
            }
            else
            {
                // Let go of everything for 1.5 s (or flipped, parked): no longer stuck. A hop that
                // brushes the drill against rock does not count as getting out.
                bool idle = Mathf.Abs(_input.Throttle) < 0.3f && Mathf.Abs(_input.Tilt) < 0.5f;
                _giveUp = idle ? _giveUp + dt : 0f;
                if (_giveUp > 1.5f || _selfRighting || Parked) EndStuck();
            }
        }

        // Tilt taps: a fresh press of Q or E (after holding the other, or nothing) is a rock.
        int sign = _input.Tilt > 0.5f ? 1 : _input.Tilt < -0.5f ? -1 : 0;
        if (sign != _heldTiltSign)
        {
            // A rock pushes off the ground. A tap in the air is kept for a moment and fires on touchdown,
            // so the rhythm survives a bounce but hops never stack in the air (the first CI run, which
            // let them stack, flew the rocket car 13 m up with 13 taps).
            if (sign != 0 && Stuck && _sinceRock >= RockMinGap)
            {
                if (grounded) Rock(sign);
                else
                {
                    _bufferedRock = sign;
                    _bufferedFor = RockBuffer;
                }
            }
            _heldTiltSign = sign;
        }
        if (_bufferedRock != 0)
        {
            _bufferedFor -= dt;
            if (!Stuck || _bufferedFor <= 0f) _bufferedRock = 0;
            else if (grounded)
            {
                Rock(_bufferedRock);
                _bufferedRock = 0;
            }
        }
    }

    private void Rock(int sign)
    {
        // Alternating taps in a rhythm build the chain; anything else starts over.
        bool rhythm = _lastRockSign == -sign && _sinceRock <= RockChainWindow;
        RockChain = rhythm ? Mathf.Min(RockChain + 1, RockChainMax) : 1;
        _lastRockSign = sign;
        _sinceRock = 0f;
        _rocksThisStuck++;
        RockPulses++;

        float hop = 70f + 40f * RockChain;                 // 110..270 px/s up (a 270 px/s hop rises about 37 px, 2.3 tiles)
        float shove = (40f + 20f * RockChain) * Mathf.Sign(_input.Throttle);   // 60..140 px/s (3–8 km/h) the way you push
        _rockPush = new Vector2(shove, -hop);
        _rockSpin = sign * (0.6f + 0.15f * RockChain);     // rad/s, the way you tapped
        _rockPending = true;
        // Unloaded wheels may spin up past a stall for a moment, like the launch kick.
        _overdrive = Mathf.Max(_overdrive, Mathf.Abs(shove));
        Rocked?.Invoke(RockChain, hop);
    }

    private void EndStuck()
    {
        Stuck = false;
        _stuckTime = 0f;
        RockChain = 0;
        _giveUp = 0f;
    }

    /// <summary>
    /// Finds hard hits, two ways: a body node slamming into the ground (its approach speed,
    /// measured in the contact solver), or a landing after real air time (the falling speed at
    /// touchdown, so driving over crests and dips never counts). Dents, snapped beams and lost
    /// wheels from the next few ticks are gathered into the same hit, which is then reported
    /// once through <see cref="Crashed"/>.
    /// </summary>
    private void DetectImpact(float delta)
    {
        _age += delta;
        _impactCooldown = Mathf.Max(0f, _impactCooldown - delta);
        Vector2 velocity = (BodyCenter() - _centerAtTickStart) / delta;

        bool airborne = Airborne;
        _landing = 0f;
        if (airborne)
        {
            _airTimer += delta;
            _airVelocity = velocity;
        }
        else
        {
            if (_airTimer > 0.3f)
            {
                // How fast the car came down into the ground it landed on (not along a matching slope).
                foreach (var w in _wheels)
                    if (w.Attached && w.Grounded) _landing = Mathf.Max(_landing, -_airVelocity.Dot(w.GroundNormal));
            }
            _airTimer = 0f;
        }

        float body = _bodyImpact;
        _bodyImpact = 0f;
        int snaps = _snapsThisTick;
        bool wheelLost = _wheelLostThisTick;
        _snapsThisTick = 0;
        _wheelLostThisTick = false;

        // Ignore the first moment after spawning (the car drops onto its wheels).
        if (_age < 0.6f)
        {
            _dentThisHit = 0f;
            return;
        }

        bool started = _impactTicks >= 0;
        bool trigger = body > ImpactThreshold || _landing > LandingThreshold || snaps > 0 || wheelLost;
        if (!started && (!trigger || (_impactCooldown > 0f && snaps == 0 && !wheelLost)))
        {
            _dentThisHit = 0f;
            return;
        }
        if (!started)
        {
            _impactTicks = 0;
            _impactPeak = 0f;
            _bodyHit = false;
            _snapsThisHit = 0;
            _wheelLostThisHit = false;
        }

        if (body > ImpactThreshold)
        {
            _bodyHit = true;
            _impactPeak = Mathf.Max(_impactPeak, body);
        }
        if (_landing > LandingThreshold) _impactPeak = Mathf.Max(_impactPeak, _landing);
        _snapsThisHit += snaps;
        _wheelLostThisHit |= wheelLost;
        _impactTicks++;
        if (_impactTicks < ImpactGatherTicks) return;

        Vector2 point = _hitPoint, normal = _hitNormal;
        if (!_bodyHit)
        {
            if (_snapsThisHit > 0)
            {
                point = _snapPoint;
            }
            else
            {
                // Landed on the wheels: the hit is under the lowest wheel.
                Wheel low = null;
                foreach (var w in _wheels)
                    if (w.Attached && (low == null || Pos[w.Hub].Y > Pos[low.Hub].Y)) low = w;
                point = low != null ? Pos[low.Hub] + new Vector2(0f, low.Radius) : BodyCenter();
            }
            normal = Vector2.Up;
        }

        Crashed?.Invoke(new CrashEvent(point + Origin, normal, _impactPeak, _bodyHit, _snapsThisHit, _dentThisHit, _wheelLostThisHit, velocity));
        _impactTicks = -1;
        _impactCooldown = 0.25f;
        _dentThisHit = 0f;
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
            _dentThisHit += Mathf.Abs(newRest - beam.Rest);
            beam.Rest = newRest;

            if (Mathf.Abs(beam.Rest - beam.OriginalRest) / beam.OriginalRest > beam.BreakLimit)
            {
                beam.Broken = true;
                Damage += 5f;
                _snapsThisTick++;
                _snapPoint = (Pos[beam.A] + Pos[beam.B]) / 2f;
            }
        }

        foreach (var wheel in _wheels)
        {
            if (!wheel.Attached) continue;
            bool connected = false;
            foreach (var beam in _beams)
                if (beam.Suspension && !beam.Broken && (beam.A == wheel.Hub || beam.B == wheel.Hub))
                    connected = true;
            if (!connected) _wheelLostThisTick = true;
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
    /// One line for the playtest log about why a car is not moving: per wheel (grounded, spin, slip,
    /// hub speed along x, ground normal), body nodes touching the ground, drive torque and mass.
    /// </summary>
    public string StallReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"origin={Origin.X:0} tilt={_input.Tilt:0.0} torque={_engineTorque:0} maxSpin={_engineMaxSpin:0} mass={_totalMass:0.00} throttle={_input.Throttle:0.0} handbrake={_input.Handbrake} overdrive={_overdrive:0}");
        int touching = 0;
        var touched = new System.Text.StringBuilder();
        for (int i = 0; i < BodyNodeCount; i++)
        {
            if (World != null && World.Terrain != null && World.Terrain.Collide(Pos[i] + Origin, NodeRadius + 0.5f, out _, out _))
            {
                touching++;
                if (touched.Length < 80) touched.Append($"{i}@({Pos[i].X:0},{Pos[i].Y:0}) ");
            }
        }
        sb.Append($" bodyTouching={touching} [{touched.ToString().Trim()}]");
        int k = 0;
        foreach (var w in _wheels)
        {
            float hubVx = (Pos[w.Hub].X - Prev[w.Hub].X) * 960f;
            sb.Append($" w{k++}(att={w.Attached} gnd={w.Grounded} spin={w.Spin:0.0} slip={w.Slip:0} hubVx={hubVx:0} n=({w.GroundNormal.X:0.00},{w.GroundNormal.Y:0.00}) hub=({Pos[w.Hub].X + Origin.X:0},{Pos[w.Hub].Y:0}))");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Drives like a careful player, for bots and automated playtests: full throttle,
    /// ease off while drilling, and tilt the nose back down if the car starts to flip.
    /// </summary>
    private int _stuckReports;

    private VehicleInput AutoPilot()
    {
        if (AutoHandbrake) return new VehicleInput(AutoThrottle, 0f, true);
        const float tick = 1f / 120f;
        // Stuck: rock it like a player would (E, Q, E, Q every 0.3 s with the throttle held) for up
        // to 4 s, then fall back to backing up.
        if (!Stuck) _autoRockTime = 0f;
        else if (AutoRock && _autoRockTime < 4f)
        {
            _autoRockTime += tick;
            _autoStuckTime = 0f;
            float rock = ((int)(_autoRockTime / 0.3f) & 1) == 0 ? 1f : -1f;
            return new VehicleInput(AutoThrottle, rock, false);
        }
        if (AutoTilt != 0f)
        {
            // Digging with the nose held down: if the car is wedged (slow, the drill biting nothing)
            // for 3 s, back out for a moment like the plain bot does.
            if (_autoReverseTime > 0f)
            {
                _autoReverseTime -= tick;
                return new VehicleInput(-AutoThrottle * 0.8f, -AutoTilt, false);
            }
            _autoStuckTime = Speed < 15f && !IsDrilling && AutoThrottle != 0f ? _autoStuckTime + tick : 0f;
            if (_autoStuckTime > 3f)
            {
                _autoStuckTime = 0f;
                _autoReverseTime = 1.2f;
                GD.Print($"[autoplay] wedged while digging at x={CenterPosition.X / 16f:0.0} pitch={PitchDegrees():0}: backing out | {StallReport()}");
            }
            return new VehicleInput(AutoThrottle, AutoTilt, false, AutoBoostNow());
        }

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
            if (_stuckReports++ < 4) GD.Print($"[autoplay] stuck at x={CenterPosition.X / 16f:0.0} pitch={PitchDegrees():0}: backing up | {StallReport()}");
        }

        return new VehicleInput(throttle, tilt, false, AutoBoostNow());
    }

    /// <summary>Bots boost like a sensible player: driving forwards, nose not too high or low, not drilling.</summary>
    private bool AutoBoostNow()
    {
        if (!AutoBoost || AutoThrottle <= 0f || IsDrilling || _selfRighting) return false;
        float pitch = PitchDegrees();
        return pitch > -24f && pitch < 20f && Fuel > FuelCapacity * 0.1f;
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
                // Crystal-tipped bits (power over 100) glow cyan; the plain steel bit is grey.
                Color color = drill.Power > 100f
                    ? (light ? new Color(0.62f, 1f, 0.97f) : new Color(0.2f, 0.66f, 0.8f))
                    : (light ? new Color(0.82f, 0.84f, 0.88f) : new Color(0.5f, 0.52f, 0.58f));
                if (i < bands - 1) FillQuad(a0, a1, b1, b0, color);
                else FillTriangleLocal(a0, tip, b0, color);
            }
            DrawLine(top, tip, new Color(0.13f, 0.11f, 0.16f), 1.5f);
            DrawLine(bottom, tip, new Color(0.13f, 0.11f, 0.16f), 1.5f);
        }

        // Rocket boosters: a nozzle bell out of the back, and a flickering flame while firing.
        foreach (var cell in _boosters)
        {
            if (!BoosterIntact(cell)) continue;
            int[] c = cell.Corners;
            Vector2 top = Pos[c[0]], bottom = Pos[c[3]];
            Vector2 back = (Pos[c[0]] - Pos[c[1]]).Normalized();
            Vector2 mid = (top + bottom) / 2f, across = (bottom - top) / 2f;
            Vector2 bellA = mid - across * 0.45f, bellB = mid + across * 0.45f;
            Vector2 mouthA = mid - across * 0.8f + back * 7f, mouthB = mid + across * 0.8f + back * 7f;
            var dark = new Color(0.13f, 0.11f, 0.16f);
            FillQuad(ToLocal(bellA), ToLocal(mouthA), ToLocal(mouthB), ToLocal(bellB), dark);
            FillQuad(ToLocal(mid - across * 0.3f + back), ToLocal(mid - across * 0.6f + back * 6f), ToLocal(mid + across * 0.6f + back * 6f), ToLocal(mid + across * 0.3f + back),
                new Color(0.42f, 0.44f, 0.5f));
            if (!Boosting) continue;
            float flicker = 0.75f + 0.25f * Mathf.Sin(Time.GetTicksMsec() * 0.06f) + (float)GD.RandRange(-0.12, 0.12);
            Vector2 mouth = mid + back * 7f;
            FillTriangleLocal(ToLocal(mouth - across * 0.7f), ToLocal(mouth + back * 46f * flicker), ToLocal(mouth + across * 0.7f), new Color(0.98f, 0.45f, 0.12f, 0.9f));
            FillTriangleLocal(ToLocal(mouth - across * 0.45f), ToLocal(mouth + back * 28f * flicker), ToLocal(mouth + across * 0.45f), new Color(1f, 0.85f, 0.3f));
            FillTriangleLocal(ToLocal(mouth - across * 0.2f), ToLocal(mouth + back * 12f * flicker), ToLocal(mouth + across * 0.2f), new Color(1f, 1f, 0.9f));
        }

        DrawTurbo();
        DrawHydraulics();

        // Dark outline around the body silhouette.
        var outlineColor = new Color(0.13f, 0.11f, 0.16f);
        foreach (var (a, b) in BoundaryEdges)
            if (Pos[a].DistanceTo(Pos[b]) < CellSize * 2f)
                DrawLine(ToLocal(Pos[a]), ToLocal(Pos[b]), outlineColor, 2f);

        // Headlight and tail light.
        if (_frontCell != null)
        {
            Vector2 at = ToLocal(Pos[_frontCell.Corners[1]].Lerp(Pos[_frontCell.Corners[2]], 0.45f)) + new Vector2(-3f, 0f);
            DrawCircle(at, 4.5f, outlineColor);
            DrawCircle(at, 3.2f, new Color(1f, 0.93f, 0.55f));
            DrawCircle(at + new Vector2(-1f, -1f), 1.2f, Colors.White);
        }
        if (_rearCell != null)
        {
            Vector2 at = ToLocal(Pos[_rearCell.Corners[0]].Lerp(Pos[_rearCell.Corners[3]], 0.45f)) + new Vector2(3f, 0f);
            DrawRect(new Rect2(at + new Vector2(-2.5f, -3.5f), new Vector2(5f, 7f)), outlineColor);
            DrawRect(new Rect2(at + new Vector2(-1.5f, -2.5f), new Vector2(3f, 5f)), new Color(0.95f, 0.2f, 0.18f));
        }

        // Fenders arching over each wheel.
        float lean = Mathf.DegToRad(PitchDegrees());
        foreach (var wheel in _wheels)
        {
            if (!wheel.Attached) continue;
            Vector2 hub = ToLocal(Pos[wheel.Hub]);
            DrawArc(hub, wheel.Radius + 4f, lean + Mathf.Pi * 1.1f, lean + Mathf.Pi * 1.9f, 12, outlineColor, 5f);
            DrawArc(hub, wheel.Radius + 4f, lean + Mathf.Pi * 1.12f, lean + Mathf.Pi * 1.88f, 12, new Color(0.62f, 0.17f, 0.13f), 3f);
        }

        DrawLightLenses();

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

