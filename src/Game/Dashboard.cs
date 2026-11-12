using Godot;

namespace Gearheart.Game;

/// <summary>
/// The car's dashboard in the bottom-left corner while driving: a speedometer dial with a needle,
/// a big speed number, a fuel gauge that turns amber and then blinking red, and a damage meter.
/// Drawn in code (no art), so it scales cleanly. Feed it every frame with <see cref="Set"/>.
/// </summary>
public partial class Dashboard : Control
{
    public const float MaxKmh = 120f;
    private const float DialRadius = 46f;
    private const float StartDeg = 150f, SweepDeg = 240f;   // needle sweeps clockwise from lower-left to lower-right

    private float _kmh, _shownKmh, _fuel, _fuelCap = 1f, _damage, _time;
    private bool _boosting;
    private StyleBoxFlat _panel;

    /// <summary>The needle's current reading (it chases the real speed with a little lag, like a real gauge).</summary>
    public float ShownKmh => _shownKmh;

    public Dashboard()
    {
        CustomMinimumSize = new Vector2(300f, 122f);
        Size = CustomMinimumSize;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Set(float kmh, float fuel, float fuelCapacity, float damage, bool boosting)
    {
        _kmh = kmh;
        _fuel = fuel;
        _fuelCap = Mathf.Max(1f, fuelCapacity);
        _damage = damage;
        _boosting = boosting;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _shownKmh = Mathf.Lerp(_shownKmh, _kmh, 1f - Mathf.Exp(-10f * dt));
        QueueRedraw();
    }

    public override void _Draw()
    {
        Font font = ThemeDB.FallbackFont;
        var panel = new Rect2(Vector2.Zero, Size);
        DrawStyleBox(_panel ??= new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.07f, 0.1f, 0.62f),
            BorderColor = new Color(1f, 1f, 1f, 0.12f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
        }, panel);

        // ---- Speedometer ------------------------------------------------------------------------
        var c = new Vector2(62f, 62f);
        DrawCircle(c, DialRadius + 4f, new Color(0f, 0f, 0f, 0.35f));
        float redFrom = 100f / MaxKmh;
        DrawArc(c, DialRadius, Deg(0f), Deg(redFrom), 40, new Color(1f, 1f, 1f, 0.22f), 5f, true);
        DrawArc(c, DialRadius, Deg(redFrom), Deg(1f), 12, new Color(1f, 0.3f, 0.25f, 0.8f), 5f, true);
        for (int k = 0; k <= 12; k++)
        {
            float t = k / 12f;
            Vector2 dir = Vector2.FromAngle(Deg(t));
            bool major = k % 2 == 0;
            DrawLine(c + dir * (DialRadius - (major ? 11f : 7f)), c + dir * (DialRadius - 2f),
                     t >= redFrom ? new Color(1f, 0.45f, 0.4f) : Colors.White, major ? 2f : 1f, true);
        }

        // The needle: orange when the booster fires, wobbling a hair at speed.
        float reading = Mathf.Clamp(_shownKmh / MaxKmh, 0f, 1.04f);
        if (_shownKmh > 60f) reading += Mathf.Sin(_time * 47f) * 0.004f * (_shownKmh - 60f) / 60f;
        Vector2 needle = Vector2.FromAngle(Deg(reading));
        Color needleColor = _boosting ? new Color(1f, 0.62f, 0.15f) : new Color(1f, 0.25f, 0.2f);
        DrawLine(c - needle * 8f, c + needle * (DialRadius - 6f), new Color(0f, 0f, 0f, 0.5f), 5f, true);
        DrawLine(c - needle * 8f, c + needle * (DialRadius - 6f), needleColor, 3f, true);
        DrawCircle(c, 5f, new Color(0.85f, 0.87f, 0.9f));

        string speed = $"{Mathf.RoundToInt(_shownKmh)}";
        DrawOutlined(font, new Vector2(c.X - 40f, c.Y + 34f), speed, 80f, 26, Colors.White, HorizontalAlignment.Center);
        DrawOutlined(font, new Vector2(c.X - 40f, c.Y + 50f), "km/h", 80f, 13, new Color(1f, 1f, 1f, 0.7f), HorizontalAlignment.Center);

        // ---- Fuel ---------------------------------------------------------------------------------
        float x = 132f, w = Size.X - x - 16f;
        float fuel = Mathf.Clamp(_fuel / _fuelCap, 0f, 1f);
        bool low = fuel < 0.2f;
        bool blinkOff = low && Mathf.PosMod(_time, 0.8f) >= 0.4f;
        Color fuelColor = low ? new Color(1f, 0.3f, 0.25f) : fuel < 0.5f ? new Color(1f, 0.72f, 0.2f) : new Color(0.45f, 0.9f, 0.4f);
        DrawCan(new Vector2(x, 20f), blinkOff ? new Color(1f, 1f, 1f, 0.3f) : fuelColor);
        DrawOutlined(font, new Vector2(x + 18f, 33f), low ? "LOW FUEL" : "FUEL", 90f, 15, blinkOff ? new Color(1f, 1f, 1f, 0.35f) : low ? fuelColor : Colors.White);
        DrawOutlined(font, new Vector2(x, 33f), $"{_fuel:0}/{_fuelCap:0}", w, 15, Colors.White, HorizontalAlignment.Right);
        Bar(new Rect2(x, 40f, w, 14f), fuel, blinkOff ? fuelColor.Darkened(0.5f) : fuelColor, 4);

        // ---- Damage -------------------------------------------------------------------------------
        // Damage has no hard ceiling; 40 is a badly bent car (a 15 m drop costs about 16).
        float dmg = Mathf.Clamp(_damage / 40f, 0f, 1f);
        Color dmgColor = dmg < 0.25f ? new Color(0.55f, 0.75f, 1f) : dmg < 0.6f ? new Color(1f, 0.72f, 0.2f) : new Color(1f, 0.3f, 0.25f);
        DrawOutlined(font, new Vector2(x, 80f), "DAMAGE", 90f, 15, Colors.White);
        DrawOutlined(font, new Vector2(x, 80f), $"{_damage:0}", w, 15, Colors.White, HorizontalAlignment.Right);
        Bar(new Rect2(x, 87f, w, 10f), dmg, dmgColor, 0);
    }

    /// <summary>Dial angle (radians) for a fraction 0..1 of the scale.</summary>
    private static float Deg(float t) => Mathf.DegToRad(StartDeg + SweepDeg * t);

    private void Bar(Rect2 r, float fill, Color color, int segments)
    {
        DrawRect(r.Grow(2f), new Color(0f, 0f, 0f, 0.6f));
        DrawRect(r, new Color(1f, 1f, 1f, 0.1f));
        if (fill > 0f)
        {
            var f = new Rect2(r.Position, new Vector2(r.Size.X * fill, r.Size.Y));
            DrawRect(f, color);
            DrawRect(new Rect2(f.Position, new Vector2(f.Size.X, Mathf.Max(2f, r.Size.Y * 0.3f))), color.Lightened(0.35f));
        }
        for (int i = 1; i < segments; i++)
        {
            float sx = r.Position.X + r.Size.X * i / segments;
            DrawLine(new Vector2(sx, r.Position.Y), new Vector2(sx, r.End.Y), new Color(0f, 0f, 0f, 0.45f), 2f);
        }
    }

    /// <summary>A tiny jerry-can glyph.</summary>
    private void DrawCan(Vector2 at, Color color)
    {
        DrawRect(new Rect2(at + new Vector2(0f, 3f), new Vector2(11f, 13f)), color);
        DrawRect(new Rect2(at + new Vector2(2f, 0f), new Vector2(6f, 3f)), color);
        DrawRect(new Rect2(at + new Vector2(8f, -1f), new Vector2(3f, 3f)), color.Lightened(0.4f));
        DrawLine(at + new Vector2(2f, 6f), at + new Vector2(9f, 14f), new Color(0f, 0f, 0f, 0.35f), 1.5f);
        DrawLine(at + new Vector2(9f, 6f), at + new Vector2(2f, 14f), new Color(0f, 0f, 0f, 0.35f), 1.5f);
    }

    private void DrawOutlined(Font font, Vector2 pos, string text, float width, int size, Color color,
                              HorizontalAlignment align = HorizontalAlignment.Left)
    {
        DrawStringOutline(font, pos, text, align, width, size, 4, new Color(0f, 0f, 0f, 0.9f));
        DrawString(font, pos, text, align, width, size, color);
    }
}
