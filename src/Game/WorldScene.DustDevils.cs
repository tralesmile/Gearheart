using Gearheart.Core;
using Gearheart.World;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// Dust devils in The Wastes (see <see cref="DustDevils"/>): on hot afternoons whirlwinds wander the
/// road. Driving into one lifts, slows and twists the car, then the devil throws it out and blows
/// itself out. On foot it picks you up and drops you a few metres away. The landing after a ride is
/// judged like any jump (perfect landings and flips count).
/// </summary>
public partial class WorldScene
{
    private const float DevilSpin = 2.4f;        // rad/s it twists the car towards at full grip
    private const float DevilHoldMax = 1.6f;     // seconds it can hold the car before it is spent
    private DustDevils _devils;
    private DustDevil _riding;                    // the devil holding (or that just threw) the car
    private bool _devilHinted, _heatAnnounced;
    private float _rideTime, _rideAir, _rideTop, _rideRot, _rideEntryKmh, _releasedAt;
    private int _devilRides, _devilThrows, _devilCollapses, _devilFootLifts;
    private float _devilBestAir, _devilBestHeight, _nextDevilLog;
    private bool _rideFlying, _rideReleased, _footLifted;

    private void SetupDevils()
    {
        _devils = new DustDevils { Name = "DustDevils", World = _world, Focus = _player.Position, Heat = DustDevils.HeatAt(GameSession.ClockHours) };
        _devils.Changed += (d, what) =>
        {
            if (what == "collapsed") _devilCollapses++;
            if (GameSession.AutoPlay)
                GD.Print($"[devil] #{d.Index} {what} at {(d.X - TileWorld.PadCenter.X) / PixelsPerMeter:0} m east, strength {d.Strength:0.00}, heat {_devils.Heat:0.00}, clock {DayCycle.Format(GameSession.ClockHours)} t={_elapsed:0.0}s frame={Engine.GetFramesDrawn()}");
        };
        AddChild(_devils);
        MoveChild(_devils, Mathf.Min(_car.GetIndex(), _player.GetIndex()));   // the back of the swirl is drawn behind the car and the player
        _heatAnnounced = _devils.Heat > 0f;
        if (GameSession.AutoPlay)
        {
            string zones = "";
            foreach (var d in _devils.All)
                zones += $" #{d.Index} {(d.ZoneFrom - TileWorld.PadCenter.X) / PixelsPerMeter:0}–{(d.ZoneTo - TileWorld.PadCenter.X) / PixelsPerMeter:0} m{(d.Alive ? $" (at {(d.X - TileWorld.PadCenter.X) / PixelsPerMeter:0} m)" : " (none yet)")}";
            GD.Print($"[devil] heat {_devils.Heat:0.00} at {DayCycle.Format(GameSession.ClockHours)}; stretches:{zones}");
        }
    }

    /// <summary>Every physics frame, after the stunts are tracked.</summary>
    private void UpdateDevils(float dt)
    {
        _devils.Heat = DustDevils.HeatAt(GameSession.ClockHours);
        _devils.Focus = Focus;
        _devils.Step(dt);
        if (!_heatAnnounced && _devils.Heat > 0.3f)
        {
            _heatAnnounced = true;
            if (Focus.X > TileWorld.PadCenter.X && !_card.Showing && _toastTime < 1.2f)
                Toast("MIDDAY HEAT\ndust devils are dancing in The Wastes");
        }
        if (_devils.Heat <= 0f) _heatAnnounced = false;

        UpdateDevilSound();
        DevilOnFoot(dt);
        if (!_driving)
        {
            _car.Whirl(0f, 0f, 0f);
            _riding = null;
            _rideTime = 0f;
            return;
        }

        // The strongest devil holding the car this frame.
        Vector2 c = _car.CenterPosition;
        DustDevil holder = null;
        float grip = 0f;
        foreach (var d in _devils.All)
        {
            float g = d.Collapsing ? 0f : DustDevils.Grip(d, c);
            if (g > grip) { grip = g; holder = d; }
        }
        if (holder != null && grip > 0.02f)
        {
            if (_riding != holder) StartRide(holder);
            float vx = _car.CenterVelocity.X;
            // Pulled towards a mix of its own speed and the devil's drift, and towards the core.
            float target = 0.5f * vx + 0.5f * holder.Vx - (c.X - holder.X) * 1.2f;
            _car.Whirl(grip, target, DevilSpin * holder.Spin * Mathf.Clamp(grip * 1.6f, 0f, 1f));
            _rideTime += dt;
            holder.Held += dt;
            _crashFx.AddTrauma(0.5f * grip * dt);
            if (holder.Held > DevilHoldMax) _devils.Collapse(holder);
        }
        else
        {
            _car.Whirl(0f, 0f, 0f);
            // Let go: a real ride (not a graze) spends the devil, and it blows itself out.
            if (_riding != null && !_rideReleased)
            {
                _rideReleased = true;
                _releasedAt = _elapsed;
                if (_rideTime > 0.12f)
                {
                    _riding.Throws++;
                    _devilThrows++;
                    _devils.Collapse(_riding);
                    _sfx.Play("gust", -8f, 0.3f);
                }
            }
        }
        TrackRide(dt, c);
    }

    private void StartRide(DustDevil d)
    {
        _riding = d;
        _rideTime = 0f;
        _rideAir = 0f;
        _rideTop = 0f;
        _rideRot = 0f;
        _rideFlying = true;
        _rideReleased = false;
        _rideEntryKmh = _car.Speed / PixelsPerMeter * 3.6f;
        _devilRides++;
        _sfx.Play("gust", -6f, 0.3f);
        if (!_card.Showing && (_toastTime < 1.2f || !_devilHinted))
            Toast(_devilHinted ? "DUST DEVIL!" : "DUST DEVIL!\nQ/E against the spin · land flat");
        _devilHinted = true;
        if (GameSession.AutoPlay)
            GD.Print($"[devil] caught by #{d.Index} at {(_car.CenterPosition.X - TileWorld.PadCenter.X) / PixelsPerMeter:0} m east, {_rideEntryKmh:0} km/h, devil strength {d.Strength:0.00} spin {(d.Spin > 0 ? "cw" : "ccw")} frame={Engine.GetFramesDrawn()}");
    }

    /// <summary>Follows a ride from the grab to the touchdown, for the log and the records.</summary>
    private void TrackRide(float dt, Vector2 c)
    {
        if (_riding == null || !_rideFlying) return;
        float ground = _world.GroundY(c.X, (_world.SurfaceRow(Mathf.FloorToInt(c.X / Ts)) - 14) * Ts);
        _rideTop = Mathf.Max(_rideTop, (ground - c.Y) / PixelsPerMeter);
        if (_car.Airborne) _rideAir += dt;
        _rideRot = Mathf.Max(_rideRot, Mathf.Abs(_airRot));
        // Down again: wheels (or the body) on the ground after the devil let go.
        if (_rideReleased && !_car.Airborne && _elapsed - _releasedAt > 0.1f || _rideReleased && _elapsed - _releasedAt > 6f)
        {
            _rideFlying = false;
            _devilBestAir = Mathf.Max(_devilBestAir, _rideAir);
            _devilBestHeight = Mathf.Max(_devilBestHeight, _rideTop);
            if (GameSession.AutoPlay)
                GD.Print($"[devil] down: held {_rideTime:0.00}s, air {_rideAir:0.00}s, {_rideTop:0.0} m up (above the ground), turned {_rideRot:0} deg, " +
                         $"{_car.Speed / PixelsPerMeter * 3.6f:0} km/h now (in at {_rideEntryKmh:0}), pitch {_car.PitchDegrees():0}, damage {_car.Damage:0} frame={Engine.GetFramesDrawn()}");
            _riding = null;
            _rideTime = 0f;
        }
    }

    /// <summary>On foot a devil picks you up and carries you a little way.</summary>
    private void DevilOnFoot(float dt)
    {
        if (_driving) return;
        float best = 0f;
        DustDevil holder = null;
        foreach (var d in _devils.All)
        {
            float g = d.Collapsing ? 0f : DustDevils.Grip(d, _player.Position, 0.7f);
            if (g > best) { best = g; holder = d; }
        }
        if (holder == null || best < 0.05f)
        {
            _footLifted = false;
            return;
        }
        _player.Velocity.Y = Mathf.MoveToward(_player.Velocity.Y, -520f, 2600f * best * dt);
        _player.Velocity.X = Mathf.MoveToward(_player.Velocity.X, holder.Vx + holder.Spin * 160f, 900f * best * dt);
        if (!_footLifted)
        {
            _footLifted = true;
            _devilFootLifts++;
            _sfx.Play("gust", -8f, 0.3f);
            if (GameSession.AutoPlay) GD.Print($"[devil] picked up the player on foot (#{holder.Index}) frame={Engine.GetFramesDrawn()}");
        }
    }

    private void UpdateDevilSound()
    {
        var (d, dist) = _devils.Nearest(Focus);
        float level = d == null ? 0f : d.Strength * Mathf.Clamp(1f - (dist - 150f) / 1500f, 0f, 1f);
        if (_driving && _car.WhirlGrip > 0f) level = Mathf.Max(level, 0.6f + 0.4f * _car.WhirlGrip);
        _sfx.UpdateDevil(level);
    }

    private string DevilSummary() =>
        $"devils={_devilRides}/{_devilThrows}/{_devilCollapses} bestDevilAir={_devilBestAir:0.00}s bestDevilHeight={_devilBestHeight:0.0}m footLifts={_devilFootLifts} heat={_devils.Heat:0.00} devilSound={_sfx.DevilLevel:0.00}";
}
