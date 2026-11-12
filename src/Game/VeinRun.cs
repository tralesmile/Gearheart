using Godot;

namespace Gearheart.Game;

/// <summary>
/// The pickaxe's combo. Ore mined within <see cref="Window"/> seconds of the last ore, and no more
/// than <see cref="Reach"/> tiles from it, continues the run ("VEIN x3"); anything else starts a new
/// one. Every <see cref="BonusEvery"/>th ore of a run pays one extra. Pure bookkeeping, no nodes.
/// </summary>
public sealed class VeinRun
{
    public const float Window = 3f;
    public const int Reach = 3;
    public const int BonusEvery = 4;

    private float _left;
    private Vector2I _last;

    /// <summary>Ore in the current run (0 when none is running).</summary>
    public int Chain { get; private set; }

    /// <summary>Longest run so far (this game session).</summary>
    public int Best { get; private set; }

    /// <summary>Bonus ore paid out, and runs of 3 or more that have ended.</summary>
    public int Bonuses { get; private set; }
    public int Runs { get; private set; }

    /// <summary>Seconds left before the run goes cold (0 when none is running).</summary>
    public float Left => Mathf.Max(_left, 0f);

    /// <summary>Counts an ore tile mined at <paramref name="at"/>. Returns the run length and whether it pays a bonus.</summary>
    public (int Chain, bool Bonus) Add(Vector2I at)
    {
        bool continues = Chain > 0 && _left > 0f && Mathf.Max(Mathf.Abs(at.X - _last.X), Mathf.Abs(at.Y - _last.Y)) <= Reach;
        if (!continues && Chain >= 3) Runs++;
        Chain = continues ? Chain + 1 : 1;
        _last = at;
        _left = Window;
        if (Chain > Best) Best = Chain;
        bool bonus = Chain % BonusEvery == 0;
        if (bonus) Bonuses++;
        return (Chain, bonus);
    }

    /// <summary>Runs the clock down. Returns the length of a run that just went cold, else 0.</summary>
    public int Tick(float dt)
    {
        if (Chain == 0) return 0;
        _left -= dt;
        if (_left > 0f) return 0;
        int ended = Chain;
        if (ended >= 3) Runs++;
        Chain = 0;
        return ended;
    }
}
