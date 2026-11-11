using System.Collections.Generic;
using Godot;

namespace Gearheart.Physics;

/// <summary>Several colliders treated as one ground (for example terrain plus ore rocks).</summary>
public sealed class CompositeCollider : ITerrainCollider
{
    private readonly List<ITerrainCollider> _parts = new();

    public CompositeCollider(params ITerrainCollider[] parts)
    {
        foreach (var part in parts)
            if (part != null) _parts.Add(part);
    }

    public bool Collide(Vector2 point, float radius, out Vector2 normal, out float penetration)
    {
        normal = Vector2.Up;
        penetration = 0f;
        bool hit = false;

        foreach (var part in _parts)
        {
            if (!part.Collide(point, radius, out Vector2 n, out float depth) || depth <= penetration) continue;
            normal = n;
            penetration = depth;
            hit = true;
        }

        return hit;
    }
}

/// <summary>Something a drill can mine.</summary>
public interface IMineable
{
    /// <summary>Applies mining work at a point. Returns true if anything was in reach.</summary>
    bool Mine(Vector2 point, float reach, float work);
}
