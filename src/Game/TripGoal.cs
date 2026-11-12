using System;
using System.Collections.Generic;
using Gearheart.Core;
using Gearheart.World;
using Godot;

namespace Gearheart.Game;

public enum GoalKind { Distance, Wastes, North, Heaps, Streak, Air, Speed, Cans, Stash, Copper }

/// <summary>
/// One bonus objective per trip ("Smash 4 scrap heaps", "Reach The Wastes", "1.0 s of air"),
/// shown at the garage before you leave and tracked on the way. Done = a bonus row on the trip report.
/// Targets grow with your records, so the goal stays a stretch. Picked from the trip number,
/// so the same trip always gets the same goal (and the bot runs are repeatable).
/// </summary>
public sealed class TripGoal
{
    public GoalKind Kind;
    public float Target;
    public int Bonus;
    public string Text = "";
    public bool Done;
    public float DoneAt = -1f;      // seconds into the trip when it was done

    /// <summary>Metres from the garage where The Wastes / the Frozen North begin.</summary>
    public const float BiomeEdge = TileWorld.MeadowReach * 0.25f;

    private static readonly GoalKind[] Pool =
    {
        GoalKind.Heaps, GoalKind.Wastes, GoalKind.Air, GoalKind.Distance, GoalKind.Cans,
        GoalKind.Streak, GoalKind.North, GoalKind.Speed, GoalKind.Copper, GoalKind.Stash,
    };

    /// <param name="tripNumber">1 for the first trip ever.</param>
    /// <param name="best">Records so far (null before the first trip).</param>
    public static TripGoal For(int tripNumber, TripBest best, bool hasBooster)
    {
        // The very first trip teaches the idea with an easy one.
        if (tripNumber <= 1) return Make(GoalKind.Distance, 50f);

        GoalKind Pick(int n) => Pool[(int)(Hash(n) % (uint)Pool.Length)];
        GoalKind kind = Pick(tripNumber);
        if (kind == Pick(tripNumber - 1)) kind = Pool[(Array.IndexOf(Pool, kind) + 1) % Pool.Length];

        float farthest = best?.Farthest ?? 0f, top = best?.TopKmh ?? 0f;
        float target = kind switch
        {
            GoalKind.Distance => Mathf.Clamp(Mathf.Round((farthest * 1.15f + 10f) / 10f) * 10f, 50f, 400f),
            GoalKind.Heaps => Mathf.Clamp(3 + tripNumber / 3, 3, 8),
            GoalKind.Streak => tripNumber < 8 ? 3 : 4,
            GoalKind.Air => hasBooster ? 1.6f : 1.0f,
            GoalKind.Speed => Mathf.Clamp(Mathf.Ceil((top + 3f) / 5f) * 5f, 40f, 110f),
            GoalKind.Cans => 3,
            GoalKind.Copper => 2,
            GoalKind.Wastes or GoalKind.North => BiomeEdge,
            _ => 1,
        };
        return Make(kind, target);
    }

    public static TripGoal Make(GoalKind kind, float target)
    {
        var g = new TripGoal { Kind = kind, Target = target };
        (g.Text, g.Bonus) = kind switch
        {
            GoalKind.Distance => ($"Go {target:0} m out", 120),
            GoalKind.Wastes => ("Reach The Wastes", 150),
            GoalKind.North => ("Reach the Frozen North", 150),
            GoalKind.Heaps => ($"Smash {target:0} scrap heaps", 120),
            GoalKind.Streak => ($"Get a SMASH x{target:0} streak", 130),
            GoalKind.Air => ($"Fly {target:0.0} s in one jump", 120),
            GoalKind.Speed => ($"Hit {target:0} km/h", 100),
            GoalKind.Cans => ($"Grab {target:0} fuel cans", 100),
            GoalKind.Copper => ($"Find {target:0} Copper", 150),
            GoalKind.Stash => ("Dig up a buried stash", 200),
            _ => ("?", 0),
        };
        return g;
    }

    /// <summary>How far along the trip is towards the target (same unit as the target).</summary>
    public float Progress(Trip t) => Kind switch
    {
        GoalKind.Distance => t.Farthest,
        GoalKind.Wastes => t.ReachedWastes ? Target : Mathf.Min(t.FarthestEast, Target - 1f),
        GoalKind.North => t.ReachedNorth ? Target : Mathf.Min(t.FarthestWest, Target - 1f),
        GoalKind.Heaps => t.Heaps,
        GoalKind.Streak => t.BestStreak,
        GoalKind.Air => t.LongestAir,
        GoalKind.Speed => t.TopKmh,
        GoalKind.Cans => t.Cans,
        GoalKind.Copper => t.Haul.GetValueOrDefault("Copper"),
        GoalKind.Stash => t.Stashes,
        _ => 0f,
    };

    /// <summary>Checks the trip; returns true on the frame the goal gets done.</summary>
    public bool Check(Trip t, float tripSeconds)
    {
        if (Done) return false;
        if (Progress(t) + 1e-3f < Target) return false;
        Done = true;
        DoneAt = tripSeconds;
        return true;
    }

    /// <summary>Short progress text: "2/4", "61/95 m", "0.7/1.0 s".</summary>
    public string ProgressText(Trip t)
    {
        float p = Mathf.Min(Progress(t), Target);
        return Kind switch
        {
            GoalKind.Distance or GoalKind.Wastes or GoalKind.North => $"{p:0}/{Target:0} m",
            GoalKind.Air => $"{p:0.0}/{Target:0.0} s",
            GoalKind.Speed => $"{p:0}/{Target:0} km/h",
            _ => $"{p:0}/{Target:0}",
        };
    }

    private static uint Hash(int n)
    {
        uint h = (uint)n * 2654435761u ^ 0x9e3779b9u;
        h ^= h >> 15;
        h *= 2246822519u;
        h ^= h >> 13;
        return h;
    }
}
