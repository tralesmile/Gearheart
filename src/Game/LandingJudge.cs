using Gearheart.Vehicles;
using Godot;

namespace Gearheart.Game;

public enum LandingGrade { Perfect, Clean, Rough }

/// <summary>The verdict on one landing after a jump (see <see cref="LandingJudge"/>).</summary>
public readonly record struct Landing(LandingGrade Grade, float Air, float AllDownAfter, float Mismatch, bool BodyHit, float DamageTaken);

/// <summary>
/// Grades the landing after every real jump, like Hill Climb Racing's flip and landing bonuses:
/// <list type="bullet">
/// <item>PERFECT: every wheel down within a blink of the first, the car matching the slope it lands on,
/// no metal on the ground and no damage. Worth a speed kick.</item>
/// <item>CLEAN: all wheels down and the body never touched.</item>
/// <item>ROUGH: anything else that ends on the wheels (a bounce, a body scrape).</item>
/// </list>
/// Landings on the body (roof, nose, tail) are handled by the scene from the crash event.
/// Start it at touchdown, feed it every physics frame, and it answers once, a quarter second later.
/// </summary>
public sealed class LandingJudge
{
    public const float MinAir = 0.6f;          // shorter hops are not judged
    public const float Window = 0.25f;         // seconds watched after touchdown
    public const float PerfectSpread = 0.12f;  // all wheels down within this long of the first
    public const float PerfectAngle = 15f;     // degrees between the car and the slope at touchdown

    private bool _judging, _bodyHit;
    private float _t, _air, _allDownAt, _mismatch, _damageAtStart;

    public bool Judging => _judging;

    /// <summary>The body (not a wheel) hit the ground: no clean landing this time.</summary>
    public void NoteBodyHit()
    {
        if (_judging) _bodyHit = true;
    }

    public void Cancel() => _judging = false;

    /// <summary>Call on the frame the wheels touch down after <paramref name="air"/> seconds in the air.</summary>
    public void Start(SoftVehicle car, float air)
    {
        _judging = true;
        _bodyHit = false;
        _t = 0f;
        _air = air;
        _mismatch = car.GroundMismatchDegrees();
        _damageAtStart = car.Damage;
        _allDownAt = car.WheelsGrounded >= car.WheelsAttached ? 0f : -1f;
    }

    /// <summary>Returns the verdict once the window has passed, otherwise null.</summary>
    public Landing? Update(SoftVehicle car, float dt)
    {
        if (!_judging) return null;
        _t += dt;
        if (_allDownAt < 0f && car.WheelsAttached > 0 && car.WheelsGrounded >= car.WheelsAttached) _allDownAt = _t;
        if (_t < Window) return null;

        _judging = false;
        float damage = car.Damage - _damageAtStart;
        LandingGrade grade =
            _allDownAt >= 0f && _allDownAt <= PerfectSpread && _mismatch >= 0f && _mismatch <= PerfectAngle && !_bodyHit && damage < 0.5f
                ? LandingGrade.Perfect
                : _allDownAt >= 0f && !_bodyHit ? LandingGrade.Clean
                : LandingGrade.Rough;
        return new Landing(grade, _air, _allDownAt, _mismatch, _bodyHit, damage);
    }

    /// <summary>Extra speed for a perfect landing, in px/s: about 9 km/h after a short jump, up to about 17 km/h after a long one.</summary>
    public static float KickFor(float air) => 160f + 70f * Mathf.Clamp(air - MinAir, 0f, 2f);
}
