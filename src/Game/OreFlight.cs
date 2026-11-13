using System;
using System.Collections.Generic;
using Gearheart.Core;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// "Ore flies home": whatever you mine, dig up or smash flies from where it broke, in a little arc,
/// into its slot on the inventory bar. The slot's count only goes up when the icon lands, and the
/// slot pops (its icon swells, a ring flashes, the number flashes in the ore's colour), so the bar
/// at the bottom finally answers what you did. Drawn on the HUD layer, above the bar.
/// </summary>
public partial class OreFlight : Control
{
    /// <summary>Where each slot's icon sits on the screen (HUD coordinates), set by the scene.</summary>
    public Func<int, Vector2> SlotCenter;

    /// <summary>Played on every landing of a valuable icon (ore, not dirt or stone).</summary>
    public Sfx Sfx;

    private const int MaxFliers = 48;
    private const float BumpTime = 0.32f;

    private struct Flier
    {
        public int Slot, Carry;
        public Rect2 Icon;
        public Vector2 From, Lift;
        public float Age, Life, Size, Spin;
        public bool Small;
        public Color Color;
    }

    private readonly List<Flier> _fliers = new();
    private readonly int[] _pending = new int[GameSession.Items.Length];
    private readonly float[] _bump = new float[GameSession.Items.Length];
    private readonly Color[] _bumpColor = new Color[GameSession.Items.Length];

    /// <summary>Totals for the bot log: icons launched, icons landed, units carried home, slowest flight.</summary>
    public int Launched { get; private set; }
    public int Landed { get; private set; }
    public int UnitsHome { get; private set; }
    public float LongestFlight { get; private set; }
    public int InFlight => _fliers.Count;

    /// <summary>Raised when an icon lands: slot, units it carried, how long it flew.</summary>
    public event Action<int, int, float> OnLanded;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    /// <summary>Units of this slot still in the air: the bar shows the count without them until they land.</summary>
    public int Pending(int slot) => slot >= 0 && slot < _pending.Length ? _pending[slot] : 0;

    /// <summary>0..1 while a slot is popping after a landing (1 = just landed).</summary>
    public float Bump(int slot) => slot >= 0 && slot < _bump.Length ? _bump[slot] / BumpTime : 0f;

    public Color BumpColor(int slot) => _bumpColor[slot];

    /// <summary>
    /// Sends <paramref name="amount"/> of an item from a screen point to its slot: one icon per unit
    /// (up to five; the last carries the rest), each a beat after the one before.
    /// </summary>
    public void Launch(Vector2 screenFrom, string item, int amount, Color color)
    {
        int slot = Array.IndexOf(GameSession.Items, item);
        if (slot < 0 || amount <= 0 || SlotCenter == null) return;
        bool small = item is "Dirt" or "Stone";
        int icons = Mathf.Min(amount, small ? 1 : 5);
        if (_fliers.Count + icons > MaxFliers) return;   // a flood (a long drill run): the count just goes up

        Vector2 to = SlotCenter(slot);
        float dist = screenFrom.DistanceTo(to);
        for (int i = 0; i < icons; i++)
        {
            int carry = i == icons - 1 ? amount - (icons - 1) : 1;
            // Each icon first hops up and a little sideways (a fan for a bundle), then curves home.
            float side = icons == 1 ? (float)GD.RandRange(-30.0, 30.0) : Mathf.Lerp(-70f, 70f, i / (float)(icons - 1));
            _fliers.Add(new Flier
            {
                Slot = slot,
                Carry = carry,
                Icon = Art.ItemIcon(item),
                From = screenFrom,
                // The control point sits well above the midpoint, so the icon is tossed up out of the hole
                // (about 60 px for a tile just above the bar) before it drops into its slot.
                Lift = new Vector2(side, -Mathf.Clamp(dist * 0.6f, 300f, 420f)),
                Age = -0.07f * i,
                Life = Mathf.Clamp(0.5f + dist / 1700f, 0.6f, 0.9f) * (small ? 0.8f : 1f),
                Size = small ? 20f : 34f,
                Spin = (float)GD.RandRange(-5.0, 5.0),
                Small = small,
                Color = color,
            });
            _pending[slot] += carry;
            Launched++;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        for (int s = 0; s < _bump.Length; s++) _bump[s] = Mathf.Max(0f, _bump[s] - dt);

        for (int i = _fliers.Count - 1; i >= 0; i--)
        {
            Flier f = _fliers[i];
            f.Age += dt;
            if (f.Age < f.Life)
            {
                _fliers[i] = f;
                continue;
            }
            _fliers.RemoveAt(i);
            _pending[f.Slot] = Mathf.Max(0, _pending[f.Slot] - f.Carry);
            _bump[f.Slot] = BumpTime;
            _bumpColor[f.Slot] = f.Color;
            Landed++;
            UnitsHome += f.Carry;
            LongestFlight = Mathf.Max(LongestFlight, f.Life);
            if (!f.Small) Sfx?.Play("tally", -17f, 0.03f, 1.5f + 0.06f * Mathf.Min(f.Carry, 5));
            OnLanded?.Invoke(f.Slot, f.Carry, f.Life);
        }
        QueueRedraw();
    }

    /// <summary>Point on the arc: a quadratic curve from the tile, up through a lift point, into the slot.</summary>
    private Vector2 Along(Flier f, float t)
    {
        Vector2 to = SlotCenter(f.Slot);
        Vector2 mid = (f.From + to) * 0.5f + f.Lift;
        float u = 1f - t;
        return u * u * f.From + 2f * u * t * mid + t * t * to;
    }

    public override void _Draw()
    {
        if (SlotCenter == null) return;
        Texture2D tex = Art.Tiles;

        // The slots that just caught something swell and ring.
        for (int s = 0; s < _bump.Length; s++)
        {
            if (_bump[s] <= 0f) continue;
            float k = 1f - _bump[s] / BumpTime;     // 0 just landed .. 1 settled
            Vector2 c = SlotCenter(s);
            float swell = 1f + 0.45f * Mathf.Sin((1f - k) * Mathf.Pi * 0.5f);
            float half = 16f * swell;
            DrawCircle(c, 20f + 6f * k, new Color(1f, 1f, 1f, 0.22f * (1f - k)));
            DrawArc(c, 18f + 16f * k, 0f, Mathf.Tau, 24, new Color(_bumpColor[s], 0.9f * (1f - k)), 3f, true);
            DrawTextureRectRegion(tex, new Rect2(c - new Vector2(half, half), new Vector2(half * 2f, half * 2f)), Art.ItemIcon(GameSession.Items[s]));
        }

        foreach (Flier f in _fliers)
        {
            if (f.Age < 0f) continue;
            float t = f.Age / f.Life;
            float ease = t * t * (1.6f - 0.6f * t);   // slow off the tile, fast into the slot
            Vector2 at = Along(f, ease);
            // The icon pops up to full size, then shrinks a little to slot size as it arrives.
            float scale = t < 0.15f ? Mathf.Lerp(0.5f, 1.25f, t / 0.15f) : Mathf.Lerp(1.25f, 0.9f, (t - 0.15f) / 0.85f);
            float half = f.Size * 0.5f * scale;

            // A short trail of fading copies behind it, and a glow in the ore's colour.
            if (!f.Small)
            {
                for (int g = 3; g >= 1; g--)
                {
                    float back = Mathf.Max(0f, ease - 0.05f * g);
                    Vector2 p = Along(f, back);
                    DrawCircle(p, half * (0.8f - 0.15f * g), new Color(f.Color, 0.22f - 0.05f * g));
                }
                // A bright disc and a white rim, so even dark iron reads against dirt and slate.
                DrawCircle(at, half * 1.25f, new Color(f.Color, 0.55f));
                DrawArc(at, half * 1.25f, 0f, Mathf.Tau, 20, new Color(1f, 1f, 1f, 0.85f), 2f, true);
            }
            else
            {
                DrawCircle(at, half * 1.1f, new Color(0f, 0f, 0f, 0.35f));
            }
            DrawSetTransform(at, f.Spin * 0.08f * (1f - t), Vector2.One);
            DrawTextureRectRegion(tex, new Rect2(-half, -half, half * 2f, half * 2f), f.Icon);
            if (f.Carry > 1)
            {
                Font font = ThemeDB.FallbackFont;
                DrawStringOutline(font, new Vector2(half - 2f, half + 4f), $"x{f.Carry}", HorizontalAlignment.Left, -1, 14, 4, Colors.Black);
                DrawString(font, new Vector2(half - 2f, half + 4f), $"x{f.Carry}", HorizontalAlignment.Left, -1, 14, Colors.White);
            }
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }
    }
}
