using System;
using Gearheart.Core;
using Gearheart.Parts;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// A small card just above the inventory bar: the part you are closest to building, its icon,
/// and each material it still takes ("Iron 2/4"), with a progress bar that slides up as ore lands.
/// When everything is there it turns gold and pulses: "READY - build it at the garage".
/// Drawn on the HUD layer; it never takes mouse input.
/// </summary>
public partial class NextPartChip : Control
{
    private const float Height = 46f;
    private const float AboveBottom = 66f;   // the inventory bar is about 56 px tall with its margin

    private PartGoal _goal;
    private Func<string, int> _have;
    private bool _nearGarage;
    private float _shownProgress;
    private float _time;
    private float _flash;       // 1 → 0 after the part became ready
    private float _tickGlow;    // 1 → 0 after the progress went up

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public void Set(PartGoal goal, Func<string, int> have, bool nearGarage)
    {
        if (goal?.Part.Id != _goal?.Part.Id) _shownProgress = goal?.Progress ?? 0f;
        else if (goal != null && goal.Progress > _shownProgress + 0.001f) _tickGlow = 1f;
        _goal = goal;
        _have = have;
        _nearGarage = nearGarage;
    }

    /// <summary>The part just became buildable: a gold flash.</summary>
    public void Flash() => _flash = 1f;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _flash = Mathf.Max(0f, _flash - dt / 1.2f);
        _tickGlow = Mathf.Max(0f, _tickGlow - dt / 0.5f);
        float target = _goal?.Progress ?? 0f;
        _shownProgress = Mathf.MoveToward(_shownProgress, target, dt * 1.6f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_goal == null || _have == null) return;
        Font font = ThemeDB.FallbackFont;
        var part = _goal.Part;
        bool ready = _goal.Ready;

        string title = ready ? "READY" : "NEXT";
        string name = part.Name;
        string action = ready ? (_nearGarage ? "G: build it" : "build it at the garage") : "";

        // Measure: icon, title, name, then the materials (or the action when ready).
        const int titleSize = 14, nameSize = 18, itemSize = 17;
        float w = 10f + 34f + 8f;
        w += font.GetStringSize(title, HorizontalAlignment.Left, -1, titleSize).X + 8f;
        w += font.GetStringSize(name, HorizontalAlignment.Left, -1, nameSize).X + 14f;
        if (ready) w += font.GetStringSize(action, HorizontalAlignment.Left, -1, itemSize).X + 10f;
        else
            foreach (var (item, need) in _goal.Need)
                w += 22f + 4f + font.GetStringSize(Fraction(item, need), HorizontalAlignment.Left, -1, itemSize).X + 12f;

        // The visible screen, not this control's size: a control straight under a CanvasLayer can be
        // left at size zero by its anchors, which put the card off the top of the screen.
        Vector2 screen = GetViewportRect().Size;
        var box = new Rect2(new Vector2((screen.X - w) / 2f, screen.Y - AboveBottom - Height), new Vector2(w, Height));
        float pulse = ready ? 0.5f + 0.5f * Mathf.Sin(_time * 5f) : 0f;
        var gold = new Color(1f, 0.82f, 0.3f);

        // Panel, progress fill along the bottom, border (gold and pulsing when ready).
        var bg = ready ? new Color(0.22f, 0.16f, 0.04f, 0.82f) : new Color(0.06f, 0.07f, 0.1f, 0.72f);
        DrawRect(box, bg);
        var fill = new Rect2(box.Position + new Vector2(0f, Height - 5f), new Vector2(w * Mathf.Clamp(_shownProgress, 0f, 1f), 5f));
        DrawRect(fill, ready ? gold : new Color(0.45f, 0.85f, 0.5f).Lerp(Colors.White, _tickGlow * 0.6f));
        var border = ready ? gold.Lerp(Colors.White, pulse * 0.5f + _flash * 0.5f) : new Color(1f, 1f, 1f, 0.25f + 0.5f * _tickGlow);
        DrawRect(box, border, false, ready ? 2f + pulse + 2f * _flash : 1.5f);
        if (_flash > 0f)
            DrawRect(box.Grow(10f * (1f - _flash)), new Color(gold, _flash * 0.8f), false, 3f);

        // Part icon.
        float x = box.Position.X + 10f;
        float cy = box.Position.Y + (Height - 5f) / 2f;
        var iconRect = new Rect2(new Vector2(x, cy - 16f), new Vector2(32f, 32f));
        if (part.Category == PartCategory.Wheel)
            DrawTextureRectRegion(Art.Wheels, iconRect, new Rect2(part.Sprite * Art.WheelCell, 0, Art.WheelCell, Art.WheelCell));
        else
            DrawTextureRectRegion(Art.Parts, iconRect, Art.PartCell(part.Sprite));
        x += 34f + 8f;

        // Title and name, baseline-aligned.
        float baseY = cy + 6f;
        Outlined(font, new Vector2(x, baseY), title, titleSize, ready ? gold.Lerp(Colors.White, pulse * 0.4f) : new Color(0.75f, 0.8f, 0.88f));
        x += font.GetStringSize(title, HorizontalAlignment.Left, -1, titleSize).X + 8f;
        Outlined(font, new Vector2(x, baseY), name, nameSize, ready ? new Color(1f, 0.95f, 0.75f) : Colors.White);
        x += font.GetStringSize(name, HorizontalAlignment.Left, -1, nameSize).X + 14f;

        if (ready)
        {
            Outlined(font, new Vector2(x, baseY), action, itemSize, gold.Lerp(Colors.White, pulse * 0.3f));
            return;
        }

        foreach (var (item, need) in _goal.Need)
        {
            DrawTextureRectRegion(Art.Tiles, new Rect2(new Vector2(x, cy - 11f), new Vector2(22f, 22f)), Art.ItemIcon(item));
            x += 22f + 4f;
            string text = Fraction(item, need);
            bool met = _have(item) >= need;
            Outlined(font, new Vector2(x, baseY), text, itemSize, met ? new Color(0.55f, 1f, 0.55f) : Colors.White);
            x += font.GetStringSize(text, HorizontalAlignment.Left, -1, itemSize).X + 12f;
        }
    }

    private string Fraction(string item, int need) => $"{Mathf.Min(_have(item), need)}/{need}";

    private void Outlined(Font font, Vector2 pos, string text, int size, Color color)
    {
        DrawStringOutline(font, pos, text, HorizontalAlignment.Left, -1, size, 4, new Color(0f, 0f, 0f, 0.9f));
        DrawString(font, pos, text, HorizontalAlignment.Left, -1, size, color);
    }
}
