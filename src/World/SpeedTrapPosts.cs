using System;
using System.Collections.Generic;
using Godot;

namespace Gearheart.World;

/// <summary>One speed camera on the road: its tile column, which side of the garage, and its number on that side.</summary>
public readonly record struct SpeedTrap(int Column, int Side, int Number)
{
    /// <summary>"E1", "W2": east or west of the garage, counted outwards.</summary>
    public string Name => $"{(Side > 0 ? "E" : "W")}{Number}";

    /// <summary>World x of the line the car is measured at (the middle of the post's column).</summary>
    public float LineX => (Column + 0.5f) * TileWorld.TileSize;
}

/// <summary>
/// Speed traps: a camera post on a flat stretch of road every 85 m or so, the first about 40 m out on
/// each side. Drive past one and it flashes and reads your speed; the sign under the camera keeps your
/// best there ("BEST 74"), so every flat stretch has a number to beat — Need for Speed's speed cameras.
/// Placed from the terrain when the world loads (nothing saved, no world-generation change); the bests
/// live with the records in the progress file.
/// </summary>
public partial class SpeedTrapPosts : Node2D
{
    private const float Ts = TileWorld.TileSize;
    private const float DrawDistance = 1500f;
    private const float FlashSeconds = 0.35f, FreshSeconds = 4f;
    public const float PoleHeight = 132f;

    public TileWorld World;

    /// <summary>World position the posts are judged from (the player or the car).</summary>
    public Vector2 Focus;

    /// <summary>The best speed through a trap so far (km/h, 0 = none yet).</summary>
    public Func<SpeedTrap, float> Best = _ => 0f;

    private List<SpeedTrap> _traps = new();
    private readonly Dictionary<int, float> _flashAt = new(), _freshAt = new();
    private float _time;

    public IReadOnlyList<SpeedTrap> Traps => _traps;

    public override void _Ready() => _traps = Place(World);

    /// <summary>
    /// Finds the posts: aims for 40, 125, 210 and 295 m out on each side and takes the nearest column
    /// (up to 25 m either way) whose road stays within two tiles for 2 m either way, clear of kickers.
    /// </summary>
    public static List<SpeedTrap> Place(TileWorld world)
    {
        var list = new List<SpeedTrap>();
        int center = (TileWorld.PadFrom + TileWorld.PadTo) / 2;
        foreach (int side in new[] { 1, -1 })
        {
            int number = 0;
            foreach (int meters in new[] { 40, 125, 210, 295 })
            {
                int target = center + side * meters * 4;
                // Nearest good column to the aim, looking both ways (out first), up to 25 m off.
                for (int step = 0; step <= 100; step++)
                {
                    int c = -1;
                    foreach (int s in new[] { side * step, -side * step })
                    {
                        int x = target + s;
                        if (x >= 30 && x <= TileWorld.Width - 30 && Flat(world, x)) { c = x; break; }
                    }
                    if (c < 0) continue;
                    list.Add(new SpeedTrap(c, side, ++number));
                    break;
                }
            }
        }
        return list;
    }

    private static bool Flat(TileWorld world, int c)
    {
        if (world.KickerNear(c, 12) is not null) return false;
        int lo = int.MaxValue, hi = int.MinValue;
        for (int x = c - 8; x <= c + 8; x++)
        {
            int row = world.SurfaceRow(x);
            lo = Mathf.Min(lo, row);
            hi = Mathf.Max(hi, row);
        }
        return hi - lo <= 2;
    }

    /// <summary>The camera fires: the lens flashes white, and a new best turns the sign gold for a few seconds.</summary>
    public void Flash(SpeedTrap trap, bool record)
    {
        _flashAt[trap.Column] = _time;
        if (record) _freshAt[trap.Column] = _time;
    }

    /// <summary>World position of the camera's lens (for effects).</summary>
    public Vector2 LensAt(SpeedTrap trap) => new(trap.LineX, Ground(trap) - PoleHeight + 10f);

    private float Ground(SpeedTrap trap) => World.GroundY(trap.LineX, (World.SurfaceRow(trap.Column) - 14) * Ts);

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (World == null) return;
        Font font = ThemeDB.FallbackFont;
        var ink = new Color(0.12f, 0.12f, 0.14f);
        foreach (SpeedTrap t in _traps)
        {
            float x = t.LineX;
            if (Mathf.Abs(x - Focus.X) > DrawDistance) continue;
            float g = Ground(t);
            float top = g - PoleHeight;

            // A concrete footing and a grey pole with a hazard-striped base.
            DrawRect(new Rect2(x - 9f, g - 6f, 18f, 8f), new Color(0.55f, 0.55f, 0.52f));
            DrawRect(new Rect2(x - 9f, g - 6f, 18f, 2f), new Color(0.7f, 0.7f, 0.66f));
            DrawRect(new Rect2(x - 4f, top, 8f, PoleHeight - 6f), ink);
            DrawRect(new Rect2(x - 3f, top, 6f, PoleHeight - 6f), new Color(0.62f, 0.64f, 0.66f));
            DrawRect(new Rect2(x - 3f, top, 2f, PoleHeight - 6f), new Color(0.78f, 0.8f, 0.82f));
            for (int i = 0; i < 4; i++)
                DrawRect(new Rect2(x - 3f, g - 30f + i * 6f, 6f, 3f), i % 2 == 0 ? new Color(0.98f, 0.8f, 0.15f) : ink);

            // The camera: a dark box with a lens on each side (it reads both ways), a little hood over each.
            var box = new Rect2(x - 18f, top - 6f, 36f, 22f);
            DrawRect(box.Grow(2f), ink);
            DrawRect(box, new Color(0.3f, 0.32f, 0.36f));
            DrawRect(new Rect2(box.Position, new Vector2(box.Size.X, 4f)), new Color(0.42f, 0.45f, 0.5f));
            DrawRect(new Rect2(x - 22f, top - 9f, 44f, 4f), ink);
            float flashAge = _flashAt.TryGetValue(t.Column, out float fa) ? _time - fa : 99f;
            bool lit = flashAge < FlashSeconds;
            float blink = 0.5f + 0.5f * Mathf.Sin(_time * 4f + t.Column);
            foreach (float s in new[] { -1f, 1f })
            {
                var lens = new Vector2(x + s * 13f, top + 6f);
                DrawCircle(lens, 5.5f, ink);
                DrawCircle(lens, 4f, lit ? Colors.White : new Color(0.25f, 0.45f, 0.65f));
                DrawCircle(lens + new Vector2(-1.5f, -1.5f), 1.3f, new Color(1f, 1f, 1f, 0.8f));
            }
            // A red "recording" light on top that blinks slowly.
            DrawCircle(new Vector2(x, top - 11f), 2.5f, new Color(1f, 0.2f, 0.15f, 0.45f + 0.55f * blink));

            // The flash: a white burst from the lenses that fades fast.
            if (lit)
            {
                float a = 1f - flashAge / FlashSeconds;
                Vector2 c = new(x, top + 6f);
                DrawCircle(c, 30f + 70f * (1f - a), new Color(1f, 1f, 1f, 0.35f * a));
                DrawCircle(c, 14f + 20f * (1f - a), new Color(1f, 1f, 1f, 0.8f * a));
                for (int i = 0; i < 8; i++)
                {
                    Vector2 dir = Vector2.Right.Rotated(i * Mathf.Tau / 8f + 0.3f);
                    DrawLine(c + dir * 18f, c + dir * (40f + 60f * (1f - a)), new Color(1f, 1f, 0.9f, 0.7f * a), 2f);
                }
            }

            // The sign under the camera: blue road-sign panel, "SPEED TRAP" and your best through it.
            float best = Best(t);
            float freshAge = _freshAt.TryGetValue(t.Column, out float fr) ? _time - fr : 99f;
            bool fresh = freshAge < FreshSeconds;
            var sign = new Rect2(x - 34f, top + 24f, 68f, 34f);
            DrawRect(sign.Grow(2f), ink);
            DrawRect(sign, fresh ? new Color(0.85f, 0.62f, 0.12f) : new Color(0.13f, 0.33f, 0.66f));
            DrawRect(sign.Grow(-2f), fresh ? new Color(0.98f, 0.8f, 0.25f) : new Color(0.18f, 0.42f, 0.78f), false, 1f);
            Color text = fresh ? new Color(0.15f, 0.1f, 0.02f) : Colors.White;
            DrawString(font, new Vector2(sign.Position.X, sign.Position.Y + 12f), "SPEED TRAP", HorizontalAlignment.Center, sign.Size.X, 9, text);
            string line = best > 0f ? $"BEST {best:0}" : "-- km/h";
            DrawString(font, new Vector2(sign.Position.X, sign.Position.Y + 28f), line, HorizontalAlignment.Center, sign.Size.X, best > 0f ? 14 : 12, text);

            // Over a fresh record, the sign pops a little sparkle ring.
            if (fresh && freshAge < 1f)
            {
                float r = 20f + freshAge * 50f, a = 1f - freshAge;
                DrawArc(sign.GetCenter(), r, 0f, Mathf.Tau, 24, new Color(1f, 0.85f, 0.3f, 0.7f * a), 2f);
            }
        }
    }
}
