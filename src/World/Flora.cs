using System.Collections.Generic;
using Gearheart.Core;
using Godot;

namespace Gearheart.World;

/// <summary>Plants the car can run over (trees and rocks it cannot).</summary>
public enum PlantKind { None, Bush, Flowers, Cactus, Tuft, SnowShrub }

/// <summary>
/// The roadside decorations: which one stands on a surface column (the same answer for drawing and for
/// the car), and the plants the car has flattened. Flattened plants show a stump or stubble and grow
/// back a couple of minutes later, out of sight. Not saved and not part of world generation: the plants
/// come from a hash of the column, so this needs no save version change.
/// </summary>
public static class Flora
{
    public const double RegrowSeconds = 120.0;
    private const float HiddenDistance = 1400f;

    private static readonly Dictionary<int, double> _flat = new();
    private static TileWorld _for;

    private static double Clock => Time.GetTicksMsec() / 1000.0;

    /// <summary>Plants flattened right now (for the bot log).</summary>
    public static int FlatCount => _flat.Count;

    /// <summary>Plants grown back so far (for the bot log).</summary>
    public static int Regrown { get; private set; }

    /// <summary>
    /// The decor cell standing on this column's surface, or -1. Kicker signs replace plants on a kicker's
    /// run-up; nothing stands on the garage pad, a dug or cut surface, or under a placed block.
    /// </summary>
    public static int DecorCell(TileWorld world, int tx)
    {
        if (tx < 0 || tx >= TileWorld.Width) return -1;
        if (tx > TileWorld.PadFrom - 3 && tx < TileWorld.PadTo + 3) return -1;
        int sy = world.SurfaceRow(tx);
        if (world.Solid(tx, sy - 1) || world.TopCut(tx, sy, -1) || world.TopCut(tx, sy, 1)) return -1;

        if (world.KickerNear(tx, 6) is Kicker k)
            return tx == k.SignColumn ? (k.Dir > 0 ? Art.DecorKickerRight : Art.DecorKickerLeft) : -1;

        int roll = TileChunk.Hash(tx, 7777) % 100;
        bool treeSpot = tx % 4 == 0;
        return world.Get(tx, sy) switch
        {
            Tile.Grass => treeSpot && roll < 22 ? 0 : roll < 8 ? 3 : roll < 20 ? 4 : roll < 55 ? 5 : -1,
            Tile.Sand => treeSpot && roll < 16 ? 2 : roll < 5 ? 6 : -1,
            Tile.Snow => treeSpot && roll < 24 ? 1 : roll < 6 ? 7 : roll < 13 ? Art.DecorSnowShrub : -1,
            _ => -1,
        };
    }

    public static PlantKind KindOf(int cell) => cell switch
    {
        2 => PlantKind.Cactus,
        3 => PlantKind.Bush,
        4 => PlantKind.Flowers,
        5 => PlantKind.Tuft,
        Art.DecorSnowShrub => PlantKind.SnowShrub,
        _ => PlantKind.None,
    };

    /// <summary>What is drawn where a flattened plant of this cell stood.</summary>
    public static int StubbleCell(int cell) => KindOf(cell) switch
    {
        PlantKind.Cactus => Art.DecorCactusStump,
        PlantKind.Bush => Art.DecorBushStubble,
        PlantKind.SnowShrub => Art.DecorSnowStubble,
        PlantKind.Flowers or PlantKind.Tuft => Art.DecorFlatStalks,
        _ => cell,
    };

    /// <summary>World-space box of a plant standing on its column (roughly its drawn shape).</summary>
    public static Rect2 Box(TileWorld world, int tx, PlantKind kind)
    {
        const float ts = TileWorld.TileSize;
        float cx = (tx + 0.5f) * ts, ground = world.SurfaceRow(tx) * ts;
        (float w, float h) = kind switch
        {
            PlantKind.Cactus => (24f, 42f),
            PlantKind.Bush or PlantKind.SnowShrub => (22f, 20f),
            PlantKind.Flowers => (20f, 14f),
            _ => (18f, 8f),
        };
        return new Rect2(cx - w / 2f, ground - h, w, h);
    }

    public static bool Flattened(TileWorld world, int tx) => world == _for && _flat.ContainsKey(tx);

    public static void Flatten(TileWorld world, int tx)
    {
        if (world != _for)
        {
            _flat.Clear();
            _for = world;
        }
        _flat[tx] = Clock + RegrowSeconds;
        world.NotifyLooksChanged(tx, world.SurfaceRow(tx));
    }

    /// <summary>Plants whose time is up grow back, but only out of sight, so nothing pops up in front of you.</summary>
    public static void Regrow(TileWorld world, float focusX)
    {
        if (world != _for || _flat.Count == 0) return;
        double now = Clock;
        List<int> back = null;
        foreach (var (tx, at) in _flat)
        {
            if (now >= at && Mathf.Abs((tx + 0.5f) * TileWorld.TileSize - focusX) > HiddenDistance)
                (back ??= new List<int>()).Add(tx);
        }
        if (back == null) return;
        foreach (int tx in back)
        {
            _flat.Remove(tx);
            Regrown++;
            world.NotifyLooksChanged(tx, world.SurfaceRow(tx));
        }
    }
}
