using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Gearheart.Parts;
using Godot;

namespace Gearheart.Vehicles;

/// <summary>A block placed in the body grid (frame, armor, engine, fuel tank or drill).</summary>
public sealed class DesignBlock
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Part { get; set; } = "";
}

/// <summary>A wheel placed in the grid. It hangs from the block directly above it.</summary>
public sealed class DesignWheel
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Wheel { get; set; } = "";
    public string Suspension { get; set; } = "";
}

/// <summary>Numbers shown in the garage for a design.</summary>
public readonly record struct DesignStats(
    float Mass,
    float Torque,
    float MaxSpin,
    float FuelCapacity,
    float TopSpeedKmh,
    float PowerToWeight,
    int Blocks,
    int Wheels);

/// <summary>
/// A vehicle as the player designed it: blocks on a side-view grid plus wheels.
/// Grid y = 0 is the top row. Saved as JSON so designs can be shared later.
/// </summary>
public sealed class VehicleDesign
{
    public const int GridWidth = 12;
    public const int GridHeight = 6;
    public const float BaseFuelCapacity = 15f;
    public const float PixelsPerMeter = 64f;

    public string Name { get; set; } = "My Car";
    public List<DesignBlock> Blocks { get; set; } = new();
    public List<DesignWheel> Wheels { get; set; } = new();

    public DesignBlock BlockAt(int x, int y) => Blocks.FirstOrDefault(b => b.X == x && b.Y == y);
    public DesignWheel WheelAt(int x, int y) => Wheels.FirstOrDefault(w => w.X == x && w.Y == y);

    public static bool InGrid(int x, int y) => x >= 0 && y >= 0 && x < GridWidth && y < GridHeight;

    public void SetBlock(int x, int y, string partId)
    {
        if (!InGrid(x, y)) return;
        Remove(x, y);
        Blocks.Add(new DesignBlock { X = x, Y = y, Part = partId });
    }

    public void SetWheel(int x, int y, string wheelId, string suspensionId)
    {
        if (!InGrid(x, y)) return;
        Remove(x, y);
        Wheels.Add(new DesignWheel { X = x, Y = y, Wheel = wheelId, Suspension = suspensionId });
    }

    public void Remove(int x, int y)
    {
        Blocks.RemoveAll(b => b.X == x && b.Y == y);
        Wheels.RemoveAll(w => w.X == x && w.Y == y);
    }

    public void Clear()
    {
        Blocks.Clear();
        Wheels.Clear();
    }

    /// <summary>Problems that stop the design from being driven. Empty list = good to go.</summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (Blocks.Count == 0)
        {
            errors.Add("Add at least one frame block.");
            return errors;
        }

        foreach (var b in Blocks)
        {
            var part = PartCatalog.Get(b.Part);
            if (part == null || !part.IsBlock) errors.Add($"Unknown block part '{b.Part}'.");
        }

        foreach (var w in Wheels)
        {
            if (PartCatalog.Get(w.Wheel)?.Category != PartCategory.Wheel) errors.Add($"Unknown wheel '{w.Wheel}'.");
            if (PartCatalog.Get(w.Suspension)?.Category != PartCategory.Suspension) errors.Add($"Unknown suspension '{w.Suspension}'.");
            if (BlockAt(w.X, w.Y - 1) == null) errors.Add($"Wheel at column {w.X + 1} needs a block directly above it.");
        }

        if (!Blocks.Any(b => PartCatalog.Get(b.Part)?.Category == PartCategory.Engine))
            errors.Add("Add an engine.");
        if (Wheels.Count == 0)
            errors.Add("Add at least one wheel under a block.");
        if (!Blocks.Any(b => PartCatalog.Get(b.Part)?.Category == PartCategory.Cab))
            errors.Add("Add a cab: the driver needs a seat.");
        if (!IsConnected())
            errors.Add("All blocks must touch each other (side by side or on top).");

        return errors;
    }

    private bool IsConnected()
    {
        if (Blocks.Count == 0) return true;
        var seen = new HashSet<(int, int)>();
        var stack = new Stack<(int, int)>();
        stack.Push((Blocks[0].X, Blocks[0].Y));

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            if (!seen.Add((x, y))) continue;
            foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                if (BlockAt(nx, ny) != null && !seen.Contains((nx, ny))) stack.Push((nx, ny));
        }

        return seen.Count == Blocks.Count;
    }

    public DesignStats Stats()
    {
        float mass = 0f, torque = 0f, maxSpin = 0f, fuel = BaseFuelCapacity, wheelRadius = 0f;

        foreach (var b in Blocks)
        {
            var part = PartCatalog.Get(b.Part);
            if (part == null) continue;
            mass += part.Mass;
            torque += part.Torque;
            maxSpin = Mathf.Max(maxSpin, part.MaxSpin);
            fuel += part.FuelCapacity;
        }

        foreach (var w in Wheels)
        {
            var wheel = PartCatalog.Get(w.Wheel);
            if (wheel == null) continue;
            mass += wheel.Mass;
            wheelRadius += wheel.Radius;
        }

        float averageRadius = Wheels.Count > 0 ? wheelRadius / Wheels.Count : 0f;
        float topSpeed = maxSpin * averageRadius / PixelsPerMeter * 3.6f;
        float powerToWeight = mass > 0f ? torque / mass / 1000f : 0f;
        return new DesignStats(mass, torque, maxSpin, fuel, topSpeed, powerToWeight, Blocks.Count, Wheels.Count);
    }

    public VehicleDesign Clone() => FromJson(ToJson());

    public string ToJson() => JsonSerializer.Serialize(this, PartCatalog.JsonOptions);

    public static VehicleDesign FromJson(string json) =>
        JsonSerializer.Deserialize<VehicleDesign>(json, PartCatalog.JsonOptions) ?? Default();

    public static string SlotPath(int slot) => $"user://designs/slot{slot}.json";

    public bool Save(string path)
    {
        DirAccess.MakeDirRecursiveAbsolute(path.GetBaseDir());
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null) return false;
        file.StoreString(ToJson());
        return true;
    }

    public static VehicleDesign Load(string path)
    {
        if (!FileAccess.FileExists(path)) return null;
        string json = FileAccess.GetFileAsString(path);
        return string.IsNullOrEmpty(json) ? null : FromJson(json);
    }

    /// <summary>Total materials this design needs: item key to amount.</summary>
    public Dictionary<string, int> Cost()
    {
        var total = new Dictionary<string, int>();
        void Add(string partId)
        {
            var part = PartCatalog.Get(partId);
            if (part == null) return;
            foreach (var (item, amount) in part.Cost)
                total[item] = total.GetValueOrDefault(item) + amount;
        }

        foreach (var b in Blocks) Add(b.Part);
        foreach (var w in Wheels)
        {
            Add(w.Wheel);
            Add(w.Suspension);
        }
        return total;
    }

    /// <summary>The starter car: a little pickup with a V6 under the hood, facing right.</summary>
    public static VehicleDesign Default()
    {
        var d = new VehicleDesign { Name = "Starter Pickup" };
        d.SetBlock(3, 3, "frame_steel");
        d.SetBlock(4, 3, "fuel_tank");
        d.SetBlock(5, 3, "frame_steel");
        d.SetBlock(6, 3, "frame_steel");
        d.SetBlock(7, 3, "engine_v6");
        d.SetBlock(8, 3, "frame_steel");
        d.SetBlock(4, 2, "frame_steel");
        d.SetBlock(5, 2, "cab");
        d.SetWheel(3, 4, "wheel_standard", "susp_soft");
        d.SetWheel(8, 4, "wheel_standard", "susp_soft");
        return d;
    }

    /// <summary>The starter car with a drill on the nose, for mining.</summary>
    public static VehicleDesign Driller()
    {
        var d = Default();
        d.Name = "Driller";
        d.SetBlock(9, 3, "drill_basic");
        return d;
    }

    /// <summary>The starter car with a rocket booster on the tail.</summary>
    public static VehicleDesign Rocket()
    {
        var d = Default();
        d.Name = "Rocket Pickup";
        d.SetBlock(2, 3, "booster_rocket");
        return d;
    }
}
