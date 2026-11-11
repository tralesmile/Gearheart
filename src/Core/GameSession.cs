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
    public const string DriveScene = "res://scenes/drive.tscn";
    public const string TestDriveScene = "res://scenes/tests/physics_test.tscn";
    public const string HillClimbTrack = "hillclimb_1";
    private const string SavePath = "user://progress.json";

    private static VehicleDesign _currentDesign;
    private static Progress _progress;

    /// <summary>The design being edited in the garage and driven on the track.</summary>
    public static VehicleDesign CurrentDesign
    {
        get => _currentDesign ??= VehicleDesign.Default();
        set => _currentDesign = value;
    }

    public static DriveMode Mode { get; set; } = DriveMode.FreeDrive;

    // ---- Command-line options for automated playtests -------------------------------------
    // Run with:  godot --path . res://scenes/drive.tscn -- --hillclimb --autodrive --design=driller

    public static bool AutoDrive => UserArgs.Contains("--autodrive");

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
        public List<RaceRecord> Records { get; set; } = new();
    }

    private static Progress Data => _progress ??= LoadProgress();

    public static int MaterialCount(OreType ore) => Data.Materials.GetValueOrDefault(ore.ToString());

    public static void AddMaterial(OreType ore, int amount)
    {
        Data.Materials[ore.ToString()] = MaterialCount(ore) + amount;
        SaveProgress();
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
        if (!FileAccess.FileExists(SavePath)) return new Progress();
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
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(Data, PartCatalog.JsonOptions));
    }
}
