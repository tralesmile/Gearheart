using System;
using System.Collections.Generic;
using Gearheart.Core;
using Godot;

namespace Gearheart.World;

/// <summary>One bat: hanging from a cavern ceiling, or flying off in a jittery weave.</summary>
public sealed class Bat
{
    public Vector2 Pos;            // hanging: the point it hangs from (the ceiling); flying: its middle
    public Vector2 Hang;           // where it roosts
    public Vector2 Vel;
    public bool Flying, Gone;
    public float Delay;            // seconds before it drops off the ceiling after the roost is startled
    public float Anim, Life, Phase;
    public int Dir = 1;            // flying: 1 = right, -1 = left
    public float NextTwitch, TwitchLeft;
    public float SwoopY;           // flying: the height of whatever startled it; the first second it dives toward it
}

/// <summary>A few bats hanging together from one stretch of cavern ceiling in the Deep.</summary>
public sealed class Roost
{
    public int Index;
    public int X, Y;               // the tile column and the first air row under the ceiling
    public readonly List<Bat> Bats = new();
    public bool Burst;
    public double RespawnAt;
    public Vector2 Center => new((X + 0.5f) * TileWorld.TileSize, Y * TileWorld.TileSize);
}

/// <summary>
/// Cave bats: small colonies hanging from the ceilings of the Deep's caverns. Walk (or drive) up to
/// them with a clear line of sight, dig into their cavern, or honk nearby, and they drop off the
/// ceiling and burst out squeaking, flitting round the cavern (bouncing off its walls) before they
/// vanish into the dark. They come back after a while, out of sight. Life underground: they do not
/// touch physics or score. Like the birds, the list lives for the game session.
/// </summary>
public partial class Bats : Node2D
{
    public const double RespawnSeconds = 90.0;
    public const float SightReachFoot = 3.5f * 64f, SightReachCar = 5f * 64f, DigReach = 3f * 64f;
    private const float Ts = TileWorld.TileSize;
    private const float DrawDistance = 1400f, HiddenDistance = 1800f;
    private const int Cell = 16, Row = 3;   // the bat row of the birds sheet

    public TileWorld World;
    public Sfx Sfx;

    /// <summary>The camera focus (car or player): drawing range, and where sounds are heard from.</summary>
    public Vector2 Focus;

    /// <summary>Called when a roost bursts: the roost, what startled it, and how far away that was (px).</summary>
    public Action<Roost, string, float> Startled;

    private static List<Roost> _all;
    private static TileWorld _placedFor;
    private readonly RandomNumberGenerator _rng = new() { Seed = 919 };

    public IReadOnlyList<Roost> All => _all;
    public int Bursts { get; private set; }
    public int InAir { get; private set; }
    public int MostInAir { get; private set; }
    public int WallBounces { get; private set; }

    private static double Clock => Time.GetTicksMsec() / 1000.0;

    public override void _Ready()
    {
        if (_all == null || _placedFor != World)
        {
            _all = Place(World);
            _placedFor = World;
            foreach (var r in _all) Hang(r, _rng);
        }
    }

    public override void _ExitTree() => _rng.Dispose();

    /// <summary>
    /// A ceiling spot in this column: plain rock (not crystal) above, at least four rows of open air
    /// below it, in the Deep. Returns the first air row, or -1.
    /// </summary>
    public static int CeilingRow(TileWorld world, int x, int fromRow)
    {
        for (int y = Mathf.Max(fromRow, world.DeepRow(x) + 1); y < TileWorld.Height - 6; y++)
        {
            Tile above = world.Get(x, y - 1);
            if (above == Tile.Air || above == Tile.Crystal || TileWorld.IsStash(above)) continue;
            if (world.Solid(x, y) || world.Solid(x, y + 1) || world.Solid(x, y + 2) || world.Solid(x, y + 3)) continue;
            return y;
        }
        return -1;
    }

    /// <summary>A roost about every 9–15 m along the Deep where a cavern ceiling has room, roughly every other one.</summary>
    public static List<Roost> Place(TileWorld world)
    {
        var list = new List<Roost>();
        int last = -1000;
        for (int x = 8; x < TileWorld.Width - 8; x++)
        {
            if (x - last < 36) continue;
            ulong h = (ulong)x * 2654435761UL ^ 0x5bd1e995UL;
            h ^= h >> 13;
            if (h % 100 >= 12) continue;   // try only a few columns, so roosts spread out
            int y = CeilingRow(world, x, 0);
            if (y < 0) continue;
            // Room for at least three bats on the same ceiling row (a pair reads as a smudge, not a colony).
            int room = 0;
            for (int dx = -3; dx <= 3; dx++) if (CeilingRow(world, x + dx, y) == y) room++;
            if (room < 3) continue;
            list.Add(new Roost { Index = list.Count, X = x, Y = y });
            last = x;
        }
        return list;
    }

    /// <summary>(Re)hangs a roost's bats along its ceiling.</summary>
    private void Hang(Roost r, RandomNumberGenerator rng)
    {
        r.Bats.Clear();
        r.Burst = false;
        var spots = new List<int>();
        for (int dx = -3; dx <= 3; dx++) if (CeilingRow(World, r.X + dx, r.Y) == r.Y) spots.Add(r.X + dx);
        int count = Mathf.Min(spots.Count, rng.RandiRange(3, 6));
        for (int i = 0; i < count; i++)
        {
            int at = rng.RandiRange(0, spots.Count - 1);
            int x = spots[at];
            spots.RemoveAt(at);
            var hang = new Vector2((x + rng.RandfRange(0.25f, 0.75f)) * Ts, r.Y * Ts);
            r.Bats.Add(new Bat { Pos = hang, Hang = hang, NextTwitch = rng.RandfRange(0.5f, 4f), Phase = rng.Randf() * 6f });
        }
    }

    /// <summary>Tiles of rock between two points (a few samples per tile).</summary>
    private int RockBetween(Vector2 a, Vector2 b)
    {
        int steps = Mathf.CeilToInt(a.DistanceTo(b) / (Ts * 0.5f));
        int rock = 0;
        Vector2I lastTile = new(int.MinValue, 0);
        for (int i = 1; i < steps; i++)
        {
            Vector2 p = a.Lerp(b, i / (float)steps);
            var t = new Vector2I(Mathf.FloorToInt(p.X / Ts), Mathf.FloorToInt(p.Y / Ts));
            if (t == lastTile) continue;
            lastTile = t;
            if (World.Solid(t.X, t.Y)) rock++;
        }
        return rock;
    }

    /// <summary>
    /// Something at <paramref name="at"/>: every hanging roost with a bat within <paramref name="radius"/> px
    /// bursts. With <paramref name="needSight"/> the bat must see it (at most one tile of rock in between).
    /// </summary>
    public void Startle(Vector2 at, float radius, string cause, bool needSight, Rect2? onScreen = null)
    {
        foreach (var r in _all)
        {
            if (r.Burst || Mathf.Abs(r.Center.X - at.X) > radius + 120f || Mathf.Abs(r.Center.Y - at.Y) > radius + 120f) continue;
            float nearest = float.MaxValue, reach = float.MaxValue;
            Bat seen = null;
            foreach (var b in r.Bats)
            {
                float d = b.Hang.DistanceTo(at);
                // Looking up reaches further (an ellipse 1.4x as tall): a colony high in a tall cavern still sees you.
                Vector2 off = b.Hang - at;
                float e = needSight && off.Y < 0f ? new Vector2(off.X, off.Y / 1.4f).Length() : d;
                if (e < reach) { reach = e; nearest = d; seen = b; }
            }
            if (seen == null || reach > radius) continue;
            if (needSight && RockBetween(seen.Hang + new Vector2(0f, 6f), at) > 1) continue;
            // Seen before they go: a colony only notices you once it is across your screen (when a view is given).
            // Above the top edge is fine: they swoop down into view (Fly).
            if (onScreen is Rect2 view && (seen.Hang.X < view.Position.X || seen.Hang.X > view.End.X || seen.Hang.Y + 12f > view.End.Y)) continue;
            Burst(r, at, cause, nearest);
        }
    }

    private void Burst(Roost r, Vector2 from, string cause, float distance)
    {
        r.Burst = true;
        r.RespawnAt = Clock + RespawnSeconds;
        Bursts++;
        int i = 0;
        foreach (var b in r.Bats)
        {
            float side = b.Hang.X - from.X;
            b.Dir = Mathf.Abs(side) > 6f ? Mathf.Sign(side) : (_rng.Randf() < 0.5f ? -1 : 1);
            if (_rng.Randf() < 0.5f) b.Dir = -b.Dir;   // half of them dart the other way, right past you
            b.Flying = true;
            b.Delay = i++ * 0.07f + _rng.RandfRange(0f, 0.15f);
            b.Pos = b.Hang + new Vector2(0f, 8f);
            b.Vel = new Vector2(b.Dir * _rng.RandfRange(40f, 90f), _rng.RandfRange(110f, 170f));   // drop off the ceiling first
            b.Life = _rng.RandfRange(3.2f, 4.8f);
            b.SwoopY = from.Y + _rng.RandfRange(-28f, 4f);
            b.Anim = 0f;
        }

        if (Sfx != null)
        {
            float far = Mathf.Clamp(r.Center.DistanceTo(Focus) / 900f, 0f, 1f);
            float db = -6f - 12f * far;
            Sfx.Play("flutter", db, 0.05f, 1.45f);
            Sfx.Play("squeak", db - 1f, 0.15f);
        }
        Startled?.Invoke(r, cause, distance);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        double now = Clock;
        int inAir = 0;
        foreach (var r in _all)
        {
            if (r.Burst)
            {
                bool allGone = true;
                foreach (var b in r.Bats)
                {
                    if (b.Gone) continue;
                    allGone = false;
                    Fly(b, dt);
                    if (!b.Gone && b.Delay <= 0f) inAir++;
                }
                if (allGone && now >= r.RespawnAt && r.Center.DistanceTo(Focus) > HiddenDistance && CeilingRow(World, r.X, r.Y) == r.Y)
                    Hang(r, _rng);
                continue;
            }
            if (r.Center.DistanceTo(Focus) > DrawDistance) continue;
            foreach (var b in r.Bats)
            {
                // Now and then a bat stretches a wing.
                if (b.TwitchLeft > 0f) b.TwitchLeft -= dt;
                b.NextTwitch -= dt;
                if (b.NextTwitch <= 0f)
                {
                    b.NextTwitch = _rng.RandfRange(1.5f, 5f);
                    b.TwitchLeft = 0.35f;
                }
                b.Anim += dt;
            }
            // Its ceiling dug away: the bats go.
            if (r.Bats.Count > 0 && CeilingRow(World, r.X, r.Y) != r.Y) Burst(r, r.Center + new Vector2(0f, -Ts), "ceiling", 0f);
        }
        InAir = inAir;
        MostInAir = Math.Max(MostInAir, inAir);
        QueueRedraw();
    }

    /// <summary>Bats (hanging or flying) inside a world-space rectangle, e.g. the camera's view.</summary>
    public int CountIn(Rect2 view)
    {
        int n = 0;
        foreach (var r in _all)
            foreach (var b in r.Bats)
                if (!b.Gone && b.Delay <= 0f && view.HasPoint(b.Flying ? b.Pos : b.Hang + new Vector2(0f, 12f))) n++;
        return n;
    }

    private void Fly(Bat b, float dt)
    {
        if (b.Delay > 0f)
        {
            b.Delay -= dt;
            return;
        }
        b.Anim += dt;
        if (b.Anim >= b.Life)
        {
            b.Gone = true;
            return;
        }
        // A jittery weave: fast flaps, swerving up and down, now and then a sudden turn.
        float weave = Mathf.Sin(b.Anim * 7.5f + b.Phase) * 150f + Mathf.Sin(b.Anim * 17f + b.Phase * 2f) * 50f;
        var want = new Vector2(b.Dir * 250f, weave - 40f);
        // The first second: a dive down to the height of whatever startled them, so even a colony high in a
        // tall cavern streams past you on screen; then the weave takes over.
        float swoop = Mathf.Clamp(1f - (b.Anim - 0.9f) / 0.4f, 0f, 1f);
        if (swoop > 0f) want.Y = Mathf.Lerp(want.Y, Mathf.Clamp((b.SwoopY - b.Pos.Y) * 4f, -180f, 320f) + weave * 0.3f, swoop);
        b.Vel = b.Vel.MoveToward(want, 900f * dt);
        if (_rng.Randf() < 0.25f * dt) b.Dir = -b.Dir;

        // Keep to the open cavern: bounce off walls, floor and ceiling (a bat that starts inside rock passes through).
        Vector2 next = b.Pos + b.Vel * dt;
        bool inRock = World.Solid(Mathf.FloorToInt(b.Pos.X / Ts), Mathf.FloorToInt(b.Pos.Y / Ts));
        if (!inRock)
        {
            if (World.Solid(Mathf.FloorToInt(next.X / Ts), Mathf.FloorToInt(b.Pos.Y / Ts)))
            {
                b.Dir = -Mathf.Sign(b.Vel.X == 0f ? b.Dir : b.Vel.X);
                b.Vel.X = b.Dir * Mathf.Abs(b.Vel.X) * 0.7f;
                next.X = b.Pos.X;
                WallBounces++;
            }
            if (World.Solid(Mathf.FloorToInt(next.X / Ts), Mathf.FloorToInt(next.Y / Ts)))
            {
                b.Vel.Y = -b.Vel.Y * 0.7f;
                b.Phase += Mathf.Pi;   // flip the weave so it does not dive straight back in
                next.Y = b.Pos.Y;
            }
        }
        b.Pos = next;
    }

    public override void _Draw()
    {
        Texture2D sheet = Art.Birds;
        const float scale = 1.5f;
        foreach (var r in _all)
        {
            if (r.Center.DistanceTo(Focus) > DrawDistance + 600f) continue;
            foreach (var b in r.Bats)
            {
                if (b.Gone) continue;
                // Drawn above the darkness: dim silhouettes in the dark, full colour in the lamp's light.
                Vector2 where = b.Flying ? b.Pos : b.Hang;
                float lit = 0.62f + 0.38f * Mathf.Clamp(1f - (where.DistanceTo(Focus) - 120f) / 260f, 0f, 1f);
                Color tint = new(lit, lit * 0.95f, lit, 1f);
                if (b.Flying && b.Delay <= 0f)
                {
                    int frame = (int)(b.Anim * 22f) % 2 == 0 ? 2 : 3;
                    tint.A = Mathf.Clamp((b.Life - b.Anim) / 0.8f, 0f, 1f);   // fades into the dark at the end
                    DrawSetTransform(b.Pos, 0f, new Vector2(Mathf.Sign(b.Vel.X == 0f ? b.Dir : b.Vel.X) * scale, scale));
                    DrawTextureRectRegion(sheet, new Rect2(-Cell / 2f, -Cell / 2f, Cell, Cell), new Rect2(frame * Cell, Row * Cell, Cell, Cell), tint);
                }
                else
                {
                    // Hanging upside down from the ceiling, swaying a little.
                    int frame = b.TwitchLeft > 0f ? 1 : 0;
                    float sway = 0.08f * Mathf.Sin(b.Anim * 1.7f + b.Phase);
                    DrawSetTransform(b.Hang, sway, new Vector2(scale, scale));
                    DrawTextureRectRegion(sheet, new Rect2(-Cell / 2f, 0f, Cell, Cell), new Rect2(frame * Cell, Row * Cell, Cell, Cell), tint);
                }
            }
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
