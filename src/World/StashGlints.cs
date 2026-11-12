using Godot;

namespace Gearheart.World;

/// <summary>
/// Twinkles over buried stashes near the camera, so a player digging underground spots a glint
/// in the rock and has a reason to tunnel towards it. Lives on its own canvas layer so the
/// underground darkness does not dim it. Also shows the gold burst when a stash is dug up.
/// </summary>
public partial class StashGlints : Node2D
{
    public TileWorld World;

    /// <summary>World position the glints are judged from (the player or the car).</summary>
    public Vector2 Focus;

    /// <summary>Full brightness within this distance, fading out by <see cref="FadeTo"/>.</summary>
    public const float FullUntil = 300f, FadeTo = 760f;

    /// <summary>Range multiplier: at night the glints shine from further away (1 by day, up to 1.8 at night).</summary>
    public float Reach = 1f;

    private const float Ts = TileWorld.TileSize;
    private static readonly Color Gold = new(1f, 0.85f, 0.35f);
    private static readonly Color Pale = new(1f, 0.97f, 0.85f);
    private float _time;

    /// <summary>Distance from the focus to the closest stash still in the ground, or infinity.</summary>
    public float Nearest { get; private set; } = float.PositiveInfinity;

    /// <summary>Stashes glinting in the last frame (for the bot log).</summary>
    public int Shown { get; private set; }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Nearest = float.PositiveInfinity;
        Shown = 0;
        if (World == null) return;

        foreach (Vector2I s in World.Stashes)
        {
            Tile tile = World.Get(s.X, s.Y);
            if (!TileWorld.IsStash(tile)) continue;
            var center = new Vector2((s.X + 0.5f) * Ts, (s.Y + 0.5f) * Ts);
            float d = center.DistanceTo(Focus);
            Nearest = Mathf.Min(Nearest, d);
            float full = FullUntil * Reach, fade = FadeTo * Reach;
            if (d > fade) continue;
            Shown++;

            float near = 1f - Mathf.Clamp((d - full) / (fade - full), 0f, 1f);
            uint h = (uint)(s.X * 92821 ^ s.Y * 68917);
            float phase = _time * 2.3f + (h % 628) / 100f;
            float flash = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase)), 8f);          // a short flash every ~2.7 s
            float flash2 = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase * 1.7f + 2f)), 10f);
            Color color = tile == Tile.StashBig ? Gold : Pale;

            // A faint steady glow, so the spot can be found between flashes.
            DrawCircle(center, 5f, new Color(color, 0.18f * near));
            DrawCircle(center, 2f, new Color(color, 0.45f * near));
            Star(center + new Vector2((h % 7) - 3f, (h / 7 % 7) - 3f), 3f + 9f * flash, color, near * (0.3f + 0.7f * flash));
            Star(center + new Vector2(5f - (h % 5), -4f), 2f + 5f * flash2, color, near * 0.8f * flash2);
        }
    }

    private void Star(Vector2 p, float size, Color color, float alpha)
    {
        if (alpha < 0.02f) return;
        var c = new Color(color, alpha);
        DrawLine(p - new Vector2(size, 0f), p + new Vector2(size, 0f), c, 1.5f);
        DrawLine(p - new Vector2(0f, size), p + new Vector2(0f, size), c, 1.5f);
        DrawLine(p - new Vector2(size, size) * 0.35f, p + new Vector2(size, size) * 0.35f, new Color(color, alpha * 0.6f), 1f);
        DrawLine(p - new Vector2(size, -size) * 0.35f, p + new Vector2(size, -size) * 0.35f, new Color(color, alpha * 0.6f), 1f);
    }

    /// <summary>A fountain of gold coins and sparkles where a stash was dug up.</summary>
    public void Burst(Vector2 at, bool big)
    {
        for (int layer = 0; layer < 2; layer++)
        {
            var particles = new CpuParticles2D
            {
                Position = at,
                OneShot = true,
                Emitting = true,
                Amount = layer == 0 ? (big ? 46 : 30) : 18,
                Lifetime = layer == 0 ? 1.3f : 0.9f,
                Explosiveness = 0.9f,
                Direction = Vector2.Up,
                Spread = layer == 0 ? 50f : 180f,
                InitialVelocityMin = layer == 0 ? 160f : 40f,
                InitialVelocityMax = layer == 0 ? 330f : 140f,
                Gravity = new Vector2(0f, layer == 0 ? 620f : 60f),
                ScaleAmountMin = layer == 0 ? 2.5f : 1.5f,
                ScaleAmountMax = layer == 0 ? 4.5f : 3f,
                Color = layer == 0 ? Gold : Pale,
                ColorRamp = new Gradient
                {
                    Offsets = new[] { 0f, 0.7f, 1f },
                    Colors = new[] { Colors.White, Colors.White, new Color(1f, 1f, 1f, 0f) },
                },
            };
            AddChild(particles);
            GetTree().CreateTimer(1.6).Timeout += () =>
            {
                if (IsInstanceValid(particles)) particles.QueueFree();
            };
        }
    }
}
