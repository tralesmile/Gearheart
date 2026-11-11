using System.Collections.Generic;
using Gearheart.Physics;
using Godot;

namespace Gearheart.World;

public enum TerrainStyle
{
    Rolling,     // flat start, rolling hills, a ramp with a cliff (free drive / physics test)
    HillClimb,   // keeps rising and gets steeper and bumpier toward the finish
}

/// <summary>
/// Placeholder ground for the prototype: a height line with a wall on the left.
/// Not the real tile world, which comes in the vertical slice.
/// </summary>
public partial class TestTerrain : StaticBody2D, ITerrainCollider
{
    [Export] public int Length = 12000;
    [Export] public int Seed = 7;
    [Export] public TerrainStyle Style = TerrainStyle.Rolling;

    private const float WallX = -600f;
    private const float Spacing = 40f;
    private Vector2[] _surface = System.Array.Empty<Vector2>();
    private float _phaseA, _phaseB;

    public override void _Ready()
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)Seed };
        _phaseA = rng.Randf() * 10f;
        _phaseB = rng.Randf() * 10f;

        var surface = new List<Vector2>();
        for (float x = WallX; x <= Length; x += Spacing)
            surface.Add(new Vector2(x, HeightAt(x)));
        _surface = surface.ToArray();

        var outline = new List<Vector2>
        {
            new(WallX - 40f, -400f),
            new(WallX, -400f),
        };
        outline.AddRange(surface);
        outline.Add(new Vector2(Length, 2000f));
        outline.Add(new Vector2(WallX - 40f, 2000f));

        Vector2[] polygon = outline.ToArray();
        AddChild(new CollisionPolygon2D { Polygon = polygon });
        AddChild(new Polygon2D { Polygon = polygon, Color = new Color(0.36f, 0.27f, 0.2f) });
        AddChild(new Line2D { Points = surface.ToArray(), Width = 8f, DefaultColor = new Color(0.3f, 0.6f, 0.25f) });

        PhysicsMaterialOverride = new PhysicsMaterial { Friction = 1f };
    }

    /// <summary>
    /// Circle-vs-terrain test used by custom physics (the soft-body vehicle).
    /// Returns the push-out direction and depth of the deepest overlap.
    /// </summary>
    public bool Collide(Vector2 p, float radius, out Vector2 normal, out float penetration)
    {
        normal = Vector2.Up;
        penetration = 0f;
        bool hit = false;

        if (p.X - radius < WallX)
        {
            normal = Vector2.Right;
            penetration = WallX - (p.X - radius);
            hit = true;
        }

        if (_surface.Length < 2) return hit;

        int first = Mathf.Max(0, Mathf.FloorToInt((p.X - radius - WallX) / Spacing));
        int last = Mathf.Min(_surface.Length - 2, Mathf.FloorToInt((p.X + radius - WallX) / Spacing));

        for (int i = first; i <= last; i++)
        {
            Vector2 a = _surface[i];
            Vector2 ab = _surface[i + 1] - a;
            float lengthSq = ab.LengthSquared();
            if (lengthSq < 1e-6f) continue;

            Vector2 up = new Vector2(ab.Y, -ab.X) / Mathf.Sqrt(lengthSq); // surface normal, pointing out of the ground
            float along = Mathf.Clamp((p - a).Dot(ab) / lengthSq, 0f, 1f);
            Vector2 closest = a + ab * along;
            float signedDistance = (p - a).Dot(up);

            float depth;
            Vector2 n;
            if (signedDistance < 0f && along > 0f && along < 1f)
            {
                // Centre is under this segment: push straight out along its normal.
                depth = radius - signedDistance;
                n = up;
            }
            else
            {
                // Centre is above the segment or beyond its ends: use the true distance.
                Vector2 d = p - closest;
                float distance = d.Length();
                if (distance >= radius || signedDistance < 0f) continue;
                depth = radius - distance;
                n = distance > 1e-4f ? d / distance : up;
            }

            if (depth > penetration)
            {
                penetration = depth;
                normal = n;
                hit = true;
            }
        }

        return hit;
    }

    /// <summary>Ground height (y) at x, read from the built surface.</summary>
    public float SurfaceY(float x)
    {
        if (_surface.Length < 2) return 0f;
        float f = (x - WallX) / Spacing;
        int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, _surface.Length - 2);
        float t = Mathf.Clamp(f - i, 0f, 1f);
        return Mathf.Lerp(_surface[i].Y, _surface[i + 1].Y, t);
    }

    private float HeightAt(float x)
    {
        float phaseA = _phaseA, phaseB = _phaseB;
        if (Style == TerrainStyle.HillClimb)
        {
            if (x < 400f) return 0f;
            float climb = x - 400f;
            float bumpSize = 40f + climb * 0.022f;           // bumps grow with distance
            float rise = -climb * 0.06f;                       // steady climb
            float bumps = Mathf.Sin(climb / 1000f * 1.7f + phaseA) * bumpSize
                          + Mathf.Sin(climb / 1000f * 4.3f + phaseB) * bumpSize * 0.4f;
            float fade = Mathf.Clamp(climb / 600f, 0f, 1f);   // ease in from the flat start
            return (rise + bumps - Mathf.Sin(phaseA) * bumpSize - Mathf.Sin(phaseB) * bumpSize * 0.4f) * fade;
        }

        float t = x / 1000f;
        float amplitude = Mathf.Clamp(x / 3000f, 0f, 1f); // flat at the start, full hills from x = 3000
        float y = amplitude * (Mathf.Sin(t * 1.7f + phaseA) * 140f + Mathf.Sin(t * 4.3f + phaseB) * 45f);

        // Ramp from x = 1600 to 2000 rising 200 px, then a cliff drop.
        if (x >= 1600f && x < 2000f)
            y -= (x - 1600f) * 0.5f;

        return y;
    }
}
