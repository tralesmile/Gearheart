using System;
using System.Collections.Generic;
using Gearheart.Core;
using Godot;

namespace Gearheart.World;

public enum BirdKind
{
    Sparrow,   // Home Meadow: small and brown, chirps
    Crow,      // The Wastes: big and black, caws
    Bunting,   // Frozen North: white with black wings
}

/// <summary>One bird: perched on the ground (pecking, hopping) or flying off.</summary>
public sealed class Bird
{
    public Vector2 Pos;            // world position of its feet (perched) or its middle (flying)
    public Vector2 Vel;
    public bool Flying, Gone;
    public float Delay;            // seconds before it takes off after the flock is startled
    public float Anim;             // seconds, drives the wing beat
    public int Face = 1;           // 1 = looks right, -1 = looks left
    public float Offset;           // perched: x offset from the flock's spot
    public float NextIdle, PeckLeft, HopLeft, HopFrom, HopTo;
}

/// <summary>A few birds of one kind sitting together on the ground (or on the garage roof).</summary>
public sealed class Flock
{
    public int Index;
    public int X;                  // tile column of the flock's spot
    public BirdKind Kind;
    public bool Roof;              // sits on the garage roof (fixed perches), not on the ground
    public Vector2[] Perches;      // roof perches, world positions
    public readonly List<Bird> Birds = new();
    public bool Scattered;
    public double RespawnAt;
    public float CenterX => Roof ? Perches[Perches.Length / 2].X : (X + 0.5f) * TileWorld.TileSize;
}

/// <summary>
/// Birds sitting along the road and on the garage roof. They peck and hop about, and take off in a
/// flutter (with a chirp, or a caw from the Wastes crows) when the car comes close — sooner the faster
/// it is going — when you walk up to them, honk the horn, start the engine nearby, crash or dig next
/// to them. Scattered flocks come back after a while, out of sight. Pure life in the world: they do
/// not touch the physics or the score. Like the roadside pickups, the list lives for the game session.
/// </summary>
public partial class Birds : Node2D
{
    public const double RespawnSeconds = 75.0;
    private const float HiddenDistance = 1400f, DrawDistance = 1600f;
    private const float Ts = TileWorld.TileSize;
    private const int Cell = 16;

    public TileWorld World;
    public Sfx Sfx;

    /// <summary>The camera focus (car or player): drawing range, and where sounds are heard from.</summary>
    public Vector2 Focus;

    /// <summary>0 by day .. 1 at night: perched birds sleep (no pecking or hopping).</summary>
    public float Night;

    /// <summary>Garage roof perches (world positions), set before the node enters the tree.</summary>
    public Vector2[] RoofPerches = Array.Empty<Vector2>();

    /// <summary>Called when a flock takes off: the flock, what startled it, and how far away that was (px).</summary>
    public Action<Flock, string, float> Scattered;

    private static List<Flock> _all;
    private static TileWorld _placedFor;
    private readonly RandomNumberGenerator _rng = new() { Seed = 77 };
    private float _time;

    public IReadOnlyList<Flock> All => _all;
    public int Scatters { get; private set; }
    public int InAir { get; private set; }
    public int MostInAir { get; private set; }

    private static double Clock => Time.GetTicksMsec() / 1000.0;

    public override void _Ready()
    {
        if (_all == null || _placedFor != World)
        {
            _all = Place(World, RoofPerches);
            _placedFor = World;
            foreach (var f in _all) Perch(f, _rng);
        }
        RefreshGround(float.PositiveInfinity);
    }

    public override void _ExitTree() => _rng.Dispose();

    /// <summary>
    /// A flock every 22–42 m along the road on both sides, starting about 28 m out (never on a
    /// kicker's ramp), plus sparrows on the garage roof.
    /// </summary>
    public static List<Flock> Place(TileWorld world, Vector2[] roof)
    {
        ulong seed = 4711;
        for (int x = 0; x < TileWorld.Width; x += 89) seed = seed * 37 + (ulong)world.SurfaceRow(x);
        using var rng = new RandomNumberGenerator { Seed = seed };
        var list = new List<Flock>();
        if (roof.Length > 0) list.Add(new Flock { Index = 0, Roof = true, Perches = roof, Kind = BirdKind.Sparrow, X = Mathf.FloorToInt(roof[0].X / Ts) });
        int center = (TileWorld.PadFrom + TileWorld.PadTo) / 2;
        foreach (int dir in new[] { 1, -1 })
        {
            for (int x = center + dir * rng.RandiRange(108, 124); x > 24 && x < TileWorld.Width - 24; x += dir * rng.RandiRange(88, 168))
            {
                if (world.KickerNear(x, 10) is Kicker k) x = (dir > 0 ? k.Max : k.Min) + dir * 14;
                if (x <= 24 || x >= TileWorld.Width - 24) break;
                list.Add(new Flock { Index = list.Count, X = x, Kind = KindFor(TileWorld.BiomeAt(x)) });
            }
        }
        return list;
    }

    public static BirdKind KindFor(Biome biome) => biome switch
    {
        Biome.Desert => BirdKind.Crow,
        Biome.Frozen => BirdKind.Bunting,
        _ => BirdKind.Sparrow,
    };

    public static string KindName(BirdKind kind) => kind switch
    {
        BirdKind.Crow => "crows",
        BirdKind.Bunting => "snow buntings",
        _ => "sparrows",
    };

    /// <summary>(Re)seats a flock's birds on their spot.</summary>
    private static void Perch(Flock f, RandomNumberGenerator rng)
    {
        f.Birds.Clear();
        f.Scattered = false;
        int count = f.Roof ? f.Perches.Length : f.Kind == BirdKind.Crow ? rng.RandiRange(2, 3) : rng.RandiRange(3, 6);
        float spread = f.Kind == BirdKind.Crow ? 30f : 22f;
        for (int i = 0; i < count; i++)
        {
            var b = new Bird
            {
                Offset = f.Roof ? 0f : (i - (count - 1) / 2f) * spread + rng.RandfRange(-5f, 5f),
                Face = rng.Randf() < 0.5f ? -1 : 1,
                NextIdle = rng.RandfRange(0.2f, 2f),
            };
            b.Pos = f.Roof ? f.Perches[i] : new Vector2(f.CenterX + b.Offset, 0f);
            f.Birds.Add(b);
        }
    }

    /// <summary>
    /// Something scary at <paramref name="at"/> (the car coming, the player walking up, a honk, a crash):
    /// every perched flock with a bird within <paramref name="radius"/> px takes off, away from it.
    /// </summary>
    public void Startle(Vector2 at, float radius, string cause)
    {
        foreach (var f in _all)
        {
            if (f.Scattered || Mathf.Abs(f.CenterX - at.X) > radius + 120f) continue;
            float nearest = float.MaxValue;
            foreach (var b in f.Birds) nearest = Mathf.Min(nearest, b.Pos.DistanceTo(at));
            if (nearest <= radius) Scatter(f, at, cause, nearest);
        }
    }

    private void Scatter(Flock f, Vector2 from, string cause, float distance)
    {
        f.Scattered = true;
        f.RespawnAt = Clock + RespawnSeconds;
        Scatters++;
        int i = 0;
        foreach (var b in f.Birds)
        {
            float side = b.Pos.X - from.X;
            int dir = Mathf.Abs(side) > 4f ? Mathf.Sign(side) : (_rng.Randf() < 0.5f ? -1 : 1);
            b.Flying = true;
            b.Face = dir;
            b.Delay = i++ * 0.05f + _rng.RandfRange(0f, 0.12f);
            float lift = f.Kind == BirdKind.Crow ? 0.8f : 1f;
            b.Vel = new Vector2(dir * _rng.RandfRange(110f, 210f), -_rng.RandfRange(200f, 290f) * lift);
            b.Pos += new Vector2(0f, -6f);   // from the feet to the middle of the bird
            b.Anim = _rng.Randf();
        }

        if (Sfx != null)
        {
            // Quieter the further away from the camera's focus.
            float far = Mathf.Clamp(f.Birds[0].Pos.DistanceTo(Focus) / 1400f, 0f, 1f);
            float db = -7f - 14f * far;
            switch (f.Kind)
            {
                case BirdKind.Crow:
                    Sfx.Play("flutter", db, 0.05f, 0.7f);
                    Sfx.Play("caw", db - 1f, 0.3f);
                    break;
                case BirdKind.Bunting:
                    Sfx.Play("flutter", db, 0.05f, 1.1f);
                    Sfx.Play("chirp", db - 3f, 0.2f, 1.18f);
                    break;
                default:
                    Sfx.Play("flutter", db, 0.05f);
                    Sfx.Play("chirp", db - 3f, 0.2f);
                    break;
            }
        }
        Scattered?.Invoke(f, cause, distance);
    }

    /// <summary>The nearest perched flock ahead of <paramref name="at"/> in direction <paramref name="dir"/>, within <paramref name="range"/> px.</summary>
    public Flock PerchedAhead(Vector2 at, int dir, float range)
    {
        Flock best = null;
        float bestD = range;
        foreach (var f in _all)
        {
            if (f.Scattered || f.Roof) continue;
            float d = (f.CenterX - at.X) * dir;
            if (d > 0f && d < bestD)
            {
                bestD = d;
                best = f;
            }
        }
        return best;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        double now = Clock;
        int inAir = 0;
        bool asleep = Night > 0.5f;
        foreach (var f in _all)
        {
            if (f.Scattered)
            {
                bool allGone = true;
                foreach (var b in f.Birds)
                {
                    if (b.Gone) continue;
                    allGone = false;
                    Fly(f, b, dt);
                    if (!b.Gone) inAir++;
                }
                // Back on their spot after a while, out of sight, so nothing pops in in front of the player.
                if (allGone && now >= f.RespawnAt && Mathf.Abs(f.CenterX - Focus.X) > HiddenDistance)
                    Perch(f, _rng);
                continue;
            }
            if (asleep || Mathf.Abs(f.CenterX - Focus.X) > DrawDistance) continue;
            foreach (var b in f.Birds) Idle(f, b, dt);
        }
        InAir = inAir;
        MostInAir = Math.Max(MostInAir, inAir);
        RefreshGround(DrawDistance + 900f);
        QueueRedraw();
    }

    private void Fly(Flock f, Bird b, float dt)
    {
        if (b.Delay > 0f)
        {
            b.Delay -= dt;
            return;
        }
        b.Anim += dt;
        float climb = f.Kind == BirdKind.Crow ? -110f : -150f;
        // Flap upward and away, with a little bob on each wing beat.
        b.Vel.Y = Mathf.MoveToward(b.Vel.Y, climb + 50f * Mathf.Sin(b.Anim * 9f), 260f * dt);
        b.Vel.X = Mathf.MoveToward(b.Vel.X, b.Face * (f.Kind == BirdKind.Crow ? 190f : 240f), 140f * dt);
        b.Pos += b.Vel * dt;
        if (b.Anim > 7f || Mathf.Abs(b.Pos.X - Focus.X) > 2400f || b.Pos.Y < Focus.Y - 1400f) b.Gone = true;
    }

    private void Idle(Flock f, Bird b, float dt)
    {
        if (b.PeckLeft > 0f) b.PeckLeft -= dt;
        if (b.HopLeft > 0f)
        {
            b.HopLeft = Mathf.Max(0f, b.HopLeft - dt);
            float k = 1f - b.HopLeft / 0.18f;
            b.Offset = Mathf.Lerp(b.HopFrom, b.HopTo, k);
            if (!f.Roof) b.Pos.X = f.CenterX + b.Offset;
        }
        b.NextIdle -= dt;
        if (b.NextIdle > 0f) return;
        b.NextIdle = _rng.RandfRange(0.5f, 2.4f);
        float roll = _rng.Randf();
        if (roll < 0.5f) b.PeckLeft = 0.22f;
        else if (roll < 0.8f && !f.Roof)
        {
            // A little hop, staying near the flock's spot.
            b.HopFrom = b.Offset;
            b.HopTo = Mathf.Clamp(b.Offset + b.Face * _rng.RandfRange(4f, 9f), -50f, 50f);
            b.HopLeft = 0.18f;
        }
        else b.Face = -b.Face;
    }

    /// <summary>Keeps perched birds on the ground when it is dug away or built up.</summary>
    private void RefreshGround(float within)
    {
        foreach (var f in _all)
        {
            if (f.Roof || f.Scattered || Mathf.Abs(f.CenterX - Focus.X) > within) continue;
            foreach (var b in f.Birds)
            {
                int col = Mathf.FloorToInt(b.Pos.X / Ts);
                b.Pos.Y = World.GroundY(b.Pos.X, (World.SurfaceRow(col) - 14) * Ts);
            }
        }
    }

    public override void _Draw()
    {
        Texture2D sheet = Art.Birds;
        foreach (var f in _all)
        {
            if (!f.Scattered && Mathf.Abs(f.CenterX - Focus.X) > DrawDistance) continue;
            float scale = f.Kind == BirdKind.Crow ? 2f : 1f;
            int row = (int)f.Kind;
            foreach (var b in f.Birds)
            {
                if (b.Gone) continue;
                int frame;
                float hop = 0f;
                Vector2 at = b.Pos;
                if (b.Flying && b.Delay <= 0f)
                {
                    float rate = f.Kind == BirdKind.Crow ? 6f : 11f;
                    frame = (int)(b.Anim * rate * 2f) % 2 == 0 ? 2 : 3;
                    at += new Vector2(0f, 8f * scale);   // the sprite's feet line sits under the bird's middle
                }
                else
                {
                    frame = b.PeckLeft > 0f ? 1 : 0;
                    if (b.HopLeft > 0f) hop = -4f * Mathf.Sin(Mathf.Pi * (1f - b.HopLeft / 0.18f));
                    if (b.Flying) at += new Vector2(0f, 6f);   // startled, about to go: still on its feet
                }
                DrawSetTransform(at + new Vector2(0f, hop + 1f), 0f, new Vector2(b.Face * scale, scale));
                DrawTextureRectRegion(sheet, new Rect2(-Cell / 2f, -Cell, Cell, Cell), new Rect2(frame * Cell, row * Cell, Cell, Cell));
            }
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
