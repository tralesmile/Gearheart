using Godot;

namespace Gearheart.World;

/// <summary>Shows a <see cref="TileWorld"/> as a grid of chunk nodes and keeps them up to date.</summary>
public partial class TileWorldView : Node2D
{
    public TileWorld World;

    private TileChunk[,] _chunks;

    public override void _Ready()
    {
        int columns = Mathf.CeilToInt(TileWorld.Width / (float)TileChunk.Size);
        int rows = Mathf.CeilToInt(TileWorld.Height / (float)TileChunk.Size);
        _chunks = new TileChunk[columns, rows];

        for (int cy = 0; cy < rows; cy++)
        {
            for (int cx = 0; cx < columns; cx++)
            {
                var chunk = new TileChunk
                {
                    World = World,
                    ChunkX = cx,
                    ChunkY = cy,
                    Position = new Vector2(cx, cy) * TileChunk.Size * TileWorld.TileSize,
                };
                _chunks[cx, cy] = chunk;
                AddChild(chunk);
            }
        }

        World.Changed += OnTileChanged;
    }

    public override void _ExitTree()
    {
        if (World != null) World.Changed -= OnTileChanged;
    }

    private void OnTileChanged(int x, int y)
    {
        // Neighbours matter too: ramps and grass tops depend on the tiles around them.
        int lastX = -1, lastY = -1;
        for (int dy = -2; dy <= 2; dy += 2)
        {
            for (int dx = -2; dx <= 2; dx += 2)
            {
                int cx = Mathf.Clamp((x + dx) / TileChunk.Size, 0, _chunks.GetLength(0) - 1);
                int cy = Mathf.Clamp((y + dy) / TileChunk.Size, 0, _chunks.GetLength(1) - 1);
                if (cx == lastX && cy == lastY) continue;
                _chunks[cx, cy].QueueRedraw();
                lastX = cx;
                lastY = cy;
            }
        }
    }
}
