using Godot;

namespace Gearheart.Vehicles;

/// <summary>What the game needs from any vehicle, whatever physics approach drives it.</summary>
public interface IVehicle
{
    string PhysicsName { get; }
    Vector2 CenterPosition { get; }
    float Speed { get; }   // px/s
    float Damage { get; }
}

/// <summary>One frame of driver input.</summary>
public readonly struct VehicleInput
{
    public readonly float Throttle;   // -1..1, positive drives right
    public readonly float Tilt;       // -1..1, positive rotates clockwise
    public readonly bool Handbrake;
    public readonly bool Boost;       // fire rocket boosters
    public readonly bool Hop;         // hydraulics: held to squat, released to hop

    public VehicleInput(float throttle, float tilt, bool handbrake, bool boost = false, bool hop = false)
    {
        Throttle = throttle;
        Tilt = tilt;
        Handbrake = handbrake;
        Boost = boost;
        Hop = hop;
    }

    public static VehicleInput Auto(float throttle) => new(throttle, 0f, false);

    public static VehicleInput ReadPlayer()
    {
        float throttle = 0f, tilt = 0f;
        if (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right)) throttle += 1f;
        if (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left)) throttle -= 1f;
        throttle += Input.GetJoyAxis(0, JoyAxis.TriggerRight) - Input.GetJoyAxis(0, JoyAxis.TriggerLeft);

        if (Input.IsPhysicalKeyPressed(Key.E)) tilt += 1f;
        if (Input.IsPhysicalKeyPressed(Key.Q)) tilt -= 1f;
        float stick = Input.GetJoyAxis(0, JoyAxis.LeftX);
        if (Mathf.Abs(stick) > 0.2f) tilt += stick;

        bool handbrake = Input.IsPhysicalKeyPressed(Key.Space) || Input.IsJoyButtonPressed(0, JoyButton.A);
        bool boost = Input.IsPhysicalKeyPressed(Key.Shift) || Input.IsJoyButtonPressed(0, JoyButton.RightShoulder)
                     || Input.IsJoyButtonPressed(0, JoyButton.X);
        bool hop = Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up) || Input.IsJoyButtonPressed(0, JoyButton.B);
        return new VehicleInput(Mathf.Clamp(throttle, -1f, 1f), Mathf.Clamp(tilt, -1f, 1f), handbrake, boost, hop);
    }
}
