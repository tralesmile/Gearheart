using Gearheart.Vehicles;

namespace Gearheart.Core;

/// <summary>State that lives across scene changes for the current play session.</summary>
public static class GameSession
{
    public const string GarageScene = "res://scenes/garage.tscn";
    public const string TestDriveScene = "res://scenes/tests/physics_test.tscn";

    private static VehicleDesign _currentDesign;

    /// <summary>The design being edited in the garage and driven in the test scene.</summary>
    public static VehicleDesign CurrentDesign
    {
        get => _currentDesign ??= VehicleDesign.Default();
        set => _currentDesign = value;
    }
}
