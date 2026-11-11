using System;
using System.Collections.Generic;
using Gearheart.Physics;
using Godot;

namespace Gearheart.World;

/// <summary>The three prototype materials.</summary>
public enum OreType
{
    Scrap,
    Iron,
    Copper,
}

/// <summary>
/// Half-buried ore rocks along the ground. Cars collide with them like solid rock;
/// a drill tip pressed against one grinds it down, and it pays out its material when gone.
/// </summary>
public partial class OreField : Node2D, ITerrainCollider, IMineable
{
    /// <summary>Raised when a rock is mined out: material and amount.</summary>
    public event Action<OreType, int> Mined;

    public int RocksLeft => _rocks.Count;

    private sealed class Rock
    {
        public Vector2 Center;
        public float MaxRadius;
        public float Health, MaxHealth;
        public OreType Ore;
        public int Yield;
        public float Shake;
    }

    private readonly List<Rock> _rocks = new();
    private CpuParticles2D _sparks;

    public static Color ColorOf(OreType ore) => ore switch
    {
        OreType.Scrap => new Color(0.55f, 0.57f, 0.6f),
        OreType.Iron => new Color(0.62f, 0.36f, 0.25f),
        OreType.Copper => new Color(0.86f, 0.52f, 0.2f),
        _ => Colors.White,
    };

    public override void _Ready()
    {
        _sparks = new CpuParticles2D
        {
            Emitting = false,
            OneShot = true,
            Amount = 16,
            Lifetime = 0.4f,
            Explosiveness = 0.9f,
            Spread = 70f,
            Direction = Vector2.Up,
            InitialVelocityMin = 120f,
            InitialVelocityMax = 260f,
            Gravity = new Vector2(0f, 900f),
            ScaleAmountMin = 2f,
            ScaleAmountMax = 4f,
            Color = new Color(1f, 0.85f, 0.4f),
        };
        AddChild(_sparks);
    }

    /// <summary>Places rocks along the ground between two x positions. Further away = rarer materials.</summary>
    public void Scatter(TestTerrain terrain, float fromX, float toX, float spacing, ulong seed)
    {
        var rng = new RandomNumberGenerator { Seed = seed };
        for (float x = fromX; x < toX; x += spacing * rng.RandfRange(0.7f, 1.3f))
        {
            float progress = (x - fromX) / Mathf.Max(1f, toX - fromX);
            float roll = rng.Randf();
            OreType ore = roll < 0.55f - progress * 0.3f ? OreType.Scrap
                : roll < 0.85f - progress * 0.1f ? OreType.Iron
                : OreType.Copper;
            float radius = rng.RandfRange(18f, 30f);
            float groundY = terrain.SurfaceY(x);
            AddRock(new Vector2(x, groundY + radius * 0.35f), radius, ore);
        }
    }

    public void AddRock(Vector2 center, float radius, OreType ore)
    {
        int yield = ore switch { OreType.Scrap => 3, OreType.Iron => 2, _ => 1 };
        float health = radius * (ore == OreType.Copper ? 3f : 2f);
        _rocks.Add(new Rock
        {
            Center = center,
            MaxRadius = radius,
            Health = health,
            MaxHealth = health,
            Ore = ore,
            Yield = yield,
        });
        QueueRedraw();
    }

    private static float RadiusOf(Rock rock) =>
        rock.MaxRadius * Mathf.Max(0.35f, Mathf.Sqrt(rock.Health / rock.MaxHealth));

    public bool Collide(Vector2 point, float radius, out Vector2 normal, out float penetration)
    {
        normal = Vector2.Up;
        penetration = 0f;
        bool hit = false;

        foreach (var rock in _rocks)
        {
            Vector2 d = point - rock.Center;
            float reach = RadiusOf(rock) + radius;
            float distanceSq = d.LengthSquared();
            if (distanceSq >= reach * reach) continue;

            float distance = Mathf.Sqrt(distanceSq);
            float depth = reach - distance;
            if (depth <= penetration) continue;
            normal = distance > 1e-4f ? d / distance : Vector2.Up;
            penetration = depth;
            hit = true;
        }

        return hit;
    }

    public bool Mine(Vector2 point, float reach, float work)
    {
        for (int i = 0; i < _rocks.Count; i++)
        {
            var rock = _rocks[i];
            if (point.DistanceTo(rock.Center) > RadiusOf(rock) + reach) continue;

            rock.Health -= work;
            rock.Shake = 1f;
            if (_sparks != null && !_sparks.Emitting)
            {
                _sparks.Position = ToLocal(point);
                _sparks.Color = ColorOf(rock.Ore).Lightened(0.4f);
                _sparks.Restart();
            }

            if (rock.Health <= 0f)
            {
                _rocks.RemoveAt(i);
                Mined?.Invoke(rock.Ore, rock.Yield);
            }

            QueueRedraw();
            return true;
        }

        return false;
    }

    public override void _Process(double delta)
    {
        bool shaking = false;
        foreach (var rock in _rocks)
        {
            if (rock.Shake <= 0f) continue;
            rock.Shake = Mathf.Max(0f, rock.Shake - (float)delta * 6f);
            shaking = true;
        }
        if (shaking) QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var rock in _rocks)
        {
            float radius = RadiusOf(rock);
            Vector2 c = ToLocal(rock.Center) + new Vector2(Mathf.Sin(rock.Shake * 40f), 0f) * rock.Shake * 2f;
            Color color = ColorOf(rock.Ore);
            DrawCircle(c, radius, color.Darkened(0.35f));
            DrawCircle(c + new Vector2(-radius * 0.15f, -radius * 0.15f), radius * 0.8f, color);
            DrawCircle(c + new Vector2(radius * 0.3f, -radius * 0.25f), radius * 0.18f, color.Lightened(0.35f));
            DrawCircle(c + new Vector2(-radius * 0.35f, radius * 0.2f), radius * 0.12f, color.Lightened(0.25f));
        }
    }
}
