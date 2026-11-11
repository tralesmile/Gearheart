using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Gearheart.Parts;
using Gearheart.Vehicles;
using Gearheart.World;
using Godot;

namespace Gearheart.Core;

public enum DriveMode
{
    FreeDrive,   // explore, smash crates, mine ore
    HillClimb,   // race to the finish against the clock
}

/// <summary>A best time on a track.</summary>
public sealed class RaceRecord
{
    public string Track { get; set; } = "";
    public float Seconds { get; set; }
    public string Vehicle { get; set; } = "";
}

/// <summary>State that lives across scene changes, plus the small save file for materials and records.</summary>
public static class GameSession
{
    public const string GarageScene = "res://scenes/garage.tscn";
    public const string WorldScene = "res://scenes/world.tscn";
    public const string DriveScene = "res://scenes/drive.tscn";
    public const string TestDriveScene = "res://scenes/tests/physics_test.tscn";
    public const string HillClimbTrack = "hillclimb_1";
    private const string SavePath = "user://progress.json";
    private const string WorldPath = "user://world.bin";
    private const string DesignPath = "user://designs/current.json";
    public const int WorldSeed = 20261111;

    private static VehicleDesign _currentDesign;
    private static Progress _progress;

    /// <summary>The design being edited in the garage and driven on the track.</summary>
    public static VehicleDesign CurrentDesign
    {
        get => _currentDesign ??= LoadDesign();
        set => _currentDesign = value;
    }

    private static VehicleDesign LoadDesign()
    {
        var saved = FreshStart ? null : VehicleDesign.Load(DesignPath);
        return saved != null && saved.Validate().Count == 0 ? saved : VehicleDesign.Default();
    }

    public static DriveMode Mode { get; set; } = DriveMode.FreeDrive;

    // ---- Command-line options for automated playtests -------------------------------------
    // Run with:  godot --path . res://scenes/drive.tscn -- --hillclimb --autodrive --design=driller

    public static bool AutoDrive => UserArgs.Contains("--autodrive");
    public static bool AutoPlay => UserArgs.Contains("--autoplay");
    private static bool FreshStart => UserArgs.Contains("--fresh");

    public static void ApplyCommandLine()
    {
        if (UserArgs.Contains("--hillclimb")) Mode = DriveMode.HillClimb;
        if (UserArgs.Contains("--freedrive")) Mode = DriveMode.FreeDrive;
        if (UserArgs.Contains("--design=driller")) CurrentDesign = VehicleDesign.Driller();
    }

    private static string[] UserArgs => OS.GetCmdlineUserArgs();

    // ---- Materials and records (saved to user://progress.json) ----------------------------

    private sealed class Progress
    {
        public Dictionary<string, int> Materials { get; set; } = new();
        public Dictionary<string, int> Built { get; set; }
        public List<RaceRecord> Records { get; set; } = new();
    }

    private static Progress Data => _progress ??= LoadProgress();

    public static readonly string[] Items = { "Dirt", "Stone", "Scrap", "Iron", "Copper" };

    public static int Count(string item) => Data.Materials.GetValueOrDefault(item);

    public static void Add(string item, int amount)
    {
        Data.Materials[item] = Mathf.Max(0, Count(item) + amount);
        _dirty = true;
    }

    public static int MaterialCount(OreType ore) => Count(ore.ToString());

    public static void AddMaterial(OreType ore, int amount)
    {
        Add(ore.ToString(), amount);
        SaveProgress();
    }

    // ---- The tile world ---------------------------------------------------------------------

    private static TileWorld _world;
    private static bool _dirty;

    /// <summary>The world being played. Loaded from disk, or generated the first time.</summary>
    public static TileWorld World
    {
        get
        {
            if (_world != null) return _world;
            _world = FreshStart ? null : TileWorld.Load(WorldPath);
            if (_world == null)
            {
                _world = TileWorld.Generate(WorldSeed);
                if (FreshStart) _progress = new Progress();
            }
            return _world;
        }
    }

    /// <summary>Throws the world away and makes a new one (keeps materials and records).</summary>
    public static void NewWorld()
    {
        _world = TileWorld.Generate((int)(Time.GetTicksUsec() % 1000000));
        SaveAll();
    }

    /// <summary>Writes the world, the inventory and the current design to disk.</summary>
    public static void SaveAll()
    {
        _world?.Save(WorldPath);
        _currentDesign?.Save(DesignPath);
        SaveProgress();
    }

    // ---- Building costs ---------------------------------------------------------------------

    /// <summary>
    /// What changing from the built car to this design costs: positive = you pay, negative = refunded.
    /// The starter pickup is free.
    /// </summary>
    public static Dictionary<string, int> BuildDelta(VehicleDesign design)
    {
        var built = Data.Built ?? VehicleDesign.Default().Cost();
        var delta = new Dictionary<string, int>();
        foreach (var (item, amount) in design.Cost()) delta[item] = amount;
        foreach (var (item, amount) in built) delta[item] = delta.GetValueOrDefault(item) - amount;
        return delta;
    }

    public static bool CanAfford(Dictionary<string, int> delta) => delta.All(kv => kv.Value <= Count(kv.Key));

    /// <summary>Pays for (or refunds) the change and makes this the built car. False if too expensive.</summary>
    public static bool Build(VehicleDesign design)
    {
        var delta = BuildDelta(design);
        if (!CanAfford(delta)) return false;
        foreach (var (item, amount) in delta) Add(item, -amount);
        Data.Built = design.Cost();
        CurrentDesign = design;
        SaveAll();
        return true;
    }

    public static string MaterialsText() =>
        string.Join("   ", new[] { OreType.Scrap, OreType.Iron, OreType.Copper }.Select(o => $"{o} {MaterialCount(o)}"));

    public static RaceRecord BestTime(string track) => Data.Records.FirstOrDefault(r => r.Track == track);

    /// <summary>Stores the time if it beats the record. Returns true for a new record.</summary>
    public static bool SubmitTime(string track, float seconds, string vehicle)
    {
        var best = BestTime(track);
        if (best != null && best.Seconds <= seconds) return false;
        Data.Records.RemoveAll(r => r.Track == track);
        Data.Records.Add(new RaceRecord { Track = track, Seconds = seconds, Vehicle = vehicle });
        SaveProgress();
        return true;
    }

    public static string FormatTime(float seconds) => $"{(int)(seconds / 60f)}:{seconds % 60f:00.00}";

    private static Progress LoadProgress()
    {
        if (FreshStart || !FileAccess.FileExists(SavePath)) return new Progress();
        try
        {
            return JsonSerializer.Deserialize<Progress>(FileAccess.GetFileAsString(SavePath), PartCatalog.JsonOptions) ?? new Progress();
        }
        catch (JsonException)
        {
            GD.PushWarning("progress.json was unreadable; starting fresh.");
            return new Progress();
        }
    }

    private static void SaveProgress()
    {
        _dirty = false;
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(Data, PartCatalog.JsonOptions));
    }
}
