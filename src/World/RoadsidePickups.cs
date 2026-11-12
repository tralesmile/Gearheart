using System.Collections.Generic;
using Gearheart.Core;
using Godot;

namespace Gearheart.World;

public enum PickupKind
{
    FuelCan,     // drive through it: tops up the tank
    ScrapHeap,   // smash through it (or walk into it): a handful of Scrap, sometimes Iron or Copper
}

/// <summary>One thing lying on the surface.</summary>
public sealed class Pickup
{
    public int X;                 // tile column
    public PickupKind Kind;
    public bool Taken;
    public double RespawnAt;      // seconds (engine clock) when a taken pickup comes back
    public float GroundY;         // world y of the ground under it, refreshed while near the camera
}

/// <summary>
/// Fuel cans and scrap heaps scattered along the surface between the garage and the far biomes, so
/// the drive is not empty: every few seconds there is something to aim for. Taken pickups come back
/// after a while (out of sight), so every trip has them. The list lives for the whole game session
/// (not saved), so going into the garage does not restock the road.
/// </summary>
public partial class RoadsidePickups : Node2D
{
    public const float FuelAmount = 18f;

    /// <summary>A can is only taken when at least this much room is in the tank, so it is not wasted.</summary>
    public const float MinRoom = 6f;
    public const double RespawnSeconds = 150.0;
    private const float HiddenDistance = 1400f, DrawDistance = 1500f;
    private const float Ts = TileWorld.TileSize;

    public TileWorld World;

    /// <summary>World position the pickups are judged from (the player or the car).</summary>
    public Vector2 Focus;

    private static List<Pickup> _all;
    private static TileWorld _placedFor;
    private float _time;

    public IReadOnlyList<Pickup> All => _all;

    private static double Clock => Time.GetTicksMsec() / 1000.0;

    public override void _Ready()
    {
        if (_all == null || _placedFor != World)
        {
            _all = Place(World);
            _placedFor = World;
        }
        RefreshGround(float.PositiveInfinity);
    }

    /// <summary>
    /// Places pickups outwards from the garage on both sides, every 10–19 m, starting about 18 m out.
    /// About 30% are fuel cans.
    /// </summary>
    public static List<Pickup> Place(TileWorld world)
    {
        ulong seed = 9001;
        for (int x = 0; x < TileWorld.Width; x += 97) seed = seed * 31 + (ulong)world.SurfaceRow(x);
        using var rng = new RandomNumberGenerator { Seed = seed };
        var list = new List<Pickup>();
        int center = (TileWorld.PadFrom + TileWorld.PadTo) / 2;
        foreach (int dir in new[] { 1, -1 })
        {
            for (int x = center + dir * rng.RandiRange(68, 80); x > 24 && x < TileWorld.Width - 24; x += dir * rng.RandiRange(40, 76))
            {
                // Not on a kicker's ramp or its landing: just past the back slope instead.
                if (world.KickerNear(x, 6) is Kicker k) x = (dir > 0 ? k.Max : k.Min) + dir * 8;
                if (x <= 24 || x >= TileWorld.Width - 24) break;
                list.Add(new Pickup { X = x, Kind = rng.Randf() < 0.3f ? PickupKind.FuelCan : PickupKind.ScrapHeap });
            }
        }
        return list;
    }

    /// <summary>What a scrap heap at this column holds (fixed per spot).</summary>
    public static List<(string Item, int Amount)> HeapLoot(int x)
    {
        using var rng = new RandomNumberGenerator { Seed = (ulong)(uint)(x * 2654435761u) };
        var loot = new List<(string, int)> { ("Scrap", rng.RandiRange(1, 3)) };
        Biome biome = TileWorld.BiomeAt(x);
        float roll = rng.Randf();
        if (biome == Biome.Desert && roll < 0.35f) loot.Add(("Copper", 1));
        else if (biome == Biome.Frozen && roll < 0.35f) loot.Add(("Iron", 1));
        else if (biome == Biome.Meadow && roll < 0.2f) loot.Add(("Iron", 1));
        return loot;
    }

    /// <summary>World-space box of a pickup, standing on the ground.</summary>
    public static Rect2 Box(Pickup p)
    {
        float cx = (p.X + 0.5f) * Ts;
        return p.Kind == PickupKind.FuelCan
            ? new Rect2(cx - 8f, p.GroundY - 24f, 16f, 24f)
            : new Rect2(cx - 15f, p.GroundY - 24f, 30f, 24f);
    }

    /// <summary>Pickups lying in the box (not yet taken).</summary>
    public List<Pickup> Touching(Rect2 box)
    {
        var hits = new List<Pickup>();
        foreach (var p in _all)
            if (!p.Taken && Mathf.Abs((p.X + 0.5f) * Ts - box.GetCenter().X) < box.Size.X / 2f + 40f && Box(p).Intersects(box))
                hits.Add(p);
        return hits;
    }

    public void Take(Pickup p)
    {
        p.Taken = true;
        p.RespawnAt = Clock + RespawnSeconds;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        double now = Clock;
        foreach (var p in _all)
        {
            // Come back only out of sight, so nothing pops in front of the player.
            if (p.Taken && now >= p.RespawnAt && Mathf.Abs((p.X + 0.5f) * Ts - Focus.X) > HiddenDistance)
                p.Taken = false;
        }
        RefreshGround(DrawDistance + 200f);
        QueueRedraw();
    }

    /// <summary>Keeps pickups on the ground when it is dug away or built up.</summary>
    private void RefreshGround(float within)
    {
        foreach (var p in _all)
        {
            float wx = (p.X + 0.5f) * Ts;
            if (Mathf.Abs(wx - Focus.X) > within) continue;
            p.GroundY = World.GroundY(wx, (World.SurfaceRow(p.X) - 14) * Ts);
        }
    }

    public override void _Draw()
    {
        Texture2D sheet = Art.Pickups;
        const int cell = Art.PickupCell;
        foreach (var p in _all)
        {
            if (p.Taken) continue;
            float cx = (p.X + 0.5f) * Ts;
            if (Mathf.Abs(cx - Focus.X) > DrawDistance) continue;

            int sprite = p.Kind == PickupKind.FuelCan ? 0 : TileWorld.BiomeAt(p.X) switch
            {
                Biome.Desert => 2,
                Biome.Frozen => 3,
                _ => 1,
            };
            var at = new Vector2(cx - cell / 2f, p.GroundY - cell + 1f);

            if (p.Kind == PickupKind.FuelCan)
            {
                // A soft pulsing halo and a twinkle, so a can is easy to spot at speed.
                float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 4f + p.X);
                var mid = new Vector2(cx, p.GroundY - 12f);
                DrawCircle(mid, 17f + 3f * pulse, new Color(1f, 0.85f, 0.4f, 0.10f + 0.08f * pulse));
                DrawCircle(mid, 12f, new Color(1f, 0.9f, 0.55f, 0.10f));
                float flash = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_time * 2.2f + p.X * 0.7f)), 10f);
                if (flash > 0.05f) Star(new Vector2(cx + 6f, p.GroundY - 24f), 2f + 6f * flash, flash);
            }
            DrawTextureRectRegion(sheet, new Rect2(at, new Vector2(cell, cell)), new Rect2(sprite * cell, 0, cell, cell));
        }
    }

    private void Star(Vector2 c, float r, float alpha)
    {
        var color = new Color(1f, 0.97f, 0.8f, alpha);
        DrawLine(c - new Vector2(r, 0f), c + new Vector2(r, 0f), color, 1.5f);
        DrawLine(c - new Vector2(0f, r), c + new Vector2(0f, r), color, 1.5f);
    }
}
