using Godot;

namespace Gearheart.Physics;

/// <summary>Anything custom physics can collide circles against (test terrain now, the tile world later).</summary>
public interface ITerrainCollider
{
    /// <summary>Returns true if a circle overlaps the ground, with the push-out direction and depth.</summary>
    bool Collide(Vector2 point, float radius, out Vector2 normal, out float penetration);
}
