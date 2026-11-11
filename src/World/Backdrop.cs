using Godot;

namespace Gearheart.World;

/// <summary>Sky, sun, clouds and two layers of far hills, drawn in screen space with parallax.</summary>
public partial class Backdrop : Node2D
{
    /// <summary>Camera centre in world pixels, set by the scene every frame.</summary>
    public Vector2 CameraPosition;

    /// <summary>0 = on the surface, 1 = deep underground (the sky fades to black).</summary>
    public float Underground;

    /// <summary>0..1 share of desert and of frozen land at the camera, for sky and hill colours.</summary>
    public float Desert, Frozen;

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
        Color top = Blend(new Color(0.25f, 0.5f, 0.86f), new Color(0.36f, 0.56f, 0.84f), new Color(0.44f, 0.58f, 0.78f)) * dark;
        Color bottom = Blend(new Color(0.72f, 0.86f, 0.96f), new Color(0.99f, 0.86f, 0.64f), new Color(0.9f, 0.94f, 0.99f)) * dark;
        top.A = bottom.A = 1f;

        DrawPolygon(
            new[] { Vector2.Zero, new Vector2(size.X, 0), size, new Vector2(0, size.Y) },
            new[] { top, top, bottom, bottom });

        if (Underground > 0.98f) return;

        float lift = -CameraPosition.Y * 0.04f + 40f;
        DrawCircle(new Vector2(size.X * 0.78f, size.Y * 0.2f + lift * 0.3f), 46f, new Color(1f, 0.96f, 0.75f, dark));
        DrawCircle(new Vector2(size.X * 0.78f, size.Y * 0.2f + lift * 0.3f), 60f, new Color(1f, 0.96f, 0.75f, 0.25f * dark));

        for (int i = 0; i < 6; i++)
        {
            float x = Mathf.PosMod(i * 310f + _time * (6f + i) - CameraPosition.X * 0.03f, size.X + 300f) - 150f;
            float y = size.Y * (0.12f + 0.07f * (i % 3)) + lift * 0.4f;
            var cloud = new Color(1f, 1f, 1f, 0.85f * dark);
            DrawCircle(new Vector2(x, y), 26f, cloud);
            DrawCircle(new Vector2(x + 30f, y + 6f), 20f, cloud);
            DrawCircle(new Vector2(x - 30f, y + 8f), 18f, cloud);
            DrawRect(new Rect2(x - 44f, y + 6f, 90f, 18f), cloud);
        }

        Hills(size, 0.08f, size.Y * 0.56f + lift, 70f, Blend(new Color(0.5f, 0.66f, 0.82f), new Color(0.86f, 0.72f, 0.56f), new Color(0.74f, 0.82f, 0.92f)) * dark);
        Hills(size, 0.16f, size.Y * 0.68f + lift * 1.5f, 50f, Blend(new Color(0.36f, 0.56f, 0.5f), new Color(0.8f, 0.62f, 0.42f), new Color(0.84f, 0.9f, 0.96f)) * dark);
        Hills(size, 0.3f, size.Y * 0.8f + lift * 2.2f, 34f, Blend(new Color(0.26f, 0.46f, 0.32f), new Color(0.7f, 0.52f, 0.32f), new Color(0.94f, 0.96f, 1f)) * dark);
    }

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
