using Godot;

namespace Gearheart.World;

/// <summary>
/// Draws the gleam on lucky ore tiles near the focus: a warm gold rim that breathes, a white shine
/// sweeping across the tile every couple of seconds and a little star at the peak. Lives on the
/// glint layer (over the underground darkness), so a lucky tile reads like a coin in the rock. Also
/// the gold ring that bursts out when one breaks.
/// </summary>
public partial class LuckyGleams : Node2D
{
    public TileWorld World;
    public Vector2 Focus;

    /// <summary>Full brightness within this distance (pixels), gone by <see cref="FadeTo"/>.</summary>
    public const float FullUntil = 240f, FadeTo = 440f;

    private const float Ts = TileWorld.TileSize;
    private static readonly Color Gold = new(1f, 0.82f, 0.25f);
    private static readonly Color Shine = new(1f, 0.98f, 0.85f);
    private float _time;

    /// <summary>Lucky tiles drawn last frame, and the distance to the closest one (for hints and the bot).</summary>
    public int Shown { get; private set; }
    public float Nearest { get; private set; } = float.PositiveInfinity;
    public Vector2I NearestTile { get; private set; }

    private readonly System.Collections.Generic.List<(Vector2 At, float Age)> _rings = new();

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        for (int i = _rings.Count - 1; i >= 0; i--)
        {
            var r = _rings[i];
            r.Age += dt;
            if (r.Age > 0.6f) _rings.RemoveAt(i); else _rings[i] = r;
        }
        QueueRedraw();
    }

    /// <summary>A gold shock ring where a lucky tile just broke.</summary>
    public void Ring(Vector2 at) => _rings.Add((at, 0f));

    public override void _Draw()
    {
        Shown = 0;
        Nearest = float.PositiveInfinity;
        if (World != null)
        {
            int r = Mathf.CeilToInt(FadeTo / Ts) + 1;
            int cx = Mathf.FloorToInt(Focus.X / Ts), cy = Mathf.FloorToInt(Focus.Y / Ts);
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    Tile t = World.Get(x, y);
                    if (!LuckyOre.IsLucky(t, x, y)) continue;
                    var origin = new Vector2(x * Ts, y * Ts);
                    float d = (origin + new Vector2(Ts * 0.5f, Ts * 0.5f)).DistanceTo(Focus);
                    if (d < Nearest) { Nearest = d; NearestTile = new Vector2I(x, y); }
                    if (d > FadeTo) continue;
                    Shown++;
                    float near = 1f - Mathf.Clamp((d - FullUntil) / (FadeTo - FullUntil), 0f, 1f);
                    DrawGleam(origin, LuckyOre.Hash(x, y), near);
                }
        }

        foreach (var (at, age) in _rings)
        {
            float k = age / 0.6f;
            DrawArc(at, 6f + 34f * Mathf.Sqrt(k), 0f, Mathf.Tau, 28, new Color(Gold, 0.9f * (1f - k)), 3f * (1f - k) + 1f);
            DrawArc(at, 3f + 20f * k, 0f, Mathf.Tau, 20, new Color(Shine, 0.7f * (1f - k)), 1.5f);
        }
    }

    private void DrawGleam(Vector2 o, uint h, float near)
    {
        float phase = _time + (h % 1000) / 400f;
        float breathe = 0.55f + 0.45f * Mathf.Sin(phase * 3.1f);

        // A gold rim just inside the tile, and a soft wash over it.
        DrawRect(new Rect2(o, new Vector2(Ts, Ts)), new Color(Gold, 0.16f * near * breathe));
        DrawRect(new Rect2(o + new Vector2(1f, 1f), new Vector2(Ts - 2f, Ts - 2f)), new Color(Gold, (0.45f + 0.4f * breathe) * near), false, 1.5f);

        // A shine sweeping corner to corner every 2.2 s (the line x + y = c, clipped to the tile).
        float cycle = (phase % 2.2f) / 2.2f;
        float c = Mathf.Lerp(-6f, Ts * 2f + 6f, cycle * 1.6f);   // the sweep takes the first 60 % of the cycle
        for (int k = 0; k < 2; k++)
        {
            float cc = c - k * 5f;
            float x0 = Mathf.Max(0f, cc - Ts), x1 = Mathf.Min(Ts, cc);
            if (x1 <= x0) continue;
            var a = o + new Vector2(x0, cc - x0);
            var b = o + new Vector2(x1, cc - x1);
            DrawLine(a, b, new Color(Shine, (k == 0 ? 0.85f : 0.4f) * near), k == 0 ? 2.5f : 1.5f);
        }

        // A star pops on the top corner as the shine leaves it.
        float star = Mathf.Clamp(1f - Mathf.Abs(cycle - 0.62f) / 0.12f, 0f, 1f);
        if (star > 0.02f)
        {
            var p = o + new Vector2(Ts - 2f, 2f);
            float s = 2f + 6f * star;
            var col = new Color(Shine, star * near);
            DrawLine(p - new Vector2(s, 0f), p + new Vector2(s, 0f), col, 1.5f);
            DrawLine(p - new Vector2(0f, s), p + new Vector2(0f, s), col, 1.5f);
        }
    }
}
