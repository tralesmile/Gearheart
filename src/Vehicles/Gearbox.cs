using Godot;

namespace Gearheart.Vehicles;

/// <summary>
/// A pretend gearbox for sound and the dashboard. The car's physics has no gears (the engine pushes
/// the wheels up to their top spin); this splits that range into gears so the engine note climbs,
/// drops at each shift and climbs again, like a real car. It never touches the physics.
///
/// Inputs are the wheel spin and the car's road speed, both as a fraction of the engine's top
/// speed (0 = still, 1 = the engine's top spin at the tyre). In this soft-body physics the wheels
/// spin near their top almost all the time (the tyres slip; the car cruises at about half that), so
/// revs and gears follow the road speed, with a little extra for real wheelspin. Gears are picked
/// from the smoothed road speed with a pause after each shift, so bumps do not make it hunt.
/// Lower gears are shorter, like a real box. In the air the clutch is "in": the gear holds and the
/// revs climb towards the limiter with the throttle open.
/// </summary>
public sealed class Gearbox
{
    public const float Idle = 0.14f;          // revs at a standstill, as a fraction of the redline
    private const float UpshiftAt = 0.9f;     // road speed as a share of this gear's top
    private const float DownshiftAt = 0.55f;  // road speed as a share of the lower gear's top; below it drops a gear
    private const float ShiftTime = 0.16f;    // revs dip and hold for a moment while the gear goes in
    private const float MinShiftGap = 0.6f;   // seconds after a shift before the next one

    private readonly float[] _top;            // top of each gear as a fraction of the top spin
    private float _rpm = Idle, _shiftTimer, _road, _roadFast, _sinceShift = 10f;

    public int Gears => _top.Length;
    /// <summary>Current gear, 1-based.</summary>
    public int Gear { get; private set; } = 1;
    /// <summary>Engine revs as a fraction of the redline (Idle..about 1.05).</summary>
    public float Rpm => _rpm;
    /// <summary>True while the revs are pinned at the limiter.</summary>
    public bool Limiter { get; private set; }
    public int Upshifts { get; private set; }
    public int Downshifts { get; private set; }
    public int TopGear { get; private set; } = 1;
    public float PeakRpm { get; private set; }

    /// <summary>Raised when a gear goes in: (new gear, true for an upshift).</summary>
    public event System.Action<int, bool> Shifted;

    public Gearbox(int gears)
    {
        gears = Mathf.Clamp(gears, 2, 8);
        _top = new float[gears];
        // Gear k tops out at 0.85 (k/n)^0.8 of the top spin's road speed: first gear is short, the
        // top gears long. With the V6 on standard wheels that is 16, 27, 38, 48 and 57 km/h, which
        // spreads the shifts over the speeds the cars really drive (25-50 km/h, 60+ with the booster).
        for (int k = 1; k <= gears; k++) _top[k - 1] = 0.85f * Mathf.Pow(k / (float)gears, 0.8f);
    }

    /// <summary>
    /// Advance by <paramref name="dt"/>. <paramref name="spin"/> is wheel spin / top spin (≥ 0),
    /// <paramref name="road"/> the car's speed / top speed, <paramref name="throttle"/> the pedal (0..1),
    /// <paramref name="airborne"/> true with no wheel down.
    /// </summary>
    public void Update(float dt, float spin, float road, float throttle, bool airborne)
    {
        spin = Mathf.Max(0f, spin);
        throttle = Mathf.Clamp(throttle, 0f, 1f);
        _shiftTimer = Mathf.Max(0f, _shiftTimer - dt);
        _sinceShift += dt;
        road = Mathf.Clamp(road, 0f, 1.3f);
        if (!airborne)
        {
            _road = Mathf.Lerp(_road, road, 1f - Mathf.Exp(-4f * dt));
            _roadFast = Mathf.Lerp(_roadFast, road, 1f - Mathf.Exp(-12f * dt));
        }

        if (!airborne && _sinceShift >= MinShiftGap)
        {
            if (Gear < Gears && _road / _top[Gear - 1] > UpshiftAt && throttle > 0.05f) Shift(+1);
            else if (Gear > 1 && _road / _top[Gear - 2] < DownshiftAt) Shift(-1);
        }

        // Revs for the road speed in this gear, plus a little for wheelspin well beyond the usual slip
        // (a standing start, spinning up a hill); first gear with the pedal down never sits at idle.
        float slip = 0.3f * Mathf.Clamp(spin - _roadFast - 0.3f, 0f, 1f) * (throttle > 0.05f ? 1f : 0f);
        float target = Mathf.Max(_roadFast / _top[Gear - 1] + slip, Idle + 0.25f * throttle * (Gear == 1 ? 1f : 0f));
        if (_shiftTimer > 0f) target = Mathf.Min(target, _rpm);   // hold the dip while the gear goes in
        target = Mathf.Clamp(target, Idle, 1.05f);

        if (airborne && throttle > 0.05f)
            _rpm = Mathf.Min(1.05f, _rpm + 1.6f * dt * throttle);                            // clutch in: revs climb freely
        else if (airborne)
            _rpm = Mathf.Max(Idle, _rpm - 1.2f * dt);                                        // off the pedal in the air: revs sag
        else
        {
            float rate = target > _rpm ? 9f : 14f;   // revs fall faster than they rise (the shift "drop")
            _rpm = Mathf.Lerp(_rpm, target, 1f - Mathf.Exp(-rate * dt));
        }
        Limiter = _rpm > 0.985f && throttle > 0.05f;
        PeakRpm = Mathf.Max(PeakRpm, _rpm);
    }

    private void Shift(int by)
    {
        Gear += by;
        _shiftTimer = by > 0 ? ShiftTime : 0.05f;
        _sinceShift = 0f;
        if (by > 0) Upshifts++; else Downshifts++;
        TopGear = Mathf.Max(TopGear, Gear);
        Shifted?.Invoke(Gear, by > 0);
    }
}
