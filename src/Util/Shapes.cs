using Godot;

namespace Gearheart.Util;

/// <summary>Small helpers that build polygon point lists for placeholder visuals and collision.</summary>
public static class Shapes
{
    public static Vector2[] Circle(float radius, int segments = 20)
    {
        var points = new Vector2[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.Tau * i / segments;
            points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        return points;
    }

    public static Vector2[] Rect(Vector2 size)
    {
        Vector2 h = size / 2f;
        return new[]
        {
            new Vector2(-h.X, -h.Y),
            new Vector2(h.X, -h.Y),
            new Vector2(h.X, h.Y),
            new Vector2(-h.X, h.Y),
        };
    }
}
