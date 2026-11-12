using System.Collections.Generic;
using Gearheart.World;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// Little world-space call-outs for mining: "+2 Scrap" rising out of a broken ore tile in the ore's
/// colour, "VEIN x3" over a run, "+1 BONUS" in gold, and a ripple of sparkles running along the rest
/// of the vein (the ore still touching the one you broke), so you can see where it goes on.
/// Lives on the glint layer, so the underground darkness does not hide it.
/// </summary>
public partial class MiningPops : Node2D
{
    /// <summary>Text size multiplier; the scene keeps it at foot zoom / camera zoom so pops read the same in the car.</summary>
    public float TextScale = 1f;

    /// <summary>The camera zoom: text is drawn at screen resolution (crisp) instead of being scaled up with the world.</summary>
    public float Zoom = 1f;

    private const float Ts = TileWorld.TileSize;
    private const int MaxPops = 24, MaxSparkles = 64;

    private struct Pop
    {
        public Vector2 At;
        public string Text;
        public Color Color;
        public float Size, Age, Life, Rise;
    }

    private struct Sparkle
    {
        public Vector2 At;
        public float Age;     // negative while waiting for its turn in the ripple
        public Color Color;
    }

    private readonly List<Pop> _pops = new();
    private readonly List<Sparkle> _sparkles = new();

    /// <summary>Pops shown so far and sparkles started (for the bot log).</summary>
    public int PopsShown { get; private set; }
    public int SparklesShown { get; private set; }

    /// <summary>Pops on screen right now (for the bot log).</summary>
    public int Active => _pops.Count;

    public void Show(Vector2 at, string text, Color color, float size = 11f, float life = 1.1f, float rise = 26f)
    {
        if (_pops.Count >= MaxPops) _pops.RemoveAt(0);
        _pops.Add(new Pop { At = at, Text = text, Color = color, Size = size, Life = life, Rise = rise });
        PopsShown++;
    }

    /// <summary>Sparkles over these tiles, each starting a beat after the one before (nearest first).</summary>
    public void Ripple(IReadOnlyList<(Vector2I Tile, int Step)> tiles, Color color)
    {
        foreach (var (tile, step) in tiles)
        {
            if (_sparkles.Count >= MaxSparkles) break;
            _sparkles.Add(new Sparkle { At = new Vector2((tile.X + 0.5f) * Ts, (tile.Y + 0.5f) * Ts), Age = -0.07f * step, Color = color });
            SparklesShown++;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        for (int i = _pops.Count - 1; i >= 0; i--)
        {
            Pop p = _pops[i];
            p.Age += dt;
            if (p.Age >= p.Life) _pops.RemoveAt(i);
            else _pops[i] = p;
        }
        for (int i = _sparkles.Count - 1; i >= 0; i--)
        {
            Sparkle s = _sparkles[i];
            s.Age += dt;
            if (s.Age >= 0.55f) _sparkles.RemoveAt(i);
            else _sparkles[i] = s;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (Sparkle s in _sparkles)
        {
            if (s.Age < 0f) continue;
            float k = s.Age / 0.55f;
            float r = 3f + 7f * Mathf.Sin(k * Mathf.Pi);          // grows, then shrinks away
            float a = 1f - k * k;
            var c = new Color(s.Color, a);
            var white = new Color(1f, 1f, 1f, a);
            DrawLine(s.At - new Vector2(r, 0f), s.At + new Vector2(r, 0f), c, 2f);
            DrawLine(s.At - new Vector2(0f, r), s.At + new Vector2(0f, r), c, 2f);
            DrawLine(s.At - new Vector2(r, r) * 0.45f, s.At + new Vector2(r, r) * 0.45f, white, 1f);
            DrawLine(s.At - new Vector2(r, -r) * 0.45f, s.At + new Vector2(r, -r) * 0.45f, white, 1f);
            DrawCircle(s.At, 1.6f, white);
        }

        Font font = ThemeDB.FallbackFont;
        foreach (Pop p in _pops)
        {
            float k = p.Age / p.Life;
            // A quick pop up in size, then a slow rise and a fade over the last third.
            float grow = p.Age < 0.12f ? Mathf.Lerp(0.6f, 1.15f, p.Age / 0.12f) : Mathf.Lerp(1.15f, 1f, Mathf.Min((p.Age - 0.12f) / 0.15f, 1f));
            float zoom = Mathf.Max(Zoom, 0.1f);
            int size = Mathf.Max(8, Mathf.RoundToInt(p.Size * grow * TextScale * zoom));   // in screen pixels
            float a = k < 0.66f ? 1f : 1f - (k - 0.66f) / 0.34f;
            float ease = 1f - (1f - k) * (1f - k);
            var at = p.At - new Vector2(0f, p.Rise * ease * TextScale);
            const float width = 600f;
            DrawSetTransform(at, 0f, new Vector2(1f / zoom, 1f / zoom));
            var origin = new Vector2(-width / 2f, size * 0.35f);
            int outline = Mathf.Max(3, size / 4);
            DrawStringOutline(font, origin, p.Text, HorizontalAlignment.Center, width, size, outline, new Color(0f, 0f, 0f, a * 0.85f));
            DrawString(font, origin, p.Text, HorizontalAlignment.Center, width, size, new Color(p.Color, a));
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
