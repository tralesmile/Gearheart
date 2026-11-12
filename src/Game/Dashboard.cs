using Godot;

namespace Gearheart.Game;

/// <summary>
/// The car's dashboard in the bottom-left corner while driving: a speedometer dial with a needle,
/// a big speed number, a gear indicator over a rev bar (green, amber, red; it flashes at the limiter), a fuel gauge that turns amber and then blinking red, and a damage meter.
/// Drawn in code (no art), so it scales cleanly. Feed it every frame with <see cref="Set"/>.
/// </summary>
public partial class Dashboard : Control
{
    public const float MaxKmh = 120f;
    private const float DialRadius = 46f;
    private const float StartDeg = 150f, SweepDeg = 240f;   // needle sweeps clockwise from lower-left to lower-right

    private float _kmh, _shownKmh, _fuel, _fuelCap = 1f, _damage, _time;
    private bool _boosting, _limiter;
    private int _gear = 1, _lastGear = 1;
    private float _rpm, _shiftFlash;
    private bool _launchArmed;
    private float _launchCharge, _launchFrom = 0.75f, _launchTo = 0.97f;
    private StyleBoxFlat _panel;
    private bool _hasTurbo;
    private float _turbo;

    /// <summary>The needle's current reading (it chases the real speed with a little lag, like a real gauge).</summary>
    public float ShownKmh => _shownKmh;

    public Dashboard()
    {
        CustomMinimumSize = new Vector2(336f, 122f);
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

    /// <summary>Gear (1-based), engine revs (0..1 of the redline) and whether the revs sit on the limiter.</summary>
    public void SetEngine(int gear, float rpm, bool limiter)
    {
        _gear = gear;
        _rpm = rpm;
        _limiter = limiter;
    }

    /// <summary>Turbo boost (0..1): an inner arc on the speedometer, shown only on a car with a turbo.</summary>
    public void SetTurbo(bool hasTurbo, float boost)
    {
        _hasTurbo = hasTurbo;
        _turbo = boost;
    }

    /// <summary>
    /// Launch control: while armed the rev bar shows the launch charge, with the green window
    /// (<paramref name="from"/>..<paramref name="to"/>) bracketed, and the gear box reads "L", then "GO".
    /// </summary>
    public void SetLaunch(bool armed, float charge, float from, float to)
    {
        _launchArmed = armed;
        _launchCharge = charge;
        _launchFrom = from;
        _launchTo = to;
    }

    /// <summary>True while the green launch window is lit (for the playtest log).</summary>
    public bool LaunchGo => _launchArmed && _launchCharge >= _launchFrom && _launchCharge <= _launchTo;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        if (_gear != _lastGear) _shiftFlash = 1f;
        _lastGear = _gear;
        _shiftFlash = Mathf.Max(0f, _shiftFlash - dt * 4f);
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

        // Turbo boost: an inner arc across the top of the dial that fills left to right, pale blue to
        // hot orange, with BOOST under it; it pulses white at full boost.
        if (_hasTurbo)
        {
            const float From = 0.12f, To = 0.88f;
            float r = DialRadius - 17f;
            DrawArc(c, r, Deg(From), Deg(To), 24, new Color(1f, 1f, 1f, 0.12f), 4f, true);
            if (_turbo > 0.01f)
            {
                Color hot = new Color(0.55f, 0.85f, 1f).Lerp(new Color(1f, 0.55f, 0.15f), Mathf.Clamp((_turbo - 0.4f) / 0.6f, 0f, 1f));
                if (_turbo > 0.97f && Mathf.PosMod(_time, 0.24f) < 0.12f) hot = hot.Lightened(0.5f);
                DrawArc(c, r, Deg(From), Deg(From + (To - From) * Mathf.Clamp(_turbo, 0f, 1f)), 24, hot, 4f, true);
            }
            DrawOutlined(ThemeDB.FallbackFont, new Vector2(c.X - 30f, c.Y - 9f), "BOOST", 60f, 10,
                         _turbo > 0.9f ? new Color(1f, 0.7f, 0.3f) : new Color(1f, 1f, 1f, 0.55f), HorizontalAlignment.Center);
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

        // ---- Gear and revs -------------------------------------------------------------------------
        // A gear number in a box, a white flash when it changes, over a rev bar that fills upward:
        // green, then amber, then red; the red tip blinks while the revs bounce off the limiter.
        var gearBox = new Rect2(122f, 10f, 28f, 30f);
        DrawRect(gearBox.Grow(2f), new Color(0f, 0f, 0f, 0.6f));
        bool go = LaunchGo;
        var goGreen = new Color(0.4f, 1f, 0.45f);
        DrawRect(gearBox, go ? new Color(0.2f, 0.75f, 0.3f, 0.75f) : new Color(1f, 1f, 1f, 0.08f + 0.5f * _shiftFlash));
        if (_launchArmed)
            DrawOutlined(font, new Vector2(gearBox.Position.X - 4f, gearBox.End.Y - (go ? 8f : 6f)), go ? "GO" : "L", gearBox.Size.X + 8f, go ? 17 : 24,
                         go ? Colors.White : new Color(1f, 0.8f, 0.3f), HorizontalAlignment.Center);
        else
            DrawOutlined(font, new Vector2(gearBox.Position.X, gearBox.End.Y - 6f), $"{_gear}", gearBox.Size.X, 24,
                         _limiter ? new Color(1f, 0.35f, 0.3f) : Colors.White, HorizontalAlignment.Center);
        const int Segments = 10;
        var revs = new Rect2(126f, 48f, 20f, 64f);
        DrawRect(revs.Grow(2f), new Color(0f, 0f, 0f, 0.6f));
        bool blinkOn = Mathf.PosMod(_time, 0.12f) < 0.06f;
        float segH = revs.Size.Y / Segments;
        for (int i = 0; i < Segments; i++)
        {
            float t = (i + 1f) / Segments;
            Color on = t > 0.85f ? new Color(1f, 0.3f, 0.25f) : t > 0.65f ? new Color(1f, 0.75f, 0.2f) : new Color(0.45f, 0.9f, 0.4f);
            bool lit = _rpm >= t - 0.5f / Segments;
            if (_launchArmed)
            {
                // Launch: the bar is the charge; the window's segments glow green, the rest stays amber/red.
                lit = _launchCharge >= t - 0.5f / Segments;
                float mid = t - 0.5f / Segments;
                bool inWindow = mid >= _launchFrom - 0.001f && mid <= _launchTo;
                on = inWindow ? goGreen : t > _launchTo ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.75f, 0.2f);
            }
            if (_limiter && t > 0.85f && !_launchArmed) lit = blinkOn;
            var seg = new Rect2(revs.Position.X, revs.End.Y - (i + 1) * segH + 1f, revs.Size.X, segH - 2f);
            DrawRect(seg, lit ? on : new Color(1f, 1f, 1f, 0.08f));
        }
        if (_launchArmed)
        {
            // Bracket the green window beside the bar, bright while the charge is in it.
            float yTop = revs.Position.Y, yBottom = revs.End.Y - (_launchFrom - 0.05f) * revs.Size.Y;
            Color bracket = go ? (blinkOn ? Colors.White : goGreen) : new Color(goGreen, 0.7f);
            DrawLine(new Vector2(revs.Position.X - 5f, yTop), new Vector2(revs.Position.X - 5f, yBottom), bracket, 2f);
            DrawLine(new Vector2(revs.End.X + 5f, yTop), new Vector2(revs.End.X + 5f, yBottom), bracket, 2f);
            DrawLine(new Vector2(revs.Position.X - 5f, yTop), new Vector2(revs.Position.X - 1f, yTop), bracket, 2f);
            DrawLine(new Vector2(revs.Position.X - 5f, yBottom), new Vector2(revs.Position.X - 1f, yBottom), bracket, 2f);
            DrawLine(new Vector2(revs.End.X + 1f, yTop), new Vector2(revs.End.X + 5f, yTop), bracket, 2f);
            DrawLine(new Vector2(revs.End.X + 1f, yBottom), new Vector2(revs.End.X + 5f, yBottom), bracket, 2f);
        }

        // ---- Fuel ---------------------------------------------------------------------------------
        float x = 164f, w = Size.X - x - 16f;
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
