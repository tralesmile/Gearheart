using Gearheart.Core;
using Godot;

namespace Gearheart.World;

/// <summary>
/// Sky, sun, moon, stars, clouds and three layers of far hills, drawn in screen space with parallax.
/// The sky follows the time of day (<see cref="Hours"/>): blue by day, orange and pink at sunset, starry at night.
/// </summary>
public partial class Backdrop : Node2D
{
    /// <summary>Camera centre in world pixels, set by the scene every frame.</summary>
    public Vector2 CameraPosition;

    /// <summary>0 = on the surface, 1 = deep underground (the sky fades to black).</summary>
    public float Underground;

    /// <summary>0..1 share of desert and of frozen land at the camera, for sky and hill colours.</summary>
    public float Desert, Frozen;

    /// <summary>Time of day, 0..24 (see <see cref="DayCycle"/>).</summary>
    public float Hours = 12f;

    private Color Blend(Color meadow, Color desert, Color frozen) => meadow.Lerp(desert, Desert).Lerp(frozen, Frozen);

    private float _time;

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 size = GetViewportRect().Size;
        float dark = 1f - 0.92f * Underground;
        float night = DayCycle.Night(Hours), warm = DayCycle.Warmth(Hours);

        // Sky: the biome's day colours, darkened to a navy night, with a sunset glow low down.
        Color top = Blend(new Color(0.25f, 0.5f, 0.86f), new Color(0.36f, 0.56f, 0.84f), new Color(0.44f, 0.58f, 0.78f));
        Color bottom = Blend(new Color(0.72f, 0.86f, 0.96f), new Color(0.99f, 0.86f, 0.64f), new Color(0.9f, 0.94f, 0.99f));
        top = top.Lerp(new Color(0.03f, 0.04f, 0.13f), night).Lerp(new Color(0.3f, 0.26f, 0.52f), 0.7f * warm) * dark;
        bottom = bottom.Lerp(new Color(0.09f, 0.12f, 0.27f), night).Lerp(new Color(1f, 0.52f, 0.3f), 0.85f * warm) * dark;
        Color middle = top.Lerp(bottom, 0.55f).Lerp(new Color(0.85f, 0.42f, 0.45f) * dark, 0.5f * warm);
        top.A = bottom.A = middle.A = 1f;
        float midY = size.Y * 0.45f;
        DrawPolygon(new[] { Vector2.Zero, new Vector2(size.X, 0), new Vector2(size.X, midY), new Vector2(0, midY) },
                    new[] { top, top, middle, middle });
        DrawPolygon(new[] { new Vector2(0, midY), new Vector2(size.X, midY), size, new Vector2(0, size.Y) },
                    new[] { middle, middle, bottom, bottom });

        if (Underground > 0.98f) return;

        float lift = -CameraPosition.Y * 0.04f + 40f;

        // Stars come out as it gets dark; each twinkles at its own pace.
        if (night > 0.05f)
        {
            for (int i = 0; i < 150; i++)
            {
                uint h = (uint)(i * 2654435761u);
                float x = Mathf.PosMod((h % 10007) / 10007f * (size.X + 200f) - CameraPosition.X * 0.01f, size.X + 200f) - 100f;
                float y = (h / 10007 % 1000) / 1000f * size.Y * 0.62f + lift * 0.2f;
                float twinkle = 0.55f + 0.45f * Mathf.Sin(_time * (1.5f + (h % 7) * 0.4f) + (h % 628) / 100f);
                float a = night * night * twinkle * dark * (0.5f + 0.5f * ((h >> 3) % 3) / 2f);
                float r = (h >> 5) % 9 == 0 ? 1.6f : 1f;
                DrawRect(new Rect2(x, y, r * 1.6f, r * 1.6f), new Color(0.95f, 0.95f, 1f, a));
            }
        }

        // The sun crosses the sky from 06:00 to 20:30, low and red at the ends; the moon from 19:30 to 07:00.
        float sunT = (Mathf.PosMod(Hours, 24f) - 6f) / 14.5f;
        if (sunT > -0.05f && sunT < 1.05f)
        {
            Vector2 sun = SkyArc(size, sunT, lift);
            var sunColor = new Color(1f, 0.96f, 0.75f).Lerp(new Color(1f, 0.55f, 0.3f), warm);
            DrawCircle(sun, 60f + 30f * warm, new Color(sunColor, (0.25f + 0.2f * warm) * dark));
            DrawCircle(sun, 46f, new Color(sunColor, dark));
        }
        float moonT = Mathf.PosMod(Mathf.PosMod(Hours, 24f) - 19.5f, 24f) / 11.5f;
        if (moonT < 1.05f && night > 0.02f)
        {
            Vector2 moon = SkyArc(size, moonT, lift);
            float a = Mathf.Clamp(night * 1.4f, 0f, 1f) * dark;
            DrawCircle(moon, 58f, new Color(0.75f, 0.82f, 1f, 0.12f * a));
            DrawCircle(moon, 40f, new Color(0.75f, 0.82f, 1f, 0.15f * a));
            DrawCircle(moon, 30f, new Color(0.93f, 0.94f, 0.88f, a));
            DrawCircle(moon + new Vector2(-9f, -6f), 7f, new Color(0.8f, 0.82f, 0.8f, a));
            DrawCircle(moon + new Vector2(8f, 9f), 5f, new Color(0.8f, 0.82f, 0.8f, a));
            DrawCircle(moon + new Vector2(10f, -10f), 3.5f, new Color(0.82f, 0.84f, 0.82f, a));
        }

        // Clouds pick up the sunset and go grey-blue at night.
        Color cloudColor = new Color(1f, 1f, 1f).Lerp(new Color(1f, 0.66f, 0.55f), warm).Lerp(new Color(0.2f, 0.24f, 0.38f), night * (1f - warm));
        for (int i = 0; i < 6; i++)
        {
            float x = Mathf.PosMod(i * 310f + _time * (6f + i) - CameraPosition.X * 0.03f, size.X + 300f) - 150f;
            float y = size.Y * (0.12f + 0.07f * (i % 3)) + lift * 0.4f;
            var cloud = new Color(cloudColor * dark, 0.85f * (1f - 0.35f * night));
            DrawCircle(new Vector2(x, y), 26f, cloud);
            DrawCircle(new Vector2(x + 30f, y + 6f), 20f, cloud);
            DrawCircle(new Vector2(x - 30f, y + 8f), 18f, cloud);
            DrawRect(new Rect2(x - 44f, y + 6f, 90f, 18f), cloud);
        }

        // Far hills turn to silhouettes against the sunset, then dark blue under the moon.
        Color hillLight = DayCycle.Tint(Hours).Lerp(new Color(0.16f, 0.18f, 0.32f), 0.6f * night) * dark;
        Hills(size, 0.08f, size.Y * 0.56f + lift, 70f, Blend(new Color(0.5f, 0.66f, 0.82f), new Color(0.86f, 0.72f, 0.56f), new Color(0.74f, 0.82f, 0.92f)) * hillLight);
        Hills(size, 0.16f, size.Y * 0.68f + lift * 1.5f, 50f, Blend(new Color(0.36f, 0.56f, 0.5f), new Color(0.8f, 0.62f, 0.42f), new Color(0.84f, 0.9f, 0.96f)) * hillLight);
        Hills(size, 0.3f, size.Y * 0.8f + lift * 2.2f, 34f, Blend(new Color(0.26f, 0.46f, 0.32f), new Color(0.7f, 0.52f, 0.32f), new Color(0.94f, 0.96f, 1f)) * hillLight);
    }

    /// <summary>A point on the arc a sun or moon follows across the screen, t = 0 rising on the left to 1 setting on the right.</summary>
    private static Vector2 SkyArc(Vector2 size, float t, float lift) =>
        new(size.X * (0.06f + 0.88f * t), size.Y * (0.66f - 0.52f * Mathf.Sin(Mathf.Pi * Mathf.Clamp(t, 0f, 1f))) + lift * 0.3f);

    private void Hills(Vector2 size, float parallax, float baseY, float height, Color color)
    {
        color.A = 1f;
        const float step = 6f;
        float scroll = CameraPosition.X * parallax;
        for (float x = 0f; x < size.X; x += step)
        {
            float wx = x + scroll;
            float y = baseY
                      + Mathf.Sin(wx / (height * 2.9f)) * height
                      + Mathf.Sin(wx / (height * 1.1f) + 1.7f) * height * 0.35f;
            DrawRect(new Rect2(x, y, step, size.Y - y + 2f), color);
        }
    }
}
