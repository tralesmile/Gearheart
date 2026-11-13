using Gearheart.Parts;
using Godot;

namespace Gearheart.Vehicles;

/// <summary>
/// The Hydraulics part: a lowrider hop on a button (W, Up, or B on a gamepad).
///
/// Hold the key and the rams pull the body down onto its springs (a visible squat) while a charge
/// builds to full in <see cref="HopChargeTime"/>; let go and the whole car is thrown upwards along
/// its own "up" (mostly) at HopTapSpeed..HopSpeed. A quick tap is a small hop (about 0.5 m), a full
/// squat about 2 m. You can squat in the air and let go just as the wheels touch: the hop fires on
/// touchdown (a short buffer), which turns a landing into a bounce. One hop per touchdown, a short
/// recharge after each, and a little fuel per hop, so it cannot climb walls on its own.
/// Only the strongest Hydraulics on a car count: two do not stack.
/// </summary>
public partial class SoftVehicle
{
    public const float HopChargeTime = 0.5f;      // seconds of squat to full charge
    private const float HopRecharge = 0.7f;       // seconds after a hop before the next one
    private const float HopSquat = 1100f;         // px/s² pressing the body down onto its springs while squatting
    private const float HopFuel = 0.6f;           // fuel units per hop
    private const float HopBuffer = 0.25f;        // a release in the air fires if the wheels touch within this
    private const float HopWorldUp = 0.25f;       // share of straight-up mixed into the car's own up

    private Cell _hydraulicsCell;
    private float _hopSpeed, _hopTapSpeed;
    private bool _hopHeld;
    private float _hopRecharge;
    private float _hopBufferLeft;
    private float _hopBufferCharge;
    private CpuParticles2D _hopPuff;
    // Tracking the hop in flight (for the call-out and the bot log).
    private bool _hopFlying, _hopLeftGround;
    private float _hopStartY, _hopRise, _hopAir, _hopSince;

    /// <summary>True when the car carries working Hydraulics.</summary>
    public bool HasHydraulics => _hydraulicsCell != null && BoosterIntact(_hydraulicsCell);
    /// <summary>The squat charge, 0..1 (0 when not squatting).</summary>
    public float HopCharge { get; private set; }
    /// <summary>True while the hop key is held and the rams are charging.</summary>
    public bool HopCharging { get; private set; }
    /// <summary>Hops fired so far.</summary>
    public int Hops { get; private set; }
    /// <summary>Bots and tests: hold the hop key while true.</summary>
    public bool AutoHop { get; set; }
    /// <summary>Raised when the car hops: the charge (0..1) and the upward speed in px/s.</summary>
    public event System.Action<float, float> Hopped;
    /// <summary>Raised when a hop comes back down: how high the car's middle rose (px) and the air time (s; 0 if it never left the ground).</summary>
    public event System.Action<float, float> HopLanded;

    /// <summary>Called while the car is built: remembers the strongest Hydraulics.</summary>
    private void SetUpHydraulics(Cell cell)
    {
        if (cell.Part.Category != PartCategory.Hydraulics) return;
        if (_hydraulicsCell != null && cell.Part.HopSpeed <= _hopSpeed) return;
        _hydraulicsCell = cell;
        _hopSpeed = cell.Part.HopSpeed > 0f ? cell.Part.HopSpeed : 560f;
        _hopTapSpeed = cell.Part.HopTapSpeed > 0f ? cell.Part.HopTapSpeed : _hopSpeed * 0.6f;
    }

    private void CreateHydraulicsEffects()
    {
        if (_hydraulicsCell == null) return;
        _hopPuff = new CpuParticles2D
        {
            Emitting = false, OneShot = true, Explosiveness = 0.9f, Amount = 22, Lifetime = 0.5f, Spread = 80f,
            Direction = new Vector2(0f, -1f), InitialVelocityMin = 50f, InitialVelocityMax = 170f, Gravity = new Vector2(0f, 60f),
            ScaleAmountMin = 3f, ScaleAmountMax = 7f, LocalCoords = false,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle, EmissionRectExtents = new Vector2(40f, 3f),
            ColorRamp = new Gradient
            {
                Offsets = new[] { 0f, 1f },
                Colors = new[] { new Color(0.92f, 0.9f, 0.86f, 0.8f), new Color(0.8f, 0.78f, 0.74f, 0f) },
            },
        };
        AddChild(_hopPuff);
    }

    /// <summary>Every physics substep: squat while held, hop on release, track the hop in flight.</summary>
    private void UpdateHydraulics(float dt)
    {
        if (_hydraulicsCell == null) return;
        _hopRecharge = Mathf.Max(0f, _hopRecharge - dt);
        bool working = HasHydraulics && !Parked && !_selfRighting && Fuel > 0f;
        bool held = _input.Hop && working;
        bool grounded = !Airborne;

        if (held)
        {
            if (!_hopHeld) HopCharge = 0f;
            HopCharging = true;
            HopCharge = Mathf.Min(1f, HopCharge + dt / HopChargeTime);
            if (grounded)
            {
                // Pull the body (not the wheels) down onto the springs: the car visibly squats.
                Vector2 down = -CarUp();
                Vector2 step = down * HopSquat * HopCharge * dt * dt;
                for (int i = 0; i < BodyNodeCount; i++) Pos[i] += step;
            }
        }
        else if (_hopHeld && HopCharging)
        {
            HopCharging = false;
            if (!working) HopCharge = 0f;
            else if (grounded && _hopRecharge <= 0f) FireHop(HopCharge, dt);
            else
            {
                _hopBufferLeft = HopBuffer;
                _hopBufferCharge = HopCharge;
            }
        }
        _hopHeld = held;

        if (_hopBufferLeft > 0f)
        {
            _hopBufferLeft -= dt;
            if (held || !working) _hopBufferLeft = 0f;
            else if (grounded && _hopRecharge <= 0f)
            {
                _hopBufferLeft = 0f;
                FireHop(_hopBufferCharge, dt);
            }
        }
        if (!HopCharging && _hopBufferLeft <= 0f) HopCharge = 0f;

        if (_hopFlying)
        {
            _hopSince += dt;
            _hopRise = Mathf.Max(_hopRise, _hopStartY - BodyCenter().Y);
            if (!grounded)
            {
                _hopLeftGround = true;
                _hopAir += dt;
            }
            else if (_hopLeftGround || _hopSince > 0.35f)
            {
                _hopFlying = false;
                HopLanded?.Invoke(_hopRise, _hopLeftGround ? _hopAir : 0f);
            }
        }
    }

    private void FireHop(float charge, float dt)
    {
        float speed = Mathf.Lerp(_hopTapSpeed, _hopSpeed, Mathf.Clamp(charge, 0f, 1f));
        Vector2 dir = (CarUp() * (1f - HopWorldUp) + new Vector2(0f, -1f) * HopWorldUp).Normalized();
        // Cancel any sinking first (the squat, a bounce), so every hop starts from the same footing.
        Vector2 v = Vector2.Zero;
        for (int i = 0; i < Pos.Length; i++) v += Pos[i] - Prev[i];
        float sink = Mathf.Max(0f, (v / Pos.Length).Dot(-dir));
        Vector2 add = dir * (speed * dt + sink);
        for (int i = 0; i < Pos.Length; i++) Prev[i] -= add;

        Fuel = Mathf.Max(0f, Fuel - HopFuel);
        _hopRecharge = HopRecharge;
        HopCharge = 0f;
        Hops++;
        _hopFlying = true;
        _hopLeftGround = false;
        _hopStartY = BodyCenter().Y;
        _hopRise = 0f;
        _hopAir = 0f;
        _hopSince = 0f;
        if (_hopPuff != null && _rearHub >= 0 && _frontHub >= 0)
        {
            _hopPuff.Position = ToLocal((Pos[_rearHub] + Pos[_frontHub]) / 2f + new Vector2(0f, 14f));
            _hopPuff.Amount = 10 + (int)(16 * charge);
            _hopPuff.Restart();
            _hopPuff.Emitting = true;
        }
        Hopped?.Invoke(charge, speed);
    }

    /// <summary>The car's own "up": square to the line from the rear wheel to the front wheel.</summary>
    private Vector2 CarUp()
    {
        Vector2 f = Forward();
        return new Vector2(f.Y, -f.X);
    }

    /// <summary>The pressure gauge: a small bar over the Hydraulics block that fills while you squat.</summary>
    private void DrawHydraulics()
    {
        if (!HasHydraulics || HopCharge <= 0.01f) return;
        int[] c = _hydraulicsCell.Corners;
        Vector2 a = ToLocal(Pos[c[0]]), b = ToLocal(Pos[c[1]]);
        Vector2 along = b - a;
        Vector2 lift = new Vector2(along.Y, -along.X).Normalized() * 6f;
        Vector2 start = a + lift, end = b + lift;
        DrawLine(start, end, new Color(0.1f, 0.1f, 0.12f, 0.8f), 5f);
        Color fill = new Color(1f, 0.85f, 0.25f).Lerp(new Color(1f, 0.3f, 0.15f), HopCharge);
        if (HopCharge >= 1f && Mathf.PosMod(Time.GetTicksMsec() / 1000f, 0.2f) < 0.1f) fill = fill.Lightened(0.5f);
        DrawLine(start, start + (end - start) * HopCharge, fill, 3f);
    }
}
