using System.Collections.Generic;
using Godot;

namespace Gearheart.World;

/// <summary>
/// Draws the magma sea (<see cref="Lava"/>): molten rock at full brightness over the underground darkness,
/// with a wavy glowing top, slowly drifting dark crust, bubbles that swell and pop, embers rising off the
/// surface and a hot orange glow (added light) that washes over the rock around it. Splashes (something
/// fell in) throw up steam and a fountain of sparks. Lives on the glint layer, above the darkness, so
/// it is also drawn over whatever sinks into it.
/// </summary>
public partial class LavaView : Node2D
{
    public TileWorld World;

    /// <summary>World position the view is centred on (the player or the car).</summary>
    public Vector2 Focus;

    /// <summary>Lava tiles drawn last frame, and how many of them were the lake's top (for the bot log).</summary>
    public int Shown { get; private set; }
    public int SurfaceShown { get; private set; }

    private const float Ts = TileWorld.TileSize;
    private const int RangeX = 46, RangeY = 20;

    private static readonly Color Deep = new(0.55f, 0.07f, 0.02f);
    private static readonly Color Body = new(0.93f, 0.33f, 0.05f);
    private static readonly Color Hot = new(1f, 0.72f, 0.18f);
    private static readonly Color Crust = new(0.28f, 0.07f, 0.04f);
    private static readonly Color Glow = new(1f, 0.42f, 0.1f);

    private float _time;
    private Node2D _halos;
    private Texture2D _soft;

    private struct Bit
    {
        public Vector2 Pos, Vel;
        public float Age, Life, Size;
        public bool Steam;
    }
    private readonly List<Bit> _bits = new();
    private readonly RandomNumberGenerator _rng = new() { Seed = 4242 };

    public override void _Ready()
    {
        _soft = new GradientTexture2D
        {
            Width = 64,
            Height = 64,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
            Gradient = new Gradient
            {
                Offsets = new[] { 0f, 1f },
                Colors = new[] { new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0f) },
            },
        };
        var add = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        _halos = new Node2D { Name = "LavaGlow", Material = add };
        _halos.Draw += DrawGlow;
        AddChild(_halos);   // over the lava itself: the hot light also brightens its own top
    }

    /// <summary>Something fell into the lava at <paramref name="at"/>: steam and a fountain of sparks, 0..1 strength.</summary>
    public void Splash(Vector2 at, float strength)
    {
        int sparks = 10 + (int)(18 * strength), steam = 5 + (int)(6 * strength);
        for (int i = 0; i < sparks; i++)
        {
            float a = -Mathf.Pi / 2f + _rng.RandfRange(-0.9f, 0.9f);
            float v = _rng.RandfRange(120f, 300f) * (0.6f + 0.6f * strength);
            _bits.Add(new Bit { Pos = at + new Vector2(_rng.RandfRange(-10f, 10f), 0f), Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * v, Life = _rng.RandfRange(0.5f, 1.0f), Size = _rng.RandfRange(1.5f, 3f) });
        }
        for (int i = 0; i < steam; i++)
            _bits.Add(new Bit { Pos = at + new Vector2(_rng.RandfRange(-16f, 16f), _rng.RandfRange(-6f, 2f)), Vel = new Vector2(_rng.RandfRange(-20f, 20f), _rng.RandfRange(-70f, -40f)), Life = _rng.RandfRange(1.0f, 1.7f), Size = _rng.RandfRange(7f, 12f), Steam = true });
    }

    /// <summary>A little steam where something hot sits in the lava (the car floating), call now and then.</summary>
    public void Smoke(Vector2 at)
    {
        _bits.Add(new Bit { Pos = at + new Vector2(_rng.RandfRange(-8f, 8f), 0f), Vel = new Vector2(_rng.RandfRange(-12f, 12f), _rng.RandfRange(-60f, -35f)), Life = _rng.RandfRange(0.9f, 1.4f), Size = _rng.RandfRange(6f, 10f), Steam = true });
        if (_rng.Randf() < 0.5f)
            _bits.Add(new Bit { Pos = at, Vel = new Vector2(_rng.RandfRange(-60f, 60f), _rng.RandfRange(-220f, -120f)), Life = 0.6f, Size = 2f });
    }

    public int BitsAlive => _bits.Count;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        for (int i = _bits.Count - 1; i >= 0; i--)
        {
            Bit b = _bits[i];
            b.Age += dt;
            if (b.Age >= b.Life)
            {
                _bits.RemoveAt(i);
                continue;
            }
            if (!b.Steam) b.Vel.Y += 520f * dt;
            else b.Vel *= 1f - 0.8f * dt;
            b.Pos += b.Vel * dt;
            _bits[i] = b;
        }
        QueueRedraw();
        _halos.QueueRedraw();
    }

    /// <summary>How far below the tile's top the lake surface is at world x (it rolls slowly), 0.7..5 px.</summary>
    private float Wave(float wx) => 2.5f + 1.8f * Mathf.Sin(wx * 0.09f + _time * 2.2f) + 0.8f * Mathf.Sin(wx * 0.23f - _time * 3.1f);

    private static uint H(int x, int y) => LuckyOre.Hash(x * 7 + 13, y * 3 + 101);

    public override void _Draw()
    {
        Shown = SurfaceShown = 0;
        if (World == null) return;
        int cx = Mathf.FloorToInt(Focus.X / Ts), cy = Mathf.FloorToInt(Focus.Y / Ts);
        if (cy + RangeY < Lava.Row) { DrawBits(); return; }
        int y0 = Mathf.Max(Lava.Row, cy - RangeY), y1 = Mathf.Min(TileWorld.Height - 3, cy + RangeY);
        for (int y = y0; y <= y1; y++)
        {
            for (int x = cx - RangeX; x <= cx + RangeX; x++)
            {
                if (!Lava.At(World, x, y)) continue;
                Shown++;
                bool top = Lava.IsSurface(World, x, y);
                float px = x * Ts, py = y * Ts;
                // Brighter near the top, darker red deeper down.
                float depthT = Mathf.Clamp((y - Lava.Row) / 8f, 0f, 1f);
                Color body = Body.Lerp(Deep, depthT * 0.8f);
                if (!top) DrawRect(new Rect2(px, py, Ts, Ts), body);
                else
                    for (int sx = 0; sx < 4; sx++)
                    {
                        float wave = Wave(px + sx * 4f);
                        DrawRect(new Rect2(px + sx * 4f, py + wave, 4f, Ts - wave), body);
                    }
                // Drifting crust and hot streaks: 8 px cells of slow noise (cheap; 4 per tile).
                for (int sy = 0; sy < 2; sy++)
                {
                    for (int sx = 0; sx < 2; sx++)
                    {
                        float wx = px + sx * 8f, wy = py + sy * 8f;
                        if (top && sy == 0) continue;
                        float n = Mathf.Sin(wx * 0.045f + _time * 0.35f + Mathf.Sin(wy * 0.08f + _time * 0.2f) * 1.7f)
                                * Mathf.Sin(wy * 0.06f - _time * 0.25f + wx * 0.013f);
                        if (n > 0.45f) DrawRect(new Rect2(wx, wy, 8f, 8f), new Color(Crust, Mathf.Clamp((n - 0.45f) * 3f, 0f, 0.85f)));
                        else if (n < -0.6f) DrawRect(new Rect2(wx, wy, 8f, 8f), new Color(Hot, Mathf.Clamp((-0.6f - n) * 3f, 0f, 0.8f)));
                    }
                }
                if (!top) continue;
                SurfaceShown++;
                // The top: a wavy bright rim (the lake surface sits a few pixels down into the tile).
                for (int sx = 0; sx < 4; sx++)
                {
                    float wx = px + sx * 4f;
                    float wave = Wave(wx);
                    DrawRect(new Rect2(wx, py + wave, 4f, 2f), new Color(1f, 0.93f, 0.55f));
                    DrawRect(new Rect2(wx, py + wave + 2f, 4f, 3f), Hot);
                }
                // A bubble swells and pops on some tiles, each on its own beat.
                uint h = H(x, y);
                if (h % 3 == 0)
                {
                    float period = 1.6f + (h % 7) * 0.35f;
                    float t = (_time + (h % 100) / 37f) % period / period;
                    var c = new Vector2(px + 4f + (h / 7 % 9), py + 5f);
                    if (t < 0.8f) DrawCircle(c, 1f + 3.5f * t, new Color(1f, 0.85f, 0.4f, 0.9f));
                    else
                    {
                        float k = (t - 0.8f) / 0.2f;
                        DrawArc(c, 3f + 6f * k, Mathf.Pi, Mathf.Tau, 8, new Color(1f, 0.9f, 0.5f, 1f - k), 1.5f);
                    }
                }
                // Embers drift up off the surface and fade.
                if (h % 4 == 1)
                {
                    float period = 2.2f + (h % 5) * 0.4f;
                    float t = (_time + (h % 89) / 23f) % period / period;
                    var e = new Vector2(px + 8f + 5f * Mathf.Sin(t * 9f + h % 13), py - t * 70f);
                    DrawRect(new Rect2(e, new Vector2(2f, 2f)), new Color(1f, 0.7f + 0.3f * (1f - t), 0.25f, 1f - t));
                }
            }
        }
        DrawBits();
    }

    private void DrawBits()
    {
        foreach (var b in _bits)
        {
            float k = b.Age / b.Life;
            if (b.Steam) DrawCircle(b.Pos, b.Size * (0.6f + 0.9f * k), new Color(0.85f, 0.82f, 0.8f, 0.45f * (1f - k)));
            else DrawRect(new Rect2(b.Pos - Vector2.One * b.Size * 0.5f, Vector2.One * b.Size), new Color(1f, 0.85f - 0.4f * k, 0.3f, 1f - k * k));
        }
    }

    /// <summary>Added light: a warm glow over the lake top and a dimmer one over the whole sea, so the rock around it glows red.</summary>
    private void DrawGlow()
    {
        if (World == null) return;
        int cx = Mathf.FloorToInt(Focus.X / Ts), cy = Mathf.FloorToInt(Focus.Y / Ts);
        if (cy + RangeY < Lava.Row) return;
        // Only the lake top and the first couple of rows glow (the deep body is darkened anyway and mostly
        // off screen); drawing a halo per tile over the whole sea is what made this heavy.
        int y1 = Mathf.Min(TileWorld.Height - 3, Lava.Row + 2);
        for (int y = Lava.Row; y <= y1; y++)
        {
            for (int x = cx - RangeX; x <= cx + RangeX; x++)
            {
                if (!Lava.At(World, x, y)) continue;
                var center = new Vector2((x + 0.5f) * Ts, (y + 0.5f) * Ts);
                if (Lava.IsSurface(World, x, y))
                {
                    float flick = 0.85f + 0.15f * Mathf.Sin(_time * 3.1f + x * 0.7f);
                    _halos.DrawTextureRect(_soft, new Rect2(center - new Vector2(56f, 64f), new Vector2(112f, 112f)), false, new Color(Glow, 0.16f * flick));
                    _halos.DrawTextureRect(_soft, new Rect2(center - new Vector2(22f, 26f), new Vector2(44f, 40f)), false, new Color(Hot, 0.18f * flick));
                }
                else if (((x + y) & 1) == 0)
                {
                    _halos.DrawTextureRect(_soft, new Rect2(center - new Vector2(30f, 30f), new Vector2(60f, 60f)), false, new Color(Glow, 0.07f));
                }
            }
        }
        foreach (var b in _bits)
            if (!b.Steam) _halos.DrawTextureRect(_soft, new Rect2(b.Pos - new Vector2(6f, 6f), new Vector2(12f, 12f)), false, new Color(Hot, 0.5f * (1f - b.Age / b.Life)));
    }
}
