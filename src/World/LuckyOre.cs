using System.Collections.Generic;

namespace Gearheart.World;

/// <summary>
/// Lucky strikes: about one ore tile in <see cref="OneIn"/> is a gleaming variant that pays
/// <see cref="Multiplier"/>x when it breaks. Which tiles are lucky follows from their position
/// alone (a fixed hash), so nothing is saved and the world format is unchanged: a lucky tile stays
/// lucky across loads until it is mined, and then it is gone like any other tile.
/// </summary>
public static class LuckyOre
{
    public const int OneIn = 16;
    public const int Multiplier = 3;

    /// <summary>Tiles a test makes lucky by hand (and tiles it makes ordinary), on top of the hash.</summary>
    public static readonly HashSet<(int, int)> Forced = new(), Suppressed = new();

    public static bool IsOreTile(Tile t) => t is Tile.ScrapOre or Tile.IronOre or Tile.CopperOre or Tile.Crystal;

    /// <summary>True when the tile at x,y is ore and one of the lucky ones.</summary>
    public static bool IsLucky(Tile t, int x, int y)
    {
        if (!IsOreTile(t)) return false;
        if (Forced.Count > 0 && Forced.Contains((x, y))) return true;
        if (Suppressed.Count > 0 && Suppressed.Contains((x, y))) return false;
        // Near the magma sea the heat makes rich ore: one in four is lucky there.
        if (y >= Lava.Row - Lava.RichBand) return Hash(x, y) % Lava.RichOneIn == 0;
        return Hash(x, y) % OneIn == 0;
    }

    /// <summary>A well-mixed position hash (so lucky tiles are not lined up in rows).</summary>
    public static uint Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)x * 0x9E3779B1u ^ (uint)y * 0x85EBCA77u ^ 0x27D4EB2Fu;
            h ^= h >> 15; h *= 0x2C1B3C6Du;
            h ^= h >> 12; h *= 0x297A2D39u;
            h ^= h >> 15;
            return h;
        }
    }
}
