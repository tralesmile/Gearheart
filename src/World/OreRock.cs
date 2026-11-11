using Gearheart.Physics;
using Godot;

namespace Gearheart.World;

/// <summary>
/// A loose ore boulder. It is a normal rigid body, so any car can push it or roll it away;
/// a drill tip pressed against it grinds it down until it breaks and pays out its material.
/// </summary>
public partial class OreRock : RigidBody2D
{
    public OreType Ore;
    public float MaxRadius;
    public float Health, MaxHealth;
    public int Yield;
    public float Shake;

    private CircleShape2D _shape;

    public float Radius => MaxRadius * Mathf.Max(0.35f, Mathf.Sqrt(Mathf.Max(0f, Health) / MaxHealth));

    public static OreRock Create(OreType ore, float radius)
    {
        float health = radius * (ore == OreType.Copper ? 3f : 2f);
        var rock = new OreRock
        {
            Ore = ore,
            MaxRadius = radius,
            Health = health,
            MaxHealth = health,
            Yield = ore switch { OreType.Scrap => 3, OreType.Iron => 2, _ => 1 },
            Mass = 2f * (radius / 30f) * (radius / 30f),
            LinearDamp = 0.2f,
            AngularDamp = 1f,
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = 1f, Bounce = 0.05f },
        };
        rock._shape = new CircleShape2D { Radius = radius };
        rock.AddChild(new CollisionShape2D { Name = "Shape", Shape = rock._shape });
        rock.AddToGroup(SoftPhysicsWorld.LooseObjectsGroup);
        return rock;
    }

    /// <summary>Applies drill work. Returns true when the rock breaks.</summary>
    public bool TakeWork(float work)
    {
        Health -= work;
        Shake = 1f;
        _shape.Radius = Radius;
        QueueRedraw();
        return Health <= 0f;
    }

    public override void _Process(double delta)
    {
        if (Shake <= 0f) return;
        Shake = Mathf.Max(0f, Shake - (float)delta * 6f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float r = Radius;
        Vector2 c = new Vector2(Mathf.Sin(Shake * 40f), 0f) * Shake * 2f;
        Color color = OreField.ColorOf(Ore);
        DrawCircle(c, r, color.Darkened(0.35f));
        DrawCircle(c + new Vector2(-r * 0.12f, -r * 0.12f), r * 0.82f, color);
        DrawCircle(c + new Vector2(r * 0.3f, -r * 0.25f), r * 0.18f, color.Lightened(0.35f));
        DrawCircle(c + new Vector2(-r * 0.35f, r * 0.2f), r * 0.12f, color.Lightened(0.25f));
        DrawCircle(c + new Vector2(r * 0.1f, r * 0.4f), r * 0.1f, color.Lightened(0.2f));
    }
}
