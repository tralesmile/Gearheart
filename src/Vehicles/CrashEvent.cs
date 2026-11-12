using Godot;

namespace Gearheart.Vehicles;

/// <summary>
/// One hard hit on a vehicle (see <see cref="SoftVehicle.Crashed"/>). Strength is how fast the
/// car hit, in pixels per second (64 px = 1 m). BodyHit is true when the body itself struck the
/// ground, not just the wheels.
/// </summary>
public readonly record struct CrashEvent(
    Vector2 Point, Vector2 Normal, float Strength, bool BodyHit, int Snapped, float Dent, bool WheelLost, Vector2 Velocity);
