using Godot;

namespace Gearheart.Game;

/// <summary>
/// "Which way is home?": when the garage is off screen, a small arrow sits at the screen edge on the
/// line from the middle of the screen to the garage door, with a house badge and the distance.
/// Plain (pale) most of the time; gold and pulsing when a part is ready to build; orange and
/// blinking when the car is low on fuel. Drawn on the HUD layer; it never takes mouse input.
/// </summary>
public partial class HomeArrow : Control
{
    public enum Mode { Plain, Ready, LowFuel }

    // Keep clear of the hint lines and status at the top and the bar, card and dashboard at the bottom.
    private const float SideMargin = 46f, TopMargin = 128f, BottomMargin = 138f;

    public bool Shown { get; private set; }
    public Mode Current { get; private set; }
    /// <summary>Where the arrow's tip is on screen, and the way it points (radians, 0 = right).</summary>
    public Vector2 Tip { get; private set; }
    public float Angle { get; private set; }

    private float _alpha;       // fades in and out
    private float _pop;         // 1 → 0 after the mode changed (a little bounce)
    private float _time;
    private float _meters;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    /// <summary>
    /// Called every frame. <paramref name="target"/> is the garage door in screen pixels;
    /// <paramref name="show"/> false hides it (garage on screen, close by, or the report is up).
    /// </summary>
    public void Set(Vector2 target, bool show, float meters, Mode mode)
    {
        if (mode != Current) _pop = 1f;
        Current = mode;
        Shown = show;
        _meters = meters;
        if (!show) return;

        Vector2 screen = GetViewportRect().Size;
        Vector2 centre = screen / 2f;
        Vector2 dir = target - centre;
        if (dir.LengthSquared() < 1f) dir = Vector2.Left;
        Angle = dir.Angle();

        // Where the ray from the middle of the screen leaves the inset box.
        var box = new Rect2(SideMargin, TopMargin, screen.X - 2f * SideMargin, screen.Y - TopMargin - BottomMargin);
        Vector2 half = box.Size / 2f;
        Vector2 mid = box.Position + half;
        float sx = Mathf.Abs(dir.X) > 0.001f ? half.X / Mathf.Abs(dir.X) : float.MaxValue;
        float sy = Mathf.Abs(dir.Y) > 0.001f ? half.Y / Mathf.Abs(dir.Y) : float.MaxValue;
        Tip = mid + dir * Mathf.Min(sx, sy);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _alpha = Mathf.MoveToward(_alpha, Shown ? 1f : 0f, dt * (Shown ? 3f : 5f));
        _pop = Mathf.Max(0f, _pop - dt / 0.6f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_alpha <= 0.01f) return;
        Font font = ThemeDB.FallbackFont;

        Color col, glow;
        float size = 15f, wobble = 0f;
        switch (Current)
        {
            case Mode.Ready:
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 5f);
                col = new Color(1f, 0.82f, 0.3f).Lerp(Colors.White, pulse * 0.35f);
                glow = new Color(1f, 0.75f, 0.2f, 0.10f + 0.10f * pulse);
                size = 19f + 2f * pulse;
                wobble = 6f * pulse;   // nudges toward home, like a finger tapping
                break;
            }
            case Mode.LowFuel:
            {
                bool on = Mathf.PosMod(_time, 0.9f) < 0.6f;
                col = on ? new Color(1f, 0.45f, 0.2f) : new Color(0.7f, 0.3f, 0.15f);
                glow = new Color(1f, 0.3f, 0.1f, on ? 0.16f : 0.05f);
                size = 18f;
                break;
            }
            default:
                col = new Color(0.86f, 0.92f, 1f, 0.85f);
                glow = new Color(0.6f, 0.75f, 1f, 0.08f);
                break;
        }
        size *= 1f + 0.45f * _pop * Mathf.Sin(_pop * Mathf.Pi);
        col.A *= _alpha;
        glow.A *= _alpha;

        Vector2 d = Vector2.FromAngle(Angle);
        Vector2 n = new(-d.Y, d.X);
        Vector2 tip = Tip + d * wobble;
        Vector2 back = tip - d * size * 1.5f;

        // Soft glow, then the arrowhead with a dark outline.
        // A soft glow (two faint discs, so it reads as light rather than a solid badge).
        DrawCircle(back + d * size * 0.6f, size * 1.45f, glow);
        DrawCircle(back + d * size * 0.6f, size * 1.05f, glow);
        var head = new[] { tip, back + n * size, back + d * size * 0.4f, back - n * size };
        var outline = new[] { tip + d * 3f, back + n * (size + 3f) - d * 1.5f, back + d * (size * 0.4f - 3f), back - n * (size + 3f) - d * 1.5f };
        DrawColoredPolygon(outline, new Color(0f, 0f, 0f, 0.75f * _alpha));
        DrawColoredPolygon(head, col);

        // The badge behind the arrow: a little house and the distance, kept on screen.
        string text = $"{_meters:0} m";
        string tag = Current switch { Mode.Ready => "PART READY", Mode.LowFuel => "LOW FUEL", _ => "" };
        const int textSize = 17, tagSize = 13;
        float tw = font.GetStringSize(text, HorizontalAlignment.Left, -1, textSize).X;
        float gw = tag == "" ? 0f : font.GetStringSize(tag, HorizontalAlignment.Left, -1, tagSize).X;
        float w = Mathf.Max(22f + 6f + tw, gw) + 14f;
        float h = tag == "" ? 30f : 44f;
        // Centre the badge just behind the arrow (clear of it whichever way it points), then keep it on screen.
        Vector2 badge = back - d * (10f + Mathf.Abs(d.X) * w / 2f + Mathf.Abs(d.Y) * h / 2f);
        Vector2 screen = GetViewportRect().Size;
        var rect = new Rect2(badge - new Vector2(w, h) / 2f, new Vector2(w, h));
        rect.Position = new Vector2(Mathf.Clamp(rect.Position.X, 6f, screen.X - w - 6f), Mathf.Clamp(rect.Position.Y, 6f, screen.Y - h - 6f));

        var bg = Current == Mode.Ready ? new Color(0.22f, 0.16f, 0.04f, 0.8f) : Current == Mode.LowFuel ? new Color(0.25f, 0.07f, 0.03f, 0.8f) : new Color(0.06f, 0.07f, 0.1f, 0.62f);
        bg.A *= _alpha;
        DrawRect(rect, bg);
        DrawRect(rect, new Color(col, col.A * 0.7f), false, Current == Mode.Plain ? 1f : 2f);

        float x = rect.Position.X + 7f;
        float rowY = rect.Position.Y + (tag == "" ? 15f : 29f);
        DrawHouse(new Vector2(x + 11f, rowY), col);
        Outlined(font, new Vector2(x + 28f, rowY + 6f), text, textSize, new Color(1f, 1f, 1f, _alpha));
        if (tag != "")
            Outlined(font, new Vector2(rect.Position.X + (w - gw) / 2f, rect.Position.Y + 14f), tag, tagSize, col);
    }

    /// <summary>A tiny garage glyph: a roof and a box with a dark door, centred on <paramref name="c"/>.</summary>
    private void DrawHouse(Vector2 c, Color col)
    {
        var dark = new Color(0f, 0f, 0f, 0.8f * _alpha);
        DrawColoredPolygon(new[] { c + new Vector2(-11f, -1f), c + new Vector2(0f, -10f), c + new Vector2(11f, -1f) }, col);
        DrawRect(new Rect2(c + new Vector2(-8f, -1f), new Vector2(16f, 10f)), col);
        DrawRect(new Rect2(c + new Vector2(-4f, 2f), new Vector2(8f, 7f)), dark);
    }

    private void Outlined(Font font, Vector2 pos, string text, int size, Color color)
    {
        DrawStringOutline(font, pos, text, HorizontalAlignment.Left, -1, size, 4, new Color(0f, 0f, 0f, 0.9f * color.A));
        DrawString(font, pos, text, HorizontalAlignment.Left, -1, size, color);
    }
}
