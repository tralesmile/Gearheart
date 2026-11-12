using Gearheart.Core;
using Gearheart.World;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// The grappling hook's side of the world scene: sounds, dust where it bites, a first-time hint,
/// the bot's counters and the --hooktest climb out of a 12 m shaft.
/// </summary>
public partial class WorldScene
{
    private int _hooksThrown, _hooksBit, _hooksMissed, _hookMantles, _hookHangs, _hookJumpOffs;
    private float _hookLongest;
    private bool _hookHinted;

    // --hooktest
    private bool _shaftDug, _shaftOut;
    private int _shaftX, _shaftTop, _shaftBottom;
    private float _shaftStart = -1f, _shaftNextThrow;
    private int _shaftThrowsAtStart;
    private bool _shaftMissTried, _shaftLeftWall;
    private int _flingStage;
    private float _flingFrom;

    private void SetupHook()
    {
        _player.HookFired += dir =>
        {
            _hooksThrown++;
            _sfx.Play("hook", -9f, 0.05f, 1f);
            if (GameSession.AutoPlay)
                GD.Print($"[hook] thrown #{_hooksThrown} at {Mathf.RadToDeg(Mathf.Atan2(-dir.Y, dir.X)):0} deg from ({_player.Position.X / Ts:0.0},{_player.Position.Y / Ts:0.0}) " +
                         $"state={_player.Hook} frame={Engine.GetFramesDrawn()}");
        };
        _player.HookLatched += (at, tile, dist) =>
        {
            _hooksBit++;
            _hookLongest = Mathf.Max(_hookLongest, dist);
            _sfx.Play("clank", -7f, 0.05f, tile is Tile.Stone or Tile.Slate or Tile.Bedrock or Tile.IronOre ? 1.1f : 0.9f);
            ChipAt(at, tile);
            _birds?.Startle(at, 90f, "hook");
            if (!_hookHinted && _toastTime < 1.2f && !_card.Showing)
            {
                _hookHinted = true;
                Toast("GRAPPLING HOOK\nSpace: jump off   S: let go   E: throw again");
            }
            if (GameSession.AutoPlay)
                GD.Print($"[hook] bit {tile} at ({at.X / Ts:0.0},{at.Y / Ts:0.0}) {dist / PixelsPerMeter:0.0} m out frame={Engine.GetFramesDrawn()}");
        };
        _player.HookMissed += at =>
        {
            _hooksMissed++;
            _sfx.Play("hook", -14f, 0.05f, 0.7f);
            if (GameSession.AutoPlay)
                GD.Print($"[hook] missed: flew the full {Player.HookRange / PixelsPerMeter:0.0} m to ({at.X / Ts:0.0},{at.Y / Ts:0.0}) and found no rock frame={Engine.GetFramesDrawn()}");
        };
        _player.HookArrived += (how, at) =>
        {
            if (how == "mantle")
            {
                _hookMantles++;
                _sfx.Play("place", -10f, 0.05f, 0.8f);
            }
            else _hookHangs++;
            if (GameSession.AutoPlay)
                GD.Print($"[hook] {(how == "mantle" ? "climbing onto the ledge" : how == "blocked" ? "hanging (the body met rock first)" : "hanging")} at ({at.X / Ts:0.0},{at.Y / Ts:0.0}) " +
                         $"depth={DepthMeters(at):0.0}m frame={Engine.GetFramesDrawn()}");
        };
        _player.HookReleased += why =>
        {
            if (why == "jump") _hookJumpOffs++;
            if (GameSession.AutoPlay && why != "car")
                GD.Print($"[hook] off the rope: {why} at ({_player.Position.X / Ts:0.0},{_player.Position.Y / Ts:0.0}) frame={Engine.GetFramesDrawn()}");
        };
    }

    private float DepthMeters(Vector2 at) =>
        (at.Y / Ts - _world.SurfaceRow(Mathf.FloorToInt(at.X / Ts))) * 0.25f;

    /// <summary>A few chips of rock where the hook bites.</summary>
    private void ChipAt(Vector2 at, Tile tile)
    {
        if (_puffs >= 14) return;
        _puffs++;
        var chips = new CpuParticles2D
        {
            Position = at,
            OneShot = true, Emitting = true, Amount = 6, Lifetime = 0.4f, Explosiveness = 1f, Spread = 180f,
            InitialVelocityMin = 20f, InitialVelocityMax = 80f, Gravity = new Vector2(0f, 500f),
            ScaleAmountMin = 1.5f, ScaleAmountMax = 3f, Color = TileColor(tile).Lightened(0.15f),
        };
        AddChild(chips);
        GetTree().CreateTimer(0.6).Timeout += () =>
        {
            _puffs--;
            if (IsInstanceValid(chips)) chips.QueueFree();
        };
    }

    private string HookSummary() =>
        $"hooks={_hooksThrown}/{_hooksBit}/{_hooksMissed} mantles={_hookMantles} hangs={_hookHangs} jumpOffs={_hookJumpOffs} longestHook={_hookLongest / PixelsPerMeter:0.0}m";

    /// <summary>
    /// --hooktest: dig a 12 m shaft, two tiles wide, a few metres east of the garage pad, put the
    /// mechanic at its bottom and climb out with nothing but the hook: each throw aims high up the
    /// shaft wall (alternating sides), and once the lip is in reach, at the lip so the hook climbs
    /// onto the surface. Then one throw straight up into the sky, which must miss.
    /// </summary>
    private void RunHookBot()
    {
        const int Depth = 48;   // tiles, 12 m
        if (!_shaftDug)
        {
            if (_elapsed < 0.3f) return;
            _shaftDug = true;
            _shaftX = TileWorld.PadTo + 14;
            _shaftTop = Mathf.Min(_world.SurfaceRow(_shaftX), _world.SurfaceRow(_shaftX + 1));
            _shaftBottom = Mathf.Max(_world.SurfaceRow(_shaftX), _world.SurfaceRow(_shaftX + 1)) + Depth;
            for (int y = _shaftTop - 2; y < _shaftBottom; y++)
                for (int x = _shaftX; x <= _shaftX + 1; x++)
                    if (_world.Get(x, y) != Tile.Air) _world.Set(x, y, Tile.Air);
            for (int x = _shaftX - 1; x <= _shaftX + 2; x++)
                if (!_world.Solid(x, _shaftBottom)) _world.Set(x, _shaftBottom, Tile.Stone);   // a floor to stand on
            var at = new Vector2((_shaftX + 1) * Ts, _shaftBottom * Ts - Player.HalfHeight - 0.5f);
            _player.Position = at;
            _player.Velocity = Vector2.Zero;
            _camera.Position = at;
            _airCam.Snap(at);
            GD.Print($"[hook] test: dug a shaft at tiles x={_shaftX}..{_shaftX + 1} from row {_shaftTop} to {_shaftBottom} " +
                     $"({(_shaftBottom - _shaftTop) * 0.25f:0.0} m); mechanic at its bottom, depth={DepthMeters(at):0.0}m frame={Engine.GetFramesDrawn()}");
            return;
        }

        Vector2 me = _player.Position;
        var input = new PlayerInput { Aim = me };
        if (_shaftStart < 0f && _player.Grounded)
        {
            _shaftStart = _elapsed;
            _shaftThrowsAtStart = _hooksThrown;
            _shaftNextThrow = _elapsed + 0.3f;
        }

        if (!_shaftOut && _shaftStart >= 0f)
        {
            float feet = me.Y + Player.HalfHeight;
            int surfaceRow = Mathf.Max(_world.SurfaceRow(_shaftX - 1), _world.SurfaceRow(_shaftX + 2));
            bool onSurface = feet <= (surfaceRow + 0.1f) * Ts && _player.Hook == Player.HookState.None && _player.Grounded;
            if (onSurface)
            {
                _shaftOut = true;
                GD.Print($"[hook] test: OUT of the {(_shaftBottom - _shaftTop) * 0.25f:0.0} m shaft in {_elapsed - _shaftStart:0.00}s with {_hooksThrown - _shaftThrowsAtStart} throws " +
                         $"(bit {_hooksBit}, missed {_hooksMissed}, mantles {_hookMantles}) standing at ({me.X / Ts:0.0},{me.Y / Ts:0.0}) frame={Engine.GetFramesDrawn()}");
                _shaftNextThrow = _elapsed + 0.8f;
            }
            else if (_elapsed - _shaftStart > 20f)
            {
                _shaftOut = true;
                GD.Print($"[hook] test: still IN the shaft after 20 s at depth={DepthMeters(me):0.0}m, {_hooksThrown - _shaftThrowsAtStart} throws frame={Engine.GetFramesDrawn()}");
            }
            else if (_elapsed >= _shaftNextThrow && !_player.HookFlying
                     && (_player.Hook == Player.HookState.Hang || (_player.Hook == Player.HookState.None && _player.Grounded)))
            {
                // The lip in reach? Throw at its corner; else high up a shaft wall.
                var lipLeft = new Vector2(_shaftX * Ts - 2f, _world.SurfaceRow(_shaftX - 1) * Ts + 3f);
                var lipRight = new Vector2((_shaftX + 2) * Ts + 2f, _world.SurfaceRow(_shaftX + 2) * Ts + 3f);
                Vector2 hand = me + new Vector2(0f, -8f);
                Vector2 aim;
                if (hand.DistanceTo(lipLeft) < Player.HookRange - 8f) aim = lipLeft;
                else if (hand.DistanceTo(lipRight) < Player.HookRange - 8f) aim = lipRight;
                else
                {
                    _shaftLeftWall = !_shaftLeftWall;
                    float wallX = _shaftLeftWall ? _shaftX * Ts - 2f : (_shaftX + 2) * Ts + 2f;
                    float up = Mathf.Sqrt(Player.HookRange * Player.HookRange * 0.85f - (wallX - hand.X) * (wallX - hand.X));
                    aim = new Vector2(wallX, hand.Y - up);
                }
                input.Grapple = true;
                input.Aim = aim;
                _shaftNextThrow = _elapsed + 0.25f;
            }
        }
        else if (_shaftOut && !_shaftMissTried && _elapsed >= _shaftNextThrow && _player.Grounded)
        {
            // Straight up into the open sky: nothing to bite.
            _shaftMissTried = true;
            input.Grapple = true;
            input.Aim = me + new Vector2(0f, -400f);
        }
        else if (_shaftOut && _shaftMissTried && _flingStage == 0 && _elapsed > _shaftNextThrow + 1.5f && _player.Grounded)
        {
            // Across the shaft: hook the ground on the far side and jump mid reel-in for a fling.
            _flingStage = 1;
            _flingFrom = me.X;
            input.Grapple = true;
            input.Aim = new Vector2((_shaftX + 6.5f) * Ts, _world.SurfaceRow(_shaftX + 6) * Ts + 3f);
        }
        else if (_flingStage == 1 && _player.Hook == Player.HookState.Pull && _player.Position.X > (_shaftX + 0.5f) * Ts)
        {
            _flingStage = 2;
            input.Jump = true;
        }
        else if (_flingStage == 2 && _player.Grounded && _player.Hook == Player.HookState.None)
        {
            _flingStage = 3;
            GD.Print($"[hook] test: fling landed at x={me.X / Ts:0.0} ({(me.X - _flingFrom) / PixelsPerMeter:0.0} m from where it was thrown, the shaft is at {(_shaftX + 1 - _flingFrom / Ts) * 0.25f:0.0} m) " +
                     $"depth={DepthMeters(me):0.0}m frame={Engine.GetFramesDrawn()}");
        }
        else if (_flingStage == 1 && _player.Hook == Player.HookState.None && !_player.HookFlying && _player.Grounded && _elapsed > _shaftNextThrow + 4f)
        {
            _flingStage = 3;
            GD.Print($"[hook] test: fling NOT tried (the hook did not reel across) at x={me.X / Ts:0.0} frame={Engine.GetFramesDrawn()}");
        }

        _player.Override = input;

        if (_elapsed >= _nextLog)
            GD.Print($"[hook] t={_elapsed:0}s state={_player.Hook} player=({me.X / Ts:0.0},{me.Y / Ts:0.0}) depth={DepthMeters(me):0.0}m {HookSummary()} frame={Engine.GetFramesDrawn()}");
    }
}
