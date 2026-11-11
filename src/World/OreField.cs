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

/// <summary>Places and tracks ore boulders, and lets drills mine them.</summary>
public partial class OreField : Node2D, IMineable
{
    /// <summary>Raised when a rock is mined out: material and amount.</summary>
    public event Action<OreType, int> Mined;

    public int RocksLeft
    {
        get
        {
            _rocks.RemoveAll(r => !IsInstanceValid(r));
            return _rocks.Count;
        }
    }

    private readonly List<OreRock> _rocks = new();
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

    /// <summary>Places boulders on the ground between two x positions. Further away = rarer materials.</summary>
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
            float radius = rng.RandfRange(32f, 46f);
            AddRock(new Vector2(x, terrain.SurfaceY(x) - radius - 2f), radius, ore);
        }
    }

    public void AddRock(Vector2 center, float radius, OreType ore)
    {
        var rock = OreRock.Create(ore, radius);
        rock.Position = ToLocal(center);
        AddChild(rock);
        _rocks.Add(rock);
    }

    /// <summary>Distance from a point to the nearest rock surface, with that rock's state (diagnostics).</summary>
    public string DebugNearest(Vector2 point)
    {
        OreRock best = null;
        float bestDistance = float.MaxValue;
        foreach (var rock in _rocks)
        {
            if (!IsInstanceValid(rock)) continue;
            float d = point.DistanceTo(rock.GlobalPosition) - rock.Radius;
            if (d < bestDistance) { bestDistance = d; best = rock; }
        }
        return best == null ? "no rocks" :
            $"nearest rock: gap={bestDistance:0}px at=({best.GlobalPosition.X:0},{best.GlobalPosition.Y:0}) r={best.Radius:0} hp={best.Health:0}/{best.MaxHealth:0} v=({best.LinearVelocity.X:0},{best.LinearVelocity.Y:0})";
    }

    public bool Mine(Vector2 point, float reach, float work)
    {
        for (int i = 0; i < _rocks.Count; i++)
        {
            var rock = _rocks[i];
            if (!IsInstanceValid(rock)) continue;
            if (point.DistanceTo(rock.GlobalPosition) > rock.Radius + reach) continue;

            if (_sparks != null && !_sparks.Emitting)
            {
                _sparks.Position = ToLocal(point);
                _sparks.Color = ColorOf(rock.Ore).Lightened(0.4f);
                _sparks.Restart();
            }

            if (rock.TakeWork(work))
            {
                _rocks.RemoveAt(i);
                Mined?.Invoke(rock.Ore, rock.Yield);
                rock.QueueFree();
            }
            return true;
        }

        return false;
    }
}
