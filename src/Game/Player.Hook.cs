using Gearheart.World;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// The grappling hook (E, or the right shoulder on a gamepad). The hook flies toward the aim; if it
/// bites rock within <see cref="HookRange"/> it reels the mechanic in fast, then holds them there
/// (they can mine from the rope). If it bit the lip of a ledge with room on top, the mechanic climbs
/// onto it. Space jumps off, S lets go, and firing again while hanging chains to the next hold
/// (the old one holds until the new one bites).
/// </summary>
public partial class Player
{
    public enum HookState { None, Pull, Hang, Mantle }

    public const float HookRange = 256f;        // 4 m
    private const float HookSpeed = 1500f;      // how fast the hook flies, px/s
    private const float PullSpeed = 640f;       // how fast it reels you in, px/s
    private const float ArriveDistance = 18f;

    /// <summary>Raised when the hook is thrown (direction).</summary>
    public event System.Action<Vector2> HookFired;
    /// <summary>Raised when the hook bites (anchor point, tile, distance from the hand).</summary>
    public event System.Action<Vector2, Tile, float> HookLatched;
    /// <summary>Raised when the hook flew its full length without finding rock.</summary>
    public event System.Action<Vector2> HookMissed;
    /// <summary>Raised when the reel-in ends: "hang", "mantle" (climbing onto the ledge), "blocked".</summary>
    public event System.Action<string, Vector2> HookArrived;
    /// <summary>Raised when the mechanic lets go of the rope: "jump", "drop", "rock gone", "car".</summary>
    public event System.Action<string> HookReleased;

    public HookState Hook { get; private set; }

    /// <summary>Where the hook is (flying) or holds (on the rope); null when it is put away.</summary>
    public Vector2? HookPoint
    {
        get
        {
            if (_hookFlying) return _hookPos;
            if (Hook is HookState.Pull or HookState.Hang) return _anchor;
            return null;
        }
    }
    public bool HookFlying => _hookFlying;

    private bool _hookFlying;
    private Vector2 _hookPos, _hookDir;
    private float _hookFlown;
    private float _hookRetract;            // a miss: the rope snaps back for a moment
    private Vector2 _anchor;               // the hold the mechanic is on
    private Vector2I _anchorTile;
    private Vector2 _mantleTo;
    private float _stall;                  // reeling in but not moving (something in the way)
    private bool _grappleHeld;             // edge detection for the human key
    private bool _jumpPrev;                // jumping off the rope needs a fresh press

    private Vector2 Hand => Position + new Vector2(_facingLeft ? -6f : 6f, -8f);

    /// <summary>Lets go of everything (getting into the car, respawning).</summary>
    public void ReleaseHook(string why)
    {
        bool had = Hook != HookState.None;
        Hook = HookState.None;
        _hookFlying = false;
        if (had) HookReleased?.Invoke(why);
    }

    private void FireHook(Vector2 aim)
    {
        Vector2 dir = aim - Hand;
        if (dir.LengthSquared() < 1f) dir = new Vector2(_facingLeft ? -0.5f : 0.5f, -1f);
        _hookDir = dir.Normalized();
        _hookPos = Hand;
        _hookFlown = 0f;
        _hookFlying = true;
        _hookRetract = 0f;
        if (Mathf.Abs(_hookDir.X) > 0.2f) _facingLeft = _hookDir.X < 0f;
        HookFired?.Invoke(_hookDir);
    }

    /// <summary>Moves a flying hook along; it bites the first solid tile it meets.</summary>
    private void FlyHook(float dt)
    {
        if (_hookRetract > 0f) _hookRetract -= dt;
        if (!_hookFlying) return;

        float step = HookSpeed * dt;
        const float Sub = 4f;
        for (float done = 0f; done < step; done += Sub)
        {
            Vector2 next = _hookPos + _hookDir * Sub;
            int tx = Mathf.FloorToInt(next.X / Ts), ty = Mathf.FloorToInt(next.Y / Ts);
            if (World.Solid(tx, ty))
            {
                _hookFlying = false;
                _anchor = _hookPos;
                _anchorTile = new Vector2I(tx, ty);
                Hook = HookState.Pull;
                _stall = 0f;
                HookLatched?.Invoke(_anchor, World.Get(tx, ty), _hookFlown);
                return;
            }
            _hookPos = next;
            _hookFlown += Sub;
            if (_hookFlown >= HookRange)
            {
                _hookFlying = false;
                _hookRetract = 0.18f;
                HookMissed?.Invoke(_hookPos);
                return;
            }
        }
    }

    /// <summary>
    /// The hook's say over the mechanic's velocity this tick. Returns true when it is in charge
    /// (no walking and no gravity while on the rope).
    /// </summary>
    private bool HookMotion(PlayerInput input, float dt)
    {
        if (Hook == HookState.None) return false;

        // The rock it bit was mined away (or the hold broke): fall.
        if (!World.Solid(_anchorTile.X, _anchorTile.Y) && Hook != HookState.Mantle)
        {
            ReleaseHook("rock gone");
            return false;
        }

        if (input.Jump && !_jumpPrev && Hook != HookState.Mantle)
        {
            // Off a hang: a normal jump. Mid reel-in: a fling that keeps the rope's speed.
            Velocity = Hook == HookState.Pull
                ? new Vector2(Velocity.X, Mathf.Max(Mathf.Min(Velocity.Y, 0f) - JumpSpeed * 0.6f, -JumpSpeed * 1.4f))
                : new Vector2(input.Move * WalkSpeed, -JumpSpeed * 0.95f);
            Hook = HookState.None;
            HookReleased?.Invoke("jump");
            return false;
        }
        if (input.Down && Hook != HookState.Mantle)
        {
            ReleaseHook("drop");
            return false;
        }

        switch (Hook)
        {
            case HookState.Pull:
            {
                Vector2 to = _anchor - Position;
                float dist = to.Length();
                if (dist <= ArriveDistance)
                {
                    Arrive("hang");
                    break;
                }
                Velocity = Velocity.MoveToward(to / dist * PullSpeed, 5000f * dt);
                // Never past the hold in one step (at low frame rates too): arrive next tick instead.
                float reach = (dist - ArriveDistance * 0.5f) / dt;
                if (Velocity.Length() > reach) Velocity = Velocity.Normalized() * reach;
                break;
            }
            case HookState.Hang:
                Velocity = Vector2.Zero;
                break;
            case HookState.Mantle:
            {
                Vector2 to = _mantleTo - Position;
                if (to.Length() < 3f)
                {
                    Position = _mantleTo;
                    Velocity = Vector2.Zero;
                    Hook = HookState.None;
                    HookReleased?.Invoke("climbed up");
                    return true;
                }
                // Climbing over the lip: moved by hand, not by the collision code (the body brushes the corner).
                Position += to.Normalized() * Mathf.Min(420f * dt, to.Length());
                Velocity = Vector2.Zero;
                break;
            }
        }
        return true;
    }

    /// <summary>After moving: notices a reel-in that is stuck against a wall.</summary>
    private void HookAfterMove(Vector2 before, float dt)
    {
        if (Hook != HookState.Pull) return;
        float moved = Position.DistanceTo(before);
        _stall = moved < PullSpeed * dt * 0.25f ? _stall + dt : 0f;
        if (_stall > 0.1f) Arrive("blocked");
    }

    /// <summary>The reel-in is over: climb onto the ledge if the hook bit one, else hang.</summary>
    private void Arrive(string how)
    {
        int ax = _anchorTile.X, ay = _anchorTile.Y;
        float top = ay * Ts;
        bool roomOnTop = !World.Solid(ax, ay - 1) && !World.Solid(ax, ay - 2) && !World.Solid(ax, ay - 3);
        if (roomOnTop && Position.Y + HalfHeight > top - 2f)
        {
            // Stand on the ledge, a little in from its lip so the body clears the rock.
            for (int side = 0; side < 3; side++)
            {
                float cx = (ax + 0.5f) * Ts + (side == 1 ? -6f : side == 2 ? 6f : 0f);
                var stand = new Vector2(cx, top - HalfHeight - 0.01f);
                if (Blocked(stand.X, stand.Y)) continue;
                Hook = HookState.Mantle;
                _mantleTo = stand;
                HookArrived?.Invoke("mantle", Position);
                return;
            }
        }
        Hook = HookState.Hang;
        Velocity = Vector2.Zero;
        HookArrived?.Invoke(how == "blocked" ? "blocked" : "hang", Position);
    }

    /// <summary>Called once per tick before movement: fire, fly and steer the hook.</summary>
    private bool UpdateHook(PlayerInput input, float dt)
    {
        if (input.Grapple) FireHook(input.Aim);
        FlyHook(dt);
        bool inCharge = HookMotion(input, dt);
        _jumpPrev = input.Jump;
        return inCharge;
    }

    /// <summary>The rope and the hook (in local coordinates).</summary>
    private void DrawHook()
    {
        Vector2 hand = Hand - Position;
        Vector2? end = null;
        if (_hookFlying) end = _hookPos - Position;
        else if (Hook is HookState.Pull or HookState.Hang) end = _anchor - Position;
        else if (_hookRetract > 0f) end = hand + (_hookPos - Hand) * (_hookRetract / 0.18f);
        if (end is not Vector2 e) return;

        var rope = new Color(0.55f, 0.42f, 0.28f);
        var dark = new Color(0.25f, 0.18f, 0.12f);
        Vector2 d = e - hand;
        float len = d.Length();
        if (len < 1f) return;
        Vector2 n = new Vector2(-d.Y, d.X) / len;
        // A taut rope while it holds, a little slack wave while it flies or snaps back.
        bool slack = _hookFlying || Hook == HookState.None;
        int segs = Mathf.Max(2, (int)(len / 6f));
        var pts = new Vector2[segs + 1];
        for (int i = 0; i <= segs; i++)
        {
            float t = i / (float)segs;
            float wave = slack ? Mathf.Sin(t * Mathf.Pi * 3f + _animTime * 30f) * 2.2f * Mathf.Sin(t * Mathf.Pi) : 0f;
            pts[i] = hand + d * t + n * wave;
        }
        DrawPolyline(pts, dark, 2.6f);
        DrawPolyline(pts, rope, 1.4f);
        // Rope twist marks.
        for (int i = 1; i < segs; i += 2) DrawLine(pts[i] - n * 0.8f, pts[i] + n * 0.8f, dark, 1f);

        // The hook: a steel shank with two curved prongs pointing along the throw.
        Vector2 f = d / len;
        Vector2 side = new(-f.Y, f.X);
        var steel = new Color(0.78f, 0.8f, 0.84f);
        var shadow = new Color(0.35f, 0.36f, 0.4f);
        Vector2 tip = e;
        Vector2 back = tip - f * 7f;
        DrawLine(back, tip, shadow, 3f);
        DrawLine(back, tip, steel, 1.6f);
        foreach (float s in new[] { -1f, 1f })
        {
            Vector2 p1 = tip - f * 1.5f;
            Vector2 p2 = p1 + side * s * 4f - f * 2f;
            Vector2 p3 = p2 - f * 3f + side * s * 0.5f;
            DrawPolyline(new[] { p1, p2, p3 }, shadow, 2.6f);
            DrawPolyline(new[] { p1, p2, p3 }, steel, 1.3f);
        }
        DrawCircle(tip, 1.4f, steel);
    }
}
