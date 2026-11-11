using System.Collections.Generic;
using Godot;

namespace Gearheart.World;

/// <summary>
/// Shows a <see cref="TileWorld"/>. Only the chunks around <see cref="Focus"/> exist as nodes,
/// so the world can be very large; chunks are created and freed as the camera moves.
/// </summary>
public partial class TileWorldView : Node2D
{
    public TileWorld World;

    /// <summary>World position to keep loaded around (the camera target).</summary>
    public Vector2 Focus;

    private const float ChunkPixels = TileChunk.Size * TileWorld.TileSize;
    private static readonly Vector2 LoadRange = new(1300f, 800f);
    private static readonly Vector2 KeepRange = new(2000f, 1300f);

    private readonly Dictionary<(int, int), TileChunk> _chunks = new();
    private readonly List<(int, int)> _toRemove = new();

    public override void _Ready()
    {
        World.Changed += OnTileChanged;
        UpdateChunks();
    }

    public override void _ExitTree()
    {
        if (World != null) World.Changed -= OnTileChanged;
    }

    public override void _Process(double delta) => UpdateChunks();

    private void UpdateChunks()
    {
        int maxX = (TileWorld.Width - 1) / TileChunk.Size, maxY = (TileWorld.Height - 1) / TileChunk.Size;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((Focus.X - LoadRange.X) / ChunkPixels), 0, maxX);
        int x1 = Mathf.Clamp(Mathf.FloorToInt((Focus.X + LoadRange.X) / ChunkPixels), 0, maxX);
        int y0 = Mathf.Clamp(Mathf.FloorToInt((Focus.Y - LoadRange.Y) / ChunkPixels), 0, maxY);
        int y1 = Mathf.Clamp(Mathf.FloorToInt((Focus.Y + LoadRange.Y) / ChunkPixels), 0, maxY);

        for (int cy = y0; cy <= y1; cy++)
        {
            for (int cx = x0; cx <= x1; cx++)
            {
                if (_chunks.ContainsKey((cx, cy))) continue;
                var chunk = new TileChunk
                {
                    World = World,
                    ChunkX = cx,
                    ChunkY = cy,
                    Position = new Vector2(cx, cy) * ChunkPixels,
                };
                _chunks[(cx, cy)] = chunk;
                AddChild(chunk);
            }
        }

        _toRemove.Clear();
        foreach (var (key, chunk) in _chunks)
        {
            Vector2 center = chunk.Position + new Vector2(ChunkPixels, ChunkPixels) / 2f;
            if (Mathf.Abs(center.X - Focus.X) > KeepRange.X + ChunkPixels || Mathf.Abs(center.Y - Focus.Y) > KeepRange.Y + ChunkPixels)
                _toRemove.Add(key);
        }
        foreach (var key in _toRemove)
        {
            _chunks[key].QueueFree();
            _chunks.Remove(key);
        }
    }

    private void OnTileChanged(int x, int y)
    {
        // Neighbours matter too: ramps and grass tops depend on the tiles around them.
        for (int dy = -2; dy <= 2; dy += 2)
        {
            for (int dx = -2; dx <= 2; dx += 2)
            {
                var key = (Mathf.FloorToInt((x + dx) / (float)TileChunk.Size), Mathf.FloorToInt((y + dy) / (float)TileChunk.Size));
                if (_chunks.TryGetValue(key, out var chunk)) chunk.QueueRedraw();
            }
        }
    }
}
