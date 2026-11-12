using System;
using System.Collections.Generic;
using Gearheart.Core;
using Godot;

namespace Gearheart.World;

/// <summary>One whirlwind of sand wandering along The Wastes road.</summary>
public sealed class DustDevil
{
    public int Index;
    public float ZoneFrom, ZoneTo;   // world x it wanders between (px)
    public float X;                  // world x of its foot (px)
    public float GroundY;            // world y of the ground under it
    public float Vx;                 // drift, px/s
    public int Spin = 1;             // 1 = the front of the column sweeps right (the car tips nose-down facing right)
    public float Strength;           // 0..1: grows when it forms, shrinks when it dies or collapses
    public float Life;               // seconds left before it dies down by itself
    public float Rest;               // seconds before the next one forms in this zone (while gone)
    public float NextTurn;           // seconds until it picks a new drift
    public bool Alive;
    public bool Collapsing;          // spent: it threw something and is blowing itself out
    public float Held;               // seconds it has been holding the car this time
    public int Formed, Collapses, Throws;
    public float Phase;              // swirl animation
    public float Height => 470f * (0.35f + 0.65f * Strength);
    public float Radius => 70f + 45f * Strength;
}

/// <summary>
/// Dust devils: on hot afternoons (about 10:30–17:30) whirlwinds of sand form in The Wastes and
/// wander up and down the road. Drive into one and it grabs the car: lifts it a couple of metres,
/// slows it and twists it, then throws it out and blows itself out in a burst of sand. Fight the
/// spin with Q/E and land flat, or ride it into a flip. They are seen from far away (a tall column
/// over the dunes), so you can wait for one to wander off or die down, or hit it on purpose.
/// The physics push is applied by <see cref="Vehicles.SoftVehicle.Whirl"/>; this node keeps the
/// devils and draws them (the back of the swirl behind the car, the front over it).
/// </summary>
public partial class DustDevils : Node2D
{
    public const int Zones = 3;
    private const float Ts = TileWorld.TileSize;
    private const float DrawDistance = 2600f;
    private const int Grains = 90;

    public TileWorld World;

    /// <summary>The camera focus (car or player), for drawing range and sound.</summary>
    public Vector2 Focus;

    /// <summary>0..1 how hot it is (devils form only while it is above zero; see <see cref="HeatAt"/>).</summary>
    public float Heat;

    /// <summary>Called when a devil forms, dies down, or collapses (the devil and what happened).</summary>
    public Action<DustDevil, string> Changed;

    private readonly List<DustDevil> _devils = new();
    private readonly RandomNumberGenerator _rng = new() { Seed = 1907 };
    private readonly float[] _grainU = new float[Grains], _grainA = new float[Grains], _grainS = new float[Grains], _grainR = new float[Grains];
    private readonly List<(Vector2 Pos, Vector2 Vel, float Life, float Size)> _puffs = new();
    private Node2D _front;
    private float _time;

    public IReadOnlyList<DustDevil> All => _devils;

    /// <summary>Afternoon heat: rises from 10:30 to 11:30, falls from 16:30 to 17:30.</summary>
    public static float HeatAt(float hours)
    {
        float up = Mathf.Clamp((hours - 10.5f) / 1f, 0f, 1f);
        float down = Mathf.Clamp((17.5f - hours) / 1f, 0f, 1f);
        return Mathf.Min(up, down);
    }

    public override void _Ready()
    {
        // The Wastes road from about 110 m east of the pad to near the world's end, in three stretches.
        int middle = (TileWorld.PadFrom + TileWorld.PadTo) / 2;
        float from = (middle + TileWorld.MeadowReach + 60) * Ts, to = (TileWorld.Width - 40) * Ts;
        float step = (to - from) / Zones;
        for (int i = 0; i < Zones; i++)
        {
            var d = new DustDevil { Index = i, ZoneFrom = from + i * step + 60f, ZoneTo = from + (i + 1) * step - 60f };
            // Most stretches already have one when you arrive at midday; the others form soon.
            bool now = _rng.Randf() < 0.75f;
            float at = _rng.RandfRange(0.3f, 0.7f);
            if (now && Heat > 0.2f)
            {
                Form(d, at);
                d.Strength = 0.8f * Heat;   // already up and spinning when the game starts at midday
            }
            else d.Rest = _rng.RandfRange(2f, 12f);
            _devils.Add(d);
        }
        for (int i = 0; i < Grains; i++)
        {
            _grainU[i] = Mathf.Pow(_rng.Randf(), 0.8f);
            _grainA[i] = _rng.RandfRange(0f, Mathf.Tau);
            _grainS[i] = _rng.RandfRange(2f, 5.5f);
            _grainR[i] = _rng.RandfRange(0.75f, 1.15f);
        }
        _front = new Node2D { Name = "Front", ZIndex = 4 };   // over the car and the player, under sparks
        _front.Draw += () => DrawLayer(_front, true);
        AddChild(_front);
    }

    public override void _ExitTree() => _rng.Dispose();

    private void Form(DustDevil d, float at)
    {
        d.Alive = true;
        d.Collapsing = false;
        d.Strength = Heat > 0f ? 0.05f : 0f;
        d.X = Mathf.Lerp(d.ZoneFrom, d.ZoneTo, at);
        d.Life = _rng.RandfRange(25f, 45f);
        d.Vx = _rng.RandfRange(45f, 95f) * (_rng.Randf() < 0.5f ? -1f : 1f);
        d.NextTurn = _rng.RandfRange(3f, 7f);
        d.Spin = _rng.Randf() < 0.5f ? -1 : 1;
        d.Held = 0f;
        d.Formed++;
        d.GroundY = Ground(d.X);
        Changed?.Invoke(d, "formed");
    }

    private float Ground(float x) => World.GroundY(x, (World.SurfaceRow(Mathf.FloorToInt(x / Ts)) - 14) * Ts);

    /// <summary>Called every physics frame.</summary>
    public void Step(float dt)
    {
        _time += dt;
        foreach (var d in _devils)
        {
            d.Phase += dt;
            if (!d.Alive)
            {
                d.Rest -= dt;
                if (d.Rest <= 0f && Heat > 0.2f)
                {
                    // Form somewhere in its stretch, but not right on top of the camera.
                    float at = _rng.Randf();
                    float x = Mathf.Lerp(d.ZoneFrom, d.ZoneTo, at);
                    if (Mathf.Abs(x - Focus.X) < 500f) at = Mathf.PosMod(at + 0.5f, 1f);
                    Form(d, at);
                }
                continue;
            }

            d.Life -= dt;
            bool dying = d.Collapsing || d.Life <= 0f || Heat <= 0f;
            float target = dying ? 0f : Mathf.Clamp(Heat, 0f, 1f) * (0.75f + 0.25f * Mathf.Sin(d.Phase * 0.6f + d.Index));
            float rate = d.Collapsing ? 1.4f : dying ? 0.35f : 0.3f;
            d.Strength = Mathf.MoveToward(d.Strength, target, rate * dt);
            if (dying && d.Strength <= 0.01f)
            {
                d.Alive = false;
                d.Strength = 0f;
                d.Rest = _rng.RandfRange(6f, 20f);
                Changed?.Invoke(d, d.Collapsing ? "gone (blown out)" : "died down");
                continue;
            }

            // Wander, turning now and then and at the ends of its stretch.
            d.NextTurn -= dt;
            if (d.NextTurn <= 0f)
            {
                d.NextTurn = _rng.RandfRange(3f, 7f);
                d.Vx = _rng.RandfRange(45f, 95f) * (_rng.Randf() < 0.6f ? -Mathf.Sign(d.Vx) : Mathf.Sign(d.Vx));
            }
            if (d.X < d.ZoneFrom) d.Vx = Mathf.Abs(d.Vx);
            if (d.X > d.ZoneTo) d.Vx = -Mathf.Abs(d.Vx);
            d.X += d.Vx * dt;
            d.GroundY = Mathf.Lerp(d.GroundY, Ground(d.X), 1f - Mathf.Exp(-8f * dt));

            // Kicks up a little sand at its foot.
            if (Mathf.Abs(d.X - Focus.X) < DrawDistance && _rng.Randf() < 12f * dt * d.Strength)
                _puffs.Add((new Vector2(d.X + _rng.RandfRange(-40f, 40f), d.GroundY - 4f),
                            new Vector2(_rng.RandfRange(-90f, 90f) + d.Vx, _rng.RandfRange(-60f, -10f)), _rng.RandfRange(0.6f, 1.2f), _rng.RandfRange(4f, 8f)));
        }

        for (int i = _puffs.Count - 1; i >= 0; i--)
        {
            var p = _puffs[i];
            p.Life -= dt;
            if (p.Life <= 0f) { _puffs.RemoveAt(i); continue; }
            p.Vel *= 1f - 1.5f * dt;
            p.Pos += p.Vel * dt;
            _puffs[i] = p;
        }
    }

    /// <summary>
    /// How hard the devil grips a point (0..1): strongest in the middle of the column and low down,
    /// nothing outside it or above its top.
    /// </summary>
    public static float Grip(DustDevil d, Vector2 at, float widen = 1f)
    {
        if (!d.Alive || d.Strength < 0.05f) return 0f;
        float r = d.Radius * widen;
        float across = 1f - Mathf.Pow((at.X - d.X) / r, 2f);
        if (across <= 0f) return 0f;
        float up = d.GroundY - at.Y;
        float h = d.Height;
        float vertical = Mathf.Clamp(1f - (up - 0.55f * h) / (0.45f * h), 0f, 1f);
        if (up < -80f) vertical = 0f;   // well below the ground (in a tunnel)
        return d.Strength * across * vertical;
    }

    /// <summary>A spent devil blows itself out: a burst of sand, then it is gone.</summary>
    public void Collapse(DustDevil d)
    {
        if (d.Collapsing) return;
        d.Collapsing = true;
        d.Collapses++;
        for (int i = 0; i < 26; i++)
        {
            float a = _rng.RandfRange(0f, Mathf.Tau);
            float u = _rng.Randf();
            _puffs.Add((new Vector2(d.X + Mathf.Cos(a) * 30f, d.GroundY - u * d.Height * 0.8f),
                        new Vector2(Mathf.Cos(a) * _rng.RandfRange(80f, 260f), Mathf.Sin(a) * _rng.RandfRange(40f, 140f) - 30f),
                        _rng.RandfRange(0.8f, 1.6f), _rng.RandfRange(5f, 10f)));
        }
        Changed?.Invoke(d, "collapsed");
    }

    /// <summary>The nearest living devil to a point (or null) and how far away its foot is.</summary>
    public (DustDevil Devil, float Distance) Nearest(Vector2 at)
    {
        DustDevil best = null;
        float bestD = float.MaxValue;
        foreach (var d in _devils)
        {
            if (!d.Alive) continue;
            float dist = new Vector2(d.X - at.X, (d.GroundY - d.Height * 0.4f) - at.Y).Length();
            if (dist < bestD) { bestD = dist; best = d; }
        }
        return (best, bestD);
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
        _front.QueueRedraw();
    }

    public override void _Draw() => DrawLayer(this, false);

    private static readonly Color Sand = new(0.87f, 0.74f, 0.5f), SandDark = new(0.69f, 0.55f, 0.34f), SandLight = new(0.96f, 0.88f, 0.68f);

    /// <summary>
    /// The column: grains on rings that widen with height, sweeping round faster near the ground,
    /// the whole thing swaying. Grains on the far side of a ring are drawn behind the car (darker),
    /// grains on the near side in front.
    /// </summary>
    private void DrawLayer(CanvasItem on, bool front)
    {
        foreach (var d in _devils)
        {
            if (!d.Alive || d.Strength < 0.02f || Mathf.Abs(d.X - Focus.X) > DrawDistance) continue;
            float s = d.Strength, h = d.Height, r0 = d.Radius;
            for (int i = 0; i < Grains; i++)
            {
                float u = _grainU[i];
                float speed = (7.5f - 4f * u) * d.Spin;
                float a = _grainA[i] + d.Phase * speed;
                float depth = Mathf.Sin(a);   // > 0: near side
                if ((depth > 0f) != front) continue;
                float ring = r0 * (0.28f + 0.95f * Mathf.Pow(u, 1.25f)) * _grainR[i];
                float sway = Mathf.Sin(d.Phase * 1.3f + u * 3.2f + d.Index) * 18f * u + Mathf.Sin(d.Phase * 0.7f) * 8f * u;
                float x = d.X + sway + Mathf.Cos(a) * ring;
                float y = d.GroundY - u * h - 6f + Mathf.Sin(a * 2f + i) * 4f;
                float alpha = s * (0.75f - 0.45f * u) * (front ? 0.85f : 0.6f);
                Color c = (front ? (i % 3 == 0 ? SandLight : Sand) : SandDark) with { A = alpha };
                float size = Mathf.Round(_grainS[i] * (0.7f + 0.6f * u));
                on.DrawRect(new Rect2(Mathf.Round(x), Mathf.Round(y), size, size), c);
                // A streak behind fast grains near the ground, so the swirl reads as motion.
                if (u < 0.45f && i % 2 == 0)
                {
                    float trail = -Mathf.Sin(a) * speed * 3f;
                    on.DrawRect(new Rect2(Mathf.Round(x - trail), Mathf.Round(y + 1f), Mathf.Max(2f, size - 2f), Mathf.Max(1f, size - 3f)), c with { A = alpha * 0.5f });
                }
            }
            if (!front)
            {
                // A haze at the foot, where the sand is thickest.
                for (int k = 0; k < 5; k++)
                {
                    float w = r0 * (1.1f + 0.25f * k) + 8f * Mathf.Sin(d.Phase * 3f + k);
                    on.DrawRect(new Rect2(Mathf.Round(d.X - w), Mathf.Round(d.GroundY - 10f - 6f * k), Mathf.Round(2f * w), 6f), Sand with { A = s * (0.32f - 0.05f * k) });
                }
            }
            else
            {
                // A few bits of debris (twigs, a scrap of tin) whirling round higher up.
                for (int k = 0; k < 4; k++)
                {
                    float a = d.Phase * (3.2f + 0.4f * k) * d.Spin + k * 1.7f;
                    float u = 0.35f + 0.15f * k;
                    float x = d.X + Mathf.Cos(a) * r0 * (0.4f + u);
                    float y = d.GroundY - u * h + Mathf.Sin(a * 1.3f) * 10f;
                    var col = k % 2 == 0 ? new Color(0.35f, 0.26f, 0.16f, s) : new Color(0.55f, 0.55f, 0.58f, s);
                    on.DrawRect(new Rect2(Mathf.Round(x), Mathf.Round(y), k % 2 == 0 ? 6f : 4f, k % 2 == 0 ? 2f : 4f), col);
                }
            }
        }
        if (front)
            foreach (var p in _puffs)
            {
                if (Mathf.Abs(p.Pos.X - Focus.X) > DrawDistance) continue;
                float sz = Mathf.Round(p.Size);
                on.DrawRect(new Rect2(Mathf.Round(p.Pos.X), Mathf.Round(p.Pos.Y), sz, sz), Sand with { A = Mathf.Clamp(p.Life, 0f, 1f) * 0.6f });
            }
    }
}
