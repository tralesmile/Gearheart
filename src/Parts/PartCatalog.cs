using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Gearheart.Parts;

public enum PartCategory
{
    Frame,
    Cab,
    Armor,
    Engine,
    FuelTank,
    Drill,
    Booster,
    Wheel,
    Suspension,
}

/// <summary>One vehicle part, loaded from data/parts/parts.json.</summary>
public sealed class PartDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Short { get; set; } = "";         // 1-2 letter label shown on the garage grid
    public PartCategory Category { get; set; }
    public string Description { get; set; } = "";
    public float Mass { get; set; }
    [JsonPropertyName("color")] public string ColorHex { get; set; } = "#ffffff";

    /// <summary>Cell index in parts.png (blocks) or wheels.png (wheels).</summary>
    public int Sprite { get; set; }

    /// <summary>Materials needed to build one: item key to amount.</summary>
    public Dictionary<string, int> Cost { get; set; } = new();

    // Structure (frames, armor, engines, tanks, drills, suspension beams)
    public float Yield { get; set; } = 0.015f;        // stretch before it bends permanently
    public float BreakLimit { get; set; } = 0.18f;    // total permanent bend before it snaps

    // Engine
    public float Torque { get; set; }
    public float MaxSpin { get; set; }
    public float FuelUse { get; set; }                // fuel units per second at full throttle

    // Drill: mining work per second while the tip presses on ore
    public float DrillPower { get; set; }

    // Booster: forward thrust while the boost key is held, fading out towards BoostSpeed (px/s)
    public float Thrust { get; set; }
    public float BoostSpeed { get; set; }
    public float BoostFuelUse { get; set; }           // fuel units per second while firing

    // Fuel tank
    public float FuelCapacity { get; set; }

    // Wheel
    public float Radius { get; set; }
    public float Grip { get; set; }
    /// <summary>Studded tyres: on snow and ice the ground's grip never drops below this (0 = no studs).</summary>
    public float FrozenGrip { get; set; }

    // Suspension
    public float SpringStiffness { get; set; }
    public float Damping { get; set; }
    public float RestLength { get; set; }

    [JsonIgnore] public Color GodotColor => new(ColorHex);

    /// <summary>True for parts placed as a block in the body grid.</summary>
    [JsonIgnore]
    public bool IsBlock => Category is PartCategory.Frame or PartCategory.Cab or PartCategory.Armor or PartCategory.Engine
        or PartCategory.FuelTank or PartCategory.Drill or PartCategory.Booster;
}

/// <summary>All parts in the game, loaded once from data files.</summary>
public static class PartCatalog
{
    public const string PartsFile = "res://data/parts/parts.json";

    private static Dictionary<string, PartDefinition> _parts;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed class PartsFileData
    {
        public List<PartDefinition> Parts { get; set; } = new();
    }

    public static IReadOnlyCollection<PartDefinition> All
    {
        get
        {
            EnsureLoaded();
            return _parts.Values;
        }
    }

    public static PartDefinition Get(string id)
    {
        EnsureLoaded();
        return id != null && _parts.TryGetValue(id, out var part) ? part : null;
    }

    public static IEnumerable<PartDefinition> InCategory(PartCategory category) =>
        All.Where(p => p.Category == category);

    private static void EnsureLoaded()
    {
        if (_parts != null) return;
        _parts = new Dictionary<string, PartDefinition>();

        string json = FileAccess.GetFileAsString(PartsFile);
        if (string.IsNullOrEmpty(json))
        {
            GD.PushError($"Could not read {PartsFile}");
            return;
        }

        var data = JsonSerializer.Deserialize<PartsFileData>(json, JsonOptions);
        foreach (var part in data?.Parts ?? new List<PartDefinition>())
            _parts[part.Id] = part;
    }
}
