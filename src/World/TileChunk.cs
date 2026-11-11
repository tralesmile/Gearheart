using Gearheart.Core;
using Godot;

namespace Gearheart.World;

/// <summary>Draws one 32 x 32 block of tiles. Redrawn only when one of its tiles changes.</summary>
public partial class TileChunk : Node2D
{
    public const int Size = 32;

    public TileWorld World;
    public int ChunkX, ChunkY;

    private static readonly Color GrassGreen = new(0.35f, 0.66f, 0.24f);

    public override void _Draw()
    {
        if (World == null) return;
        Texture2D tex = Art.Tiles;
        const float ts = TileWorld.TileSize;
        const float half = TileWorld.Half;

        for (int j = 0; j < Size; j++)
        {
            int ty = ChunkY * Size + j;
            if (ty >= TileWorld.Height) break;

            for (int i = 0; i < Size; i++)
            {
                int tx = ChunkX * Size + i;
                if (tx >= TileWorld.Width) break;

                Tile tile = World.Get(tx, ty);
                var o = new Vector2(i * ts, j * ts);
                var cell = new Rect2(o, new Vector2(ts, ts));
                int depth = ty - World.SurfaceRow(tx);
                int variant = Hash(tx, ty);
                float shade = 1f - 0.3f * Mathf.Clamp(depth / 70f, 0f, 1f);
                var tint = new Color(shade, shade, shade);

                if (tile == Tile.Air)
                {
                    if (depth > 0) DrawTextureRectRegion(tex, cell, WallCell(depth, variant), tint);

                    if (World.FilletRisingRight(tx, ty))
                    {
                        Tile step = World.Get(tx + 1, ty);
                        Poly(tex, SourceOf(step, false, variant), o, tint, new Vector2(0, ts), new Vector2(ts, half), new Vector2(ts, ts));
                        if (step == Tile.Grass) DrawLine(o + new Vector2(0, ts), o + new Vector2(ts, half), GrassGreen, 3f);
                    }
                    if (World.FilletRisingLeft(tx, ty))
                    {
                        Tile step = World.Get(tx - 1, ty);
                        Poly(tex, SourceOf(step, false, variant), o, tint, new Vector2(0, half), new Vector2(ts, ts), new Vector2(0, ts));
                        if (step == Tile.Grass) DrawLine(o + new Vector2(0, half), o + new Vector2(ts, ts), GrassGreen, 3f);
                    }
                    continue;
                }

                bool open = !World.Solid(tx, ty - 1);
                bool cutL = World.TopCut(tx, ty, -1), cutR = World.TopCut(tx, ty, 1);
                Rect2 src = SourceOf(tile, open && !cutL && !cutR, variant);

                if (cutL || cutR)
                {
                    if (depth > 0) DrawTextureRectRegion(tex, cell, WallCell(depth, variant), tint);
                    if (cutL && cutR)
                    {
                        Poly(tex, src, o, tint, new Vector2(0, ts), new Vector2(0, half), new Vector2(half, 0), new Vector2(ts, half), new Vector2(ts, ts));
                        if (tile == Tile.Grass)
                        {
                            DrawLine(o + new Vector2(0, half), o + new Vector2(half, 0), GrassGreen, 3f);
                            DrawLine(o + new Vector2(half, 0), o + new Vector2(ts, half), GrassGreen, 3f);
                        }
                    }
                    else if (cutL)
                    {
                        Poly(tex, src, o, tint, new Vector2(0, ts), new Vector2(0, half), new Vector2(ts, 0), new Vector2(ts, ts));
                        if (tile == Tile.Grass) DrawLine(o + new Vector2(0, half), o + new Vector2(ts, 0), GrassGreen, 3f);
                    }
                    else
                    {
                        Poly(tex, src, o, tint, new Vector2(0, ts), new Vector2(0, 0), new Vector2(ts, half), new Vector2(ts, ts));
                        if (tile == Tile.Grass) DrawLine(o + new Vector2(0, 0), o + new Vector2(ts, half), GrassGreen, 3f);
                    }
                }
                else
                {
                    DrawTextureRectRegion(tex, cell, src, tint);
                }

                float damage = World.DamageOf(tx, ty);
                if (damage > 0f)
                    DrawTextureRectRegion(tex, cell, Art.TileCell(Mathf.Clamp((int)(damage * 4f), 0, 3), 4));
            }
        }
    }

    private static int Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177;
            return (int)((h ^ (h >> 16)) & 0x7fffffff);
        }
    }

    private static Rect2 WallCell(int depth, int variant) => Art.TileCell((depth < 9 ? 4 : 6) + (variant & 1), 4);

    private static Rect2 SourceOf(Tile tile, bool grassTop, int variant) => tile switch
    {
        Tile.Grass => grassTop ? Art.TileCell(variant & 3, 2) : Art.TileCell(variant & 3, 0),
        Tile.Dirt => Art.TileCell(variant & 3, 0),
        Tile.Stone => Art.TileCell(variant & 3, 1),
        Tile.ScrapOre => Art.TileCell(variant & 1, 3),
        Tile.IronOre => Art.TileCell(2 + (variant & 1), 3),
        Tile.CopperOre => Art.TileCell(4 + (variant & 1), 3),
        Tile.Bedrock => Art.TileCell(6 + (variant & 1), 3),
        _ => Art.TileCell(variant & 1, 5),
    };

    /// <summary>Draws a textured convex polygon inside one tile; points are in tile-local pixels.</summary>
    private void Poly(Texture2D tex, Rect2 src, Vector2 origin, Color tint, params Vector2[] local)
    {
        Vector2 size = tex.GetSize();
        var points = new Vector2[local.Length];
        var uvs = new Vector2[local.Length];
        for (int k = 0; k < local.Length; k++)
        {
            points[k] = origin + local[k];
            // Inset by a hair so neighbouring atlas cells never bleed in.
            Vector2 inside = new(Mathf.Clamp(local[k].X, 0.05f, 15.95f), Mathf.Clamp(local[k].Y, 0.05f, 15.95f));
            uvs[k] = (src.Position + inside) / size;
        }
        DrawPolygon(points, new[] { tint }, uvs, tex);
    }
}
