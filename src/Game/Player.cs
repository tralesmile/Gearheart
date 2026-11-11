using Gearheart.Core;
using Gearheart.World;
using Godot;
using TileType = Gearheart.World.Tile;

namespace Gearheart.Game;

/// <summary>What the player (or a test bot) wants to do this tick.</summary>
public struct PlayerInput
{
    public float Move;        // -1..1
    public bool Jump;
    public bool Mine;         // hold to dig at Aim
    public bool Place;        // place the selected block at Aim
    public Vector2 Aim;       // world position
}

/// <summary>
/// The mechanic on foot: walks, jumps, mines tiles with a pickaxe and places blocks.
/// Movement is a simple box against the tile grid, with ramps followed by the feet
/// and one-tile steps climbed automatically.
/// </summary>
public partial class Player : Node2D
{
    public TileWorld World;

    /// <summary>When set, replaces keyboard and mouse (used by automated playtests).</summary>
    public PlayerInput? Override;

    /// <summary>False while driving: the player neither moves nor draws.</summary>
    public bool Active = true;

    public string SelectedBlock = "Dirt";
    public bool Grounded { get; private set; }
    public Vector2 Velocity;

    /// <summary>Raised with an inventory key and amount when something is picked up.</summary>
    public event System.Action<string, int> Collected;

    /// <summary>Raised when a block is placed.</summary>
    public event System.Action Placed;

    public bool IsMining => _mining;

    public const float HalfWidth = 9f, HalfHeight = 22f;
    private const float WalkSpeed = 150f, Acceleration = 1400f, Gravity = 1250f, JumpSpeed = 430f;
    private const float Reach = 92f;
    private const float PickPower = 46f;       // mining work per second
    private const float Ts = TileWorld.TileSize;

    private bool _facingLeft;
    private float _animTime;
    private bool _mining;
    private Vector2I _target = new(-1, -1);
    private bool _targetValid;
    private float _placeCooldown;

    public override void _PhysicsProcess(double delta)
    {
        if (!Active || World == null) return;
        float dt = (float)delta;
        PlayerInput input = Override ?? ReadHuman();

        // Horizontal movement.
        float wanted = input.Move * WalkSpeed;
        float footing = Grounded && World.GripAt(Position + new Vector2(0f, HalfHeight + 4f)) < 0.5f ? 0.1f : 1f;   // ice is slippery
        Velocity.X = Mathf.MoveToward(Velocity.X, wanted, Acceleration * footing * dt);
        if (Mathf.Abs(input.Move) > 0.1f && !input.Mine) _facingLeft = input.Move < 0f;

        // Jump and gravity.
        if (input.Jump && Grounded) Velocity.Y = -JumpSpeed;
        Velocity.Y = Mathf.Min(Velocity.Y + Gravity * dt, 900f);

        bool wasGrounded = Grounded;
        Grounded = false;
        Vector2 p = Position;

        // X axis, with auto step-up over one-tile ledges.
        float nx = p.X + Velocity.X * dt;
        if (!Blocked(nx, p.Y))
        {
            p.X = nx;
        }
        else
        {
            float feet = p.Y + HalfHeight;
            float rowTop = Mathf.Floor((feet - 0.01f) / Ts) * Ts;
            float lift = feet - rowTop + 0.1f;
            if (wasGrounded && lift <= Ts + 0.2f && !Blocked(nx, p.Y - lift))
            {
                p.X = nx;
                p.Y -= lift;
            }
            else
            {
                Velocity.X = 0f;
            }
        }

        // Y axis.
        float ny = p.Y + Velocity.Y * dt;
        if (!Blocked(p.X, ny))
        {
            p.Y = ny;
        }
        else if (Velocity.Y > 0f)
        {
            p.Y = Mathf.Floor((ny + HalfHeight) / Ts) * Ts - HalfHeight - 0.01f;
            Velocity.Y = 0f;
            Grounded = true;
        }
        else
        {
            p.Y = Mathf.Floor((ny - HalfHeight) / Ts + 1f) * Ts + HalfHeight + 0.01f;
            Velocity.Y = 0f;
        }

        // Ramps: the feet follow the ramp surface under the body's centre.
        if (Velocity.Y >= 0f)
        {
            float feet = p.Y + HalfHeight;
            int tx = Mathf.FloorToInt(p.X / Ts);
            int row = Mathf.FloorToInt(feet / Ts);
            float snapDown = wasGrounded ? 7f : 0f;
            for (int ty = row - 1; ty <= row + 1; ty++)
            {
                float rampY = World.RampY(tx, ty, p.X);
                if (float.IsNaN(rampY) || feet < rampY - snapDown) continue;
                if (feet > rampY + Ts) continue;
                if (Blocked(p.X, rampY - HalfHeight - 0.01f)) continue;
                p.Y = rampY - HalfHeight - 0.01f;
                Velocity.Y = 0f;
                Grounded = true;
                break;
            }
        }

        Position = p;

        UpdateTarget(input);
        _mining = false;
        _placeCooldown -= dt;

        if (input.Mine && _targetValid && World.Solid(_target.X, _target.Y))
        {
            _mining = true;
            _facingLeft = input.Aim.X < Position.X;
            World.Damage(_target.X, _target.Y, PickPower * dt);
        }
        else if (input.Place && _targetValid && _placeCooldown <= 0f)
        {
            TryPlace();
        }

        _animTime += dt;
        QueueRedraw();
    }

    private PlayerInput ReadHuman()
    {
        var input = new PlayerInput { Aim = GetGlobalMousePosition() };
        if (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right)) input.Move += 1f;
        if (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left)) input.Move -= 1f;
        float stick = Input.GetJoyAxis(0, JoyAxis.LeftX);
        if (Mathf.Abs(stick) > 0.25f) input.Move += stick;
        input.Move = Mathf.Clamp(input.Move, -1f, 1f);
        input.Jump = Input.IsPhysicalKeyPressed(Key.Space) || Input.IsPhysicalKeyPressed(Key.W)
                     || Input.IsPhysicalKeyPressed(Key.Up) || Input.IsJoyButtonPressed(0, JoyButton.A);
        input.Mine = Input.IsMouseButtonPressed(MouseButton.Left);
        input.Place = Input.IsMouseButtonPressed(MouseButton.Right);
        return input;
    }

    /// <summary>True if the body box at this centre overlaps a full block.</summary>
    public bool Blocked(float cx, float cy)
    {
        int x0 = Mathf.FloorToInt((cx - HalfWidth) / Ts), x1 = Mathf.FloorToInt((cx + HalfWidth) / Ts);
        int y0 = Mathf.FloorToInt((cy - HalfHeight) / Ts), y1 = Mathf.FloorToInt((cy + HalfHeight) / Ts);
        for (int ty = y0; ty <= y1; ty++)
            for (int tx = x0; tx <= x1; tx++)
                if (World.FullBlock(tx, ty)) return true;
        return false;
    }

    private void UpdateTarget(PlayerInput input)
    {
        _target = new Vector2I(Mathf.FloorToInt(input.Aim.X / Ts), Mathf.FloorToInt(input.Aim.Y / Ts));
        Vector2 center = new((_target.X + 0.5f) * Ts, (_target.Y + 0.5f) * Ts);
        _targetValid = center.DistanceTo(Position) <= Reach;
    }

    private void TryPlace()
    {
        int x = _target.X, y = _target.Y;
        if (World.Solid(x, y) || GameSession.Count(SelectedBlock) <= 0) return;
        if (!World.Solid(x - 1, y) && !World.Solid(x + 1, y) && !World.Solid(x, y - 1) && !World.Solid(x, y + 1)) return;

        // Not inside the player's own body.
        float l = x * Ts, t = y * Ts;
        if (l < Position.X + HalfWidth && l + Ts > Position.X - HalfWidth &&
            t < Position.Y + HalfHeight && t + Ts > Position.Y - HalfHeight) return;

        World.Set(x, y, SelectedBlock == "Stone" ? TileType.Stone : TileType.Dirt);
        GameSession.Add(SelectedBlock, -1);
        _placeCooldown = 0.12f;
        Placed?.Invoke();
    }

    /// <summary>Called by the scene when a tile this player mined breaks.</summary>
    public void Collect(string item, int amount) => Collected?.Invoke(item, amount);

    public override void _Draw()
    {
        if (!Active) return;

        int frame;
        if (_mining) frame = 6 + ((int)(_animTime * 7f) & 1);
        else if (!Grounded) frame = 5;
        else if (Mathf.Abs(Velocity.X) > 12f) frame = 1 + ((int)(_animTime * 9f) & 3);
        else frame = 0;

        DrawSetTransform(Vector2.Zero, 0f, new Vector2(_facingLeft ? -1f : 1f, 1f));
        DrawTextureRectRegion(Art.Player, new Rect2(-12f, -HalfHeight - 3.5f, 32f, 48f), new Rect2(frame * 32, 0, 32, 48));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

        // Highlight the tile in reach under the cursor.
        if (_targetValid && Override == null)
        {
            var rect = new Rect2(new Vector2(_target.X, _target.Y) * Ts - Position, new Vector2(Ts, Ts));
            DrawRect(rect, new Color(1f, 1f, 1f, World.Solid(_target.X, _target.Y) ? 0.9f : 0.35f), false, 1f);
        }
    }
}
