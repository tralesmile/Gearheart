using System.Collections.Generic;
using Gearheart.Core;
using Godot;

namespace Gearheart.Game;

/// <summary>One line of the trip report: what you did, and the points it scored.</summary>
public readonly record struct TripRow(string Label, string Value, int Points, bool Record);

/// <summary>
/// One trip away from the garage: starts when you leave the garage grounds (on foot or driving),
/// ends when you come back. Counts what happened on the way and scores it for the trip report.
/// </summary>
public sealed class Trip
{
    // Points per thing. Tuned so a 30 s drive to The Wastes scores a few hundred and every
    // kind of fun (distance, speed, air, smashing, digging, crashing) is worth chasing.
    public const int PerMeter = 2, PerHeap = 10, PerStreakStep = 15, PerAirTenth = 4, PerStash = 100, PerCrash = 5, PerCan = 5;
    public const int PerScrap = 2, PerIron = 5, PerCopper = 8;

    public float StartedAt;
    public float Farthest;          // metres from the garage, the furthest point reached
    public float FarthestSide;      // -1 west, +1 east
    public float FarthestX;         // world x of the furthest point (for the biome name)
    public float TopKmh;
    public float LongestAir;
    public int BestStreak;
    public float Deepest;           // metres below the surface

    // Counters at the start of the trip; the trip's share is the difference.
    public int HeapsAtStart, CansAtStart, CrashesAtStart, RollOversAtStart, StashesAtStart;
    public readonly Dictionary<string, int> MaterialsAtStart = new();

    // Filled in when the trip ends.
    public int Heaps, Cans, Crashes, RollOvers, Stashes;
    public readonly Dictionary<string, int> Haul = new();
    public float Seconds;

    public static readonly string[] HaulItems = { "Scrap", "Iron", "Copper" };

    public void NoteSpeed(float kmh) => TopKmh = Mathf.Max(TopKmh, kmh);
    public void NoteAir(float seconds) => LongestAir = Mathf.Max(LongestAir, seconds);
    public void NoteStreak(int streak) => BestStreak = Mathf.Max(BestStreak, streak);

    /// <summary>True once the trip has anything worth a report (not just a stroll past the gate).</summary>
    public bool WorthReporting => Farthest >= 20f || Stashes > 0 || Heaps > 0 || HaulTotal > 0;

    public int HaulTotal
    {
        get
        {
            int n = 0;
            foreach (var kv in Haul) n += kv.Value;
            return n;
        }
    }

    public static int HaulPoints(string item, int amount) => amount * item switch
    {
        "Iron" => PerIron,
        "Copper" => PerCopper,
        _ => PerScrap,
    };

    public string HaulText()
    {
        var parts = new List<string>();
        foreach (string item in HaulItems)
            if (Haul.GetValueOrDefault(item) > 0) parts.Add($"+{Haul[item]} {item}");
        return parts.Count == 0 ? "nothing" : string.Join("  ", parts);
    }

    /// <summary>The report lines; <paramref name="best"/> is the record before this trip (null on the first trip).</summary>
    public List<TripRow> Rows(TripBest best)
    {
        int haulPts = 0;
        foreach (var kv in Haul) haulPts += HaulPoints(kv.Key, kv.Value);
        string side = FarthestSide < 0f ? "west" : "east";
        string streak = BestStreak >= 2 ? $", best streak x{BestStreak}" : "";
        string carnage = Crashes == 0 && RollOvers == 0 ? "spotless" : $"{Crashes} crash{(Crashes == 1 ? "" : "es")}" + (RollOvers > 0 ? $", {RollOvers} roll-over{(RollOvers == 1 ? "" : "s")}" : "");
        return new List<TripRow>
        {
            new("Farthest", $"{Farthest:0} m {side}" + (Deepest >= 2f ? $", {Deepest:0} m deep" : ""), Mathf.RoundToInt(Farthest) * PerMeter, best != null && Farthest > best.Farthest + 0.5f),
            new("Top speed", $"{TopKmh:0} km/h", Mathf.RoundToInt(TopKmh), best != null && TopKmh > best.TopKmh + 0.5f),
            new("Longest air", LongestAir > 0f ? $"{LongestAir:0.0} s" : "none", Mathf.RoundToInt(LongestAir * 10f) * PerAirTenth, best != null && LongestAir > best.LongestAir + 0.05f),
            new("Scrap heaps", $"{Heaps}{streak}", Heaps * PerHeap + Mathf.Max(0, BestStreak - 1) * PerStreakStep, best != null && BestStreak > best.BestStreak),
            new("Fuel cans", $"{Cans}", Cans * PerCan, false),
            new("Stashes", $"{Stashes}", Stashes * PerStash, false),
            new("Haul", HaulText(), haulPts, false),
            new("Carnage", carnage, (Crashes + RollOvers) * PerCrash, false),
        };
    }

    public static int Score(List<TripRow> rows)
    {
        int total = 0;
        foreach (TripRow r in rows) total += r.Points;
        return total;
    }
}
