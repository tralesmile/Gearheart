using Godot;

namespace Gearheart.Vehicles;

/// <summary>
/// A dust devil's grip on the car (see World.DustDevils). Each substep the car's velocity is eased
/// towards the whirlwind's: up at <see cref="WhirlLiftSpeed"/>, sideways towards a mix of its own
/// speed and the devil's drift, and its rotation towards the devil's spin. The pull is a uniform
/// velocity change plus a rigid turn, so the soft body is moved, never bent. Set every physics frame
/// with <see cref="Whirl"/>; a grip of 0 lets go.
/// </summary>
public partial class SoftVehicle
{
    public const float WhirlLiftSpeed = 900f;   // px/s upwards (14 m/s) at full grip
    private const float WhirlLiftRate = 9f;     // 1/s at full grip
    private const float WhirlSideRate = 2.5f;
    private const float WhirlSpinRate = 3.5f;

    private float _whirlGrip, _whirlVx, _whirlSpin;

    /// <summary>How hard a whirlwind holds the car right now (0..1).</summary>
    public float WhirlGrip => _whirlGrip;

    /// <summary>
    /// Holds the car in a whirlwind for this frame: <paramref name="grip"/> 0..1, the sideways speed it
    /// pulls the car towards (px/s) and the spin it twists it towards (rad/s, positive = clockwise on screen).
    /// </summary>
    public void Whirl(float grip, float targetVx, float spin)
    {
        _whirlGrip = Mathf.Clamp(grip, 0f, 1f);
        _whirlVx = targetVx;
        _whirlSpin = spin;
    }

    private void ApplyWhirl(float dt)
    {
        if (_whirlGrip <= 0f || dt <= 0f) return;
        Vector2 c = BodyCenter();
        Vector2 v = Vector2.Zero;
        float angular = 0f, inertia = 0f;
        for (int i = 0; i < BodyNodeCount; i++)
        {
            Vector2 step = Pos[i] - Prev[i];
            v += step;
            Vector2 r = Pos[i] - c;
            angular += r.Cross(step);
            inertia += r.LengthSquared();
        }
        v /= BodyNodeCount * dt;                               // px/s
        float w = inertia > 1f ? angular / inertia / dt : 0f;  // rad/s

        float g = _whirlGrip;
        Vector2 dv = new(
            (_whirlVx - v.X) * Mathf.Min(1f, WhirlSideRate * g * dt),
            (-WhirlLiftSpeed - v.Y) * Mathf.Min(1f, WhirlLiftRate * g * dt));
        if (dv.Y > 0f) dv.Y = 0f;   // it only ever lifts
        float dw = (_whirlSpin - w) * Mathf.Min(1f, WhirlSpinRate * g * dt);

        for (int i = 0; i < Pos.Length; i++)
        {
            Vector2 r = Pos[i] - c;
            Prev[i] -= (dv + new Vector2(-r.Y, r.X) * dw) * dt;
        }
    }
}
