using System.Collections.Generic;
using Gearheart.Core;
using Gearheart.World;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// The Deep comes alive: cave bats (see <see cref="Bats"/>) burst off the cavern ceilings when you come
/// into sight, dig into their cavern or honk, and the Effects bus gets a cave echo that grows with depth,
/// so the pickaxe, crystals and squeaks ring underground. Nothing here touches physics or scoring.
/// </summary>
public partial class WorldScene
{
    private Bats _bats;
    private float _echo, _peakEcho;
    private readonly Dictionary<string, int> _batCauses = new();
    private float _firstBurstDistance = -1f;
    private Roost _batTarget;
    private bool _batPlaced, _batWatchLogged, _batBurstViewLogged, _batStepped;
    private int _batsMostOnScreen;
    private Vector2? _batNear;
    private float _batPlacedAt;

    private void SetupBats(CanvasLayer glintLayer)
    {
        _bats = new Bats { Name = "Bats", World = _world, Sfx = _sfx, Focus = _player.Position };
        _bats.Startled += OnBatsStartled;
        // On the glint layer, so the Deep's darkness does not swallow them: the first two CI runs' frames
        // showed nothing at all past the lamp's 4 m. They shade themselves instead (Bats._Draw).
        glintLayer.AddChild(_bats);

        if (!GameSession.AutoPlay) return;
        int bats = 0, mid = (TileWorld.PadFrom + TileWorld.PadTo) / 2;
        float nearest = float.MaxValue, minDepth = float.MaxValue, maxDepth = 0f;
        foreach (var r in _bats.All)
        {
            bats += r.Bats.Count;
            nearest = Mathf.Min(nearest, Mathf.Abs(r.X - mid) * 0.25f);
            float depth = (r.Y - _world.SurfaceRow(r.X)) * 0.25f;
            minDepth = Mathf.Min(minDepth, depth);
            maxDepth = Mathf.Max(maxDepth, depth);
        }
        GD.Print($"[bats] placed {_bats.All.Count} roosts, {bats} bats; ceilings {minDepth:0.0}-{maxDepth:0.0} m down; nearest roost {nearest:0} m from the garage");
    }

    /// <summary>Every frame: bats see the player or the car (with a clear view), and the cave echo follows the depth.</summary>
    private void UpdateBats(Vector2 focus, float dt)
    {
        _bats.Focus = focus;
        // Only colonies on screen (a margin in from the edges) see you, so you always see them hanging first.
        Rect2 view = CameraView().Grow(-24f);
        if (_driving) _bats.Startle(_car.CenterPosition, Bats.SightReachCar, "car", true, view);
        else _bats.Startle(_player.Position, Bats.SightReachFoot, "foot", true, view);

        // Echo: none in a shallow hole, growing from about 1.5 m down, full from about 6 m (and in the Deep).
        int tx = Mathf.FloorToInt(focus.X / Ts);
        float depthTiles = focus.Y / Ts - _world.SurfaceRow(tx);
        float target = Mathf.Clamp((depthTiles - 6f) / 18f, 0f, 1f);
        if (focus.Y / Ts >= _world.DeepRow(tx)) target = 1f;
        _echo = Mathf.MoveToward(_echo, target, dt * 0.8f);
        _peakEcho = Mathf.Max(_peakEcho, _echo);
        Settings.SetCaveEcho(_echo);
    }

    private void OnBatsStartled(Roost r, string cause, float distance)
    {
        _batCauses.TryGetValue(cause, out int n);
        _batCauses[cause] = n + 1;
        if (_firstBurstDistance < 0f) _firstBurstDistance = distance / PixelsPerMeter;
        if (!GameSession.AutoPlay) return;
        GD.Print($"[bats] burst: roost {r.Index} x{r.Bats.Count} at tile ({r.X},{r.Y}) depth={(r.Y - _world.SurfaceRow(r.X)) * 0.25f:0.0}m " +
                 $"cause={cause} nearest={distance / PixelsPerMeter:0.0}m {(_driving ? "driving" : "on foot")} echo={_echo:0.00} frame={Engine.GetFramesDrawn()}");
    }

    /// <summary>
    /// --battest: put the player on a cavern floor at most 2 m under the roost's ceiling and 4–10 m from the
    /// roost nearest the garage with a clear view of it, then walk toward it (jumping steps, mining what is in
    /// the way) until the bats go up; if they have not after 2.5 s, step to 1.5–2.8 m from them.
    /// </summary>
    private void RunBatBot()
    {
        if (!_batPlaced)
        {
            if (_elapsed < 0.3f) return;
            _batPlaced = true;
            int mid = (TileWorld.PadFrom + TileWorld.PadTo) / 2;
            var roosts = new List<Roost>(_bats.All);
            roosts.Sort((a, b) => Mathf.Abs(a.X - mid).CompareTo(Mathf.Abs(b.X - mid)));
            // The roost nearest the garage that has both a floor 4-10 m out (the "hanging" frames; a clear view
            // first, then up to three tiles of rock) and a floor 1.5-2.8 m from it with a clear view (the step).
            // Last pass: any roost with a far floor (the walker alone has to get there).
            for (int pass = 0; pass < 3; pass++)
            foreach (var r in roosts)
            {
                if (r.Bats.Count == 0 || BatSpot(r, 4f, 10f, pass == 0 ? 1 : 3, 8) is not Vector2 spot) continue;
                Vector2? near = BatSpot(r, 1.5f, 2.8f, 1, 12);
                if (near == null && pass < 2) continue;
                _batNear = near;
                _batTarget = r;
                PutPlayer(spot);
                _batPlacedAt = _elapsed;
                string nearText = near is Vector2 n ? (n.DistanceTo(r.Center) / PixelsPerMeter).ToString("0.0") + " m" : "none";
                GD.Print($"[bats] test: player placed {spot.DistanceTo(r.Center) / PixelsPerMeter:0.0} m from roost {r.Index} (x{r.Bats.Count}, tile ({r.X},{r.Y}), " +
                         $"{(r.X - mid) * 0.25f:0} m from the garage), rock on the line {RockOnLine(spot, r.Center + new Vector2(0f, 6f))} tiles, roosts tried {roosts.IndexOf(r) + 1} pass {pass} near={nearText} frame={Engine.GetFramesDrawn()}");
                return;
            }
            GD.Print($"[bats] test: NO roost of {roosts.Count} has a cavern floor 4-10 m away with at most 3 tiles of rock in between");
            return;
        }
        if (_batTarget == null) return;

        Vector2 me = _player.Position;
        // The cavern floor is often broken up, so the walker can stall short of the bats' reach. After 2.5 s
        // the bot steps (as if it had walked) to a floor spot 1.5-2.8 m from the roost with a clear view, and the
        // game's own sight check (reach, rock, on screen) has to do the rest.
        if (!_batTarget.Burst && !_batStepped && _elapsed - _batPlacedAt > 2.5f)
        {
            _batStepped = true;
            if (_batNear is Vector2 step)
            {
                float was = me.DistanceTo(_batTarget.Center) / PixelsPerMeter;
                PutPlayer(step);
                me = step;
                GD.Print($"[bats] test: no burst yet at {was:0.0} m; stepped to {step.DistanceTo(_batTarget.Center) / PixelsPerMeter:0.0} m frame={Engine.GetFramesDrawn()}");
            }
            else GD.Print($"[bats] test: no burst yet and no near floor; the walker keeps going frame={Engine.GetFramesDrawn()}");
        }
        if (!_batTarget.Burst)
        {
            // Walk at the roost; jump when stuck; dig at whatever is between us after a few seconds.
            float dir = Mathf.Sign(_batTarget.Center.X - me.X);
            bool dig = _elapsed - _batPlacedAt > 4f;
            _player.Override = new PlayerInput
            {
                Move = dir,
                Jump = Mathf.Abs(_player.Velocity.X) < 5f,
                Mine = dig,
                Aim = dig ? me + (_batTarget.Center + new Vector2(0f, 24f) - me).LimitLength(80f) : _batTarget.Center,
            };
        }
        else
        {
            _player.Override = new PlayerInput { Aim = _batTarget.Center };
            if (!_batWatchLogged && _bats.InAir == 0 && _elapsed - _batPlacedAt > 2f)
            {
                _batWatchLogged = true;
                GD.Print($"[bats] test: all bats gone by t={_elapsed:0.0}s mostInAir={_bats.MostInAir} wallBounces={_bats.WallBounces} echo={_echo:0.00} frame={Engine.GetFramesDrawn()}");
            }
        }
        int onScreen = _bats.CountIn(CameraView());
        _batsMostOnScreen = Mathf.Max(_batsMostOnScreen, onScreen);
        if (_batTarget.Burst && !_batBurstViewLogged)
        {
            _batBurstViewLogged = true;
            GD.Print($"[bats] test: at the burst {onScreen} of {_batTarget.Bats.Count} bats are on screen frame={Engine.GetFramesDrawn()}");
        }
        if (_elapsed >= _nextLog)
            GD.Print($"[bats] t={_elapsed:0}s burst={_batTarget.Burst} inAir={_bats.InAir} onScreen={onScreen} mostOnScreen={_batsMostOnScreen} player=({me.X / Ts:0.0},{me.Y / Ts:0.0}) " +
                     $"toRoost={me.DistanceTo(_batTarget.Center) / PixelsPerMeter:0.0}m echo={_echo:0.00} effectsWet={Settings.CaveEchoWet:0.00} frame={Engine.GetFramesDrawn()}");
    }

    /// <summary>
    /// A cavern-floor standing spot for the bat test: <paramref name="minD"/>–<paramref name="maxD"/> m from the
    /// roost, at most <paramref name="maxBelow"/> rows under its ceiling, with at most <paramref name="maxRock"/>
    /// tiles of rock on the line to it. The spot nearest the middle of the range wins.
    /// </summary>
    private Vector2? BatSpot(Roost r, float minD, float maxD, int maxRock, int maxBelow)
    {
        Vector2? best = null;
        float bestScore = float.MaxValue, mid = (minD + maxD) / 2f;
        for (int x = r.X - 44; x <= r.X + 44; x++)
            for (int y = r.Y + 3; y <= r.Y + maxBelow && y < TileWorld.Height - 3; y++)
            {
                if (_world.Solid(x, y) || _world.Solid(x, y - 1) || _world.Solid(x, y - 2) || !_world.Solid(x, y + 1)) continue;
                var at = new Vector2((x + 0.5f) * Ts, (y + 1) * Ts - Player.HalfHeight - 0.5f);
                float d = at.DistanceTo(r.Center) / PixelsPerMeter;
                if (d < minD || d > maxD || _player.Blocked(at.X, at.Y)) continue;
                int rock = RockOnLine(at, r.Center + new Vector2(0f, 6f));
                if (rock > maxRock) continue;
                float score = Mathf.Abs(d - mid) + rock;
                if (score < bestScore) { bestScore = score; best = at; }
            }
        return best;
    }

    private void PutPlayer(Vector2 spot)
    {
        _player.Position = spot;
        _player.Velocity = Vector2.Zero;
        _camera.Position = spot;
        _airCam.Snap(spot);
    }

    /// <summary>The part of the world the camera shows (world pixels).</summary>
    private Rect2 CameraView()
    {
        Vector2 size = GetViewport().GetVisibleRect().Size / _camera.Zoom;
        return new Rect2(_camera.GetScreenCenterPosition() - size / 2f, size);
    }

    private int RockOnLine(Vector2 a, Vector2 b)
    {
        int steps = Mathf.CeilToInt(a.DistanceTo(b) / (Ts * 0.5f));
        int rock = 0;
        var last = new Vector2I(int.MinValue, 0);
        for (int i = 1; i < steps; i++)
        {
            Vector2 p = a.Lerp(b, i / (float)steps);
            var t = new Vector2I(Mathf.FloorToInt(p.X / Ts), Mathf.FloorToInt(p.Y / Ts));
            if (t == last) continue;
            last = t;
            if (_world.Solid(t.X, t.Y)) rock++;
        }
        return rock;
    }

    private string BatSummary()
    {
        var causes = new List<string>();
        foreach (var (k, v) in _batCauses) causes.Add($"{k}:{v}");
        return $"bats={_bats.Bursts} ({string.Join(",", causes)}) batsInAir={_bats.InAir} mostBats={_bats.MostInAir} batsOnScreen={_batsMostOnScreen} firstBat={_firstBurstDistance:0.0}m echo={_echo:0.00} peakEcho={_peakEcho:0.00}";
    }
}
