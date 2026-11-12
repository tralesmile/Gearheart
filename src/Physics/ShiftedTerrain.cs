using Godot;

namespace Gearheart.Physics;

/// <summary>
/// Lets a soft body that keeps its nodes relative to a local origin talk to the world: every point
/// it asks about is moved by <see cref="Shift"/> (the body's origin in world pixels) before the
/// question is passed on. Normals, depths, grip and floor heights need no change (the shift is a
/// pure sideways move by a whole multiple of 1,024 px).
/// </summary>
public sealed class ShiftedTerrain : ITerrainCollider, ISurfaceInfo, IMineable
{
    public ITerrainCollider Inner;
    public IMineable InnerMine;
    public Vector2 Shift;

    public bool Collide(Vector2 point, float radius, out Vector2 normal, out float penetration) =>
        Inner.Collide(point + Shift, radius, out normal, out penetration);

    public float GripAt(Vector2 point) => Inner is ISurfaceInfo info ? info.GripAt(point + Shift) : 1f;

    public bool FrozenAt(Vector2 point) => Inner is ISurfaceInfo info && info.FrozenAt(point + Shift);

    public bool Mine(Vector2 point, float reach, float work, float floorY = float.NaN) =>
        InnerMine != null && InnerMine.Mine(point + Shift, reach, work, floorY);
}
