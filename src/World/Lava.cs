using System.Collections.Generic;
using Godot;

namespace Gearheart.World;

/// <summary>
/// The magma sea at the bottom of the world: every open (air) tile at or below <see cref="Row"/>
/// is molten lava. Nothing is stored and the world format is unchanged: lava follows from the tiles,
/// so a cavern that reaches the line holds a lake whose top is flat at the line, and a tile you dig
/// out down there fills with lava at once (the magma was right under it).
/// </summary>
public static class Lava
{
    /// <summary>First lava row: about 29 m under the garage pad, 2.5 m above the bedrock floor.</summary>
    public const int Row = TileWorld.Height - 12;

    /// <summary>Ore tiles this close above the line (or in it) are lucky far more often: the heat makes rich ore.</summary>
    public const int RichBand = 8;
    public const int RichOneIn = 4;

    public static bool At(TileWorld w, int x, int y) =>
        y >= Row && y < TileWorld.Height - 2 && x >= 0 && x < TileWorld.Width && w.Get(x, y) == Tile.Air;

    public static bool AtPoint(TileWorld w, Vector2 p) =>
        At(w, Mathf.FloorToInt(p.X / TileWorld.TileSize), Mathf.FloorToInt(p.Y / TileWorld.TileSize));

    /// <summary>A lava tile with open air (not rock) right above it: the lake's top, where it bubbles.</summary>
    public static bool IsSurface(TileWorld w, int x, int y) => At(w, x, y) && y == Row && !w.Solid(x, y - 1);

    /// <summary>
    /// Lakes along the top lava row: runs of open lava tiles with air above, as (first column, last column).
    /// For the census and the test bot.
    /// </summary>
    public static List<(int From, int To)> Lakes(TileWorld w, int fromX = 0, int toX = TileWorld.Width - 1)
    {
        var lakes = new List<(int, int)>();
        int start = -1;
        for (int x = fromX; x <= toX + 1; x++)
        {
            bool open = x <= toX && IsSurface(w, x, Row);
            if (open && start < 0) start = x;
            if (!open && start >= 0)
            {
                lakes.Add((start, x - 1));
                start = -1;
            }
        }
        return lakes;
    }

    /// <summary>Lava tiles in the whole world (census).</summary>
    public static int Count(TileWorld w)
    {
        int n = 0;
        for (int y = Row; y < TileWorld.Height - 2; y++)
            for (int x = 0; x < TileWorld.Width; x++)
                if (w.Get(x, y) == Tile.Air) n++;
        return n;
    }
}
