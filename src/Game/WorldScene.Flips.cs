using Gearheart.Core;
using Gearheart.Vehicles;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// Flip tricks: the car's rotation is added up while it flies. Every full turn ticks in the air
/// ("FULL TURN! now land it"), and a jump that ends on the wheels after one or more turns is a
/// BACKFLIP or FRONTFLIP (DOUBLE, TRIPLE ...) with a speed kick and points on the trip report.
/// A flip that ends on the roof, nose or tail does not count. Backwards means the end you are
/// driving towards goes up and over the back, the way a car flips off a ramp.
/// </summary>
public partial class WorldScene
{
    private const float FlipSlack = 60f;          // degrees short of a full turn that still count (landing on a slope)
    private const float FlipKickPerTurn = 150f;   // px/s per landed turn (about 8 km/h)
    private const int MaxFlipKickTurns = 3;

    private float _airRot;         // degrees turned since the wheels left the ground (+ = clockwise on screen)
    private float _airRotRate;     // smoothed, degrees per second
    private float _lastPitch;
    private bool _pitchKnown;
    private int _airTurnsTicked;
    private float _flipRot;        // the rotation of the jump that just touched down, until it is judged
    private int _backflips, _frontflips, _flipsCrashed;
    private float _maxAirRot;      // largest rotation in one jump this run (for the log)

    // --fliptest bot: which way it tries to flip off the current kicker.
    private int _flipBotLaunch = -1;
    private float _flipBotTarget;
    private float _flipBotAir, _flipBotPeak, _flipBotPush, _flipBotBrake, _flipBotNextLog;

    /// <summary>Full turns in a rotation, allowing a landing a little short of level (onto a slope).</summary>
    public static int FlipTurns(float rotDegrees) => Mathf.FloorToInt((Mathf.Abs(rotDegrees) + FlipSlack) / 360f);

    /// <summary>"BACKFLIP", "DOUBLE FRONTFLIP", "4x BACKFLIP".</summary>
    public static string FlipName(int turns, bool back)
    {
        string kind = back ? "BACKFLIP" : "FRONTFLIP";
        return turns switch
        {
            1 => kind,
            2 => "DOUBLE " + kind,
            3 => "TRIPLE " + kind,
            _ => $"{turns}x {kind}",
        };
    }

    /// <summary>Backwards: the leading end (the way the car travels) rotated up and over.</summary>
    private bool IsBackflip(float rot) => rot * _carDir < 0f;

    /// <summary>Called every physics frame from <see cref="TrackStunts"/>: adds up the rotation while flying.</summary>
    private void TrackRotation(float dt, bool flying)
    {
        float pitch = _car.PitchDegrees();
        float delta = _pitchKnown ? Mathf.Wrap(pitch - _lastPitch, -180f, 180f) : 0f;
        _lastPitch = pitch;
        _pitchKnown = true;
        if (!flying)
        {
            _airRotRate = 0f;
            return;
        }
        _airRot += delta;
        _airRotRate = Mathf.Lerp(_airRotRate, delta / Mathf.Max(dt, 1e-4f), 0.35f);
        _maxAirRot = Mathf.Max(_maxAirRot, Mathf.Abs(_airRot));

        int turns = Mathf.FloorToInt(Mathf.Abs(_airRot) / 360f);
        if (turns <= _airTurnsTicked) return;
        _airTurnsTicked = turns;
        _sfx.Play("pickup", -5f, 0.05f, 1f + 0.25f * turns);
        if (!_goalToast || _toastTime < 1.2f)
            Toast(turns == 1 ? "FULL TURN!\nnow land it" : $"{turns} TURNS!\nnow land it");
        if (GameSession.AutoPlay)
            GD.Print($"[flip] turn {turns} ({(IsBackflip(_airRot) ? "backwards" : "forwards")}) after {_airTime:0.00}s of air, spinning {_airRotRate:0} deg/s at x={_car.CenterPosition.X / Ts:0} frame={Engine.GetFramesDrawn()}");
    }

    /// <summary>The wheels touched down: keep this jump's rotation for the landing verdict.</summary>
    private void TouchdownRotation()
    {
        _flipRot = _airRot;
        _airRot = 0f;
        _airTurnsTicked = 0;
    }

    private void ResetRotation()
    {
        _airRot = 0f;
        _flipRot = 0f;
        _airTurnsTicked = 0;
    }

    /// <summary>
    /// The landing was judged (on the wheels). Returns the flip call-out line ("BACKFLIP!") or null,
    /// and adds the flip's speed kick to <paramref name="kick"/>.
    /// </summary>
    private string LandedFlip(Landing l, ref float kick)
    {
        float rot = _flipRot;
        _flipRot = 0f;
        int turns = FlipTurns(rot);
        if (turns == 0)
        {
            if (GameSession.AutoPlay && Mathf.Abs(rot) > 150f)
                GD.Print($"[flip] no flip: turned {rot * _carDir:0} deg (needs {360f - FlipSlack:0}) and landed {l.Grade.ToString().ToLowerInvariant()}");
            return null;
        }
        bool back = IsBackflip(rot);
        // Wheels touched first but the body slammed down at a wild angle: that is a crash, not a landed flip.
        if (l.BodyHit && l.Mismatch > 35f)
        {
            _flipsCrashed++;
            if (GameSession.AutoPlay)
                GD.Print($"[flip] crashed a {FlipName(turns, back)}: turned {rot * _carDir:0} deg, wheels first but the body hit at {l.Mismatch:0} deg off the slope frame={Engine.GetFramesDrawn()} | backflips={_backflips} frontflips={_frontflips} crashed={_flipsCrashed}");
            return null;
        }
        if (back) _backflips += turns; else _frontflips += turns;
        if (_trip != null) _trip.Flips += turns;
        float flipKick = FlipKickPerTurn * Mathf.Min(turns, MaxFlipKickTurns);
        kick += flipKick;
        string name = FlipName(turns, back);
        _sfx.Play("record", -7f, 0.5f, 1.1f + 0.1f * turns);
        if (GameSession.AutoPlay)
            GD.Print($"[flip] LANDED {name}: turned {rot * _carDir:0} deg in {l.Air:0.00}s, {l.Grade.ToString().ToLowerInvariant()} landing (mismatch {l.Mismatch:0} deg), " +
                     $"flip kick {flipKick / PixelsPerMeter * 3.6f:0} km/h at x={_car.CenterPosition.X / Ts:0} frame={Engine.GetFramesDrawn()} | backflips={_backflips} frontflips={_frontflips} crashed={_flipsCrashed}");
        return name;
    }

    /// <summary>A botched landing (roof, nose, tail): a flip that ends like this does not count. Returns " · BACKFLIP didn't count" or "".</summary>
    private string CrashedFlip(string what)
    {
        float rot = Mathf.Abs(_airRot) >= Mathf.Abs(_flipRot) ? _airRot : _flipRot;
        ResetRotation();
        int turns = FlipTurns(rot);
        if (turns == 0) return "";
        _flipsCrashed++;
        string name = FlipName(turns, IsBackflip(rot));
        if (GameSession.AutoPlay)
            GD.Print($"[flip] crashed a {name}: turned {rot * _carDir:0} deg, came down as {what.ToLowerInvariant()} frame={Engine.GetFramesDrawn()} | backflips={_backflips} frontflips={_frontflips} crashed={_flipsCrashed}");
        return $"\n{name}, but it doesn't count";
    }

    /// <summary>
    /// --fliptest: straight into the car and out east (the rocket car boosts on the ground). Off every
    /// kicker it tries a flip like a player would: hold the tilt until a full turn is close, then
    /// counter-tilt to stop the spin level. Odd kickers backflip, even ones frontflip. No boost during a flip.
    /// </summary>
    private void RunFlipBot()
    {
        if (!_driving)
        {
            if (_elapsed > 0.5f) EnterCar();
            return;
        }
        _car.AutoThrottle = 1f;
        bool air = _car.Airborne && !_car.SelfRighting && !_bodyDown;
        _car.AutoBoost = _car.HasBooster && _flipBotTarget == 0f;   // no thrust while spinning
        if (!air)
        {
            if (_flipBotTarget != 0f)
                GD.Print($"[flip] bot: touchdown, turned {_flipRot:0} of {_flipBotTarget:0} after {_flipBotAir:0.00}s, peak spin {_flipBotPeak:0} deg/s, tilt held {_flipBotPush:0.00}s push / {_flipBotBrake:0.00}s brake, " +
                         $"wheels {_car.WheelsGrounded}/{_car.WheelsAttached} pitch {_car.PitchDegrees():0} bodyDown={_bodyDown} frame={Engine.GetFramesDrawn()}");
            _car.AutoTilt = 0f;
            _flipBotTarget = 0f;
            return;
        }
        // Only jumps off a kicker crest (the take-off was a moment ago; the wheels may leave just before the crest).
        if (_flipBotTarget == 0f && _flipBotLaunch != _kickerLaunches && _elapsed - _kickerAt < 0.4f && _airTime < 0.5f)
        {
            _flipBotLaunch = _kickerLaunches;
            bool back = (_kickerLaunches & 1) == 1;
            // Backflip: rotation against the travel direction (see IsBackflip). Aim from where the jump has turned so far.
            _flipBotTarget = (back ? -360f : 360f) * _carDir;
            _flipBotAir = _flipBotPeak = _flipBotPush = _flipBotBrake = 0f;
            _flipBotNextLog = 0f;
            GD.Print($"[flip] bot: going for a {(back ? "backflip" : "frontflip")} off kicker {_kickerLaunches} at {_car.Speed / PixelsPerMeter * 3.6f:0} km/h, " +
                     $"pitch {_car.PitchDegrees():0}, turned {_airRot:0} so far frame={Engine.GetFramesDrawn()}");
        }
        if (_flipBotTarget == 0f)
        {
            _car.AutoTilt = 0f;
            return;
        }

        // Bang-bang with braking: push towards the target, and counter-tilt once the spin would carry it there.
        float a = _car.AirControl * SoftVehicle.AirFlipBoost * Mathf.RadToDeg(1f) * 0.75f;   // deg/s², with a margin
        float e = _flipBotTarget - _airRot;
        float w = _airRotRate;
        float stop = w * w / (2f * a);
        float tilt;
        if (Mathf.Abs(e) < 12f && Mathf.Abs(w) < 90f) tilt = 0.001f;
        else if (Mathf.Sign(w) == Mathf.Sign(e) && Mathf.Abs(e) <= stop) tilt = -Mathf.Sign(w);
        else tilt = Mathf.Sign(e);
        _car.AutoTilt = tilt;

        const float dt = 1f / 120f;
        _flipBotAir += dt;
        _flipBotPeak = Mathf.Max(_flipBotPeak, Mathf.Abs(w));
        if (Mathf.Abs(tilt) > 0.5f) { if (Mathf.Sign(tilt) == Mathf.Sign(e)) _flipBotPush += dt; else _flipBotBrake += dt; }
        if (_flipBotAir >= _flipBotNextLog)
        {
            _flipBotNextLog += 0.1f;
            GD.Print($"[flip] bot: t+{_flipBotAir:0.00}s turned {_airRot:0} spin {w:0} deg/s tilt {tilt:0.0} airTime {_airTime:0.00} pitch {_car.PitchDegrees():0}");
        }
    }
}
