using System.Collections.Generic;
using Gearheart.Vehicles;
using Godot;

namespace Gearheart.Physics;

/// <summary>
/// Steps every soft body together and resolves contacts between them:
/// soft car vs soft car, and soft car vs loose rigid objects (Godot RigidBody2D nodes
/// in the "loose_objects" group, with a rectangle or circle collision shape).
///
/// Order per physics tick: for each substep, integrate all cars, run the constraint
/// iterations, resolve contacts, then velocity fixes. Impulses for rigid objects are
/// collected during the tick and handed to Godot's physics at the end.
/// </summary>
public partial class SoftPhysicsWorld : Node
{
    public const string LooseObjectsGroup = "loose_objects";

    public const int Substeps = 8;
    public const int Iterations = 4;
    private const float ContactMargin = 4f;

    public ITerrainCollider Terrain { get; set; }

    private readonly List<SoftVehicle> _bodies = new();
    private readonly List<LooseObject> _loose = new();

    private sealed class LooseObject
    {
        public RigidBody2D Body;
        public Transform2D Xform;
        public Transform2D Inverse;
        public bool IsCircle;
        public Vector2 HalfSize;
        public float Radius;
        public float InvMass;
        public float InvInertia;
        public Vector2 Velocity;          // local estimate, updated by our impulses this tick
        public float AngularVelocity;
        public Vector2 Impulse;           // collected, applied to Godot at the end of the tick
        public float TorqueImpulse;
    }

    public void Register(SoftVehicle body)
    {
        if (!_bodies.Contains(body)) _bodies.Add(body);
    }

    public void Unregister(SoftVehicle body) => _bodies.Remove(body);

    public override void _PhysicsProcess(double delta)
    {
        if (Terrain == null) return;
        _bodies.RemoveAll(b => !IsInstanceValid(b) || b.Pos == null || b.Pos.Length == 0);
        if (_bodies.Count == 0) return;

        float tick = (float)delta;
        float dt = tick / Substeps;
        GatherLooseObjects();

        foreach (var body in _bodies) body.BeginTick();

        for (int s = 0; s < Substeps; s++)
        {
            foreach (var body in _bodies) body.Integrate(dt);

            for (int it = 0; it < Iterations; it++)
                foreach (var body in _bodies) body.SolveIteration(Terrain);

            ResolveVehicleContacts();
            ResolveLooseContacts(dt);

            foreach (var body in _bodies) body.EndSubstep(dt, Terrain);
        }

        foreach (var body in _bodies) body.EndTick(tick);
        ApplyLooseImpulses();
    }

    // ---- Soft car vs soft car --------------------------------------------------------------

    private void ResolveVehicleContacts()
    {
        for (int a = 0; a < _bodies.Count; a++)
        {
            for (int b = a + 1; b < _bodies.Count; b++)
            {
                var first = _bodies[a];
                var second = _bodies[b];
                if (!first.Bounds().Intersects(second.Bounds())) continue;

                PushNodesOutOf(first, second);
                PushNodesOutOf(second, first);
                PushWheelsApart(first, second);
            }
        }
    }

    /// <summary>Pushes every node of <paramref name="mover"/> out of <paramref name="shape"/>'s body.</summary>
    private static void PushNodesOutOf(SoftVehicle mover, SoftVehicle shape)
    {
        var edges = shape.BoundaryEdges;
        Vector2[] sp = shape.Pos;
        Rect2 shapeBounds = shape.Bounds();

        for (int i = 0; i < mover.Pos.Length; i++)
        {
            Vector2 p = mover.Pos[i];
            float radius = mover.RadiusOf(i);
            if (!shapeBounds.Grow(radius).HasPoint(p)) continue;

            bool inside = shape.ContainsPoint(p);

            // Closest outside edge of the other body.
            float bestDistance = float.MaxValue;
            int bestEdge = -1;
            float bestT = 0f;
            Vector2 bestPoint = Vector2.Zero;
            for (int e = 0; e < edges.Count; e++)
            {
                Vector2 e0 = sp[edges[e].A];
                Vector2 e1 = sp[edges[e].B];
                Vector2 edge = e1 - e0;
                float lengthSq = edge.LengthSquared();
                float t = lengthSq < 1e-6f ? 0f : Mathf.Clamp((p - e0).Dot(edge) / lengthSq, 0f, 1f);
                Vector2 closest = e0 + edge * t;
                float distance = p.DistanceTo(closest);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestEdge = e;
                    bestT = t;
                    bestPoint = closest;
                }
            }

            if (bestEdge < 0 || bestDistance < 1e-4f) continue;
            if (!inside && bestDistance >= radius) continue;

            Vector2 normal;
            float depth;
            if (inside)
            {
                normal = (bestPoint - p) / bestDistance;   // out through the nearest edge
                depth = bestDistance + radius;
            }
            else
            {
                normal = (p - bestPoint) / bestDistance;
                depth = radius - bestDistance;
            }

            int n0 = edges[bestEdge].A;
            int n1 = edges[bestEdge].B;
            float wp = mover.InvMass[i];
            float w0 = shape.InvMass[n0] * (1f - bestT);
            float w1 = shape.InvMass[n1] * bestT;
            float w = wp + w0 + w1;
            if (w <= 0f) continue;

            Vector2 push = normal * depth;
            mover.Pos[i] += push * (wp / w);
            shape.Pos[n0] -= push * (w0 / w);
            shape.Pos[n1] -= push * (w1 / w);
        }
    }

    private static void PushWheelsApart(SoftVehicle a, SoftVehicle b)
    {
        foreach (int ha in a.HubNodes)
        {
            foreach (int hb in b.HubNodes)
            {
                Vector2 d = b.Pos[hb] - a.Pos[ha];
                float distance = d.Length();
                float minDistance = a.RadiusOf(ha) + b.RadiusOf(hb);
                if (distance >= minDistance || distance < 1e-4f) continue;

                Vector2 n = d / distance;
                float wa = a.InvMass[ha], wb = b.InvMass[hb];
                float overlap = minDistance - distance;
                a.Pos[ha] -= n * overlap * (wa / (wa + wb));
                b.Pos[hb] += n * overlap * (wb / (wa + wb));
            }
        }
    }

    // ---- Soft car vs loose rigid objects -----------------------------------------------------

    private void GatherLooseObjects()
    {
        _loose.Clear();
        foreach (Node node in GetTree().GetNodesInGroup(LooseObjectsGroup))
        {
            if (node is not RigidBody2D body || !IsInstanceValid(body)) continue;

            var shapeNode = body.GetNodeOrNull<CollisionShape2D>("Shape");
            if (shapeNode?.Shape == null) continue;

            var obj = new LooseObject
            {
                Body = body,
                Xform = body.GlobalTransform,
                InvMass = body.Mass > 0f ? 1f / body.Mass : 0f,
                Velocity = body.LinearVelocity,
                AngularVelocity = body.AngularVelocity,
            };
            obj.Inverse = obj.Xform.AffineInverse();

            switch (shapeNode.Shape)
            {
                case RectangleShape2D rect:
                    obj.HalfSize = rect.Size / 2f;
                    obj.InvInertia = 12f / (body.Mass * (rect.Size.X * rect.Size.X + rect.Size.Y * rect.Size.Y));
                    break;
                case CircleShape2D circle:
                    obj.IsCircle = true;
                    obj.Radius = circle.Radius;
                    obj.InvInertia = 2f / (body.Mass * circle.Radius * circle.Radius);
                    break;
                default:
                    continue;
            }

            _loose.Add(obj);
        }
    }

    private void ResolveLooseContacts(float dt)
    {
        if (_loose.Count == 0) return;

        foreach (var body in _bodies)
        {
            Rect2 bounds = body.Bounds().Grow(ContactMargin);
            foreach (var obj in _loose)
            {
                Vector2 c = obj.Xform.Origin;
                float reach = obj.IsCircle ? obj.Radius : obj.HalfSize.Length();
                if (!bounds.Grow(reach).HasPoint(c)) continue;

                for (int i = 0; i < body.Pos.Length; i++)
                    ResolveNodeVsObject(body, i, obj, dt);
            }
        }
    }

    private static void ResolveNodeVsObject(SoftVehicle body, int i, LooseObject obj, float dt)
    {
        float radius = body.RadiusOf(i);
        Vector2 p = body.Pos[i];

        if (!ContactWithObject(p, radius, obj, out Vector2 normal, out float depth)) return;

        // Positional: push the node out (the object only moves when Godot steps it).
        p += normal * depth;
        body.Pos[i] = p;

        // Velocity: an impulse between the node and the object along the normal.
        Vector2 nodeVelocity = (p - body.Prev[i]) / dt;
        Vector2 contact = p - normal * radius;
        Vector2 r = contact - obj.Xform.Origin;
        Vector2 objectPointVelocity = obj.Velocity + new Vector2(-obj.AngularVelocity * r.Y, obj.AngularVelocity * r.X);
        float approach = (nodeVelocity - objectPointVelocity).Dot(normal);
        if (approach >= 0f) return;

        float rCrossN = r.Cross(normal);
        float invNode = body.InvMass[i];
        float j = -approach / (invNode + obj.InvMass + rCrossN * rCrossN * obj.InvInertia);

        nodeVelocity += normal * (j * invNode);
        body.Prev[i] = p - nodeVelocity * dt;

        Vector2 objectImpulse = -normal * j;
        obj.Velocity += objectImpulse * obj.InvMass;
        obj.AngularVelocity += r.Cross(objectImpulse) * obj.InvInertia;
        obj.Impulse += objectImpulse;
        obj.TorqueImpulse += r.Cross(objectImpulse);
    }

    private static bool ContactWithObject(Vector2 p, float radius, LooseObject obj, out Vector2 normal, out float depth)
    {
        normal = Vector2.Up;
        depth = 0f;

        if (obj.IsCircle)
        {
            Vector2 d = p - obj.Xform.Origin;
            float distance = d.Length();
            float minDistance = obj.Radius + radius;
            if (distance >= minDistance) return false;
            normal = distance > 1e-4f ? d / distance : Vector2.Up;
            depth = minDistance - distance;
            return true;
        }

        Vector2 local = obj.Inverse * p;
        Vector2 h = obj.HalfSize;
        Vector2 clamped = new(Mathf.Clamp(local.X, -h.X, h.X), Mathf.Clamp(local.Y, -h.Y, h.Y));
        Vector2 localNormal;

        if (clamped == local)
        {
            // Centre inside the box: leave through the nearest face.
            float dx = h.X - Mathf.Abs(local.X);
            float dy = h.Y - Mathf.Abs(local.Y);
            if (dx < dy)
            {
                localNormal = new Vector2(Mathf.Sign(local.X) == 0 ? 1f : Mathf.Sign(local.X), 0f);
                depth = dx + radius;
            }
            else
            {
                localNormal = new Vector2(0f, Mathf.Sign(local.Y) == 0 ? -1f : Mathf.Sign(local.Y));
                depth = dy + radius;
            }
        }
        else
        {
            Vector2 d = local - clamped;
            float distance = d.Length();
            if (distance >= radius) return false;
            localNormal = d / distance;
            depth = radius - distance;
        }

        normal = obj.Xform.BasisXform(localNormal).Normalized();
        return true;
    }

    private void ApplyLooseImpulses()
    {
        foreach (var obj in _loose)
        {
            if (!IsInstanceValid(obj.Body)) continue;
            if (obj.Impulse != Vector2.Zero) obj.Body.ApplyCentralImpulse(obj.Impulse);
            if (obj.TorqueImpulse != 0f) obj.Body.ApplyTorqueImpulse(obj.TorqueImpulse);
        }
    }
}
