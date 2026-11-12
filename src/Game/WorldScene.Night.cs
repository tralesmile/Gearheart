using Gearheart.Core;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// Day and night: the world clock runs while you play (about seven minutes a day, see DayCycle).
/// The sky turns orange at sunset and starry at night, the world takes on a moonlit blue, the car's
/// headlights come on by themselves, crickets chirp on the surface, and buried stashes glint from
/// much further away at night — so a night drive is a treasure hunt.
/// </summary>
public partial class WorldScene
{
    private const float NightGlintReach = 1.8f;
    private bool _wasNight;
    private string _pendingClockToast;
    private float _pendingClockToastFor;
    private float _nextNightLog;
    private int _nightFalls, _dawns;
    private float _nightSeconds, _headlightSeconds;
    private int _maxGlintsAtNight, _maxGlintsByDay;

    private static bool ClockLogging => GameSession.AutoPlay && (GameSession.StartTime != null || GameSession.ClockRate != 1f);

    private void StartClock()
    {
        if (GameSession.StartTime is float start) GameSession.ClockHours = Mathf.PosMod(start, 24f);
        _wasNight = DayCycle.Night(GameSession.ClockHours) > 0.5f;
        if (GameSession.AutoPlay)
            GD.Print($"[night] clock starts at {DayCycle.Format(GameSession.ClockHours)} on day {GameSession.Day}, rate x{GameSession.ClockRate:0.#} " +
                     $"night={DayCycle.Night(GameSession.ClockHours):0.00}");
    }

    /// <summary>Called every physics frame: moves the clock on and announces dusk and dawn.</summary>
    private void AdvanceClock(float dt)
    {
        float h = GameSession.ClockHours;
        h += DayCycle.Rate(h) * GameSession.ClockRate * dt;
        if (h >= 24f)
        {
            h -= 24f;
            GameSession.Day++;
        }
        GameSession.ClockHours = h;

        float night = DayCycle.Night(h);
        if (night > 0.5f) _nightSeconds += dt;
        if (_car.HeadlightsOn) _headlightSeconds += dt;
        bool isNight = night > 0.5f;
        if (isNight != _wasNight)
        {
            _wasNight = isNight;
            if (isNight)
            {
                _nightFalls++;
                _pendingClockToast = "NIGHT FALLS\nheadlights on · buried stashes glint from further away";
            }
            else
            {
                _dawns++;
                _pendingClockToast = $"DAWN · DAY {GameSession.Day}";
            }
            _pendingClockToastFor = 8f;
            if (GameSession.AutoPlay)
                GD.Print($"[night] {(isNight ? "night falls" : "dawn")} at {DayCycle.Format(h)} day {GameSession.Day} " +
                         $"{(_driving ? "driving" : "on foot")} x={Focus.X / Ts:0} frame={Engine.GetFramesDrawn()}");
        }

        // Wait for the current call-out to be read, but not forever.
        if (_pendingClockToast != null)
        {
            _pendingClockToastFor -= dt;
            if (_pendingClockToastFor <= 0f) _pendingClockToast = null;
            else if (_toastTime < 0.6f && !_card.Showing)
            {
                Toast(_pendingClockToast);
                _toastTime = 3f;
                _sfx?.Play("record", -14f, 0.5f, isNight ? 0.55f : 0.9f);
                _pendingClockToast = null;
            }
        }
    }

    /// <summary>Called every frame: light and colour from the time of day and the depth.</summary>
    private void ApplyDaylight(Vector2 focus, float under)
    {
        float h = GameSession.ClockHours;
        float night = DayCycle.Night(h);
        Color surface = DayCycle.Tint(h);
        _darkness.Color = surface.Lerp(new Color(0.16f, 0.17f, 0.24f), under);

        // The lamp you carry: underground, and (dimmer) on foot at night. In the car the headlights take over.
        float carried = Mathf.Max(under, _driving ? 0.35f * night : 0.75f * night);
        _lamp.Position = focus;
        _lamp.Energy = carried * 1.15f;
        _lamp.TextureScale = _driving ? 1.6f : 1.1f;

        // Headlights come on at dusk and underground (a little after the sun touches the hills).
        float target = Mathf.Clamp(Mathf.Max((night - 0.2f) / 0.4f, (under - 0.4f) / 0.4f), 0f, 1f);
        _car.Lights = target;
        _beams.Car = _car;

        _glints.Reach = Mathf.Lerp(1f, NightGlintReach, night * (1f - under));
        _backdrop.Hours = h;
        _sfx.UpdateNight(night * (1f - under), _driving);

        if (night > 0.9f) _maxGlintsAtNight = Mathf.Max(_maxGlintsAtNight, _glints.Shown);
        if (night < 0.1f) _maxGlintsByDay = Mathf.Max(_maxGlintsByDay, _glints.Shown);
        if (ClockLogging && _elapsed >= _nextNightLog)
        {
            _nextNightLog = _elapsed + 4f;
            GD.Print($"[night] {DayCycle.Format(h)} night={night:0.00} warm={DayCycle.Warmth(h):0.00} tint=({_darkness.Color.R:0.00},{_darkness.Color.G:0.00},{_darkness.Color.B:0.00}) " +
                     $"under={under:0.00} lights={_car.Lights:0.00} headlightsOn={_car.HeadlightsOn} reverseLamp={_car.ReverseLamp:0.00} lamp={_lamp.Energy:0.00} glintReach={_glints.Reach:0.00} glints={_glints.Shown} " +
                     $"crickets={_sfx.CricketLevel:0.00} {(_driving ? "driving" : "on foot")} x={focus.X / Ts:0} frame={Engine.GetFramesDrawn()}");
        }
    }

    /// <summary>"21:40" for the status line (the day number is on the dawn call-out).</summary>
    private static string ClockStatus() => $"{DayCycle.Format(GameSession.ClockHours)}   ";

    private string NightSummary() =>
        $"clock={DayCycle.Format(GameSession.ClockHours)} day={GameSession.Day} nightFalls={_nightFalls} dawns={_dawns} " +
        $"nightSeconds={_nightSeconds:0.0} headlightSeconds={_headlightSeconds:0.0} glintsNight={_maxGlintsAtNight} glintsDay={_maxGlintsByDay}";
}
