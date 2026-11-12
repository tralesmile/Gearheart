using Godot;

namespace Gearheart.Game;

/// <summary>
/// Wind streaks across the screen at speed: nothing below about 50 km/h, then more, longer and faster
/// streaks the faster you go, warm-tinted while the booster fires. They are kept away from the middle
/// of the screen (where the car is) and drawn under the HUD text. Feed it every frame with <see cref="Set"/>.
/// </summary>
public partial class SpeedLines : Control
{
    public const float StartKmh = 50f, FullKmh = 95f;
    private const int Count = 46;

    private readonly Vector2[] _pos = new Vector2[Count];
    private readonly float[] _len = new float[Count], _speed = new float[Count];
    private readonly RandomNumberGenerator _rng = new() { Seed = 77 };
    private float _kmh, _dir = 1f, _intensity;
    private bool _boosting;

    /// <summary>0 (no streaks) .. 1 (full); smoothed. Logged by the bot.</summary>
    public float Intensity => _intensity;

    public SpeedLines()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    private bool _seeded;

    /// <param name="dir">+1 when the car moves right, -1 when it moves left.</param>
    public void Set(float kmh, float dir, bool boosting)
    {
        _kmh = kmh;
        if (dir != 0f) _dir = Mathf.Sign(dir);
        _boosting = boosting;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        float target = Mathf.Clamp((_kmh - StartKmh) / (FullKmh - StartKmh), 0f, 1f);
        if (_boosting) target = Mathf.Min(1f, target + 0.25f);
        _intensity = Mathf.Lerp(_intensity, target, 1f - Mathf.Exp(-4f * dt));

        float w = Size.X;
        if (!_seeded && w > 0f)
        {
            _seeded = true;   // spread the first streaks once the layout has a size
            for (int i = 0; i < Count; i++) Respawn(i, true);
        }
        for (int i = 0; i < Count; i++)
        {
            // Streaks fly the opposite way to the car, faster the faster it goes.
            _pos[i].X -= _dir * _speed[i] * (0.6f + _intensity * 1.6f) * dt;
            if (_pos[i].X < -_len[i] - 40f || _pos[i].X > w + _len[i] + 40f) Respawn(i, false);
        }
        if (_intensity > 0.01f) QueueRedraw();
        else if (_intensity > 0f) { _intensity = 0f; QueueRedraw(); }
    }

    private void Respawn(int i, bool anywhere)
    {
        float w = Mathf.Max(Size.X, 640f), h = Mathf.Max(Size.Y, 360f);
        // Mostly in the top and bottom bands, rarely across the middle where the car is.
        float y = _rng.Randf();
        y = y < 0.5f ? Mathf.Lerp(0.04f, 0.36f, y * 2f) : Mathf.Lerp(0.6f, 0.92f, (y - 0.5f) * 2f);
        if (_rng.Randf() < 0.12f) y = _rng.RandfRange(0.36f, 0.6f);
        float x = anywhere ? _rng.RandfRange(0f, w) : _dir > 0f ? w + _rng.RandfRange(0f, 200f) : -_rng.RandfRange(0f, 200f);
        _pos[i] = new Vector2(x, y * h);
        _len[i] = _rng.RandfRange(40f, 150f);
        _speed[i] = _rng.RandfRange(1300f, 2400f);
    }

    public override void _Draw()
    {
        if (_intensity <= 0.01f) return;
        float h = Mathf.Max(Size.Y, 1f);
        Color tint = _boosting ? new Color(1f, 0.82f, 0.55f) : new Color(1f, 1f, 1f);
        int shown = Mathf.CeilToInt(Count * Mathf.Clamp(0.25f + _intensity, 0f, 1f));
        for (int i = 0; i < shown; i++)
        {
            float fromMiddle = Mathf.Abs(_pos[i].Y / h - 0.48f) * 2f;          // 0 at the car's height, 1 at the edges
            float alpha = _intensity * (0.1f + 0.32f * fromMiddle);
            float len = _len[i] * (0.4f + 0.9f * _intensity);
            var tail = _pos[i] + new Vector2(_dir * len, 0f);
            // A bright head fading into the tail.
            DrawLine(_pos[i], _pos[i] + new Vector2(_dir * len * 0.35f, 0f), new Color(tint, alpha), 2f, true);
            DrawLine(_pos[i] + new Vector2(_dir * len * 0.35f, 0f), tail, new Color(tint, alpha * 0.45f), 1.5f, true);
        }
    }
}
