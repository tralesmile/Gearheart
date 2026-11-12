using Gearheart.Parts;
using Godot;

namespace Gearheart.Vehicles;

/// <summary>
/// The Turbocharger part. Boost pressure (0..1) builds while the throttle is held, faster the higher
/// the engine revs (turbo lag: nothing below about a third of the redline), drops quickly when you
/// lift off, and sags a little on every upshift. At full boost the engine has TurboTorque more torque
/// and TurboSpin more top spin, and burns a little more fuel. Lifting off with boost up vents it:
/// the blow-off "pssh" and a puff of air out of the turbo.
///
/// Launch control holds the revs high while armed, so a launch spools the turbo before you go.
/// The revs come from the pretend gearbox, which so far was sound and show only; the turbo is the
/// first thing that reads it to change the physics (see docs/DECISIONS.md).
/// Only the best turbo on a car counts: two turbos do not stack.
/// </summary>
public partial class SoftVehicle
{
    private const float TurboSpoolFromRpm = 0.3f;   // below this the turbo does not spool at all
    private const float TurboFullRateRpm = 0.8f;    // at or above this it spools at the part's full rate
    private const float TurboBleedRate = 2.6f;      // boost lost per second off the throttle
    private const float TurboShiftSag = 0.25f;      // boost lost on an upshift (the gear change vents a little)
    private const float TurboBlowOffMin = 0.45f;    // lifting off above this boost blows off audibly
    private const float TurboFuelExtra = 0.3f;      // extra fuel use at full boost

    private Cell _turboCell;
    private float _turboTorque, _turboSpin, _spoolTime = 1f;
    private bool _turboOnPower;
    private float _turboWheelAngle, _turboFullTime;
    private CpuParticles2D _blowOffPuff;

    /// <summary>True when the car carries a working Turbocharger.</summary>
    public bool HasTurbo => _turboCell != null && BoosterIntact(_turboCell);
    /// <summary>Boost pressure, 0..1.</summary>
    public float TurboBoost { get; private set; }
    /// <summary>Highest boost so far, seconds spent at 0.9+ boost, and blow-offs (for the bot log).</summary>
    public float TurboPeak { get; private set; }
    public float TurboFullSeconds { get; private set; }
    public int TurboBlowOffs { get; private set; }
    /// <summary>Raised when the turbo vents on lift-off: the boost it had (0..1).</summary>
    public event System.Action<float> TurboBlewOff;
    /// <summary>Raised once each time boost first reaches full after being low.</summary>
    public event System.Action TurboFull;

    /// <summary>Torque multiplier the turbo adds right now (1 = none).</summary>
    private float TurboTorqueFactor => 1f + _turboTorque * TurboBoost;
    /// <summary>Extra top spin (rad/s) the turbo adds right now.</summary>
    private float TurboExtraSpin => _engineMaxSpin * _turboSpin * TurboBoost;

    /// <summary>Called while the car is built: remembers the best turbo part.</summary>
    private void SetUpTurbo(Cell cell)
    {
        if (cell.Part.Category != PartCategory.Turbo) return;
        if (_turboCell != null && cell.Part.TurboTorque <= _turboTorque) return;
        _turboCell = cell;
        _turboTorque = cell.Part.TurboTorque;
        _turboSpin = cell.Part.TurboSpin;
        _spoolTime = Mathf.Max(0.1f, cell.Part.SpoolTime);
    }

    private void CreateTurboEffects()
    {
        if (_turboCell == null) return;
        _blowOffPuff = new CpuParticles2D
        {
            Emitting = false, OneShot = true, Explosiveness = 0.85f, Amount = 16, Lifetime = 0.45f, Spread = 35f,
            Direction = new Vector2(0f, -1f), InitialVelocityMin = 60f, InitialVelocityMax = 150f, Gravity = new Vector2(0f, -30f),
            ScaleAmountMin = 3f, ScaleAmountMax = 7f, LocalCoords = false,
            ColorRamp = new Gradient
            {
                Offsets = new[] { 0f, 1f },
                Colors = new[] { new Color(0.95f, 0.97f, 1f, 0.75f), new Color(0.85f, 0.88f, 0.92f, 0f) },
            },
        };
        AddChild(_blowOffPuff);
        Gearbox.Shifted += (gear, up) =>
        {
            if (up) TurboBoost = Mathf.Max(0f, TurboBoost - TurboShiftSag);
        };
    }

    /// <summary>Once per frame, after the gearbox: spool up, bleed off, blow off.</summary>
    private void UpdateTurbo(float delta)
    {
        if (!HasTurbo)
        {
            TurboBoost = 0f;
            _turboOnPower = false;
            return;
        }
        float throttle = Mathf.Abs(Throttle);
        bool onPower = throttle > 0.5f && !Parked && Fuel > 0f;
        float before = TurboBoost;
        if (onPower)
        {
            float rate = Mathf.Clamp((Gearbox.Rpm - TurboSpoolFromRpm) / (TurboFullRateRpm - TurboSpoolFromRpm), 0f, 1f) / _spoolTime;
            TurboBoost = Mathf.Min(1f, TurboBoost + rate * throttle * delta);
        }
        else
        {
            TurboBoost = Mathf.Max(0f, TurboBoost - TurboBleedRate * delta);
            if (_turboOnPower && before >= TurboBlowOffMin) BlowOff(before);
        }
        _turboOnPower = onPower;

        if (TurboBoost >= 0.99f && before < 0.99f && _turboFullTime <= 0f) TurboFull?.Invoke();
        _turboFullTime = TurboBoost >= 0.9f ? _turboFullTime + delta : 0f;
        if (TurboBoost >= 0.9f) TurboFullSeconds += delta;
        TurboPeak = Mathf.Max(TurboPeak, TurboBoost);
        _turboWheelAngle += delta * (4f + 70f * TurboBoost);
        if (TurboBoost > 0f && throttle > 0f) Fuel = Mathf.Max(0f, Fuel - _fuelUse * TurboFuelExtra * TurboBoost * throttle * delta);
    }

    private void BlowOff(float boost)
    {
        TurboBlowOffs++;
        if (_blowOffPuff != null)
        {
            int[] c = _turboCell.Corners;
            _blowOffPuff.Position = ToLocal((Pos[c[0]] + Pos[c[1]]) / 2f);
            _blowOffPuff.Amount = 8 + (int)(10 * boost);
            _blowOffPuff.Restart();
            _blowOffPuff.Emitting = true;
        }
        TurboBlewOff?.Invoke(boost);
    }

    /// <summary>The compressor wheel: a dark ring on the turbo block with blades that spin with the boost; it glows when hot.</summary>
    private void DrawTurbo()
    {
        if (!HasTurbo) return;
        int[] c = _turboCell.Corners;
        Vector2 centre = ToLocal((Pos[c[0]] + Pos[c[1]] + Pos[c[2]] + Pos[c[3]]) / 4f);
        float r = CellSize * 0.3f;
        var dark = new Color(0.13f, 0.11f, 0.16f);
        DrawCircle(centre, r + 1.5f, dark);
        DrawCircle(centre, r, new Color(0.55f, 0.58f, 0.64f).Lerp(new Color(1f, 0.55f, 0.2f), Mathf.Clamp(TurboBoost - 0.6f, 0f, 0.4f) * 1.5f));
        for (int i = 0; i < 6; i++)
        {
            float a = _turboWheelAngle + i * Mathf.Tau / 6f;
            Vector2 tip = centre + Vector2.FromAngle(a) * (r - 0.5f);
            Vector2 bend = centre + Vector2.FromAngle(a + 0.6f) * (r * 0.55f);
            DrawLine(centre, bend, dark, 1.2f);
            DrawLine(bend, tip, dark, 1.2f);
        }
        DrawCircle(centre, 1.6f, new Color(0.85f, 0.87f, 0.9f));
    }
}
