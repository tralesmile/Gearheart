using System.Collections.Generic;
using Gearheart.Core;
using Godot;

namespace Gearheart.World;

/// <summary>
/// The long-jump record flag past each kicker: a chequered flag planted where your longest landed
/// jump off that kicker came down, with the distance over it. Seen on the approach, it turns every
/// kicker into a little challenge: clear the flag. A fresh record drops the flag in with a gold
/// label for a few seconds.
/// </summary>
public partial class JumpMarks : Node2D
{
    public const float PixelsPerMeter = 64f;
    private const float Ts = TileWorld.TileSize;
    private const float DrawDistance = 1500f;
    private const float FreshSeconds = 4f;

    public TileWorld World;

    /// <summary>World position the marks are judged from (the player or the car).</summary>
    public Vector2 Focus;

    private readonly Dictionary<int, float> _freshAt = new();   // kicker start -> time the record was set
    private float _time;

    /// <summary>World x of the kicker's take-off edge (the far side of the crest column).</summary>
    public static float EdgeX(Kicker k) => (k.Crest + (k.Dir > 0 ? 1 : 0)) * Ts;

    /// <summary>World x of a spot this many metres past the crest.</summary>
    public static float SpotX(Kicker k, float meters) => EdgeX(k) + k.Dir * meters * PixelsPerMeter;

    /// <summary>Plays the "new record" drop-in for this kicker's flag.</summary>
    public void Fresh(Kicker k) => _freshAt[k.Start] = _time;

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (World == null) return;
        Font font = ThemeDB.FallbackFont;
        Texture2D sheet = Art.Decor;
        foreach (Kicker k in World.Kickers)
        {
            float best = World.JumpBest(k);
            if (best <= 0f) continue;
            float x = SpotX(k, best);
            if (Mathf.Abs(x - Focus.X) > DrawDistance) continue;
            int col = Mathf.FloorToInt(x / Ts);
            float ground = World.GroundY(x, (World.SurfaceRow(col) - 14) * Ts);

            // A fresh record: the flag drops in from above with a little bounce, and the label glows gold.
            float age = _freshAt.TryGetValue(k.Start, out float at) ? _time - at : 99f;
            float drop = age < 0.35f ? (1f - age / 0.35f) * (1f - age / 0.35f) * -90f : age < 0.55f ? Mathf.Sin((age - 0.35f) / 0.2f * Mathf.Pi) * -5f : 0f;
            bool fresh = age < FreshSeconds;

            int cell = k.Dir > 0 ? Art.DecorRecordFlagRight : Art.DecorRecordFlagLeft;
            var dest = new Rect2(x - Art.DecorWidth / 2f, ground - Art.DecorHeight + 2f + drop, Art.DecorWidth, Art.DecorHeight);
            DrawTextureRectRegion(sheet, dest, new Rect2(cell * Art.DecorWidth, 0, Art.DecorWidth, Art.DecorHeight));

            // The distance over the flag, readable at driving zoom.
            string text = fresh ? $"RECORD {best:0.0} m" : $"{best:0.0} m";
            Color ink = fresh ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 1f, 1f, 0.95f);
            int size = fresh ? 15 : 13;
            var pos = new Vector2(x - 80f, ground - Art.DecorHeight + 8f + drop);
            DrawStringOutline(font, pos, text, HorizontalAlignment.Center, 160f, size, 4, new Color(0f, 0f, 0f, 0.75f));
            DrawString(font, pos, text, HorizontalAlignment.Center, 160f, size, ink);

            if (fresh && age < 1.2f)
            {
                // A short ring of sparkle where it landed.
                float r = 8f + age * 40f, a = 1f - age / 1.2f;
                DrawArc(new Vector2(x, ground - 2f), r, Mathf.Pi, Mathf.Tau, 18, new Color(1f, 0.85f, 0.3f, 0.7f * a), 2f);
            }
        }
    }
}
