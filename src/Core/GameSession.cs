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

/// <summary>Your best trip so far (saved in progress.json).</summary>
public sealed class TripBest
{
    public int Score { get; set; }
    public float Farthest { get; set; }
    public float TopKmh { get; set; }
    public float LongestAir { get; set; }
    public int BestStreak { get; set; }
    public int Trips { get; set; }
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
    public static bool DriveLeft => UserArgs.Contains("--left");
    /// <summary>Bot only: drop the car from high up to test crash damage and crash effects.</summary>
    public static bool CrashTest => UserArgs.Contains("--crashtest");
    /// <summary>Bot only: bury stashes right where the bot digs, to check the payout and the effects.</summary>
    public static bool StashTest => UserArgs.Contains("--stashtest");
    /// <summary>Bot only: two out-and-back drives from the garage, to check the trip report card.</summary>
    public static bool TripTest => UserArgs.Contains("--triptest");
    /// <summary>Bot only: lay a long strip of ice east of the garage and drive across it at full throttle.</summary>
    public static bool IceTest => UserArgs.Contains("--icetest");
    /// <summary>Bot only: plant long-jump record flags past the kickers before driving (one to beat, the rest out of reach).</summary>
    public static bool JumpTest => UserArgs.Contains("--jumptest");

    /// <summary>--deeptest: the bot starts on foot in a glowing cavern of the Deep and digs out crystals.</summary>
    public static bool DeepTest => UserArgs.Contains("--deeptest");
    /// <summary>--hooktest: a 12 m shaft is dug next to the garage, the bot starts at its bottom and climbs out with the grappling hook.</summary>
    public static bool HookTest => UserArgs.Contains("--hooktest");
    /// <summary>--veintest: a seven-tile iron vein is buried under the mechanic, who mines it out as one vein run.</summary>
    public static bool VeinTest => UserArgs.Contains("--veintest");
    public static bool PauseTest => UserArgs.Contains("--pausetest");
    /// <summary>--launchtest: the bot gets straight in and launches with launch control, letting go in the green
    /// (--launchearly: lets go far too early for a bogged start).</summary>
    public static bool LaunchTest => UserArgs.Contains("--launchtest") || LaunchEarly;
    public static bool LaunchEarly => UserArgs.Contains("--launchearly");
    /// <summary>--rocktest: a trench past the garage pad with a wall too tall to drive up; the bot rocks the car out
    /// (--norock: the control run, no rocking). --rockdepth=N sets the trench depth in tiles (default 3).</summary>
    public static bool RockTest => UserArgs.Contains("--rocktest");
    public static bool NoRock => UserArgs.Contains("--norock");

    /// <summary>--fliptest: straight into the car and east; the bot tries a flip off every kicker (see WorldScene.RunFlipBot).</summary>
    public static bool FlipTest => UserArgs.Contains("--fliptest");
    /// <summary>--hoptest: with a car carrying Hydraulics, the bot hops on the flat (a tap, then a full squat) and off every kicker's crest (see WorldScene.RunHopBot).</summary>
    public static bool HopTest => UserArgs.Contains("--hoptest");
    public static int RockDepth
    {
        get
        {
            foreach (string a in UserArgs)
                if (a.StartsWith("--rockdepth=") && int.TryParse(a.Substring(12), out int n)) return Mathf.Clamp(n, 1, 8);
            return 3;
        }
    }
    private static bool FreshStart => UserArgs.Contains("--fresh");

    /// <summary>--time=H: start the world at this hour (0..24), for testing the day-night cycle.</summary>
    public static float? StartTime => FloatArg("--time=");

    /// <summary>--clockrate=N: the clock runs N times as fast (CI: see a sunset in a 40 s run).</summary>
    public static float ClockRate => FloatArg("--clockrate=") ?? 1f;

    private static float? FloatArg(string prefix)
    {
        foreach (string a in UserArgs)
            if (a.StartsWith(prefix) && float.TryParse(a.Substring(prefix.Length), System.Globalization.NumberStyles.Float,
                                                       System.Globalization.CultureInfo.InvariantCulture, out float v)) return v;
        return null;
    }

    // ---- Time of day (saved in progress.json) -------------------------------------------------

    /// <summary>The hour of the day in the world, 0..24 (see <see cref="DayCycle"/>).</summary>
    public static float ClockHours
    {
        get => Data.ClockHours;
        set
        {
            Data.ClockHours = value;
            _dirty = true;
        }
    }

    /// <summary>Which day it is (1 on a new game); goes up at midnight.</summary>
    public static int Day
    {
        get => Mathf.Max(1, Data.Day);
        set
        {
            Data.Day = value;
            _dirty = true;
        }
    }

    public static void ApplyCommandLine()
    {
        if (UserArgs.Contains("--hillclimb")) Mode = DriveMode.HillClimb;
        if (UserArgs.Contains("--freedrive")) Mode = DriveMode.FreeDrive;
        if (UserArgs.Contains("--design=driller")) CurrentDesign = VehicleDesign.Driller();
        if (UserArgs.Contains("--design=rocket")) CurrentDesign = VehicleDesign.Rocket();
        if (UserArgs.Contains("--design=starter")) CurrentDesign = VehicleDesign.Default();
        if (UserArgs.Contains("--design=spiked")) CurrentDesign = VehicleDesign.Spiked();
        if (UserArgs.Contains("--design=crystal")) CurrentDesign = VehicleDesign.CrystalDriller();
        if (UserArgs.Contains("--design=v8")) CurrentDesign = VehicleDesign.V8();
        if (UserArgs.Contains("--design=scrap4")) CurrentDesign = VehicleDesign.Scrap4();
        if (UserArgs.Contains("--design=turbo")) CurrentDesign = VehicleDesign.Turbo();
        if (UserArgs.Contains("--design=hopper")) CurrentDesign = VehicleDesign.Hopper();
    }

    private static string[] UserArgs => OS.GetCmdlineUserArgs();

    // ---- Materials and records (saved to user://progress.json) ----------------------------

    private sealed class Progress
    {
        public Dictionary<string, int> Materials { get; set; } = new();
        public Dictionary<string, int> Built { get; set; }
        public List<RaceRecord> Records { get; set; } = new();
        public int StashesFound { get; set; }
        public TripBest BestTrip { get; set; }
        public float DeepestDig { get; set; }
        public float SprintBest { get; set; }   // gate sprint, seconds (0 = none yet)
        public int Sprints { get; set; }
        public float ClockHours { get; set; } = DayCycle.StartHours;   // time of day in the world, 0..24
        public int Day { get; set; } = 1;
    }

    private static Progress Data => _progress ??= LoadProgress();

    public static readonly string[] Items = { "Dirt", "Stone", "Scrap", "Iron", "Copper", "Crystal" };

    /// <summary>Buried stashes dug up so far (all worlds).</summary>
    public static int StashesFound
    {
        get => Data.StashesFound;
        set
        {
            Data.StashesFound = value;
            _dirty = true;
        }
    }

    /// <summary>The deepest point reached below the surface, in metres (all worlds).</summary>
    public static float DeepestDig
    {
        get => Data.DeepestDig;
        set
        {
            Data.DeepestDig = value;
            _dirty = true;
        }
    }

    /// <summary>The best gate sprint in seconds (0 before the first one).</summary>
    public static float SprintBest => Data.SprintBest;

    /// <summary>Gate sprints finished (all worlds).</summary>
    public static int Sprints => Data.Sprints;

    /// <summary>Records a finished gate sprint; returns the best time before it (0 if none).</summary>
    public static float SubmitSprint(float seconds)
    {
        float before = Data.SprintBest;
        if (before <= 0f || seconds < before) Data.SprintBest = seconds;
        Data.Sprints++;
        SaveProgress();
        return before;
    }

    /// <summary>The best trip so far, or null before the first trip report.</summary>
    public static TripBest BestTrip => Data.BestTrip;

    /// <summary>
    /// Records a finished trip: keeps the best score and the best of each stat, counts the trip.
    /// Returns the record as it was before (null on the first trip), so the report can show what was beaten.
    /// </summary>
    public static TripBest SubmitTrip(int score, float farthest, float topKmh, float longestAir, int bestStreak)
    {
        TripBest old = Data.BestTrip;
        TripBest before = old == null ? null : new TripBest
        {
            Score = old.Score, Farthest = old.Farthest, TopKmh = old.TopKmh, LongestAir = old.LongestAir, BestStreak = old.BestStreak, Trips = old.Trips,
        };
        var best = old ?? new TripBest();
        best.Score = Mathf.Max(best.Score, score);
        best.Farthest = Mathf.Max(best.Farthest, farthest);
        best.TopKmh = Mathf.Max(best.TopKmh, topKmh);
        best.LongestAir = Mathf.Max(best.LongestAir, longestAir);
        best.BestStreak = Mathf.Max(best.BestStreak, bestStreak);
        best.Trips++;
        Data.BestTrip = best;
        SaveProgress();
        return before;
    }

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
