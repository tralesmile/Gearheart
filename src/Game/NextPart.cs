using System;
using System.Collections.Generic;
using System.Linq;
using Gearheart.Core;
using Gearheart.Parts;
using Gearheart.Vehicles;

namespace Gearheart.Game;

/// <summary>One upgrade you could build next, and what it still takes.</summary>
public sealed class PartGoal
{
    public PartDefinition Part;
    /// <summary>What building it costs on top of the car you have (refunds of swapped parts taken off).</summary>
    public Dictionary<string, int> Need = new();
    /// <summary>0..1: share of the needed materials you already carry.</summary>
    public float Progress;
    public bool Ready;
    public int TotalNeed => Need.Values.Sum();
    /// <summary>How much the tracker wants to show it: progress, with later tiers held back a little.</summary>
    public float Score => Progress - 0.2f * (Part.GoalTier - 1);
}

/// <summary>
/// The "next part" tracker: from the car you drive and the materials you carry, which upgrade is
/// closest, and what is still missing. Parts opt in with "goalTier" in data/parts/parts.json.
/// Wheels are counted as a full set swapped (the old tyres refunded), an engine or a drill as a swap,
/// everything else as one more block.
/// </summary>
public static class NextPart
{
    public static List<PartGoal> Goals(VehicleDesign design, Func<string, int> have)
    {
        var goals = new List<PartGoal>();
        if (design == null) return goals;
        var owned = design.Blocks.Select(b => PartCatalog.Get(b.Part))
            .Concat(design.Wheels.Select(w => PartCatalog.Get(w.Wheel)))
            .Where(p => p != null).ToList();

        foreach (var part in PartCatalog.All.Where(p => p.GoalTier > 0))
        {
            // Already on the car, or the car has something as good or better of the same kind.
            if (owned.Any(o => o.Id == part.Id || (o.Category == part.Category && o.GoalTier >= part.GoalTier))) continue;

            var delta = new Dictionary<string, int>();
            void AddCost(PartDefinition p, int times)
            {
                if (p == null) return;
                foreach (var (item, amount) in p.Cost) delta[item] = delta.GetValueOrDefault(item) + amount * times;
            }

            switch (part.Category)
            {
                case PartCategory.Wheel:
                    if (design.Wheels.Count == 0) AddCost(part, 2);
                    foreach (var w in design.Wheels)
                    {
                        AddCost(part, 1);
                        AddCost(PartCatalog.Get(w.Wheel), -1);
                    }
                    break;
                case PartCategory.Engine:
                case PartCategory.Drill:
                    AddCost(part, 1);
                    AddCost(owned.FirstOrDefault(o => o.Category == part.Category), -1);
                    break;
                default:
                    AddCost(part, 1);
                    break;
            }

            var goal = new PartGoal { Part = part };
            int total = 0, got = 0;
            foreach (var (item, amount) in delta.OrderBy(kv => Array.IndexOf(GameSession.Items, kv.Key)))
            {
                if (amount <= 0) continue;
                goal.Need[item] = amount;
                total += amount;
                got += Math.Min(amount, Math.Max(0, have(item)));
            }
            goal.Progress = total == 0 ? 1f : got / (float)total;
            goal.Ready = got >= total;
            goals.Add(goal);
        }
        return goals;
    }

    /// <summary>
    /// The goal to show. A ready part wins (the best one if several); otherwise the closest, with
    /// later tiers held back so a new player is pointed at the Drill Head before the Crystal Drill.
    /// Keeps <paramref name="currentId"/> unless another goal is clearly better (no flicker).
    /// </summary>
    public static PartGoal Pick(List<PartGoal> goals, string currentId)
    {
        if (goals.Count == 0) return null;
        var ready = goals.Where(g => g.Ready).OrderByDescending(g => g.Part.GoalTier).ThenByDescending(g => g.TotalNeed).ThenBy(g => g.Part.Id).ToList();
        var current = goals.FirstOrDefault(g => g.Part.Id == currentId);
        if (ready.Count > 0) return current is { Ready: true } ? current : ready[0];
        var best = goals.OrderByDescending(g => g.Score).ThenBy(g => g.TotalNeed).ThenBy(g => g.Part.Id).First();
        if (current != null && best.Score < current.Score + 0.1f) return current;
        return best;
    }
}
