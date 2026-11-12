namespace Gearheart.Vehicles;

/// <summary>How a launch-control start went (see <see cref="SoftVehicle.LaunchArmed"/>).</summary>
public enum LaunchGrade
{
    /// <summary>Let go too early, with the revs low: no shove.</summary>
    Bogged,
    /// <summary>Let go in the amber: a small shove.</summary>
    Good,
    /// <summary>Let go in the green window near the top: a big shove, an exhaust pop and a cheer.</summary>
    Perfect,
}
