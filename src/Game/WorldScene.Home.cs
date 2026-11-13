using Gearheart.Core;
using Gearheart.World;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// Finding your way home. A small arrow at the screen edge points to the garage whenever it is off
/// screen (gold with "PART READY" when you can build something, orange with "LOW FUEL" when the tank
/// runs low), and at dusk the garage lights up: a lamp post on the pad, the shed lit inside, and a
/// searchlight sweeping the sky.
/// </summary>
public partial class WorldScene
{
    private const float HomeArrowFrom = 10f;       // metres from the door before the arrow shows
    private const float LowFuelShare = 0.25f;      // of the tank

    private HomeArrow _homeArrow;
    private BeaconPost _beaconPost;
    private BeaconGlow _beaconGlow;
    private PointLight2D _padLight, _shedLight;
    private Vector2 _searchlight;
    private float _beaconLevel;
    private bool _beaconOn;
    private int _homeLogs, _arrowShows, _beaconOns;
    private string _homeLogKey = "";
    private float _arrowSeconds, _readyArrowSeconds, _lowFuelArrowSeconds, _maxArrowMeters, _nextHomeLog;
    private float _maxBeacon;
    private bool _arrowWrongWay, _arrowWasShown;

    /// <summary>The lamp post, the lights and the glow; called from _Ready once the garage sprite exists.</summary>
    private void SetupBeacon(Sprite2D garage, CanvasLayer glintLayer, Texture2D glow)
    {
        float padY = TileWorld.BaseSurface * Ts;
        float w = Art.Garage.GetWidth() * 2f;
        var foot = new Vector2(garage.Position.X + w + 22f, padY);
        _searchlight = garage.Position + new Vector2(190f, 20f);
        _beaconPost = new BeaconPost { Name = "BeaconPost", Position = foot, Searchlight = _searchlight - foot };
        AddChild(_beaconPost);
        MoveChild(_beaconPost, garage.GetIndex() + 1);

        _padLight = new PointLight2D { Texture = glow, Energy = 0f, TextureScale = 1.25f, Color = new Color(1f, 0.82f, 0.55f), Position = foot + BeaconPost.Bulb + new Vector2(0f, 60f) };
        AddChild(_padLight);
        _shedLight = new PointLight2D { Texture = glow, Energy = 0f, TextureScale = 0.7f, Color = new Color(1f, 0.75f, 0.45f), Position = garage.Position + new Vector2(w / 2f, 110f) };
        AddChild(_shedLight);

        _beaconGlow = new BeaconGlow
        {
            Name = "BeaconGlow",
            Bulb = foot + BeaconPost.Bulb,
            Searchlight = _searchlight,
            Inside = new Rect2(garage.Position + new Vector2(26f, 48f), new Vector2(w - 54f, 96f)),
        };
        glintLayer.AddChild(_beaconGlow);
        glintLayer.MoveChild(_beaconGlow, 0);   // under the stash glints and the crystals
    }

    /// <summary>Called by BuildHud: the arrow sits under the trip card and the flying ore.</summary>
    private void SetupHomeArrow(CanvasLayer hud)
    {
        _homeArrow = new HomeArrow { Name = "HomeArrow" };
        hud.AddChild(_homeArrow);
    }

    private void UpdateHome(float dt)
    {
        if (_homeArrow == null) return;

        // The beacon: on from a little after sunset until dawn.
        float night = DayCycle.Night(GameSession.ClockHours);
        _beaconLevel = Mathf.Clamp((night - 0.15f) / 0.5f, 0f, 1f);
        _maxBeacon = Mathf.Max(_maxBeacon, _beaconLevel);
        _beaconPost.Level = _beaconLevel;
        _beaconPost.BeamAngle = _beaconGlow.BeamAngle;
        _beaconGlow.Level = _beaconLevel;
        _beaconGlow.Visible = _beaconLevel > 0.01f;
        _padLight.Energy = 1.1f * _beaconLevel;
        _shedLight.Energy = 0.8f * _beaconLevel;
        bool on = _beaconLevel > 0.5f;
        if (on != _beaconOn)
        {
            _beaconOn = on;
            if (on) _beaconOns++;
            if (GameSession.AutoPlay)
                GD.Print($"[home] garage lights {(on ? "on" : "off")} at {DayCycle.Format(GameSession.ClockHours)} night={night:0.00} " +
                         $"{MetersToDoor():0} m from the door frame={Engine.GetFramesDrawn()}");
        }

        // The arrow.
        Vector2 door = _garageDoor;
        Transform2D toScreen = GetViewport().CanvasTransform;
        Vector2 onScreen = toScreen * door;
        Vector2 size = GetViewportRect().Size;
        bool visible = new Rect2(-40f, -40f, size.X + 80f, size.Y + 80f).HasPoint(onScreen);
        float meters = MetersToDoor();
        bool show = !visible && meters > HomeArrowFrom && !_card.Showing;
        bool lowFuel = _driving && _car.Fuel < _car.FuelCapacity * LowFuelShare;
        var mode = lowFuel ? HomeArrow.Mode.LowFuel : _partGoal != null && _partGoal.Ready ? HomeArrow.Mode.Ready : HomeArrow.Mode.Plain;
        _homeArrow.Set(onScreen, show, meters, mode);

        if (show)
        {
            if (!_arrowWasShown) _arrowShows++;
            _arrowSeconds += dt;
            if (mode == HomeArrow.Mode.Ready) _readyArrowSeconds += dt;
            if (mode == HomeArrow.Mode.LowFuel) _lowFuelArrowSeconds += dt;
            _maxArrowMeters = Mathf.Max(_maxArrowMeters, meters);
            // The arrow must point the way home: west of the garage it points east, and so on.
            float dx = door.X - Focus.X;
            float ax = Mathf.Cos(_homeArrow.Angle);
            if (Mathf.Abs(dx) > 400f && Mathf.Sign(ax) != Mathf.Sign(dx)) _arrowWrongWay = true;
        }
        _arrowWasShown = show;
        LogHome(show, mode, meters);
    }

    private float MetersToDoor() => (_garageDoor - Focus).Length() / PixelsPerMeter;

    /// <summary>Logs the arrow when it appears, hides or changes mode (and every 6 s while shown).</summary>
    private void LogHome(bool show, HomeArrow.Mode mode, float meters)
    {
        if (!GameSession.AutoPlay || _homeLogs >= 40) return;
        string key = show ? mode.ToString() : "hidden";
        bool periodic = show && _elapsed >= _nextHomeLog;
        if (key == _homeLogKey && !periodic) return;
        if (key != _homeLogKey && show) GD.Print($"[home] arrow on: {mode} frame={Engine.GetFramesDrawn()}");   // the screenshot step greps this line
        _homeLogKey = key;
        _nextHomeLog = _elapsed + 6f;
        _homeLogs++;
        float deg = Mathf.RadToDeg(_homeArrow.Angle);
        GD.Print(show
            ? $"[home] arrow {mode} {meters:0} m to the door, points {deg:0} deg ({(Mathf.Cos(_homeArrow.Angle) < 0f ? "west" : "east")}), tip=({_homeArrow.Tip.X:0},{_homeArrow.Tip.Y:0}) " +
              $"{(_driving ? $"driving fuel={_car.Fuel:0}/{_car.FuelCapacity:0}" : "on foot")} x={Focus.X / Ts:0} t={_elapsed:0.0}s frame={Engine.GetFramesDrawn()}"
            : $"[home] arrow hidden: {meters:0} m to the door, {(_card.Showing ? "report up" : meters <= HomeArrowFrom ? "close" : "garage on screen")} t={_elapsed:0.0}s frame={Engine.GetFramesDrawn()}");
    }

    private string HomeSummary() =>
        $"homeArrow={_arrowShows}x/{_arrowSeconds:0.0}s ready={_readyArrowSeconds:0.0}s lowFuel={_lowFuelArrowSeconds:0.0}s maxDist={_maxArrowMeters:0}m wrongWay={_arrowWrongWay} " +
        $"beacon={_beaconLevel:0.00} beaconMax={_maxBeacon:0.00} beaconOns={_beaconOns}";
}
