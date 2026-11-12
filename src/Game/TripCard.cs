using System.Collections.Generic;
using Gearheart.Core;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// The trip report that pops up when you get back to the garage: one line per kind of fun
/// (distance, speed, air, heaps, stashes, haul, carnage) ticking in one by one with its points,
/// then the trip score counting up, and your best trip to beat (with a fanfare when you beat it).
/// Drawn in code like the dashboard. Enter (or Esc) closes it.
/// </summary>
public partial class TripCard : Control
{
    public const float W = 620f, H = 490f;
    private const float RowStart = 0.35f, RowGap = 0.16f, CountTime = 1.1f, Life = 10f;
    private static readonly Color Gold = new(1f, 0.82f, 0.32f), Dim = new(0.72f, 0.75f, 0.8f), Ink = new(0.95f, 0.96f, 0.98f);

    public Sfx Sfx;

    private List<TripRow> _rows = new();
    private string _subtitle = "";
    private int _score, _oldBest, _shownScore;
    private bool _newBest, _firstTrip;
    private float _t, _life;
    private int _rowsShown;
    private bool _counted, _closing;
    private StyleBoxFlat _panel;

    /// <summary>True from when the card pops up until it has faded out.</summary>
    public bool Showing => Visible;

    /// <summary>True while it fades out after being closed.</summary>
    public bool Closing => _closing;

    /// <summary>The score as the counter shows it right now (for the bot log).</summary>
    public int ShownScore => _shownScore;

    public TripCard()
    {
        CustomMinimumSize = new Vector2(W, H);
        Size = CustomMinimumSize;
        PivotOffset = Size / 2f;
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
    }

    /// <param name="before">The best trip before this one (null if this is the first trip).</param>
    public void Open(List<TripRow> rows, int score, TripBest before, string subtitle)
    {
        _rows = rows;
        _score = score;
        _firstTrip = before == null;
        _oldBest = before?.Score ?? 0;
        _newBest = !_firstTrip && score > _oldBest;
        _subtitle = subtitle;
        _t = 0f;
        _life = Life;
        _rowsShown = 0;
        _shownScore = 0;
        _counted = false;
        _closing = false;
        Visible = true;
        Modulate = Colors.White;
        Scale = new Vector2(0.85f, 0.85f);
    }

    /// <summary>Close early (Enter or Esc). Rows and score jump to the end first, so a quick close still shows the result.</summary>
    public void Dismiss()
    {
        if (!Visible || _closing) return;
        if (!_counted)
        {
            // First press: skip the tally to the end. A second press closes.
            _t = RowStart + _rows.Count * RowGap + CountTime + 0.01f;
            return;
        }
        _closing = true;
        _life = Mathf.Min(_life, 0.25f);
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        float dt = (float)delta;
        _t += dt;
        _life -= dt;

        float pop = Mathf.Clamp(_t / 0.18f, 0f, 1f);
        float s = Mathf.Lerp(0.85f, 1f, 1f - (1f - pop) * (1f - pop)) + (pop < 1f ? 0f : 0.015f * Mathf.Max(0f, 1f - (_t - 0.18f) * 6f));
        Scale = new Vector2(s, s);
        Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(_life / 0.4f, 0f, 1f) * Mathf.Clamp(_t / 0.12f, 0f, 1f));

        // Rows tick in one by one.
        int due = Mathf.Clamp(Mathf.FloorToInt((_t - RowStart) / RowGap) + 1, 0, _rows.Count);
        while (_rowsShown < due)
        {
            if (_rows[_rowsShown].Points > 0) Sfx?.Play("tally", _rows[_rowsShown].Record ? -6f : -12f, 0.03f, 1f + 0.06f * _rowsShown);
            _rowsShown++;
        }

        // Then the score counts up.
        float countFrom = RowStart + _rows.Count * RowGap;
        float k = Mathf.Clamp((_t - countFrom) / CountTime, 0f, 1f);
        int shown = Mathf.RoundToInt(_score * (1f - Mathf.Pow(1f - k, 2f)));
        if (shown != _shownScore && k < 1f && ((int)(_t * 30f) & 1) == 0) Sfx?.Play("tally", -16f, 0.05f, 1.4f + 0.6f * k);
        _shownScore = shown;
        if (k >= 1f && !_counted)
        {
            _counted = true;
            _shownScore = _score;
            if (_newBest) Sfx?.Play("record", -3f, 0.5f, 1f);
            else Sfx?.Play("pickup", -6f, 0.1f, 0.9f);
        }

        if (_life <= 0f) Visible = false;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Font font = ThemeDB.FallbackFont;
        bool glow = _counted && (_newBest || _firstTrip);
        _panel ??= new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.07f, 0.1f, 0.9f),
            BorderWidthLeft = 3, BorderWidthTop = 3, BorderWidthRight = 3, BorderWidthBottom = 3,
            CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16, CornerRadiusBottomLeft = 16, CornerRadiusBottomRight = 16,
            ShadowColor = new Color(0f, 0f, 0f, 0.4f), ShadowSize = 12,
        };
        float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 6f);
        _panel.BorderColor = glow ? Gold.Lerp(Colors.White, 0.3f * pulse) : new Color(1f, 1f, 1f, 0.18f);
        DrawStyleBox(_panel, new Rect2(Vector2.Zero, Size));

        DrawString(font, new Vector2(0f, 48f), "TRIP REPORT", HorizontalAlignment.Center, W, 32, Ink);
        DrawString(font, new Vector2(0f, 76f), _subtitle, HorizontalAlignment.Center, W, 16, Dim);
        DrawLine(new Vector2(28f, 92f), new Vector2(W - 28f, 92f), new Color(1f, 1f, 1f, 0.15f), 2f);

        // One line per kind of fun: name, what you did, points.
        for (int i = 0; i < _rowsShown; i++)
        {
            TripRow r = _rows[i];
            float y = 124f + i * 30f;
            float age = _t - (RowStart + i * RowGap);
            float slide = Mathf.Max(0f, 1f - age * 8f) * 24f;
            DrawString(font, new Vector2(32f - slide, y), r.Label, HorizontalAlignment.Left, 150f, 19, Dim);
            DrawString(font, new Vector2(184f - slide, y), r.Value, HorizontalAlignment.Left, 300f, 19, Ink);
            if (r.Record)
            {
                var tag = new Rect2(W - 196f, y - 17f, 82f, 22f);
                DrawRect(tag, Gold.Lerp(Colors.White, 0.25f * pulse));
                DrawString(font, new Vector2(tag.Position.X, y), "NEW BEST", HorizontalAlignment.Center, tag.Size.X, 14, new Color(0.15f, 0.1f, 0.02f));
            }
            DrawString(font, new Vector2(W - 140f, y), r.Points > 0 ? $"+{r.Points}" : "-", HorizontalAlignment.Right, 108f, 19, r.Points > 0 ? Gold : Dim);
        }

        float sy = 124f + _rows.Count * 30f + 4f;
        DrawLine(new Vector2(28f, sy - 10f), new Vector2(W - 28f, sy - 10f), new Color(1f, 1f, 1f, 0.15f), 2f);
        if (_t >= RowStart + _rows.Count * RowGap)
        {
            DrawString(font, new Vector2(32f, sy + 30f), "TRIP SCORE", HorizontalAlignment.Left, 220f, 24, Ink);
            float big = _counted && glow ? 44f + 4f * Mathf.Max(0f, 1f - (_t - (RowStart + _rows.Count * RowGap + CountTime)) * 4f) : 44f;
            DrawString(font, new Vector2(W - 340f, sy + 34f), $"{_shownScore}", HorizontalAlignment.Right, 308f, (int)big, glow ? Gold : Ink);
        }
        if (_counted)
        {
            string line = _firstTrip ? "Your first trip! Now beat it."
                : _newBest ? $"NEW BEST TRIP!   old best {_oldBest}"
                : $"Best trip {_oldBest}   ·   {_oldBest - _score} to beat it";
            DrawString(font, new Vector2(0f, sy + 68f), line, HorizontalAlignment.Center, W, _newBest ? 22 : 18, _newBest ? Gold.Lerp(Colors.White, 0.3f * pulse) : Dim);
        }
        DrawString(font, new Vector2(0f, H - 16f), "G: garage (spend your haul)      Enter: close", HorizontalAlignment.Center, W, 14, new Color(Dim, 0.8f));
    }
}
