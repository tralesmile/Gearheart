using Gearheart.Core;
using Godot;

namespace Gearheart.World;

/// <summary>
/// Makes the glow crystals of the Deep shine: the crystals themselves are redrawn at full brightness
/// over the underground darkness, with a soft cyan halo that slowly breathes and the odd sparkle.
/// Crystals on a cavern wall glow strongly; ones still buried in the rock show only a faint gleam,
/// so a driller can spot a seam through the slate. Lives on the glint layer, above the darkness.
/// </summary>
public partial class CrystalGlow : Node2D
{
    public TileWorld World;

    /// <summary>World position the view is centred on (the player or the car).</summary>
    public Vector2 Focus;

    /// <summary>0 on the surface, 1 underground: the halos only show in the dark.</summary>
    public float Darkness;

    /// <summary>Crystals drawn in the last frame (open to a cave, and buried) for the bot log.</summary>
    public int ShownOpen { get; private set; }
    public int ShownBuried { get; private set; }

    private const float Ts = TileWorld.TileSize;
    private const int RangeX = 52, RangeY = 32;   // tiles around the focus (covers the drive zoom)
    private static readonly Color Cyan = new(0.45f, 1f, 0.95f);
    private float _time;
    private CanvasItemMaterial _add;
    private Node2D _halos;

    public override void _Ready()
    {
        // Halos are added light: drawn on a child with an additive material, under the crystals.
        _add = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        _halos = new Node2D { Name = "Halos", Material = _add, ShowBehindParent = true };
        _halos.Draw += () => DrawHalos(_halos);
        AddChild(_halos);
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
        _halos.QueueRedraw();
    }

    public override void _Draw()
    {
        ShownOpen = ShownBuried = 0;
        if (World == null || Darkness < 0.05f) return;
        Texture2D tex = Art.Tiles;
        int cx = Mathf.FloorToInt(Focus.X / Ts), cy = Mathf.FloorToInt(Focus.Y / Ts);
        for (int y = cy - RangeY; y <= cy + RangeY; y++)
        {
            for (int x = cx - RangeX; x <= cx + RangeX; x++)
            {
                if (World.Get(x, y) != Tile.Crystal) continue;
                bool open = Open(x, y);
                if (open) ShownOpen++;
                else ShownBuried++;
                int variant = TileChunk.Hash(x, y) & 3;
                // Open crystals shine at full brightness; buried ones only gleam through the rock.
                float a = (open ? 1f : 0.35f) * Darkness;
                DrawTextureRectRegion(tex, new Rect2(x * Ts, y * Ts, Ts, Ts), Art.TileCell(4 + variant, 9), new Color(1f, 1f, 1f, a));
            }
        }
    }

    private bool Open(int x, int y) =>
        !World.Solid(x, y - 1) || !World.Solid(x, y + 1) || !World.Solid(x - 1, y) || !World.Solid(x + 1, y);

    private void DrawHalos(CanvasItem canvas)
    {
        if (World == null || Darkness < 0.05f) return;
        int cx = Mathf.FloorToInt(Focus.X / Ts), cy = Mathf.FloorToInt(Focus.Y / Ts);
        for (int y = cy - RangeY; y <= cy + RangeY; y++)
        {
            for (int x = cx - RangeX; x <= cx + RangeX; x++)
            {
                if (World.Get(x, y) != Tile.Crystal) continue;
                bool open = Open(x, y);
                uint h = (uint)(x * 92821 ^ y * 68917);
                float breathe = 0.8f + 0.2f * Mathf.Sin(_time * 1.3f + (h % 628) / 100f);
                float a = Darkness * breathe * (open ? 1f : 0.3f);
                var center = new Vector2((x + 0.5f) * Ts, (y + 0.5f) * Ts);
                canvas.DrawCircle(center, 44f, new Color(Cyan, 0.035f * a));
                canvas.DrawCircle(center, 26f, new Color(Cyan, 0.06f * a));
                canvas.DrawCircle(center, 13f, new Color(Cyan, 0.09f * a));
                if (!open) continue;
                // Now and then a crystal flashes.
                float flash = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_time * 1.7f + (h % 997) / 37f)), 24f);
                if (flash < 0.05f) continue;
                Vector2 p = center + new Vector2((h % 9) - 4f, (h / 9 % 9) - 6f);
                float s = 2f + 6f * flash;
                var c = new Color(0.85f, 1f, 1f, flash * Darkness);
                canvas.DrawLine(p - new Vector2(s, 0f), p + new Vector2(s, 0f), c, 1.2f);
                canvas.DrawLine(p - new Vector2(0f, s), p + new Vector2(0f, s), c, 1.2f);
            }
        }
    }
}
