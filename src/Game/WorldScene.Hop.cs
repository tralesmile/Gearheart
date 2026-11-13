using Gearheart.Core;
using Gearheart.Vehicles;
using Gearheart.World;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// The Hydraulics part's feel and log: the hop's sound, dust and shake, the first-time call-out,
/// BIG HOP / KICKER HOP call-outs, and the --hoptest bot (the hop itself lives in SoftVehicle.Hydraulics).
/// </summary>
public partial class WorldScene
{
    private const float BigHopMeters = 1.4f;

    private bool _hopHinted, _hopOffKicker;
    private int _hopsLogged, _hopsLanded, _kickerHops;
    private float _bestHopRise, _bestHopAir, _hopFiredAt = -10f, _hopCharge;

    // --hoptest bot
    private int _hopBotPhase;
    private float _hopBotStartX = float.NaN, _hopBotHoldUntil = -1f;
    private int _hopBotKicker = int.MinValue;   // crest column of the kicker being charged for

    private void OnHop(float charge, float speed)
    {
        if (!_driving) return;
        _hopFiredAt = _elapsed;
        _hopCharge = charge;
        _sfx.Play("hop", -10f + 5f * charge, 0.1f, 1.12f - 0.22f * charge);
        foreach (var contact in _car.WheelContacts)
            _crashFx.Dust(contact.Point, contact.Normal, GroundColorAt(contact.Point - contact.Normal * 4f), 6 + (int)(8 * charge), 60f + 80f * charge);
        _crashFx.AddTrauma(0.04f + 0.07f * charge);
        _birds.Startle(_car.CenterPosition, 3f * PixelsPerMeter, "hop");

        // A hop fired on a kicker's face, close to the crest, throws the car off the lip.
        int col = Mathf.FloorToInt(_car.CenterPosition.X / Ts);
        _hopOffKicker = false;
        foreach (Kicker k in _world.Kickers)
        {
            int toCrest = (k.Crest - col) * k.Dir;
            if (k.Dir == (int)_carDir && toCrest is >= -2 and <= 8) _hopOffKicker = true;
        }
        if (_hopOffKicker) _kickerHops++;

        if (!_hopHinted)
        {
            _hopHinted = true;
            Toast("HYDRAULICS!\nhold W to squat longer, hop higher · hop off a kicker's lip");
        }
        else if (_hopOffKicker && charge > 0.6f)
        {
            Toast("KICKER HOP!");
        }
        if (GameSession.AutoPlay && _hopsLogged < 30)
        {
            _hopsLogged++;
            GD.Print($"[hop] hop #{_car.Hops} charge={charge:0.00} up={speed / PixelsPerMeter:0.0}m/s at {_car.Speed / PixelsPerMeter * 3.6f:0}km/h x={_car.CenterPosition.X / Ts:0.0} " +
                     $"pitch={_car.PitchDegrees():0} wheels={_car.WheelsGrounded}/{_car.WheelsAttached} offKicker={_hopOffKicker} fuel={_car.Fuel:0.0} frame={Engine.GetFramesDrawn()}");
        }
    }

    private void OnHopLanded(float rise, float air)
    {
        if (!_driving) return;
        _hopsLanded++;
        float meters = rise / PixelsPerMeter;
        _bestHopRise = Mathf.Max(_bestHopRise, meters);
        _bestHopAir = Mathf.Max(_bestHopAir, air);
        // A big hop on the flat earns a call-out; off a kicker the jump's own call-outs speak for it.
        if (!_hopOffKicker && meters >= BigHopMeters && _toastTime <= 0.3f) Toast($"BIG HOP {meters:0.0} m");
        if (GameSession.AutoPlay && _hopsLogged < 40)
        {
            _hopsLogged++;
            GD.Print($"[hop] down: rose {meters:0.00} m, {air:0.00} s in the air, charge was {_hopCharge:0.00}, offKicker={_hopOffKicker} " +
                     $"pitch={_car.PitchDegrees():0} damage={_car.Damage:0} frame={Engine.GetFramesDrawn()} | {HopSummary()}");
        }
    }

    /// <summary>True if the jump that just ended (air seconds long) was a hop on the flat, not off a kicker.</summary>
    private bool FlatHop(float air) => !_hopOffKicker && _elapsed - _hopFiredAt < air + 0.6f && _elapsed - _hopFiredAt < 5f;

    private string HopSummary() =>
        _car != null && _car.HasHydraulics
            ? $"hops={_car.Hops}/{_hopsLanded} kickerHops={_kickerHops} bestHop={_bestHopRise:0.00}m bestHopAir={_bestHopAir:0.00}s"
            : "hops=none";

    /// <summary>
    /// --hoptest: into the car and east at full throttle (the flip bot drives instead when --fliptest is
    /// also given). About 5 m out a quick tap; about 11 m out a full squat (held 0.6 s) on the flat; then
    /// at every kicker ahead it squats on the run-up and lets go at the crest.
    /// </summary>
    private void RunHopBot()
    {
        if (!GameSession.FlipTest)
        {
            if (!_driving)
            {
                if (_elapsed > 0.5f) EnterCar();
                return;
            }
            _car.AutoThrottle = 1f;
        }
        if (!_driving || !_car.HasHydraulics) return;
        if (float.IsNaN(_hopBotStartX)) _hopBotStartX = _car.CenterPosition.X;
        float x = _car.CenterPosition.X;
        float outM = (x - _hopBotStartX) * _carDir / PixelsPerMeter;
        int col = Mathf.FloorToInt(x / Ts);
        bool grounded = !_car.Airborne;

        // Holding a squat for a fixed time.
        if (_hopBotHoldUntil > 0f)
        {
            if (_elapsed < _hopBotHoldUntil) return;
            _car.AutoHop = false;
            _hopBotHoldUntil = -1f;
            GD.Print($"[hop] bot: let go after the squat at {outM:0.0} m out frame={Engine.GetFramesDrawn()}");
            return;
        }

        // Charging for a kicker: let go with the middle of the car two tiles short of the crest, when the
        // front wheel has just gone over the lip and the rear one still pushes on the face. Bumps on the
        // run-up that lift a wheel for a moment do not count (the first CI run let go on those, early).
        if (_hopBotKicker != int.MinValue)
        {
            float crestX = _hopBotKicker * Ts + Ts * 0.5f;
            float toCrest = (crestX - x) * _carDir;
            if (toCrest <= 2f * Ts)
            {
                _car.AutoHop = false;
                GD.Print($"[hop] bot: let go {toCrest / Ts:0.0} tiles before the crest at x={_hopBotKicker}, wheels {_car.WheelsGrounded}/{_car.WheelsAttached}, " +
                         $"charge {_car.HopCharge:0.00}, {_car.Speed / PixelsPerMeter * 3.6f:0} km/h frame={Engine.GetFramesDrawn()}");
                _hopBotKicker = int.MinValue;
            }
            return;
        }

        bool kickerSoon = false;
        foreach (Kicker k in _world.Kickers)
        {
            if (k.Dir != (int)_carDir) continue;
            float crestX = k.Crest * Ts + Ts * 0.5f;
            float toCrest = (crestX - x) * _carDir;
            if (toCrest > -2f * Ts && toCrest < 14f * Ts) kickerSoon = true;
            // Squat so the charge is full (0.5 s) just as the car reaches the crest.
            float lead = _car.Speed * (SoftVehicle.HopChargeTime + 0.05f);
            if (_hopBotPhase >= 2 && grounded && toCrest > 0f && toCrest <= Mathf.Max(lead, 3f * Ts))
            {
                _hopBotKicker = k.Crest;
                _car.AutoHop = true;
                GD.Print($"[hop] bot: squatting for the {k.Meters:0.00} m kicker at x={k.Crest}, {toCrest / Ts:0.0} tiles out at {_car.Speed / PixelsPerMeter * 3.6f:0} km/h frame={Engine.GetFramesDrawn()}");
                return;
            }
        }

        if (_hopBotPhase == 0 && outM >= 5f && grounded && !kickerSoon)
        {
            _hopBotPhase = 1;
            _car.AutoHop = true;
            _hopBotHoldUntil = _elapsed + 0.02f;   // a tap: one or two ticks
            GD.Print($"[hop] bot: a quick tap at {outM:0.0} m out ({col}) frame={Engine.GetFramesDrawn()}");
        }
        else if (_hopBotPhase == 1 && outM >= 11f && grounded && !kickerSoon && _elapsed - _hopFiredAt > 1f)
        {
            _hopBotPhase = 2;
            _car.AutoHop = true;
            _hopBotHoldUntil = _elapsed + 0.6f;   // a full squat
            GD.Print($"[hop] bot: full squat at {outM:0.0} m out frame={Engine.GetFramesDrawn()}");
        }
        else if (_hopBotPhase < 2 && outM >= 18f)
        {
            _hopBotPhase = 2;   // the flat stretch ran out (a kicker in the way): go on to the kickers
            GD.Print($"[hop] bot: no room for the flat hops before {outM:0.0} m, on to the kickers");
        }
    }
}
