using System.Collections.Generic;
using Godot;

namespace Gearheart.World;

/// <summary>
/// Placeholder ground for the physics test: a flat start, rolling hills,
/// a ramp that ends in a cliff, and a wall on the left. Not the real tile world.
/// </summary>
public partial class TestTerrain : StaticBody2D
{
    [Export] public int Length = 12000;
    [Export] public int Seed = 7;

    public override void _Ready()
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)Seed };
        float phaseA = rng.Randf() * 10f;
        float phaseB = rng.Randf() * 10f;

        var surface = new List<Vector2>();
        for (int x = 0; x <= Length; x += 40)
            surface.Add(new Vector2(x, HeightAt(x, phaseA, phaseB)));

        var outline = new List<Vector2>
        {
            new(-640f, -400f),
            new(-600f, -400f),
            new(-600f, 0f),
        };
        outline.AddRange(surface);
        outline.Add(new Vector2(Length, 2000f));
        outline.Add(new Vector2(-640f, 2000f));

        Vector2[] polygon = outline.ToArray();
        AddChild(new CollisionPolygon2D { Polygon = polygon });
        AddChild(new Polygon2D { Polygon = polygon, Color = new Color(0.36f, 0.27f, 0.2f) });
        AddChild(new Line2D { Points = surface.ToArray(), Width = 8f, DefaultColor = new Color(0.3f, 0.6f, 0.25f) });

        PhysicsMaterialOverride = new PhysicsMaterial { Friction = 1f };
    }

    private static float HeightAt(float x, float phaseA, float phaseB)
    {
        float t = x / 1000f;
        float amplitude = Mathf.Min(1f, x / 3000f); // flat at the start, full hills from x = 3000
        float y = amplitude * (Mathf.Sin(t * 1.7f + phaseA) * 140f + Mathf.Sin(t * 4.3f + phaseB) * 45f);

        // Ramp from x = 1600 to 2000 rising 200 px, then a cliff drop.
        if (x >= 1600f && x < 2000f)
            y -= (x - 1600f) * 0.5f;

        return y;
    }
}
