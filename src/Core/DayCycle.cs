using Godot;

namespace Gearheart.Core;

/// <summary>
/// The world's clock: a full day takes about seven minutes of play. Daytime (06:30–18:00) is the
/// longest stretch; dusk and dawn take about 40 s each, and the night hours run twice as fast, so a
/// night lasts about a minute and a quarter. Pure functions of the hour; the hour itself lives in
/// <see cref="GameSession.ClockHours"/>.
/// </summary>
public static class DayCycle
{
    /// <summary>A new game starts in the morning.</summary>
    public const float StartHours = 8f;

    /// <summary>Game hours per real second in the daytime (13 hours in 270 s).</summary>
    public const float HoursPerSecond = 13f / 270f;

    /// <summary>Night hours (21:00–04:00) pass this much faster.</summary>
    public const float NightSpeedUp = 2f;

    public static bool NightHours(float h) => h >= 21f || h < 4f;

    /// <summary>How fast the clock runs at this hour, in game hours per second.</summary>
    public static float Rate(float h) => HoursPerSecond * (NightHours(h) ? NightSpeedUp : 1f);

    /// <summary>0 at night, 1 in full daylight; eases through dusk (18:00–20:30) and dawn (04:00–06:30).</summary>
    public static float Daylight(float h)
    {
        h = Mathf.PosMod(h, 24f);
        if (h >= 6.5f && h <= 18f) return 1f;
        if (h > 18f && h < 20.5f) return Smooth(1f - (h - 18f) / 2.5f);
        if (h >= 4f && h < 6.5f) return Smooth((h - 4f) / 2.5f);
        return 0f;
    }

    /// <summary>1 - daylight.</summary>
    public static float Night(float h) => 1f - Daylight(h);

    /// <summary>0..1 how red and golden the light is: peaks around sunset (19:10) and sunrise (05:20).</summary>
    public static float Warmth(float h)
    {
        h = Mathf.PosMod(h, 24f);
        float dusk = 1f - Mathf.Abs(h - 19.2f) / 1.5f;
        float dawn = 1f - Mathf.Abs(h - 5.3f) / 1.3f;
        return Smooth(Mathf.Clamp(Mathf.Max(dusk, dawn), 0f, 1f));
    }

    /// <summary>
    /// Colour the world is multiplied by on the surface: white by day, a warm orange at sunset,
    /// a deep moonlit blue at night (dark enough that headlights matter, light enough to see the hills).
    /// </summary>
    public static Color Tint(float h)
    {
        Color warm = new(1f, 0.74f, 0.58f), night = new(0.27f, 0.31f, 0.52f);
        return Colors.White.Lerp(warm, 0.55f * Warmth(h)).Lerp(night, Night(h));
    }

    /// <summary>"19:40".</summary>
    public static string Format(float h)
    {
        h = Mathf.PosMod(h, 24f);
        int hours = Mathf.FloorToInt(h), minutes = Mathf.FloorToInt((h - hours) * 60f);
        return $"{hours:00}:{minutes:00}";
    }

    private static float Smooth(float x)
    {
        x = Mathf.Clamp(x, 0f, 1f);
        return x * x * (3f - 2f * x);
    }
}
