using System;
using System.Collections.Generic;
using Gearheart.Physics;
using Godot;

namespace Gearheart.World;

public enum Tile : byte
{
    Air = 0,
    Grass,
    Dirt,
    Stone,
    ScrapOre,
    IronOre,
    CopperOre,
    Bedrock,
    Planks,
    Sand,
    Sandstone,
    Snow,
    Ice,
}

public enum Biome
{
    Meadow,
    Desert,
    Frozen,
}

/// <summary>
/// The destructible tile world: a grid of 16 px tiles with generation, mining, placing,
/// saving, and circle collision for the soft-body vehicles. Exposed one-tile steps are
/// automatically bevelled into 45-degree slopes so wheels (and feet) can travel over them.
/// </summary>
public sealed class TileWorld : ITerrainCollider, IMineable, ISurfaceInfo
{
    public const int TileSize = 16;
    public const int Width = 2560;           // 640 m across
    public const int Height = 192;
    public const int BaseSurface = 64;       // surface row at the spawn pad
    public const int PadFrom = 1258, PadTo = 1302;   // flat ground for the garage (tile columns)
    private const uint SaveMagic = 0x35574847;   // "GHW5": bump when generation changes

    private readonly byte[] _tiles = new byte[Width * Height];
    private readonly int[] _surface = new int[Width];
    private readonly Dictionary<int, float> _damage = new();

    /// <summary>A tile changed (broken, placed or damaged): redraw it.</summary>
    public event Action<int, int> Changed;

    /// <summary>A tile was mined out. Arguments: x, y, what it was.</summary>
    public event Action<int, int, Tile> TileBroken;

    public static Vector2 PadCenter => new((PadFrom + PadTo) / 2f * TileSize, BaseSurface * TileSize);

    // ---- Access -----------------------------------------------------------------------------

    public Tile Get(int x, int y) =>
        x < 0 || x >= Width || y >= Height ? Tile.Bedrock : y < 0 ? Tile.Air : (Tile)_tiles[y * Width + x];

    public bool Solid(int x, int y) => Get(x, y) != Tile.Air;

    public int SurfaceRow(int x) => _surface[Mathf.Clamp(x, 0, Width - 1)];

    /// <summary>World y of the top of the first solid tile at or below the given point.</summary>
    public float GroundY(float worldX, float fromWorldY = 0f)
    {
        int tx = Mathf.FloorToInt(worldX / TileSize);
        for (int ty = Mathf.Max(0, Mathf.FloorToInt(fromWorldY / TileSize)); ty < Height; ty++)
            if (Solid(tx, ty)) return ty * TileSize;
        return Height * TileSize;
    }

    public static float Hardness(Tile tile) => tile switch
    {
        Tile.Snow => 5f,
        Tile.Sand => 7f,
        Tile.Grass or Tile.Dirt => 10f,
        Tile.Ice => 18f,
        Tile.Sandstone => 22f,
        Tile.Planks => 14f,
        Tile.Stone => 28f,
        Tile.ScrapOre => 32f,
        Tile.IronOre => 40f,
        Tile.CopperOre => 48f,
        _ => float.PositiveInfinity,
    };

    /// <summary>What a mined tile gives: inventory key and amount.</summary>
    public static (string Item, int Amount) Drop(Tile tile) => tile switch
    {
        Tile.Grass or Tile.Dirt or Tile.Sand => ("Dirt", 1),
        Tile.Stone or Tile.Sandstone => ("Stone", 1),
        Tile.ScrapOre => ("Scrap", 2),
        Tile.IronOre => ("Iron", 1),
        Tile.CopperOre => ("Copper", 1),
        _ => (null, 0),
    };

    /// <summary>0..1 damage progress of a tile, for the crack overlay.</summary>
    public float DamageOf(int x, int y) =>
        _damage.TryGetValue(y * Width + x, out float d) ? d / Hardness(Get(x, y)) : 0f;

    // Ramps. A one-tile step would stop a wheel dead, so every exposed step is smoothed into a
    // gentle ramp (rise 16 over run 32): a wedge-shaped "fillet" fills the lower half in the open
    // tile beside the step, and the step tile's top is cut to continue the same line.
    // Nothing is stored: shapes are worked out from the neighbours, so digging reshapes them.

    public const float Half = TileSize / 2f;

    /// <summary>True if tile (x, y) is a step with a ramp on the given side (dir = -1 left, +1 right).</summary>
    public bool StepSide(int x, int y, int dir)
    {
        Tile t = Get(x, y);
        if (t == Tile.Air || t == Tile.Bedrock || t == Tile.Planks) return false;
        return !Solid(x, y - 1) && Solid(x, y + 1) && !Solid(x + dir, y) && Solid(x + dir, y + 1);
    }

    /// <summary>True if this open tile holds a ramp wedge rising toward the step on its right.</summary>
    public bool FilletRisingRight(int x, int y) => !Solid(x, y) && StepSide(x + 1, y, -1);

    /// <summary>True if this open tile holds a ramp wedge rising toward the step on its left.</summary>
    public bool FilletRisingLeft(int x, int y) => !Solid(x, y) && StepSide(x - 1, y, 1);

    /// <summary>True if the step tile's top is cut on that side to continue its ramp.</summary>
    public bool TopCut(int x, int y, int dir) =>
        StepSide(x, y, dir) && !FilletRisingRight(x, y - 1) && !FilletRisingLeft(x, y - 1);

    /// <summary>True for tiles that are full, square blocks (what the walking player bumps into).</summary>
    public bool FullBlock(int x, int y) => Solid(x, y) && !TopCut(x, y, -1) && !TopCut(x, y, 1);

    /// <summary>
    /// World y of the walkable ramp surface inside cell (x, y) at worldX, or NaN if the cell has no ramp.
    /// </summary>
    public float RampY(int x, int y, float worldX)
    {
        float l = x * TileSize, t = y * TileSize, b = t + TileSize;
        float f = Mathf.Clamp((worldX - l) / TileSize, 0f, 1f);
        if (!Solid(x, y))
        {
            float best = float.NaN;
            if (FilletRisingRight(x, y)) best = b - Half * f;
            if (FilletRisingLeft(x, y))
            {
                float other = b - Half * (1f - f);
                best = float.IsNaN(best) ? other : Mathf.Min(best, other);
            }
            return best;
        }

        bool cl = TopCut(x, y, -1), cr = TopCut(x, y, 1);
        if (cl && cr) return t + Half * Mathf.Abs(f - 0.5f) * 2f;
        if (cl) return t + Half * (1f - f);
        if (cr) return t + Half * f;
        return float.NaN;
    }

    // ---- Biomes --------------------------------------------------------------------------

    /// <summary>How far (in tiles) the meadow reaches from the garage before the next biome starts.</summary>
    public const int MeadowReach = 380;

    /// <summary>Desert to the right (east), frozen land to the left (west), meadow around the garage.</summary>
    public static Biome BiomeAt(int x)
    {
        int middle = (PadFrom + PadTo) / 2;
        // A ragged border a few tiles wide, so biomes do not meet in a straight line.
        int wobble = (int)(((uint)(x * 2654435761u) >> 28) % 7) - 3;
        int d = x - middle + wobble;
        return d > MeadowReach ? Biome.Desert : d < -MeadowReach ? Biome.Frozen : Biome.Meadow;
    }

    /// <summary>0..1 share of desert and frozen at a world x, blended over a stretch (for sky colours).</summary>
    public static (float Desert, float Frozen) BiomeBlend(float worldX)
    {
        float d = worldX / TileSize - (PadFrom + PadTo) / 2f;
        float t = Mathf.Clamp((Mathf.Abs(d) - MeadowReach + 30f) / 60f, 0f, 1f);
        return d > 0f ? (t, 0f) : (0f, t);
    }

    /// <summary>Tyre and boot grip of the ground at a point: 1 = normal, ice is very slippery.</summary>
    public float GripAt(Vector2 point)
    {
        int tx = Mathf.FloorToInt(point.X / TileSize), ty = Mathf.FloorToInt(point.Y / TileSize);
        Tile t = Get(tx, ty);
        if (t == Tile.Air) t = Get(tx, ty + 1);
        return t switch
        {
            Tile.Ice => 0.22f,
            Tile.Snow => 0.75f,
            Tile.Sand => 0.85f,
            _ => 1f,
        };
    }

    // ---- Changing ---------------------------------------------------------------------------

    public void Set(int x, int y, Tile tile)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        _tiles[y * Width + x] = (byte)tile;
        _damage.Remove(y * Width + x);
        Changed?.Invoke(x, y);
    }

    /// <summary>Applies mining work to one tile. Returns true when it breaks.</summary>
    public bool Damage(int x, int y, float work)
    {
        Tile tile = Get(x, y);
        float hardness = Hardness(tile);
        if (tile == Tile.Air || float.IsInfinity(hardness) || x < 0 || x >= Width || y < 0 || y >= Height) return false;

        int index = y * Width + x;
        float before = _damage.GetValueOrDefault(index);
        float total = before + work;
        if (total >= hardness)
        {
            _tiles[index] = (byte)Tile.Air;
            _damage.Remove(index);
            TileBroken?.Invoke(x, y, tile);
            Changed?.Invoke(x, y);
            return true;
        }

        _damage[index] = total;
        if ((int)(before / hardness * 4f) != (int)(total / hardness * 4f)) Changed?.Invoke(x, y);   // crack stage changed
        return false;
    }

    /// <summary>
    /// Drill mining. The bite is a box in front of the tip, sized so a car can dig a tunnel it fits
    /// through: 3 tiles wide and reaching well above the tip. Its bottom is the level the car's wheels
    /// stand on (floorY), so the tunnel floor stays flat even when the car is nosing up a slope.
    /// With no floorY (the car is tilted nose-down on purpose) it reaches below the tip and digs down.
    /// </summary>
    public bool Mine(Vector2 point, float reach, float work, float floorY = float.NaN)
    {
        const float halfWidth = 26f, above = 56f;
        float top = point.Y - above;
        float bottom = float.IsNaN(floorY) ? point.Y + 44f : Mathf.Max(floorY - 5f, point.Y);
        bool any = false;
        int x0 = Mathf.FloorToInt((point.X - halfWidth) / TileSize), x1 = Mathf.FloorToInt((point.X + halfWidth) / TileSize);
        int y0 = Mathf.FloorToInt(top / TileSize), y1 = Mathf.FloorToInt(bottom / TileSize);
        for (int ty = y0; ty <= y1; ty++)
        {
            for (int tx = x0; tx <= x1; tx++)
            {
                Tile tile = Get(tx, ty);
                if (tile == Tile.Air || float.IsInfinity(Hardness(tile))) continue;
                float cx = (tx + 0.5f) * TileSize, cy = (ty + 0.5f) * TileSize;
                if (Mathf.Abs(cx - point.X) > halfWidth || cy < top || cy > bottom) continue;
                Damage(tx, ty, work * 2f);
                any = true;
            }
        }
        return any;
    }

    // ---- Collision --------------------------------------------------------------------------

    public bool Collide(Vector2 p, float r, out Vector2 normal, out float penetration)
    {
        normal = Vector2.Up;
        penetration = 0f;
        const float ts = TileSize;
        int x0 = Mathf.FloorToInt((p.X - r) / ts), x1 = Mathf.FloorToInt((p.X + r) / ts);
        int y0 = Mathf.FloorToInt((p.Y - r) / ts), y1 = Mathf.FloorToInt((p.Y + r) / ts);

        const float A = 0.4472136f, B = 0.8944272f, D = 0.70710678f;
        for (int ty = y0; ty <= y1; ty++)
        {
            for (int tx = x0; tx <= x1; tx++)
            {
                float l = tx * ts, t = ty * ts, rr = l + ts, b = t + ts;

                if (!Solid(tx, ty))
                {
                    if (StepSide(tx + 1, ty, -1)) Edge(p, r, new Vector2(l, b), new Vector2(rr, b - Half), new Vector2(-A, -B), ref normal, ref penetration);
                    if (StepSide(tx - 1, ty, 1)) Edge(p, r, new Vector2(l, b - Half), new Vector2(rr, b), new Vector2(A, -B), ref normal, ref penetration);
                    continue;
                }

                bool stepL = StepSide(tx, ty, -1), stepR = StepSide(tx, ty, 1);
                bool cutL = TopCut(tx, ty, -1), cutR = TopCut(tx, ty, 1);

                if (!Solid(tx, ty - 1))
                {
                    if (cutL && cutR)
                    {
                        Edge(p, r, new Vector2(l, t + Half), new Vector2(l + Half, t), new Vector2(-D, -D), ref normal, ref penetration);
                        Edge(p, r, new Vector2(l + Half, t), new Vector2(rr, t + Half), new Vector2(D, -D), ref normal, ref penetration);
                    }
                    else if (cutL) Edge(p, r, new Vector2(l, t + Half), new Vector2(rr, t), new Vector2(-A, -B), ref normal, ref penetration);
                    else if (cutR) Edge(p, r, new Vector2(l, t), new Vector2(rr, t + Half), new Vector2(A, -B), ref normal, ref penetration);
                    else Edge(p, r, new Vector2(l, t), new Vector2(rr, t), Vector2.Up, ref normal, ref penetration);
                }

                if (!Solid(tx, ty + 1)) Edge(p, r, new Vector2(rr, b), new Vector2(l, b), Vector2.Down, ref normal, ref penetration);

                if (!Solid(tx - 1, ty))
                {
                    if (!stepL) Edge(p, r, new Vector2(l, b), new Vector2(l, t), Vector2.Left, ref normal, ref penetration);
                    else if (!cutL) Edge(p, r, new Vector2(l, b - Half), new Vector2(l, t), Vector2.Left, ref normal, ref penetration);
                }

                if (!Solid(tx + 1, ty))
                {
                    if (!stepR) Edge(p, r, new Vector2(rr, t), new Vector2(rr, b), Vector2.Right, ref normal, ref penetration);
                    else if (!cutR) Edge(p, r, new Vector2(rr, t), new Vector2(rr, b - Half), Vector2.Right, ref normal, ref penetration);
                }
            }
        }

        return penetration > 0f;
    }

    /// <summary>Circle against one exposed tile edge with outward normal n; keeps the deepest hit.</summary>
    private static void Edge(Vector2 p, float r, Vector2 a, Vector2 b, Vector2 n, ref Vector2 normal, ref float penetration)
    {
        Vector2 ab = b - a;
        float along = (p - a).Dot(ab) / ab.LengthSquared();
        float signed = (p - a).Dot(n);
        float depth;
        Vector2 hitNormal;

        if (along >= 0f && along <= 1f)
        {
            if (signed >= r || signed < -TileSize * 0.75f) return;
            depth = r - signed;
            hitNormal = n;
        }
        else
        {
            if (signed < 0f) return;
            Vector2 d = p - (along < 0f ? a : b);
            float distance = d.Length();
            if (distance >= r) return;
            depth = r - distance;
            hitNormal = distance > 1e-4f ? d / distance : n;
        }

        if (depth > penetration)
        {
            penetration = depth;
            normal = hitNormal;
        }
    }

    // ---- Generation -------------------------------------------------------------------------

    public static TileWorld Generate(int seed)
    {
        var world = new TileWorld();
        var caves = new FastNoiseLite { Seed = seed, Frequency = 0.045f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        var ore = new FastNoiseLite { Seed = seed + 1, Frequency = 0.16f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        var hills = new FastNoiseLite { Seed = seed + 2, Frequency = 0.012f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        var patches = new FastNoiseLite { Seed = seed + 3, Frequency = 0.03f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        var rng = new RandomNumberGenerator { Seed = (ulong)seed };

        // Surface line: flat at the garage pad, hillier the further you go.
        // Desert dunes are long and smooth; the frozen land is rugged.
        float padMiddle = (PadFrom + PadTo) / 2f;
        for (int x = 0; x < Width; x++)
        {
            Biome biome = BiomeAt(x);
            float away = Mathf.Max(0f, Mathf.Abs(x - padMiddle) - (PadTo - PadFrom) / 2f);
            float amplitude = Mathf.Min(biome == Biome.Frozen ? 22f : biome == Biome.Desert ? 12f : 16f, away * 0.1f);
            float stretch = biome == Biome.Desert ? 0.6f : 1f;
            float h = hills.GetNoise1D(x * stretch) * amplitude + Mathf.Sin(x / 9f) * Mathf.Min(2f, away * 0.05f) * (biome == Biome.Desert ? 0.3f : 1f);
            world._surface[x] = BaseSurface + Mathf.RoundToInt(h);
        }

        // Walk outward from the pad: at most one tile of change every three columns (about 18 degrees),
        // so every slope is a ramp a car can climb.
        for (int side = -1; side <= 1; side += 2)
        {
            int sinceStep = 3;
            for (int x = (int)padMiddle + side; x >= 0 && x < Width; x += side)
            {
                int previous = world._surface[x - side];
                int wanted = Mathf.Clamp(world._surface[x], previous - 1, previous + 1);
                if (wanted != previous && sinceStep < 3) wanted = previous;
                sinceStep = wanted != previous ? 0 : sinceStep + 1;
                world._surface[x] = wanted;
            }
        }

        for (int x = 0; x < Width; x++)
        {
            Biome biome = BiomeAt(x);
            int surface = world._surface[x];
            int topDepth = 5 + (int)(rng.Randi() % 4);
            bool nearPad = x > PadFrom - 6 && x < PadTo + 6;
            bool flat = world._surface[Mathf.Max(0, x - 1)] == surface && world._surface[Mathf.Min(Width - 1, x + 1)] == surface;
            bool icePatch = biome == Biome.Frozen && flat && patches.GetNoise1D(x) > 0.1f;

            for (int y = surface; y < Height; y++)
            {
                int depth = y - surface;
                Tile tile = biome switch
                {
                    Biome.Desert => depth < topDepth + 2 ? Tile.Sand : depth < topDepth + 18 ? Tile.Sandstone : Tile.Stone,
                    Biome.Frozen => depth == 0 ? (icePatch ? Tile.Ice : Tile.Snow)
                        : depth == 1 ? (icePatch ? Tile.Ice : Tile.Snow)
                        : depth < topDepth ? Tile.Dirt : Tile.Stone,
                    _ => depth == 0 ? Tile.Grass : depth < topDepth ? Tile.Dirt : Tile.Stone,
                };

                // Caves, away from the surface and the garage. The frozen land has more of them.
                float caveLimit = biome == Biome.Frozen ? 0.36f : 0.42f;
                if (depth > 7 && !(nearPad && depth < 14) && caves.GetNoise2D(x, y * 1.6f) > caveLimit) tile = Tile.Air;

                // Ore pockets: scrap is shallow and common, iron deeper, copper deepest.
                // The frozen land is rich in iron; the desert has copper close to the surface.
                if (tile != Tile.Air && depth >= 2)
                {
                    float n = ore.GetNoise2D(x, y);
                    float n2 = ore.GetNoise2D(x + 500, y + 500);
                    float n3 = ore.GetNoise2D(x - 500, y + 900);
                    if (n > 0.62f && depth < 40) tile = Tile.ScrapOre;
                    else if (n2 > (biome == Biome.Frozen ? 0.58f : 0.66f) && depth >= (biome == Biome.Frozen ? 5 : 8)) tile = Tile.IronOre;
                    else if (n3 > (biome == Biome.Desert ? 0.62f : 0.7f) && depth >= (biome == Biome.Desert ? 9 : 22)) tile = Tile.CopperOre;
                }

                if (y >= Height - 2 || x < 2 || x >= Width - 2) tile = Tile.Bedrock;
                world._tiles[y * Width + x] = (byte)tile;
            }

            // Bedrock cliffs at the world's edges.
            if (x < 2 || x >= Width - 2)
                for (int y = Mathf.Max(0, surface - 40); y < surface; y++)
                    world._tiles[y * Width + x] = (byte)Tile.Bedrock;
        }

        // A wooden floor under the garage, so the pad never gets dug away by accident.
        for (int x = PadFrom + 1; x < PadFrom + 15; x++)
            world._tiles[BaseSurface * Width + x] = (byte)Tile.Planks;

        return world;
    }

    // ---- Saving -----------------------------------------------------------------------------

    public bool Save(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null) return false;
        file.Store32(SaveMagic);
        file.Store32(Width);
        file.Store32(Height);
        file.StoreBuffer(_tiles);
        foreach (int s in _surface) file.Store32((uint)s);
        return true;
    }

    public static TileWorld Load(string path)
    {
        if (!FileAccess.FileExists(path)) return null;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null || file.Get32() != SaveMagic || file.Get32() != Width || file.Get32() != Height) return null;

        var world = new TileWorld();
        byte[] tiles = file.GetBuffer(Width * Height);
        if (tiles.Length != Width * Height) return null;
        Array.Copy(tiles, world._tiles, tiles.Length);
        for (int x = 0; x < Width; x++) world._surface[x] = (int)file.Get32();
        return world;
    }
}
