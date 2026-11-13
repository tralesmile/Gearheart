using System.Collections.Generic;
using Gearheart.Core;
using Gearheart.Physics;
using Gearheart.Vehicles;
using Gearheart.World;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// The main game: a destructible tile world you explore on foot, mine by hand,
/// and drive across (and drill through) in the car you built at the garage.
/// </summary>
public partial class WorldScene : Node2D
{
    private const float Ts = TileWorld.TileSize;
    private const float PixelsPerMeter = 64f;
    private const float EnterDistance = 120f;
    private const float FootZoom = 2.4f, DriveZoom = 1.5f;

    private TileWorld _world;
    private Backdrop _backdrop;
    private TileWorldView _view;
    private SoftPhysicsWorld _physics;
    private SoftVehicle _car;
    private Player _player;
    private Camera2D _camera;
    private CanvasModulate _darkness;
    private PointLight2D _lamp;
    private Label _hint, _toast, _status;
    private Sfx _sfx;
    private CrashFx _crashFx;
    private TyreSpray _spray;
    private bool _icePlanted;
    private int _iceFrom, _iceTo;          // --icetest: the ice strip (tile columns)
    private float _iceEnterAt = -1f, _iceNextLog;
    private bool _iceDone;
    private StashGlints _glints;
    private CrystalGlow _crystals;
    private HeadlightBeams _beams;
    private bool _deepHinted, _deepPlaced;
    private float _depthAnnounced, _deepestThisRun;
    private int _crystalsMined, _depthRecords;
    private RoadsidePickups _pickups;
    private bool _canHinted;
    private int _cansTaken, _heapsTaken, _heapsSmashed;
    private int _smashStreak, _bestStreak;
    private float _lastSmashAt = -99f;
    private bool _smashToast;
    private const float StreakWindow = 2.5f;     // seconds between smashes that still chain
    private Dashboard _dash;
    private SpeedLines _lines;
    private AirCam _airCam;
    private float _carDir = 1f, _dirCarX, _carVx;
    private float _peakLines;
    private bool _linesLogged, _wasRighting;
    private int _rollOvers;
    private bool _glintHinted, _stashPlanted;
    private bool _crashDropped;
    private float _pickTimer;
    private int _puffs;
    private readonly Label[] _counts = new Label[GameSession.Items.Length];
    private Vector2 _garageDoor;
    private bool _driving;
    private float _toastTime, _elapsed, _nextLog, _saveTimer;
    private float _airTime, _airStartSpeed, _boostSeconds, _topSpeed, _longestAir;

    // Landing grades: PERFECT (speed kick), CLEAN, rough, or a botched one on the body.
    private readonly LandingJudge _judge = new();
    private bool _bodyDown;                // lying on the body after a botched landing: that is not air time
    private float _kickGlow;               // seconds of warm speed streaks left after a perfect-landing kick
    private float _kickCheckAt = -1f, _kickKmhBefore;
    private bool _bodyHitTick;             // the body hit the ground since the last stunt update
    private int _perfectLandings, _cleanLandings, _roughLandings, _flops;

    // Jump kickers on the road: count take-offs from a crest, and tie the next landing to it.
    private int _lastCarCol = int.MinValue;
    private float _kickerAt = -10f, _kickerKmh;
    private int _kickerRise, _kickerLaunches, _kickerJumps, _kickerPerfects;
    private static bool _kickerHinted;    // "KICKER AHEAD" is shown once per game session

    // Long jumps: how far past the crest the car came down, against a record flag per kicker.
    private readonly record struct LongJump(float Meters, float Before, bool Record, bool Counted);
    private JumpMarks _marks;
    private Kicker? _jumpKicker;           // the kicker the car just took off from, until it comes down
    private float _jumpAt = -10f;
    private string _jumpLine;              // "LONG JUMP 7.4 m · NEW RECORD!", waiting for the landing grade
    private float _jumpLineAt = -10f;
    private int _longJumps, _jumpRecords;
    private float _longestJump;
    private readonly HashSet<int> _flagLogged = new();

    // Trips: leave the garage grounds, come back, get a report card with a score and a best to beat.
    private const float TripLeave = 18f, TripHome = 8f;   // metres from the middle of the garage pad
    private Trip _trip;
    private TripCard _card;
    private TripGoal _nextGoal;    // shown at the garage before you leave; becomes the trip's goal
    private Label _goalLabel;
    private int _tripsReported;
    private bool _farthestCheered;
    private int _tripLeg;          // --triptest bot: which leg of the two out-and-back trips
    private float _legStart;

    public override void _Ready()
    {
        GameSession.ApplyCommandLine();
        _world = GameSession.World;
        _world.TileBroken += OnTileBroken;

        var sky = new CanvasLayer { Layer = -10 };
        AddChild(sky);
        _backdrop = new Backdrop();
        sky.AddChild(_backdrop);

        _view = new TileWorldView { Name = "Tiles", World = _world, Focus = TileWorld.PadCenter };
        AddChild(_view);

        // The garage shed on its wooden floor.
        float padY = TileWorld.BaseSurface * Ts;
        var garage = new Sprite2D
        {
            Texture = Art.Garage,
            Centered = false,
            Scale = new Vector2(2f, 2f),
            Position = new Vector2((TileWorld.PadFrom + 1) * Ts, padY - Art.Garage.GetHeight() * 2f),
        };
        AddChild(garage);
        _garageDoor = new Vector2(garage.Position.X + Art.Garage.GetWidth(), padY - 30f);

        // Fuel cans and scrap heaps along the road (drawn behind the car and the player).
        _pickups = new RoadsidePickups { Name = "Pickups", World = _world, Focus = TileWorld.PadCenter };
        AddChild(_pickups);

        // Long-jump record flags past the kickers.
        if (GameSession.JumpTest) PlantJumpRecords();
        _marks = new JumpMarks { Name = "JumpMarks", World = _world, Focus = TileWorld.PadCenter };
        AddChild(_marks);

        _physics = new SoftPhysicsWorld { Name = "SoftPhysicsWorld", Terrain = _world, Mineable = _world };
        AddChild(_physics);

        _crashFx = new CrashFx { Name = "CrashFx", Log = GameSession.AutoPlay, GroundColor = GroundColorAt };
        AddChild(_crashFx);
        _airCam = new AirCam { Name = "AirCam", World = _world };
        AddChild(_airCam);   // before the car: the landing shadow is drawn under it
        SpawnCar();

        _player = new Player
        {
            Name = "Player",
            World = _world,
            Position = new Vector2((TileWorld.PadFrom + 19) * Ts, padY - Player.HalfHeight - 1f),
        };
        AddChild(_player);

        _camera = new Camera2D
        {
            Position = _player.Position,
            Zoom = new Vector2(FootZoom, FootZoom),
            PositionSmoothingEnabled = false,   // AirCam eases the position itself (it knows the car's speed)
        };
        AddChild(_camera);
        _camera.MakeCurrent();

        // Underground it gets dark; a lamp follows the player or the car.
        _darkness = new CanvasModulate { Color = Colors.White };
        AddChild(_darkness);
        var glow = new GradientTexture2D
        {
            Width = 512,
            Height = 512,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
            Gradient = new Gradient
            {
                Offsets = new[] { 0f, 1f },
                Colors = new[] { new Color(1f, 0.95f, 0.8f, 1f), new Color(1f, 0.95f, 0.8f, 0f) },
            },
        };
        _lamp = new PointLight2D { Texture = glow, Energy = 0f, TextureScale = 1.1f };
        AddChild(_lamp);

        // Glints over buried stashes, on their own layer so the underground darkness does not hide them.
        var glintLayer = new CanvasLayer { Layer = 1, FollowViewportEnabled = true };
        AddChild(glintLayer);
        _glints = new StashGlints { Name = "StashGlints", World = _world, Focus = _player.Position };
        glintLayer.AddChild(_glints);
        _crystals = new CrystalGlow { Name = "CrystalGlow", World = _world, Focus = _player.Position };
        glintLayer.AddChild(_crystals);
        glintLayer.MoveChild(_crystals, 0);   // under the stash glints
        _beams = new HeadlightBeams { Name = "HeadlightBeams", Car = _car };
        glintLayer.AddChild(_beams);
        SetupVeins(glintLayer);
        SetupLucky(glintLayer);
        SetupBeacon(garage, glintLayer, glow);
        _depthAnnounced = GameSession.DeepestDig;
        if (GameSession.AutoPlay)
            GD.Print($"[deep] world: slate={_world.CountTiles(Tile.Slate)} crystals={_world.CountTiles(Tile.Crystal)} deepRow at garage={_world.DeepRow((TileWorld.PadFrom + TileWorld.PadTo) / 2)} " +
                     $"({(_world.DeepRow((TileWorld.PadFrom + TileWorld.PadTo) / 2) - TileWorld.BaseSurface) * 0.25f:0.0} m down) record={GameSession.DeepestDig:0.0}m");

        _sfx = new Sfx { Name = "Sfx" };
        AddChild(_sfx);
        _player.Placed += () => _sfx.Play("place");
        SetupHook();
        SetupBirds(garage.Position);
        SetupBats(glintLayer);
        LogPlantCensus();
        _crashFx.Camera = _camera;
        _crashFx.Sfx = _sfx;
        _crashFx.Toast = text =>
        {
            if (_toastTime < 1.2f) Toast(text);   // never hide a fresh stunt call-out
        };

        _spray = new TyreSpray
        {
            Name = "TyreSpray",
            IsIce = p => _world.GripAt(p) < 0.5f,
            Log = GameSession.AutoPlay,
            Toast = text =>
            {
                if (_toastTime >= 1.2f || _card.Showing) return false;   // let the current call-out be read first
                Toast(text);
                return true;
            },
        };
        AddChild(_spray);

        BuildHud();
        StartClock();
        SetupDevils();   // after the clock: a game started at midday has its devils up already
        LogStashes();
        LogPickups();
        GD.Print($"[world] ready: design=\"{GameSession.CurrentDesign.Name}\" autoplay={GameSession.AutoPlay} " +
                 $"player=({_player.Position.X / Ts:0},{_player.Position.Y / Ts:0}) tiles");
    }

    public override void _ExitTree()
    {
        if (_world != null) _world.TileBroken -= OnTileBroken;
        Settings.SetCaveEcho(0f);
        GameSession.SaveAll();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) GameSession.SaveAll();
    }

    private void SpawnCar(float dropHeight = 0f)
    {
        if (IsInstanceValid(_car)) _car.QueueFree();
        float x = (TileWorld.PadFrom + 30) * Ts;
        _car = new SoftVehicle
        {
            Name = "Car",
            World = _physics,
            Design = GameSession.CurrentDesign.Clone(),
            Spawn = new Vector2(x, _world.GroundY(x, (TileWorld.BaseSurface - 12) * Ts) - 75f - dropHeight),
            Parked = true,
            DrillEnabled = false,
        };
        AddChild(_car);
        _crashFx.Watch(_car);
        _car.Crashed += OnCarCrash;
        _car.Gearbox.Shifted += OnShift;
        _car.Backfired += OnBackfire;
        _car.Launched += OnLaunch;
        _car.StuckStarted += OnStuck;
        _car.Rocked += OnRock;
        _car.RockedFree += OnRockedFree;
        _car.TurboBlewOff += OnTurboBlowOff;
        _car.TurboFull += OnTurboFull;
        _car.Hopped += OnHop;
        _car.HopLanded += OnHopLanded;
        _car.AutoRock = !GameSession.NoRock;
        if (GameSession.LowFuel) _car.AddFuel(_car.FuelCapacity * 0.3f - _car.Fuel);
        _judge.Cancel();
        _airTime = 0f;
        _bodyDown = false;
    }

    // ---- Input ------------------------------------------------------------------------------

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (_card.Showing && key.PhysicalKeycode is Key.Enter or Key.KpEnter or Key.Escape)
        {
            _card.Dismiss();
            GetViewport().SetInputAsHandled();
            return;
        }

        switch (key.PhysicalKeycode)
        {
            case Key.F:
                if (_driving) ExitCar();
                else if (NearCar()) EnterCar();
                break;
            case Key.G:
                if (NearGarage()) GetTree().ChangeSceneToFile(GameSession.GarageScene);
                break;
            case Key.R:
                if (_driving || NearCar()) Toast(NearGarageCar() ? RepairCar() : "Bring the car to the garage to repair it");
                break;
            case Key.H:
                if (_driving) Honk();
                break;
            case Key.X:
                if (_driving)
                {
                    _car.DrillEnabled = !_car.DrillEnabled;
                    Toast(_car.DrillEnabled ? "Drill ON" : "Drill OFF");
                }
                break;
            case Key.Key1: _player.SelectedBlock = "Dirt"; break;
            case Key.Key2: _player.SelectedBlock = "Stone"; break;
        }
    }

    private Vector2 Focus => _driving ? _car.CenterPosition : _player.Position;

    /// <summary>What the camera follows: on foot it leans toward a thrown hook so you see where it bites.</summary>
    private Vector2 CameraFocus => !_driving && _player.HookPoint is Vector2 h
        ? _player.Position + (h - _player.Position).LimitLength(Player.HookRange) * 0.35f
        : Focus;
    private bool NearCar() => _player.Position.DistanceTo(_car.CenterPosition) < EnterDistance;
    private bool NearGarage() => Mathf.Abs(Focus.X - _garageDoor.X) < 190f && Mathf.Abs(Focus.Y - _garageDoor.Y) < 140f;
    private bool NearGarageCar() => Mathf.Abs(_car.CenterPosition.X - _garageDoor.X) < 420f && Mathf.Abs(_car.CenterPosition.Y - _garageDoor.Y) < 160f;

    private string RepairCar()
    {
        bool wasDriving = _driving;
        SpawnCar();
        _driving = false;
        if (wasDriving) EnterCar();
        return "Car repaired and refuelled";
    }

    private void EnterCar()
    {
        _driving = true;
        _player.Active = false;
        _player.Visible = false;
        _car.Parked = false;
        _car.Occupied = true;
        _car.PlayerControlled = !GameSession.AutoPlay;
        _car.AutoThrottle = GameSession.AutoPlay ? (GameSession.DriveLeft ? -1f : 1f) : 0f;
        if (GameSession.AutoPlay) _car.DrillEnabled = true;
        _car.AutoBoost = GameSession.AutoPlay && _car.HasBooster;
        _sfx.StartEngine(_car.EngineSound);
        _birds?.Startle(_car.CenterPosition, EngineStartReach, "engine");
        _enteredAt = _elapsed;
        _stallLines = 0;
        if (GameSession.AutoPlay)
        {
            GD.Print($"[engine] start: {_car.EngineName}, {_car.Gearbox.Gears} gears, loop {_car.EngineSound} frame={Engine.GetFramesDrawn()}");
            if (GameSession.LaunchTest) _car.AutoHandbrake = true;   // RunLaunchBot lets go
            else StartWatch("plain");
        }
    }

    // ---- Launch control (see SoftVehicle.UpdateLaunch) ------------------------------------------

    private float _enteredAt;
    private float _nextStallLog;
    private int _stallLines, _perfectStarts, _goodStarts, _boggedStarts;
    private string _watchKind;
    private float _watchAt = -1f, _watchX;
    private int _watchStage;

    private void OnLaunch(LaunchGrade grade, float kick)
    {
        if (!_driving) return;
        _lastLaunch = grade.ToString().ToLowerInvariant();
        _lastLaunchAt = _elapsed;
        float kmh = kick / PixelsPerMeter * 3.6f;
        switch (grade)
        {
            case LaunchGrade.Perfect:
                _perfectStarts++;
                _kickGlow = 0.8f;
                Toast($"PERFECT START!\n+{kmh:0} km/h");
                _sfx.Play("land", -4f, 0.2f, 1.1f);
                break;
            case LaunchGrade.Good:
                _goodStarts++;
                Toast($"GOOD START\n+{kmh:0} km/h · let go in the green for more");
                _sfx.Play("pickup", -6f);
                break;
            default:
                _boggedStarts++;
                Toast("BOGGED DOWN\nhold Space + throttle, let go in the green");
                _sfx.Play("thud", -14f, 0.1f, 0.8f);
                break;
        }
        if (GameSession.AutoPlay)
        {
            GD.Print($"[launch] {grade} start: charge {_car.LaunchCharge:0.00} bounces={_car.LaunchBounces} kick {kmh:0} km/h revs {_car.Gearbox.Rpm:0.00} t={_elapsed:0.00}s frame={Engine.GetFramesDrawn()}");
            StartWatch(grade.ToString().ToLowerInvariant());
        }
    }

    /// <summary>Bots: measure a start (speed 0.5 s after, distance 1 s and 2 s after) for the playtest log.</summary>
    private void StartWatch(string kind)
    {
        _watchKind = kind;
        _watchAt = _elapsed;
        _watchX = _car.CenterPosition.X;
        _watchStage = 0;
    }

    private void UpdateWatch()
    {
        if (_watchAt < 0f || !_driving) return;
        float since = _elapsed - _watchAt;
        float meters = Mathf.Abs(_car.CenterPosition.X - _watchX) / PixelsPerMeter;
        float kmh = _car.Speed / PixelsPerMeter * 3.6f;
        if (_watchStage == 0 && since >= 0.5f) { _watchStage = 1; GD.Print($"[launch] {_watchKind} +0.5s: {kmh:0} km/h, {meters:0.0} m"); }
        else if (_watchStage == 1 && since >= 1f) { _watchStage = 2; GD.Print($"[launch] {_watchKind} +1s: {kmh:0} km/h, {meters:0.0} m"); }
        else if (_watchStage == 2 && since >= 2f)
        {
            _watchStage = 3;
            _watchAt = -1f;
            GD.Print($"[launch] {_watchKind} +2s: {kmh:0} km/h, {meters:0.0} m, gear {_car.Gearbox.Gear}");
        }
    }

    /// <summary>
    /// --launchtest: get straight in, hold the handbrake and the throttle, and let go when the charge is
    /// in the green (or, with --launchearly, at a third of the way up). The first try deliberately rides
    /// the limiter once, so the bounce is tested too.
    /// </summary>
    private int _launchLeg;   // 0 first start, 1 reversing back onto the pad, 2 second start

    private void RunLaunchBot()
    {
        if (!_driving && _elapsed > 0.5f && _launchLeg == 0) EnterCar();
        if (_driving && _launchLeg == 1)
        {
            // Back up onto the pad, then hold the handbrake + throttle for a second launch.
            if (MetersFromGarage(_car.CenterPosition.X) > 1.5f) _car.AutoThrottle = -1f;
            else
            {
                _car.AutoThrottle = 1f;
                _car.AutoHandbrake = true;
                _launchLeg = 2;
                GD.Print($"[sprint] test: on the pad again at t={_elapsed:0.0}s, second launch");
            }
            return;
        }
        if (!_driving || !_car.AutoHandbrake) return;
        float letGoAt = GameSession.LaunchEarly ? 0.3f : 0.86f;
        bool ready = GameSession.LaunchEarly || _car.LaunchBounces >= 1;
        if (_car.LaunchArmed && ready && _car.LaunchCharge >= letGoAt)
            _car.AutoHandbrake = false;
    }

    /// <summary>Bots: why is the car not moving right after getting in? Per-wheel numbers, a few lines per run.</summary>
    private void LogStall()
    {
        if (!_driving || _car.LaunchArmed || _stallLines >= 8) return;
        float since = _elapsed - _enteredAt;
        if (since < 0.5f || since > 4.5f || _car.Speed > 60f || _elapsed < _nextStallLog) return;
        _nextStallLog = _elapsed + 0.5f;
        _stallLines++;
        GD.Print($"[stall] t={_elapsed:0.0}s {since:0.0}s after getting in: speed {_car.Speed:0} px/s pitch {_car.PitchDegrees():0} {_car.StallReport()}");
    }

    // ---- Rock it free (see SoftVehicle.UpdateRock) ---------------------------------------------

    private int _stuckCount, _rocksLogged, _rockFrees;
    private float _longestStuck;

    private void OnStuck()
    {
        if (!_driving) return;
        _stuckCount++;
        Toast("STUCK!\nRock it free: tap Q, E, Q, E");
        _sfx.Play("thud", -18f, 0.1f, 0.7f);
        if (GameSession.AutoPlay && _stuckCount <= 6)
            GD.Print($"[rock] stuck #{_stuckCount} at x={_car.CenterPosition.X / Ts:0.0} pitch={_car.PitchDegrees():0} t={_elapsed:0.0}s frame={Engine.GetFramesDrawn()} | {_car.StallReport()}");
    }

    private void OnRock(int chain, float hop)
    {
        if (!_driving) return;
        _sfx.Play("creak", -10f + 1.5f * chain, 0.06f, 0.9f + 0.07f * chain);
        foreach (var contact in _car.WheelContacts)
            _crashFx.Dust(contact.Point, contact.Normal, GroundColorAt(contact.Point - contact.Normal * 4f), 5 + 2 * chain, 50f + 25f * chain);
        _crashFx.AddTrauma(0.05f + 0.025f * chain);
        Toast(chain >= 2 ? $"ROCK IT! x{chain}\nkeep the throttle on" : "ROCK IT!\nkeep tapping Q, E");
        if (GameSession.AutoPlay && _rocksLogged < 16)
        {
            _rocksLogged++;
            GD.Print($"[rock] rock chain={chain} hop={hop:0}px/s x={_car.CenterPosition.X / Ts:0.0} speed={_car.Speed / PixelsPerMeter * 3.6f:0}km/h t={_elapsed:0.00}s frame={Engine.GetFramesDrawn()}");
        }
    }

    private void OnRockedFree(int rocks, float seconds)
    {
        if (!_driving) return;
        _longestStuck = Mathf.Max(_longestStuck, seconds);
        if (rocks > 0)
        {
            _rockFrees++;
            Toast($"ROCKED FREE!\n{rocks} rock{(rocks == 1 ? "" : "s")} · {seconds:0.0} s");
            _sfx.Play("land", -6f, 0.15f, 1.05f);
            _kickGlow = Mathf.Max(_kickGlow, 0.5f);
        }
        if (GameSession.AutoPlay)
            GD.Print($"[rock] free after {seconds:0.0}s with {rocks} rocks at x={_car.CenterPosition.X / Ts:0.0} speed={_car.Speed / PixelsPerMeter * 3.6f:0}km/h t={_elapsed:0.0}s frame={Engine.GetFramesDrawn()}");
    }

    // ---- Engine: gear shifts and exhaust pops (sound and smoke only, see Gearbox) --------------

    private int _shiftsLogged;

    private void OnShift(int gear, bool up)
    {
        if (!_driving) return;
        float pitch = Sfx.EnginePitch(_car.EngineSound);
        _sfx.Play("shift", up ? -13f : -18f, 0.08f, pitch * (up ? 1f : 1.15f));
        if (GameSession.AutoPlay && _shiftsLogged < 14)
        {
            _shiftsLogged++;
            GD.Print($"[engine] {(up ? "upshift" : "downshift")} to {gear} at {_car.Speed / PixelsPerMeter * 3.6f:0} km/h, revs {_car.Gearbox.Rpm:0.00}, " +
                     $"pitch {_sfx.LastEnginePitch:0.00}, throttle {_car.Throttle:0.0} frame={Engine.GetFramesDrawn()}");
        }
    }

    private void OnBackfire(bool liftOff)
    {
        if (!_driving) return;
        _sfx.Play("pop", liftOff ? -9f : -12f, 0.15f, Sfx.EnginePitch(_car.EngineSound));
        if (GameSession.AutoPlay && _car.Backfires <= 6)
            GD.Print($"[engine] backfire #{_car.Backfires} ({(liftOff ? "lift-off" : "upshift")}) revs {_car.Gearbox.Rpm:0.00} frame={Engine.GetFramesDrawn()}");
    }

    private void ExitCar()
    {
        _driving = false;
        _car.Parked = true;
        _car.Occupied = false;
        _car.PlayerControlled = false;

        // Step out beside the car, or on top of it if both sides are blocked.
        Vector2 c = _car.CenterPosition;
        Vector2 spot = c + new Vector2(0f, -70f);
        foreach (var offset in new[] { new Vector2(-95f, -20f), new Vector2(95f, -20f), new Vector2(-95f, -50f), new Vector2(95f, -50f) })
        {
            if (_player.Blocked(c.X + offset.X, c.Y + offset.Y)) continue;
            spot = c + offset;
            break;
        }
        _player.Position = spot;
        _player.Velocity = Vector2.Zero;
        _player.Active = true;
        _player.Visible = true;
    }

    // ---- Frame updates ----------------------------------------------------------------------

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _elapsed += dt;
        if (GameSession.AutoPlay) RunBot();
        AdvanceClock(dt);
        if (_driving) TrackStunts(dt);
        else _judge.Cancel();
        UpdateDevils(dt);
        _bodyHitTick = false;
        _spray.Track(_car, dt, _driving);
        CheckPickups();
        UpdatePlants(dt);
        UpdateSprint();
        UpdateTrip();

        _saveTimer += dt;
        if (_saveTimer > 60f)
        {
            _saveTimer = 0f;
            GameSession.SaveAll();
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        Vector2 focus = Focus;
        // Pull the camera back at speed so jumps and what is ahead stay in view; look ahead the
        // way the car is going, and frame the landing on a big jump (AirCam).
        float driveZoom = DriveZoom - 0.4f * Mathf.Clamp((_car.Speed - 500f) / 1000f, 0f, 1f);
        bool flying = _driving && _car.Airborne && !_bodyDown && !_car.SelfRighting;
        _airCam.Step(dt, _driving, CameraFocus, _car.CenterVelocity, flying, flying ? _airTime : 0f,
                     _driving ? driveZoom : FootZoom, _camera.Zoom.X, GetViewportRect().Size);
        _camera.Position = _airCam.Target;
        _view.Focus = _airCam.Target;
        float zoomRate = _airCam.Zoom < _camera.Zoom.X - 0.01f && _airCam.Blend > 0.05f ? 3f : 2f;
        float zoom = Mathf.Lerp(_camera.Zoom.X, _airCam.Zoom, 1f - Mathf.Exp(-zoomRate * dt));
        _camera.Zoom = new Vector2(zoom, zoom);
        if (GameSession.AutoPlay) LogAirCam(flying);

        // Darkness grows with depth below the surface.
        float depth = focus.Y / Ts - _world.SurfaceRow(Mathf.FloorToInt(focus.X / Ts));
        float under = Mathf.Clamp((depth - 3f) / 12f, 0f, 1f);
        ApplyDaylight(focus, under);
        _glints.Focus = focus;
        UpdateLucky(focus);
        _crystals.Focus = focus;
        _crystals.Darkness = under;
        UpdateDepth(focus);
        _marks.Focus = focus;
        UpdateBirds(focus);
        UpdateBats(focus, dt);
        if (!_glintHinted && _glints.Nearest < 230f && _toastTime <= 0f)
        {
            _glintHinted = true;
            Toast("Something glints in the rock...");
        }
        _backdrop.CameraPosition = _camera.GetScreenCenterPosition();
        _backdrop.Underground = under;
        (_backdrop.Desert, _backdrop.Frozen) = TileWorld.BiomeBlend(focus.X);

        // Which way the car is travelling (for the wind streaks); ignore tiny jitters.
        float moved = _car.CenterPosition.X - _dirCarX;
        if (Mathf.Abs(moved) > 0.5f) _carDir = Mathf.Sign(moved);
        if (dt > 0f) _carVx = Mathf.Lerp(_carVx, Mathf.Abs(moved) / dt, 1f - Mathf.Exp(-12f * dt));
        _dirCarX = _car.CenterPosition.X;

        _crashFx.Enabled = _driving;
        _sfx.UpdateCar(_driving, _car.Speed, _car.Throttle, _car.IsDrilling && _car.DrillEnabled, _car.Boosting,
                       _car.EngineSound, _car.Gearbox.Rpm, _car.Gearbox.Limiter);
        _sfx.UpdateTurbo(_driving, _car.TurboBoost, _car.Gearbox.Rpm);
        _pickTimer -= dt;
        if (!_driving && _player.IsMining && _pickTimer <= 0f)
        {
            _sfx.Play("pick", -10f);
            _pickTimer = 0.22f;
        }

        UpdateVeins(dt);
        UpdateHud(dt);
    }

    // ---- The Deep and the depth record --------------------------------------------------------

    private void UpdateDepth(Vector2 focus)
    {
        int tx = Mathf.FloorToInt(focus.X / Ts), ty = Mathf.FloorToInt(focus.Y / Ts);
        float meters = Mathf.Max(0f, (focus.Y / Ts - _world.SurfaceRow(tx)) * 0.25f);
        _deepestThisRun = Mathf.Max(_deepestThisRun, meters);

        // First time down in the Deep (this session): a title card, so arriving feels like an event.
        if (!_deepHinted && ty >= _world.DeepRow(tx) && _toastTime < 1.2f && !_card.Showing)
        {
            _deepHinted = true;
            _toast.Text = "THE DEEP\nglow crystals grow on the cavern walls: dig them out";
            _toastTime = 3.5f;
            _smashToast = _goalToast = false;
            _sfx?.Play("record", -9f, 0.5f, 0.75f);
            if (GameSession.AutoPlay) GD.Print($"[deep] entered the Deep at depth={meters:0.0}m tile=({tx},{ty}) deepRow={_world.DeepRow(tx)} {(_driving ? "driving" : "on foot")} frame={Engine.GetFramesDrawn()}");
        }

        // Depth record: kept across worlds; called out every 2.5 m past the old record.
        if (meters <= GameSession.DeepestDig) return;
        GameSession.DeepestDig = meters;
        if (meters >= 5f && meters - _depthAnnounced >= 2.5f && _toastTime < 1.2f && !_card.Showing)
        {
            _depthAnnounced = meters;
            _depthRecords++;
            Toast($"NEW DEPTH RECORD\n{meters:0} m down");
            _sfx?.Play("record", -10f, 0.5f, 1.1f);
            if (GameSession.AutoPlay) GD.Print($"[deep] depth record {meters:0.0}m {(_driving ? "driving" : "on foot")} frame={Engine.GetFramesDrawn()}");
        }
    }

    // ---- Mining results ---------------------------------------------------------------------

    private void OnTileBroken(int x, int y, Tile tile)
    {
        _sfx?.Play("break", -9f, 0.06f);
        _birds?.Startle(new Vector2((x + 0.5f) * Ts, (y + 0.5f) * Ts), 160f, "dig");
        _bats?.Startle(new Vector2((x + 0.5f) * Ts, (y + 0.5f) * Ts), Bats.DigReach, "dig", false);
        Puff(x, y, tile);
        if (TileWorld.IsStash(tile))
        {
            OpenStash(x, y, tile);
            return;
        }

        var (item, amount) = TileWorld.Drop(tile);
        if (item == null) return;
        if (item != "Dirt" && item != "Stone") _sfx?.Play("pickup", -8f, 0.1f);
        GameSession.Add(item, amount);
        FlyHome(TileCenter(x, y), item, amount);
        if (IsOre(tile))
        {
            if (LuckyOre.IsLucky(tile, x, y)) OnLuckyStrike(x, y, tile, item, amount);
            int bonus = OnOreMined(x, y, tile, item, amount);
            if (bonus > 0)
            {
                GameSession.Add(item, bonus);
                FlyHome(TileCenter(x, y) - new Vector2(0f, 12f), item, bonus);
            }
        }
        if (item == "Crystal")
        {
            _crystalsMined += amount;
            Toast($"+{amount} Crystal   ({GameSession.Count("Crystal")})" + (GameSession.Count("Crystal") == 4 ? "\nenough for a Crystal Drill (garage)" : ""));
            if (GameSession.AutoPlay) GD.Print($"[deep] crystal mined at ({x},{y}) total={GameSession.Count("Crystal")} {(_driving ? "by the car" : "on foot")} frame={Engine.GetFramesDrawn()}");
        }
        // Other ore pops its amount where it broke (MiningPops) instead of taking over the call-out line.
        if (GameSession.AutoPlay && item != "Dirt" && item != "Stone") GD.Print($"[mining] +{amount} {item} at ({x},{y})");
    }

    /// <summary>The big find: a buried stash pays out a bundle of materials with a fanfare and a gold fountain.</summary>
    private void OpenStash(int x, int y, Tile tile)
    {
        var loot = TileWorld.StashLoot(tile, x, y);
        foreach (var (item, amount) in loot)
        {
            GameSession.Add(item, amount);
            FlyHome(TileCenter(x, y), item, amount);
        }
        GameSession.StashesFound++;
        GameSession.SaveAll();

        string items = string.Join("   ", loot.ConvertAll(l => $"+{l.Amount} {l.Item}"));
        _toast.Text = $"{TileWorld.StashName(tile)}!\n{items}";
        _toastTime = 3.5f;
        _sfx?.Play("treasure", -4f, 0.2f, 1f);
        var at = new Vector2((x + 0.5f) * Ts, (y + 0.5f) * Ts);
        _glints.Burst(at, tile == Tile.StashBig);
        _crashFx.AddTrauma(tile == Tile.StashBig ? 0.45f : 0.3f);

        int depth = y - _world.SurfaceRow(x);
        if (GameSession.AutoPlay)
            GD.Print($"[stash] found {TileWorld.StashName(tile).ToLowerInvariant()} at ({x},{y}) depth={depth * 0.25f:0.0}m by {(_driving ? "drill" : "pickaxe")}: {items} | " +
                     $"stashesFound={GameSession.StashesFound} " + string.Join(" ", System.Array.ConvertAll(GameSession.Items, i => $"{i}={GameSession.Count(i)}")));
    }

    // ---- Roadside pickups ----------------------------------------------------------------------

    /// <summary>Drive through a fuel can to top up; smash through a scrap heap (or walk into it) for its loot.</summary>
    private void CheckPickups()
    {
        _pickups.Focus = Focus;
        Rect2 box = _driving
            ? _car.Bounds().Grow(2f)
            : new Rect2(_player.Position - new Vector2(Player.HalfWidth, Player.HalfHeight), new Vector2(Player.HalfWidth * 2f, Player.HalfHeight * 2f));

        foreach (Pickup p in _pickups.Touching(box))
        {
            Rect2 at = RoadsidePickups.Box(p);
            float kmh = _driving ? _car.Speed / PixelsPerMeter * 3.6f : 0f;
            if (p.Kind == PickupKind.FuelCan)
            {
                if (!_driving)
                {
                    if (!_canHinted && _toastTime <= 0f)
                    {
                        _canHinted = true;
                        Toast("A fuel can: drive through it to top up the tank");
                    }
                    continue;
                }
                if (_car.Fuel > _car.FuelCapacity - RoadsidePickups.MinRoom) continue;   // nearly full: leave it for later
                float before = _car.Fuel;
                _car.AddFuel(RoadsidePickups.FuelAmount);
                _pickups.Take(p);
                _cansTaken++;
                _sfx.Play("fuel", -5f, 0.1f, 1f);
                Burst(at.GetCenter(), new Color(1f, 0.82f, 0.3f), 14, 140f);
                if (_toastTime < 1.2f) Toast($"+{_car.Fuel - before:0} Fuel");
                if (GameSession.AutoPlay)
                    GD.Print($"[pickup] fuel can at x={p.X}: fuel {before:0} -> {_car.Fuel:0}/{_car.FuelCapacity:0} at {kmh:0} km/h frame={Engine.GetFramesDrawn()}");
                continue;
            }

            var loot = RoadsidePickups.HeapLoot(p.X);
            foreach (var (item, amount) in loot)
            {
                GameSession.Add(item, amount);
                FlyHome(at.GetCenter(), item, amount);
            }
            _pickups.Take(p);
            _heapsTaken++;
            string items = string.Join("   ", loot.ConvertAll(l => $"+{l.Amount} {l.Item}"));
            bool smash = kmh > 15f;
            Color scrap = new(0.72f, 0.75f, 0.8f);
            float kmhAfter = kmh;
            if (smash)
            {
                // Plough through at speed: junk flies, metal crunches, the camera kicks a little,
                // and the car loses a bit of speed so the heap has weight.
                _heapsSmashed++;
                _smashStreak = _elapsed - _lastSmashAt <= StreakWindow ? _smashStreak + 1 : 1;
                _lastSmashAt = _elapsed;
                _bestStreak = Mathf.Max(_bestStreak, _smashStreak);
                _trip?.NoteStreak(_smashStreak);
                int bonus = _smashStreak >= 3 ? 1 : 0;   // a chain of three or more pays a little extra
                if (bonus > 0)
                {
                    GameSession.Add("Scrap", bonus);
                    FlyHome(at.GetCenter(), "Scrap", bonus);
                    items += $"   +{bonus} combo";
                }
                float rise = Mathf.Min(_smashStreak - 1, 5);
                _sfx.Play("crunch", -11f + rise * 0.6f, 0.1f, 1.35f + 0.1f * rise);
                _sfx.Play("pickup", -9f, 0.1f, 1f + 0.08f * rise);
                Burst(at.GetCenter(), scrap, 22 + 4 * (int)rise, 120f + kmh * 4f, _car.Speed > 0f ? Mathf.Sign(_car.CenterPosition.X - _lastCarX) : 0f);
                Burst(at.GetCenter(), new Color(0.3f, 0.3f, 0.34f), 8, 90f + kmh * 3f);
                _crashFx.AddTrauma(0.12f + Mathf.Min(0.15f, kmh / 400f) + 0.03f * rise);
                if (!_car.Airborne)   // slowing a car in mid-air would wreck its jump
                {
                    _car.Slow(HeapKeep);
                    kmhAfter = kmh * HeapKeep;
                }
                if (_toastTime < 1.2f || _smashToast)
                {
                    Toast(_smashStreak > 1 ? $"SMASH x{_smashStreak}!\n{items}" : $"SMASH!\n{items}");
                    _smashToast = true;
                }
            }
            else
            {
                _sfx.Play("pickup", -7f, 0.1f);
                Burst(at.GetCenter(), scrap, 10, 90f);
                if (_toastTime < 1.2f) Toast(items);
            }
            if (GameSession.AutoPlay)
                GD.Print($"[pickup] scrap heap at x={p.X} {(smash ? $"smashed (streak x{_smashStreak}, speed {kmh:0} -> {kmhAfter:0} km/h)" : "picked up")} at {kmh:0} km/h ({(_driving ? "car" : "on foot")}): {items} frame={Engine.GetFramesDrawn()} | " +
                         string.Join(" ", System.Array.ConvertAll(GameSession.Items, i => $"{i}={GameSession.Count(i)}")));
        }
        _lastCarX = _car.CenterPosition.X;
    }

    private float _lastCarX;

    /// <summary>Share of its speed the car keeps when it ploughs through a scrap heap.</summary>
    private const float HeapKeep = 0.9f;

    /// <summary>A one-shot spray of bits; <paramref name="forward"/> (-1, 0 or 1) throws them ahead of the car.</summary>
    private void Burst(Vector2 at, Color color, int amount, float speed, float forward = 0f)
    {
        var burst = new CpuParticles2D
        {
            Position = at,
            OneShot = true, Emitting = true, Amount = amount, Lifetime = 0.8f, Explosiveness = 1f,
            Direction = new Vector2(forward * 0.8f, -1f).Normalized(), Spread = forward == 0f ? 80f : 50f,
            InitialVelocityMin = speed * 0.4f, InitialVelocityMax = speed, Gravity = new Vector2(0f, 600f),
            AngularVelocityMin = -360f, AngularVelocityMax = 360f,
            ScaleAmountMin = 2f, ScaleAmountMax = 4.5f, Color = color,
        };
        AddChild(burst);
        GetTree().CreateTimer(1.2).Timeout += () =>
        {
            if (IsInstanceValid(burst)) burst.QueueFree();
        };
    }

    /// <summary>Bot log: what lies along the road.</summary>
    private void LogPickups()
    {
        if (!GameSession.AutoPlay) return;
        int cans = 0, heaps = 0, nearest = int.MaxValue;
        int garageX = (TileWorld.PadFrom + TileWorld.PadTo) / 2;
        foreach (Pickup p in _pickups.All)
        {
            if (p.Kind == PickupKind.FuelCan) cans++; else heaps++;
            nearest = Mathf.Min(nearest, Mathf.Abs(p.X - garageX));
        }
        GD.Print($"[pickup] road has {cans} fuel cans and {heaps} scrap heaps; nearest {nearest * 0.25f:0} m from the garage");
        var kickers = new List<Kicker>(_world.Kickers);
        kickers.Sort((a, b) => a.Crest.CompareTo(b.Crest));
        GD.Print($"[kicker] world has {kickers.Count} kickers: " + string.Join(" ", kickers.ConvertAll(k =>
            $"{(k.Dir > 0 ? "E" : "W")}{Mathf.Abs(k.Crest - garageX) * 0.25f:0}m/{k.Meters:0.00}m/{TileWorld.BiomeAt(k.Crest)}")));
    }

    /// <summary>Bot log: how many stashes the world has and how deep they lie.</summary>
    private void LogStashes()
    {
        if (!GameSession.AutoPlay) return;
        int small = 0, mid = 0, big = 0, nearestDx = int.MaxValue;
        float minDepth = float.MaxValue, maxDepth = 0f;
        int garageX = (TileWorld.PadFrom + TileWorld.PadTo) / 2;
        foreach (Vector2I s in _world.Stashes)
        {
            Tile t = _world.Get(s.X, s.Y);
            if (t == Tile.StashSmall) small++; else if (t == Tile.StashMid) mid++; else if (t == Tile.StashBig) big++; else continue;
            float depth = (s.Y - _world.SurfaceRow(s.X)) * 0.25f;
            minDepth = Mathf.Min(minDepth, depth);
            maxDepth = Mathf.Max(maxDepth, depth);
            nearestDx = Mathf.Min(nearestDx, Mathf.Abs(s.X - garageX));
        }
        GD.Print($"[stash] world has {small + mid + big} stashes: {small} toolboxes, {mid} supply crates, {big} strongboxes; " +
                 $"depth {minDepth:0.0}-{maxDepth:0.0} m; nearest {nearestDx * 0.25f:0} m from the garage");
    }

    /// <summary>A small burst of debris in the tile's colour where it broke.</summary>
    private void Puff(int x, int y, Tile tile)
    {
        if (_puffs >= 14) return;
        _puffs++;
        Color color = TileColor(tile);
        var puff = new CpuParticles2D
        {
            Position = new Vector2((x + 0.5f) * Ts, (y + 0.5f) * Ts),
            OneShot = true, Emitting = true, Amount = 8, Lifetime = 0.5f, Explosiveness = 1f, Spread = 180f,
            InitialVelocityMin = 30f, InitialVelocityMax = 110f, Gravity = new Vector2(0f, 500f),
            ScaleAmountMin = 2f, ScaleAmountMax = 4f, Color = color,
        };
        AddChild(puff);
        GetTree().CreateTimer(0.8).Timeout += () =>
        {
            _puffs--;
            if (IsInstanceValid(puff)) puff.QueueFree();
        };
    }

    private Color GroundColorAt(Vector2 point)
    {
        int tx = Mathf.FloorToInt(point.X / Ts), ty = Mathf.FloorToInt(point.Y / Ts);
        Tile tile = _world.Get(tx, ty);
        if (tile == Tile.Air) tile = _world.Get(tx, ty + 1);
        return tile == Tile.Air ? new Color(0.6f, 0.55f, 0.48f) : TileColor(tile);
    }

    private static Color TileColor(Tile tile) => tile switch
        {
            Tile.Grass or Tile.Dirt => new Color(0.52f, 0.33f, 0.22f),
            Tile.Sand => new Color(0.89f, 0.77f, 0.49f),
            Tile.Sandstone => new Color(0.77f, 0.59f, 0.38f),
            Tile.Snow => new Color(0.95f, 0.97f, 1f),
            Tile.Ice => new Color(0.6f, 0.82f, 0.93f),
            Tile.ScrapOre => new Color(0.8f, 0.82f, 0.86f),
            Tile.IronOre => new Color(0.71f, 0.4f, 0.29f),
            Tile.CopperOre => new Color(0.91f, 0.55f, 0.24f),
            Tile.StashSmall or Tile.StashMid or Tile.StashBig => new Color(0.95f, 0.8f, 0.35f),
            Tile.Slate => new Color(0.27f, 0.31f, 0.42f),
            Tile.Crystal => new Color(0.45f, 0.95f, 1f),
            _ => new Color(0.47f, 0.49f, 0.52f),
        };

    // ---- HUD --------------------------------------------------------------------------------

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        // Wind streaks first, so every label is drawn over them.
        _lines = new SpeedLines { Name = "SpeedLines" };
        layer.AddChild(_lines);

        // The car's dashboard, bottom-left (only while driving).
        _dash = new Dashboard { Name = "Dashboard", Visible = false };
        _dash.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        _dash.Position = new Vector2(16f, -16f - _dash.CustomMinimumSize.Y);
        layer.AddChild(_dash);

        _hint = Outlined(new Label { Position = new Vector2(16f, 12f) }, 18);
        layer.AddChild(_hint);

        _status = Outlined(new Label { HorizontalAlignment = HorizontalAlignment.Right }, 20);
        _status.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        // Own row under the controls hint, right-aligned in a box wide enough for the driving status.
        _status.Position += new Vector2(-836f, 44f);
        _status.CustomMinimumSize = new Vector2(820f, 0f);
        layer.AddChild(_status);

        // The trip goal, in gold under the status line.
        _goalLabel = Outlined(new Label { HorizontalAlignment = HorizontalAlignment.Right }, 19);
        _goalLabel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _goalLabel.Position += new Vector2(-836f, 72f);
        _goalLabel.CustomMinimumSize = new Vector2(820f, 0f);
        layer.AddChild(_goalLabel);

        _toast = Outlined(new Label { HorizontalAlignment = HorizontalAlignment.Center }, 28);
        _toast.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        // 400 px centred under the top edge; a longer line grows both ways and stays centred
        // (it used to grow only to the right, so every long call-out sat off-centre).
        _toast.OffsetLeft = -200f;
        _toast.OffsetRight = 200f;
        _toast.OffsetTop = 110f;
        _toast.OffsetBottom = 110f;
        _toast.CustomMinimumSize = new Vector2(400f, 0f);
        _toast.GrowHorizontal = Control.GrowDirection.Both;
        layer.AddChild(_toast);

        // The trip report, centred; it pops up when you get back to the garage.
        _card = new TripCard { Name = "TripCard", Sfx = _sfx };
        layer.AddChild(_card);
        _card.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center, Control.LayoutPresetMode.KeepSize);

        // Inventory bar along the bottom.
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 18);
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        panel.GrowHorizontal = Control.GrowDirection.Both;
        panel.GrowVertical = Control.GrowDirection.Begin;
        panel.Position += new Vector2(0f, -12f);
        panel.AddChild(bar);
        layer.AddChild(panel);

        for (int i = 0; i < GameSession.Items.Length; i++)
        {
            var slot = new HBoxContainer();
            slot.AddChild(_slotIcons[i] = new TextureRect
            {
                Texture = new AtlasTexture { Atlas = Art.Tiles, Region = Art.ItemIcon(GameSession.Items[i]) },
                CustomMinimumSize = new Vector2(32f, 32f),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
            });
            _counts[i] = Outlined(new Label(), 20);
            slot.AddChild(_counts[i]);
            bar.AddChild(slot);
        }

        // The way home: an arrow at the screen edge when the garage is off screen.
        SetupHomeArrow(layer);

        // The part you are closest to building, in a card above the bar.
        SetupNextPart(layer);

        // Mined ore flies into its slot over the bar.
        SetupFlyHome(layer);

        // Esc / Start: the pause menu, over everything else.
        SetupPause();
    }

    private static Label Outlined(Label label, int size)
    {
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 5);
        return label;
    }

    private static string BiomeName(float worldX) => TileWorld.BiomeAt(Mathf.FloorToInt(worldX / Ts)) switch
    {
        Biome.Desert => "The Wastes",
        Biome.Frozen => "Frozen North",
        _ => "Home Meadow",
    };

    private void Toast(string text)
    {
        _smashToast = false;
        _goalToast = false;
        _toast.Text = text;
        _toastTime = 2f;
    }

    private void UpdateHud(float dt)
    {
        for (int i = 0; i < _counts.Length; i++)
        {
            string item = GameSession.Items[i];
            bool selected = !_driving && item == _player.SelectedBlock;
            // The number goes up when the flying icon lands, and flashes in the ore's colour.
            _counts[i].Text = $"{ShownCount(i)}{(i < 2 ? $" [{i + 1}]" : "")}";
            Color baseColor = selected ? new Color(1f, 0.9f, 0.4f) : Colors.White;
            float bump = _flight?.Bump(i) ?? 0f;
            _counts[i].Modulate = bump > 0f ? baseColor.Lerp(_flight.BumpColor(i) * 1.3f, bump) : baseColor;
        }

        if (_driving)
        {
            _hint.Text = (_car.Stuck ? "STUCK! Tap Q, E, Q, E to rock it free (keep the throttle on)   "
                          : _car.Speed < 60f && !_car.Airborne ? "Hold Space + D, let go in the green: PERFECT START   " : "") +
                         "A/D: drive   Q/E: tilt (also rolls you back over)   Space: handbrake   H: horn   F: get out" +
                         (_car.HasDrill ? $"   X: drill {(_car.DrillEnabled ? "ON" : "OFF")}" : "") +
                         (_car.HasBooster ? "   Shift: BOOST" : "") +
                         (_car.HasHydraulics ? "   W: hop (hold to squat)" : "") +
                         (NearGarage() ? "   G: garage   R: repair" : "");
            // Speed, fuel and damage live on the dashboard now; the status line keeps where you are.
            float kmh = _car.Speed / PixelsPerMeter * 3.6f;
            string sprintNow = _sprint.Running ? $"SPRINT {_sprint.Time:0.00} s   " : "";
            string sprintBest = _sprint.Armed && GameSession.SprintBest > 0f ? $"   {GateSprint.Distance:0} m sprint best {GameSession.SprintBest:0.00} s" : "";
            _status.Text = $"{(_car.Boosting ? "BOOST!   " : "")}{sprintNow}{ClockStatus()}{BiomeName(_car.CenterPosition.X)}{TripStatus()}{DepthStatus(_car.CenterPosition)}{sprintBest}";
            _status.Modulate = _car.Boosting ? new Color(1f, 0.8f, 0.4f) : Colors.White;
            _dash.Visible = true;
            _dash.Set(kmh, _car.Fuel, _car.FuelCapacity, _car.Damage, _car.Boosting);
            _dash.SetEngine(_car.Gearbox.Gear, _car.Gearbox.Rpm, _car.Gearbox.Limiter);
            _dash.SetTurbo(_car.HasTurbo, _car.TurboBoost);
            _dash.SetLaunch(_car.LaunchArmed, _car.LaunchCharge, SoftVehicle.LaunchPerfectFrom, SoftVehicle.LaunchPerfectTo);
            _lines.Set(_carVx / PixelsPerMeter * 3.6f, _carDir, _car.Boosting || _kickGlow > 0f);
            _peakLines = Mathf.Max(_peakLines, _lines.Intensity);
            if (GameSession.AutoPlay && !_linesLogged && _lines.Intensity > 0.6f)
            {
                _linesLogged = true;
                GD.Print($"[speed] streaks at {_lines.Intensity:0.00} at {_carVx / PixelsPerMeter * 3.6f:0} km/h sideways, wind {_sfx.WindLevel:0.00}, boosting={_car.Boosting} frame={Engine.GetFramesDrawn()}");
            }
        }
        else
        {
            string hint = _player.Hook != Player.HookState.None
                ? "On the rope:   Space: jump off   S: let go   E: throw again   Hold left mouse: mine"
                : "A/D: walk   Space: jump   Hold left mouse: mine   Right mouse: place block   E: grappling hook   1/2: choose block   Esc: menu";
            if (NearCar()) hint += "\nF: get in the car";
            if (NearGarage()) hint += "\nG: open the garage (build and upgrade your car)";
            _hint.Text = hint;
            _dash.Visible = false;
            _lines.Set(0f, _carDir, false);
            _status.Modulate = Colors.White;
            _status.Text = $"{ClockStatus()}{BiomeName(_player.Position.X)}{TripStatus()}{DepthStatus(_player.Position)}" +
                           (GameSession.StashesFound > 0 ? $"   Stashes found {GameSession.StashesFound}" : "");
        }

        UpdateGoalLabel();
        UpdateNextPart(dt);
        UpdateHome(dt);
        _toast.Visible = !_card.Showing;   // the report card has the stage
        if (_toastTime > 0f)
        {
            _toastTime -= dt;
            _toast.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(_toastTime, 0f, 1f));
        }
    }

    // ---- Trips ------------------------------------------------------------------------------

    /// <summary>Metres from the middle of the garage pad (negative = west).</summary>
    private float MetersFromGarage(float worldX) => (worldX - TileWorld.PadCenter.X) / Ts * 0.25f;

    /// <summary>"   Depth 9 m (best 14 m)" once below 1 m; "The Deep" in the slate layer.</summary>
    private string DepthStatus(Vector2 at)
    {
        int tx = Mathf.FloorToInt(at.X / Ts);
        float meters = (at.Y / Ts - _world.SurfaceRow(tx)) * 0.25f;
        if (meters < 1f) return "";
        string where = at.Y / Ts >= _world.DeepRow(tx) ? "   THE DEEP" : "";
        return $"{where}   Depth {meters:0} m" + (GameSession.DeepestDig >= 2f ? $" (best {GameSession.DeepestDig:0} m)" : "");
    }

    private string TripStatus()
    {
        if (_trip == null) return "";
        TripBest best = GameSession.BestTrip;
        return $"   Trip {_trip.Farthest:0} m" + (best != null && best.Farthest >= 20f ? $" (best {best.Farthest:0} m)" : "");
    }

    /// <summary>Starts a trip when you leave the garage grounds and ends it (with the report card) when you come back.</summary>
    private void UpdateTrip()
    {
        Vector2 focus = Focus;
        float dx = MetersFromGarage(focus.X);
        float away = Mathf.Abs(dx);
        if (_trip == null)
        {
            _nextGoal ??= TripGoal.For((GameSession.BestTrip?.Trips ?? 0) + 1, GameSession.BestTrip, _car != null && _car.HasBooster);
            // The report card has the stage: coasting past the garage while it shows does not start a new trip.
            if (away > TripLeave && !_card.Showing) StartTrip();
            return;
        }

        if (away > _trip.Farthest)
        {
            _trip.Farthest = away;
            _trip.FarthestSide = Mathf.Sign(dx);
            _trip.FarthestX = focus.X;
        }
        float depth = (focus.Y / Ts - _world.SurfaceRow(Mathf.FloorToInt(focus.X / Ts))) * 0.25f;
        _trip.Deepest = Mathf.Max(_trip.Deepest, depth);
        if (_driving) _trip.NoteSpeed(_car.Speed / PixelsPerMeter * 3.6f);
        if (dx > 0f) _trip.FarthestEast = Mathf.Max(_trip.FarthestEast, dx);
        else _trip.FarthestWest = Mathf.Max(_trip.FarthestWest, -dx);
        Biome here = TileWorld.BiomeAt(Mathf.FloorToInt(focus.X / Ts));
        _trip.ReachedWastes |= here == Biome.Desert;
        _trip.ReachedNorth |= here == Biome.Frozen;
        CountTrip(_trip);
        UpdateGoal();

        // Passing your farthest point ever: a cheer, like the best-distance flag in Hill Climb Racing.
        TripBest best = GameSession.BestTrip;
        if (!_farthestCheered && best != null && best.Farthest >= 20f && _trip.Farthest > best.Farthest)
        {
            _farthestCheered = true;
            Toast($"NEW FARTHEST!\n{best.Farthest:0} m passed");
            _sfx.Play("tally", -4f, 0.1f, 1.2f);
            _sfx.Play("pickup", -5f, 0.1f, 1.3f);
            if (GameSession.AutoPlay) GD.Print($"[trip] passed the farthest point ever ({best.Farthest:0} m) at x={focus.X / Ts:0} t={_elapsed:0.0}s");
        }

        if (away < TripHome && depth < 3f) EndTrip();
    }

    private void StartTrip()
    {
        _trip = new Trip
        {
            StartedAt = _elapsed,
            HeapsAtStart = _heapsTaken, CansAtStart = _cansTaken, CrashesAtStart = _crashFx.Hits,
            RollOversAtStart = _rollOvers, StashesAtStart = GameSession.StashesFound,
            Goal = _nextGoal ?? TripGoal.For((GameSession.BestTrip?.Trips ?? 0) + 1, GameSession.BestTrip, _car != null && _car.HasBooster),
        };
        _nextGoal = null;
        foreach (string item in Trip.HaulItems) _trip.MaterialsAtStart[item] = GameSession.Count(item);
        _farthestCheered = false;
        // Out of the gate: the goal for this trip, big and centred (with the sprint time if one is on).
        if (_sprint.Running) _goalToastDeferred = true;
        else ShowGoalToast();
        _sfx.Play("tally", -6f, 0.05f, 0.9f);
        if (GameSession.AutoPlay)
            GD.Print($"[trip] start: left the garage grounds {(_driving ? "driving" : "on foot")} at t={_elapsed:0.0}s goal=\"{_trip.Goal.Text}\" kind={_trip.Goal.Kind} target={_trip.Goal.Target:0.##} bonus={_trip.Goal.Bonus} frame={Engine.GetFramesDrawn()}");
    }

    private void ShowGoalToast()
    {
        _goalToastDeferred = false;
        if (_trip?.Goal == null) return;
        Toast($"TRIP GOAL\n{_trip.Goal.Text}");
        _toastTime = 2.6f;
        _goalToast = true;
    }

    // ---- Gate sprint (see GateSprint) -----------------------------------------------------------

    private readonly GateSprint _sprint = new();
    private string _lastLaunch = "plain";
    private float _lastLaunchAt = -99f;
    private bool _goalToastDeferred;
    private int _sprintsRun;
    private const float PadHalfMeters = (TileWorld.PadTo - TileWorld.PadFrom) / 2f * 0.25f;

    /// <summary>Times a 20 m dash from a standing start on the garage pad; the result shows as a call-out.</summary>
    private void UpdateSprint()
    {
        if (_car == null) return;
        float x = _car.CenterPosition.X;
        bool onPad = Mathf.Abs(MetersFromGarage(x)) <= PadHalfMeters;
        float kmh = _car.Speed / PixelsPerMeter * 3.6f;
        switch (_sprint.Update(_elapsed, x / PixelsPerMeter, kmh, onPad, _driving))
        {
            case GateSprint.Result.Started:
                if (GameSession.AutoPlay)
                    GD.Print($"[sprint] start at {MetersFromGarage(x):0.0} m from the pad's middle, t={_sprint.StartedAt:0.00}s frame={Engine.GetFramesDrawn()}");
                break;
            case GateSprint.Result.Finished:
                FinishSprint();
                break;
            case GateSprint.Result.GaveUp:
                if (GameSession.AutoPlay) GD.Print($"[sprint] no time: {_sprint.Covered:0.0} m in {_sprint.Time:0.00} s (stopped, got out or too slow)");
                if (_goalToastDeferred) ShowGoalToast();
                break;
        }
    }

    private void FinishSprint()
    {
        float t = _sprint.Time;
        string launch = _lastLaunchAt >= _sprint.StartedAt - 0.05f ? _lastLaunch : "plain";
        float before = GameSession.SubmitSprint(t);
        bool first = before <= 0f, newBest = !first && t < before;
        _sprintsRun++;
        string head = $"{GateSprint.Distance:0} m SPRINT  {t:0.00} s";
        string verdict = first ? "a time to beat" : newBest ? $"NEW BEST!  {before - t:0.00} s faster" : $"best {before:0.00} s  (+{t - before:0.00})";
        if (_goalToastDeferred && _trip?.Goal != null)
        {
            Toast($"{head} · {verdict}\nTRIP GOAL: {_trip.Goal.Text}");
            _toastTime = 3f;
            _goalToast = true;
        }
        else
        {
            Toast($"{head}\n{verdict}");
            _toastTime = 2.4f;
        }
        _goalToastDeferred = false;
        if (newBest)
        {
            _sfx.Play("record", -7f, 0.1f, 1.15f);
            _kickGlow = Mathf.Max(_kickGlow, 0.5f);
        }
        else _sfx.Play("tally", -4f, 0.05f, 1.1f);
        if (GameSession.AutoPlay)
            GD.Print($"[sprint] finish: {GateSprint.Distance:0} m in {t:0.00} s launch={launch} top={_car.Speed / PixelsPerMeter * 3.6f:0}km/h " +
                     $"best_before={(first ? "none" : before.ToString("0.00"))} newBest={newBest} goalShown={_trip?.Goal != null && _goalToast} frame={Engine.GetFramesDrawn()}");
        if (GameSession.LaunchTest && !GameSession.LaunchEarly && _launchLeg == 0)
        {
            _launchLeg = 1;   // the bot drives back onto the pad for a second go
            GD.Print("[sprint] test: back to the pad for a second sprint");
        }
    }

    /// <summary>The trip's running counts (heaps, cans, stashes, haul) from the counters at its start.</summary>
    private void CountTrip(Trip t)
    {
        t.Heaps = _heapsTaken - t.HeapsAtStart;
        t.Cans = _cansTaken - t.CansAtStart;
        t.Crashes = _crashFx.Hits - t.CrashesAtStart;
        t.RollOvers = _rollOvers - t.RollOversAtStart;
        t.Stashes = GameSession.StashesFound - t.StashesAtStart;
        foreach (string item in Trip.HaulItems) t.Haul[item] = Mathf.Max(0, GameSession.Count(item) - t.MaterialsAtStart.GetValueOrDefault(item));
    }

    /// <summary>Checks the trip goal; on the frame it gets done: a call-out, a fanfare and a log line.</summary>
    private void UpdateGoal()
    {
        TripGoal goal = _trip.Goal;
        if (goal == null || !goal.Check(_trip, _elapsed - _trip.StartedAt)) return;
        Toast($"GOAL DONE!\n{goal.Text}   +{goal.Bonus}");
        _toastTime = 2.6f;
        _goalToast = true;
        _sfx.Play("record", -7f, 0.05f, 1.1f);
        _goalFlash = 1f;
        if (GameSession.AutoPlay)
            GD.Print($"[trip] goal done: \"{goal.Text}\" after {goal.DoneAt:0.0}s of the trip at x={Focus.X / Ts:0} ({MetersFromGarage(Focus.X):0} m) bonus={goal.Bonus} frame={Engine.GetFramesDrawn()}");
    }

    private float _goalFlash;
    private bool _goalToast;       // the toast shows a goal call-out: stunt cheers wait until it has been read

    /// <summary>The gold goal line under the status: the next goal at the garage, progress on a trip, DONE when done.</summary>
    private void UpdateGoalLabel()
    {
        _goalFlash = Mathf.Max(0f, _goalFlash - (float)GetProcessDeltaTime() * 0.8f);
        TripGoal goal = _trip?.Goal ?? _nextGoal;
        if (goal == null)
        {
            _goalLabel.Text = "";
            return;
        }
        var gold = new Color(1f, 0.85f, 0.4f);
        var green = new Color(0.55f, 1f, 0.55f);
        if (_trip == null)
        {
            _goalLabel.Text = $"Next trip goal: {goal.Text}";
            _goalLabel.Modulate = gold;
        }
        else if (goal.Done)
        {
            _goalLabel.Text = $"GOAL DONE   {goal.Text}   +{goal.Bonus}";
            _goalLabel.Modulate = green.Lerp(Colors.White, _goalFlash * (0.5f + 0.5f * Mathf.Sin(_elapsed * 20f)));
        }
        else
        {
            _goalLabel.Text = $"GOAL   {goal.Text}   {goal.ProgressText(_trip)}";
            _goalLabel.Modulate = gold;
        }
    }

    private void EndTrip()
    {
        Trip t = _trip;
        _trip = null;
        t.Seconds = _elapsed - t.StartedAt;
        CountTrip(t);
        // Bringing the goods home counts the moment you are back (the Copper goal, say).
        if (t.Goal != null && t.Goal.Check(t, t.Seconds) && GameSession.AutoPlay)
            GD.Print($"[trip] goal done on arrival: \"{t.Goal.Text}\" bonus={t.Goal.Bonus}");
        if (!t.WorthReporting)
        {
            if (GameSession.AutoPlay) GD.Print($"[trip] back after a short stroll ({t.Farthest:0} m), no report");
            return;
        }

        var rows = t.Rows(GameSession.BestTrip);
        int score = Trip.Score(rows);
        TripBest before = GameSession.SubmitTrip(score, t.Farthest, t.TopKmh, t.LongestAir, t.BestStreak);
        int number = (before?.Trips ?? 0) + 1;
        string subtitle = $"Trip {number}   ·   {(int)(t.Seconds / 60f)}:{(int)(t.Seconds % 60f):00}   ·   reached {BiomeName(t.FarthestX)}";
        _card.Open(rows, score, before, subtitle);
        _tripsReported++;
        if (GameSession.AutoPlay)
        {
            string records = string.Join(",", rows.FindAll(r => r.Record).ConvertAll(r => r.Label.ToLowerInvariant().Replace(' ', '_')));
            GD.Print($"[trip] report {number}: score={score} best_before={(before == null ? "none" : before.Score.ToString())} newBest={(before != null && score > before.Score)} " +
                     $"farthest={t.Farthest:0}m side={(t.FarthestSide < 0 ? "west" : "east")} reached=\"{BiomeName(t.FarthestX)}\" time={t.Seconds:0.0}s top={t.TopKmh:0}km/h air={t.LongestAir:0.0}s landings={t.PerfectLandings}perfect/{t.CleanLandings}clean " +
                     $"heaps={t.Heaps} streak={t.BestStreak} cans={t.Cans} stashes={t.Stashes} haul=\"{t.HaulText()}\" crashes={t.Crashes} rollOvers={t.RollOvers} " +
                     $"records=[{records}] goal=\"{t.Goal?.Text}\" goalDone={t.Goal?.Done} goalBonus={(t.Goal?.Done == true ? t.Goal.Bonus : 0)} frame={Engine.GetFramesDrawn()}");
            foreach (TripRow r in rows) GD.Print($"[trip]   {r.Label}: {r.Value} -> +{r.Points}{(r.Record ? " NEW BEST" : "")}{(r.Tag != null ? " " + r.Tag : "")}");
        }
    }

    // ---- Stunts -----------------------------------------------------------------------------

    /// <summary>Times jumps and boosts: a long jump gets a cheer on screen (and a line in the bot log).</summary>
    private void TrackStunts(float dt)
    {
        if (_car.Boosting) _boostSeconds += dt;
        if (_car.SelfRighting != _wasRighting)
        {
            _wasRighting = _car.SelfRighting;
            if (!_wasRighting) _rollOvers++;
            if (GameSession.AutoPlay)
                GD.Print($"[stunt] {(_wasRighting ? "upside down: car starts rocking itself over" : "back on its wheels")} at x={_car.CenterPosition.X / Ts:0} pitch={_car.PitchDegrees():0}");
        }
        _topSpeed = Mathf.Max(_topSpeed, _car.Speed);
        TrackKickers();
        TrackRotation(dt, _car.Airborne && !_bodyDown && !_car.SelfRighting);

        // Lying on the roof also has no wheels on the ground: that is not air time worth a cheer.
        if (_car.SelfRighting)
        {
            _airTime = 0f;
            _judge.Cancel();
            ResetRotation();
            return;
        }

        if (_kickGlow > 0f) _kickGlow -= dt;
        if (_kickCheckAt >= 0f && _elapsed >= _kickCheckAt)
        {
            _kickCheckAt = -1f;
            if (GameSession.AutoPlay)
                GD.Print($"[landing] kick result: {_kickKmhBefore:0} km/h at the landing -> {_car.Speed / PixelsPerMeter * 3.6f:0} km/h half a second later (overdrive left {_car.Overdrive / PixelsPerMeter * 3.6f:0} km/h)");
        }
        if (_judge.Update(_car, dt) is Landing landing) Landed(landing);

        if (_car.Airborne)
        {
            if (_bodyDown) return;   // on its roof or side after a botched landing
            if (_airTime == 0f) _airStartSpeed = _car.Speed;
            _airTime += dt;
            return;
        }
        _bodyDown = false;
        if (_airTime > 0f)
        {
            TouchdownRotation();
            LongJumpTouchdown(_airTime);
        }
        if (_airTime > 0.3f) _trip?.NoteAir(_airTime);
        if (_airTime > 0.9f) _longestAir = Mathf.Max(_longestAir, _airTime);
        if (_airTime >= LandingJudge.MinAir)
        {
            _judge.Start(_car, _airTime);   // the cheer waits for the verdict
            if (_bodyHitTick) _judge.NoteBodyHit();
        }
        _airTime = 0f;
    }

    /// <summary>
    /// Notices the car crossing a kicker's crest in the jump direction (a take-off), and shows a
    /// one-time hint on the approach to the first kicker so the player knows what the sign means.
    /// </summary>
    private void TrackKickers()
    {
        int col = Mathf.FloorToInt(_car.CenterPosition.X / Ts);
        int last = _lastCarCol;
        _lastCarCol = col;

        if (!_kickerHinted && _toastTime <= 0f && _world.KickerNear(col, 44) is Kicker ahead &&
            (ahead.Start - col) * ahead.Dir > 16 && _carDir == ahead.Dir && _car.Speed > 3f * PixelsPerMeter)
        {
            _kickerHinted = true;
            Toast("KICKER AHEAD!\nhit it fast · Q/E in the air · land flat");
            if (GameSession.AutoPlay) GD.Print($"[kicker] hint shown {Mathf.Abs(ahead.Start - col) * 0.25f:0} m before the kicker at x={ahead.Start} frame={Engine.GetFramesDrawn()}");
        }

        if (last == int.MinValue || last == col || Mathf.Abs(col - last) > 8) return;
        foreach (Kicker k in _world.Kickers)
        {
            if (GameSession.AutoPlay && _world.JumpBest(k) > 0f && (k.Start - col) * k.Dir is > 30 and < 40 && (col - last) * k.Dir > 0 && _flagLogged.Add(k.Start))
                GD.Print($"[kicker] approach: record flag {_world.JumpBest(k):0.0} m past the {k.Meters:0.00} m kicker at x={k.Crest} " +
                         $"({Mathf.Abs(MetersFromGarage(k.Crest * Ts)):0} m out) speed={_car.Speed / PixelsPerMeter * 3.6f:0}km/h frame={Engine.GetFramesDrawn()}");
            bool before = (k.Crest - last) * k.Dir >= 0, after = (k.Crest - col) * k.Dir < 0;
            bool backBefore = (last - k.Crest) * k.Dir > 0, backAfter = (col - k.Crest) * k.Dir <= 0;
            if (before && after)
            {
                _kickerLaunches++;
                _kickerAt = _elapsed;
                _kickerRise = k.Rise;
                _kickerKmh = _car.Speed / PixelsPerMeter * 3.6f;
                _jumpKicker = k;
                _jumpAt = _elapsed;
                _jumpLine = null;
                if (GameSession.AutoPlay)
                    GD.Print($"[kicker] take-off: {k.Meters:0.00} m kicker in {TileWorld.BiomeAt(k.Crest)} at x={k.Crest} ({Mathf.Abs(MetersFromGarage(k.Crest * Ts)):0} m out) " +
                             $"speed={_kickerKmh:0}km/h pitch={_car.PitchDegrees():0} wheelsDown={_car.WheelsGrounded}/{_car.WheelsAttached} frame={Engine.GetFramesDrawn()} | launches={_kickerLaunches}");
            }
            else if (backBefore && backAfter && GameSession.AutoPlay)
                GD.Print($"[kicker] crossed the kicker at x={k.Crest} the other way (homeward) at {_car.Speed / PixelsPerMeter * 3.6f:0}km/h");
        }
    }

    /// <summary>True if the jump that just ended started at a kicker's crest.</summary>
    private bool FromKicker(float air) => _elapsed - air - _kickerAt < 1.2f && _elapsed - _kickerAt < 6f;

    private void NoteKickerJump(string grade, float air)
    {
        if (!FromKicker(air)) return;
        _kickerAt = -10f;
        _kickerJumps++;
        if (grade == "perfect") _kickerPerfects++;
        if (GameSession.AutoPlay)
            GD.Print($"[kicker] jump: {grade} after air={air:0.00}s from a {_kickerRise * 0.25f:0.00} m kicker at {_kickerKmh:0}km/h | jumps={_kickerJumps}/{_kickerLaunches} perfect={_kickerPerfects}");
    }

    /// <summary>
    /// The car came down after a jump. If it took off from a kicker, measure how far past the crest it
    /// landed. Long enough jumps get their line with the landing grade (or straight away when the jump
    /// was too short to be graded).
    /// </summary>
    private void LongJumpTouchdown(float air)
    {
        if (MeasureLongJump(air, flop: false) is not LongJump j) return;
        _jumpLine = JumpLine(j);
        _jumpLineAt = _elapsed;
        if (air >= LandingJudge.MinAir) return;   // Landed() tells it with the grade
        _jumpLine = null;
        if (!_goalToast || _toastTime < 1.2f) Toast(JumpLine(j).Replace(" · ", "\n"));
        _sfx.Play("pickup", -6f);
    }

    /// <summary>
    /// Distance from the kicker's crest to where the car came down, checked against the kicker's record.
    /// A botched landing (on the roof, nose or tail) is measured but never sets a record.
    /// </summary>
    private LongJump? MeasureLongJump(float air, bool flop)
    {
        if (_jumpKicker is not Kicker k) return null;
        bool fromKicker = _elapsed - air - _jumpAt < 1.2f && _elapsed - _jumpAt < 6f;
        _jumpKicker = null;
        if (!fromKicker || air < 0.25f) return null;
        float meters = (_car.CenterPosition.X - JumpMarks.EdgeX(k)) * k.Dir / PixelsPerMeter;
        if (meters < 1.5f) return null;   // rolled off the crest, no real jump

        float before = _world.JumpBest(k);
        bool record = !flop && meters > before + 0.05f;
        if (record)
        {
            _world.SetJumpBest(k, meters);
            _marks.Fresh(k);
            _jumpRecords++;
            if (before > 0f) _sfx.Play("record", -9f, 0.5f, 1.25f);
        }
        if (!flop)
        {
            _longJumps++;
            _longestJump = Mathf.Max(_longestJump, meters);
        }
        if (GameSession.AutoPlay)
            GD.Print($"[kicker] long jump: {meters:0.0} m past the {k.Meters:0.00} m kicker at x={k.Crest} air={air:0.00}s took off at {_kickerKmh:0}km/h " +
                     $"{(flop ? "botched (does not count)" : record ? (before > 0f ? $"NEW RECORD (was {before:0.0} m)" : "first jump: flag planted") : $"no record (best {before:0.0} m)")} " +
                     $"frame={Engine.GetFramesDrawn()} | longJumps={_longJumps} records={_jumpRecords} longest={_longestJump:0.0}m");
        return new LongJump(meters, before, record, !flop);
    }

    private static string JumpLine(LongJump j) =>
        j.Before <= 0f ? $"LONG JUMP {j.Meters:0.0} m · flag planted"
        : j.Record ? $"LONG JUMP {j.Meters:0.0} m · NEW RECORD! (+{j.Meters - j.Before:0.0} m)"
        : $"LONG JUMP {j.Meters:0.0} m · flag at {j.Before:0.0} m";

    /// <summary>--jumptest: plant record flags before the bot drives, so it meets a flag to beat (2 m past the first
    /// kicker east) and flags it cannot beat (9 m past the others).</summary>
    private void PlantJumpRecords()
    {
        Kicker? first = null;
        foreach (Kicker k in _world.Kickers)
            if (k.Dir > 0 && (first == null || k.Crest < first.Value.Crest)) first = k;
        foreach (Kicker k in _world.Kickers) _world.SetJumpBest(k, first is Kicker f && f.Start == k.Start ? 2f : 9f);
        GD.Print($"[kicker] jump test: planted record flags (2.0 m on the first kicker east, 9.0 m on the other {_world.Kickers.Count - 1})");
    }

    private static string AirPraise(float air) => air > 2.5f ? "MONSTER AIR" : air > 1.6f ? "HUGE AIR" : "BIG AIR";

    /// <summary>The landing has been judged: cheer it, and kick the car forward if it was perfect.</summary>
    private void Landed(Landing l)
    {
        bool bigAir = l.Air > 0.9f;
        bool mayToast = !_goalToast || _toastTime < 1.2f;   // let a fresh goal call-out be read first
        float kmh = _car.Speed / PixelsPerMeter * 3.6f;
        float kick = 0f;
        string jump = _elapsed - _jumpLineAt < 1.5f ? _jumpLine : null;   // a long jump off a kicker, told with the grade
        _jumpLine = null;
        float flipKick = 0f;
        string flip = LandedFlip(l, ref flipKick);   // "BACKFLIP" if the jump turned the car over and it came down on its wheels
        switch (l.Grade)
        {
            case LandingGrade.Perfect when FlatHop(l.Air):
                // A hop on the flat landed square: no speed kick (or bunny-hopping would beat driving).
                _perfectLandings++;
                kick = flipKick;
                if (kick > 0f) _car.Kick(kick);
                if (mayToast && flip == null) Toast($"PERFECT HOP LANDING\n{l.Air:0.0} s air");
                _sfx.Play("pickup", -6f);
                break;
            case LandingGrade.Perfect:
                kick = LandingJudge.KickFor(l.Air) + flipKick;
                _car.Kick(kick);
                _kickGlow = 0.8f;
                _kickCheckAt = _elapsed + 0.5f;
                _kickKmhBefore = kmh;
                _perfectLandings++;
                if (_trip != null) _trip.PerfectLandings++;
                if (mayToast)
                    Toast(jump == null ? $"PERFECT LANDING!\n{l.Air:0.0} s air   +{kick / PixelsPerMeter * 3.6f:0} km/h"
                                       : $"PERFECT LANDING!  +{kick / PixelsPerMeter * 3.6f:0} km/h\n{jump}");
                _sfx.Play("land", -4f, 0.2f, 1f);
                break;
            case LandingGrade.Clean:
                _cleanLandings++;
                if (_trip != null) _trip.CleanLandings++;
                if (mayToast)
                    Toast(jump != null ? $"CLEAN LANDING\n{jump}" : bigAir ? $"{AirPraise(l.Air)}!  {l.Air:0.0} s\nclean landing" : "CLEAN LANDING");
                _sfx.Play("pickup", -6f);
                break;
            default:
                _roughLandings++;
                if (bigAir)
                {
                    if (mayToast) Toast(jump == null ? $"{AirPraise(l.Air)}!  {l.Air:0.0} s" : $"{AirPraise(l.Air)}!  {l.Air:0.0} s\n{jump}");
                    _sfx.Play("pickup", -6f);
                }
                else if (jump != null)
                {
                    if (mayToast) Toast(jump.Replace(" · ", "\n"));
                    _sfx.Play("pickup", -6f);
                }
                break;
        }
        if (flip != null)
        {
            if (l.Grade != LandingGrade.Perfect)
            {
                kick = flipKick;
                _car.Kick(kick);
                _kickGlow = 0.8f;
                _kickCheckAt = _elapsed + 0.5f;
                _kickKmhBefore = kmh;
            }
            string grade = l.Grade == LandingGrade.Perfect ? "perfect landing" : l.Grade == LandingGrade.Clean ? "clean landing" : "landed it";
            if (mayToast) Toast($"{flip}!  +{kick / PixelsPerMeter * 3.6f:0} km/h\n{jump ?? grade}");
        }
        NoteKickerJump(l.Grade.ToString().ToLowerInvariant(), l.Air);
        if (GameSession.AutoPlay)
        {
            if (bigAir)
                GD.Print($"[stunt] {AirPraise(l.Air).ToLowerInvariant()}: airborne {l.Air:0.00}s, took off at {_airStartSpeed / PixelsPerMeter * 3.6f:0} km/h, damage={_car.Damage:0}");
            GD.Print($"[landing] {l.Grade.ToString().ToLowerInvariant()}: air={l.Air:0.00}s allWheelsDownAfter={(l.AllDownAfter < 0f ? "never" : $"{l.AllDownAfter:0.00}s")} " +
                     $"mismatch={l.Mismatch:0}deg bodyHit={l.BodyHit} damage+={l.DamageTaken:0.0} speed={kmh:0}km/h kick={kick / PixelsPerMeter * 3.6f:0}km/h " +
                     $"at x={_car.CenterPosition.X / Ts:0} frame={Engine.GetFramesDrawn()} | perfect={_perfectLandings} clean={_cleanLandings} rough={_roughLandings} flops={_flops}");
        }
    }

    /// <summary>The body slammed into the ground. After a real jump that is a botched landing with a name.</summary>
    private void OnCarCrash(CrashEvent e)
    {
        if (_driving) _birds?.Startle(e.Point, 600f, "crash");
        if (!e.BodyHit || !_driving) return;
        _judge.NoteBodyHit();
        _bodyHitTick = true;
        if (_airTime < LandingJudge.MinAir || _bodyDown || _car.SelfRighting) return;

        float pitch = _car.PitchDegrees();
        float lead = pitch * _carDir;   // positive: the end you are driving towards points down
        // All wheels down and fairly level: a hard but proper landing, judged (as rough) by the judge.
        bool botched = Mathf.Abs(pitch) > 100f || _car.Airborne || (_car.WheelsGrounded < _car.WheelsAttached && Mathf.Abs(lead) > 25f);
        if (!botched) return;
        string what = Mathf.Abs(pitch) > 100f ? "ROOF LANDING" : lead > 25f ? "NOSE DIVE" : lead < -25f ? "TAIL SLAM" : "BELLY FLOP";
        float air = _airTime;
        _flops++;
        _trip?.NoteAir(air);
        if (air > 0.9f) _longestAir = Mathf.Max(_longestAir, air);
        _airTime = 0f;
        _bodyDown = true;
        LongJump? jump = MeasureLongJump(air, flop: true);
        string flipNote = CrashedFlip(what);   // "\nBACKFLIP, but it doesn't count"
        if (!_goalToast || _toastTime < 1.2f)
            Toast((jump is LongJump j ? $"{what}!\n{j.Meters:0.0} m, but it doesn't count" : $"{what}!\n{air:0.0} s of air") + flipNote);
        NoteKickerJump(what.ToLowerInvariant(), air);
        if (GameSession.AutoPlay)
            GD.Print($"[landing] flop: {what.ToLowerInvariant()} after air={air:0.00}s pitch={pitch:0} hit={e.Strength / PixelsPerMeter * 3.6f:0}km/h " +
                     $"at x={_car.CenterPosition.X / Ts:0} frame={Engine.GetFramesDrawn()} | perfect={_perfectLandings} clean={_cleanLandings} rough={_roughLandings} flops={_flops}");
    }

    // ---- Automated playtest -----------------------------------------------------------------
    // --autoplay: walk, dig down by hand, then get in the car and drive (and drill) to the right.

    /// <summary>
    /// --triptest: straight into the car, drive out east for 9 s and back to the garage (report 1),
    /// then out for 15 s (further: a new farthest and, most likely, a new best trip) and back (report 2).
    /// </summary>
    private void RunTripBot()
    {
        if (!_driving)
        {
            if (_elapsed > 0.5f && _tripLeg == 0)
            {
                EnterCar();
                _legStart = _elapsed;
                GD.Print("[trip] test: two out-and-back drives from the garage");
            }
            return;
        }

        float throttle = 0f;
        switch (_tripLeg)
        {
            case 0:
                throttle = 1f;
                if (_elapsed - _legStart > 9f) Leg(1, "heading home");
                break;
            case 1:
                throttle = -1f;
                if (_tripsReported >= 1) Leg(2, "out again, further this time");
                break;
            case 2:
                throttle = 1f;
                // Like a player pressing Enter once the score has counted up (a new trip waits for the card).
                if (_card.Showing && !_card.Closing && _elapsed - _legStart > 5f)
                {
                    _card.Dismiss();
                    GD.Print($"[trip] test: closed the card (Enter) at {MetersFromGarage(_car.CenterPosition.X):0} m t={_elapsed:0.0}s");
                }
                if (_elapsed - _legStart > 15f) Leg(3, "heading home");
                break;
            case 3:
                throttle = -1f;
                if (_tripsReported >= 2) Leg(4, "home, parking");
                break;
        }
        _car.AutoThrottle = throttle;
    }

    // ---- --rocktest: a trench east of the garage with a wall too tall to drive up ----------------

    private bool _trenchDug, _trenchDone;
    private int _trenchFrom, _trenchTo, _trenchFloor;
    private float _trenchNextLog;

    /// <summary>
    /// --rocktest: dig a trench (--rockdepth=N tiles deep, default 3, 20 tiles long) just past the garage
    /// pad, get straight in and drive at it at full throttle. The car drops in and meets the far wall.
    /// With rocking on (the default) the bot taps E, Q, E, Q once it is stuck; --norock is the control
    /// run (the bot only backs up and tries again). Logs whether and when the car climbs out.
    /// </summary>
    private void RunRockBot()
    {
        if (!_trenchDug)
        {
            _trenchDug = true;
            _trenchFrom = TileWorld.PadTo + 2;
            _trenchTo = _trenchFrom + 20;
            _world.RemoveKickers(_trenchFrom - 8, _trenchTo + 30);
            _trenchFloor = _world.SurfaceRow(_trenchFrom) + GameSession.RockDepth;
            for (int x = _trenchFrom; x < _trenchTo; x++)
                for (int y = _world.SurfaceRow(x) - 8; y < _trenchFloor; y++)
                    _world.Set(x, y, Tile.Air);
            GD.Print($"[rock] test: trench x={_trenchFrom}..{_trenchTo} floor row {_trenchFloor}, far wall {(_trenchFloor - _world.SurfaceRow(_trenchTo)) * 0.25f:0.00} m tall " +
                     $"({_trenchFloor - _world.SurfaceRow(_trenchTo)} tiles), rocking={_car.AutoRock} design=\"{GameSession.CurrentDesign.Name}\"");
        }
        if (!_driving)
        {
            if (_elapsed > 0.5f) EnterCar();
            return;
        }
        _car.AutoThrottle = 1f;
        if (_trenchDone) return;

        float carTile = _car.CenterPosition.X / Ts;
        float carRow = _car.CenterPosition.Y / Ts;
        if (_elapsed >= _trenchNextLog)
        {
            _trenchNextLog = _elapsed + 1f;
            GD.Print($"[rock] t={_elapsed:0.0}s car=({carTile:0.0},{carRow:0.0}) speed={_car.Speed / PixelsPerMeter * 3.6f:0}km/h pitch={_car.PitchDegrees():0} " +
                     $"stuck={_car.Stuck} chain={_car.RockChain} rocks={_car.RockPulses} damage={_car.Damage:0} frame={Engine.GetFramesDrawn()}");
        }
        if (carTile > _trenchTo + 6)
        {
            _trenchDone = true;
            GD.Print($"[rock] test: OUT of the trench at t={_elapsed:0.0}s after {_car.RockPulses} rocks, stuck {_stuckCount} times, damage={_car.Damage:0} frame={Engine.GetFramesDrawn()}");
        }
        else if (_elapsed > 20f)
        {
            _trenchDone = true;
            GD.Print($"[rock] test: still IN the trench after 20 s at x={carTile:0.0}, {_car.RockPulses} rocks, stuck {_stuckCount} times frame={Engine.GetFramesDrawn()}");
        }
    }

    /// <summary>
    /// --icetest: lay 40 m of ice over the ground east of the garage (two tiles deep, like the
    /// Frozen North's patches), get straight into the car and drive across at full throttle.
    /// Logs how long the crossing takes and how much the tyres slid, so plain and spiked tyres can be compared.
    /// </summary>
    private void RunIceBot()
    {
        const float StripMeters = 40f;
        if (!_icePlanted)
        {
            _icePlanted = true;
            _iceFrom = TileWorld.PadTo + 4;
            _iceTo = _iceFrom + (int)(StripMeters * 4f);
            _world.RemoveKickers(_iceFrom - 8, _iceTo + 8);   // a flat strip, comparable with older runs
            for (int x = _iceFrom; x < _iceTo; x++)
            {
                int top = _world.SurfaceRow(x);
                for (int y = top; y < top + 2; y++)
                    if (_world.Get(x, y) != Tile.Air) _world.Set(x, y, Tile.Ice);
            }
            GD.Print($"[ice] test: laid {StripMeters:0} m of ice from x={_iceFrom} to x={_iceTo} (tiles); design=\"{GameSession.CurrentDesign.Name}\" studs={_car.HasStuds}");
        }
        if (!_driving)
        {
            if (_elapsed > 0.5f) EnterCar();
            return;
        }
        _car.AutoThrottle = _iceDone ? 0f : 1f;

        float carTile = _car.CenterPosition.X / Ts;
        if (_iceEnterAt < 0f && carTile >= _iceFrom)
        {
            _iceEnterAt = _elapsed;
            _iceNextLog = _elapsed + 2f;
            GD.Print($"[ice] onto the ice at t={_elapsed:0.0}s speed={_car.Speed / PixelsPerMeter * 3.6f:0}km/h frame={Engine.GetFramesDrawn()}");
        }
        if (_iceEnterAt >= 0f && !_iceDone && _elapsed >= _iceNextLog)
        {
            _iceNextLog += 2f;
            GD.Print($"[ice] t={_elapsed - _iceEnterAt:0.0}s on ice: {(carTile - _iceFrom) * 0.25f:0.0}/{StripMeters:0} m speed={_car.Speed / PixelsPerMeter * 3.6f:0}km/h " +
                     $"iceTime={_spray.IceSeconds:0.0}s slip={_spray.SlipSeconds:0.0}s bite={_spray.BiteSeconds:0.0}s chips={_spray.ChipsThrown} damage={_car.Damage:0} frame={Engine.GetFramesDrawn()}");
        }
        if (_iceEnterAt >= 0f && !_iceDone && (carTile >= _iceTo || _elapsed - _iceEnterAt > 30f))
        {
            _iceDone = true;
            float time = _elapsed - _iceEnterAt;
            float meters = Mathf.Min(carTile - _iceFrom, _iceTo - _iceFrom) * 0.25f;
            GD.Print($"[ice] {(carTile >= _iceTo ? "crossed" : "did NOT cross")} the strip: {meters:0.0} m in {time:0.0}s (avg {meters / Mathf.Max(time, 0.01f) * 3.6f:0} km/h) " +
                     $"top={_topSpeed / PixelsPerMeter * 3.6f:0}km/h slip={_spray.SlipSeconds:0.0}s bite={_spray.BiteSeconds:0.0}s iceTime={_spray.IceSeconds:0.0}s chips={_spray.ChipsThrown} " +
                     $"studs={_car.HasStuds} fuel={_car.Fuel:0} damage={_car.Damage:0} frame={Engine.GetFramesDrawn()}");
        }
    }

    /// <summary>
    /// --deeptest: put the player on the floor of a real cavern in the Deep next to a cluster of glow
    /// crystals (the nearest one to the garage that has room to stand), then dig out every crystal in reach,
    /// walking to the next cluster when none is left. Checks generation, the glow and the Crystal drop.
    /// </summary>
    private void RunDeepBot()
    {
        if (!_deepPlaced)
        {
            if (_elapsed < 0.3f) return;
            _deepPlaced = true;
            int mid = (TileWorld.PadFrom + TileWorld.PadTo) / 2;
            for (int d = 0; d < 900; d++)
            {
                int x = mid + ((d & 1) == 0 ? d / 2 : -(d / 2 + 1));
                for (int y = _world.DeepRow(x); y < TileWorld.Height - 3; y++)
                {
                    // Room to stand: three open rows above solid ground, and a crystal within reach.
                    if (_world.Solid(x, y) || _world.Solid(x, y - 1) || _world.Solid(x, y - 2) || !_world.Solid(x, y + 1)) continue;
                    var at = new Vector2((x + 0.5f) * Ts, (y + 1) * Ts - Player.HalfHeight - 0.5f);
                    if (_player.Blocked(at.X, at.Y) || NearestCrystal(at, 70f) == null) continue;
                    _player.Position = at;
                    _player.Velocity = Vector2.Zero;
                    _camera.Position = at;
                    _airCam.Snap(at);
                    GD.Print($"[deep] test: player placed in a cavern at tile ({x},{y}) depth={(y - _world.SurfaceRow(x)) * 0.25f:0.0}m " +
                             $"{(x - mid) * 0.25f:0} m from the garage, biome={TileWorld.BiomeAt(x)} frame={Engine.GetFramesDrawn()}");
                    return;
                }
            }
            GD.Print("[deep] test: NO cavern with crystals and room to stand found");
            return;
        }

        Vector2 me = _player.Position;
        Vector2I? target = NearestCrystal(me, 88f);
        if (target is Vector2I t)
        {
            _player.Override = new PlayerInput { Mine = true, Aim = new Vector2((t.X + 0.5f) * Ts, (t.Y + 0.5f) * Ts) };
        }
        else if (NearestCrystal(me, 260f) is Vector2I far)
        {
            float dir = Mathf.Sign((far.X + 0.5f) * Ts - me.X);
            _player.Override = new PlayerInput { Move = dir, Jump = Mathf.Abs(_player.Velocity.X) < 5f, Aim = me };
        }
        else
        {
            _player.Override = new PlayerInput();
        }

        if (_elapsed >= _nextLog)
            GD.Print($"[deep] t={_elapsed:0}s mined={_crystalsMined} crystalsHeld={GameSession.Count("Crystal")} glowOpen={_crystals.ShownOpen} glowBuried={_crystals.ShownBuried} " +
                     $"darkness={_crystals.Darkness:0.00} depthRecord={GameSession.DeepestDig:0.0}m records={_depthRecords} player=({me.X / Ts:0.0},{me.Y / Ts:0.0}) frame={Engine.GetFramesDrawn()}");
    }

    /// <summary>The closest crystal tile within a distance (pixels) of a point, if any.</summary>
    private Vector2I? NearestCrystal(Vector2 from, float within)
    {
        int r = Mathf.CeilToInt(within / Ts) + 1;
        int cx = Mathf.FloorToInt(from.X / Ts), cy = Mathf.FloorToInt(from.Y / Ts);
        Vector2I? best = null;
        float bestD = within;
        for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (_world.Get(x, y) != Tile.Crystal) continue;
                float d = new Vector2((x + 0.5f) * Ts, (y + 0.5f) * Ts).DistanceTo(from);
                if (d <= bestD)
                {
                    bestD = d;
                    best = new Vector2I(x, y);
                }
            }
        return best;
    }

    private void Leg(int leg, string what)
    {
        _tripLeg = leg;
        _legStart = _elapsed;
        GD.Print($"[trip] test leg {leg}: {what} at x={_car.CenterPosition.X / Ts:0} ({MetersFromGarage(_car.CenterPosition.X):0} m) t={_elapsed:0.0}s");
    }

    private void RunBot()
    {
        // --crashtest: skip the walk, drop the car from about 15 m up with the bot at the wheel.
        if (GameSession.CrashTest && !_crashDropped && _elapsed > 0.5f)
        {
            _crashDropped = true;
            SpawnCar(900f);
            EnterCar();
            GD.Print("[crash] test: car dropped from about 15 m");
        }

        if (GameSession.TripTest) RunTripBot();
        if (GameSession.IceTest) RunIceBot();
        if (GameSession.DeepTest) RunDeepBot();
        if (GameSession.BatTest) RunBatBot();
        if (GameSession.HookTest) RunHookBot();
        if (GameSession.VeinTest) RunVeinBot();
        if (GameSession.LaunchTest) RunLaunchBot();
        if (GameSession.RockTest) RunRockBot();
        if (GameSession.FlipTest) RunFlipBot();
        if (GameSession.HopTest) RunHopBot();
        if (GameSession.PauseTest) RunPauseBot();
        UpdateWatch();
        LogStall();
        RunHornBot();

        if (!_driving && !GameSession.CrashTest && !GameSession.TripTest && !GameSession.IceTest && !GameSession.DeepTest && !GameSession.BatTest && !GameSession.HookTest && !GameSession.VeinTest && !GameSession.LaunchTest && !GameSession.RockTest && !GameSession.FlipTest && !GameSession.HopTest)
        {
            Vector2 feet = _player.Position + new Vector2(0f, Player.HalfHeight);
            // --stashtest: once the bot starts digging, bury a toolbox and a strongbox under its feet
            // (it mines both) and a supply crate a few tiles further down, to see the glint.
            if (GameSession.StashTest && !_stashPlanted && _elapsed >= 3f)
            {
                _stashPlanted = true;
                int y = Mathf.FloorToInt((feet.Y + 8f) / Ts);
                int xl = Mathf.FloorToInt((feet.X - 7f) / Ts), xr = Mathf.FloorToInt((feet.X + 7f) / Ts);
                _world.PlaceStash(xl, y, Tile.StashSmall);
                if (xr != xl) _world.PlaceStash(xr, y, Tile.StashBig);
                _world.PlaceStash(xr + 3, y + 4, Tile.StashMid);
                GD.Print($"[stash] test: buried a toolbox at ({xl},{y}){(xr != xl ? $", a strongbox at ({xr},{y})" : "")} and a crate at ({xr + 3},{y + 4})");
            }
            if (_elapsed < 3f)
                _player.Override = new PlayerInput { Move = GameSession.DriveLeft ? 1f : -1f, Jump = Mathf.Abs(_player.Velocity.X) < 5f, Aim = feet };
            else if (_elapsed < 9f)
                _player.Override = new PlayerInput { Mine = true, Aim = feet + new Vector2(((int)(_elapsed * 3f) & 1) == 0 ? -7f : 7f, 8f) };
            else if (_elapsed < 10f)
                _player.Override = new PlayerInput { Place = true, Aim = _player.Position + new Vector2(0f, -Player.HalfHeight - Ts * 1.5f) };
            else
                EnterCar();
        }

        // With a drill: after a stretch of driving, point the nose down and tunnel for a while.
        if (_driving && _car.HasDrill)
        {
            // First it meets hills with the drill on (level tunnelling), later it noses down to dig deeper.
            bool dig = _elapsed > 26f && _elapsed < 36f;
            // Since the local-origin fix (PR #21) Q/E tilt works at full strength everywhere (near the
            // garage its sideways part used to be rounded away), so the bot eases off before 30 degrees
            // and pulls the nose back up past 36 instead of diving into the first cave it meets.
            float pitchNow = _car.PitchDegrees();
            _car.AutoTilt = !dig ? 0f : pitchNow < 22f ? 1f : pitchNow > 36f ? -0.6f : 0.001f;
            _car.AutoThrottle = (dig ? 0.6f : 1f) * (GameSession.DriveLeft ? -1f : 1f);
        }

        if (_elapsed < _nextLog) return;
        _nextLog += 2f;
        string tripNow = _trip == null ? "none" : $"{_trip.Farthest:0}m";
        Vector2 p = _player.Position, c = _car.CenterPosition;
        int surface = _world.SurfaceRow(Mathf.FloorToInt(c.X / Ts));
        GD.Print($"[autoplay] t={_elapsed:0}s {(_driving ? "driving" : "on foot")} biome={TileWorld.BiomeAt(Mathf.FloorToInt(c.X / Ts))} grip={_world.GripAt(c + new Vector2(0f, 60f)):0.00} player=({p.X / Ts:0.0},{p.Y / Ts:0.0}) grounded={_player.Grounded} " +
                 $"car=({c.X / Ts:0.0},{c.Y / Ts:0.0}) surfaceRow={surface} depth={(c.Y / Ts - surface) * 0.25f:0.0}m speed={_car.Speed / PixelsPerMeter * 3.6f:0}km/h pitch={_car.PitchDegrees():0} " +
                 $"fuel={_car.Fuel:0} damage={_car.Damage:0} drilling={_car.IsDrilling} boosting={_car.Boosting} boostTotal={_boostSeconds:0.0}s " +
                 $"top={_topSpeed / PixelsPerMeter * 3.6f:0}km/h longestAir={_longestAir:0.0}s landings={_perfectLandings}/{_cleanLandings}/{_roughLandings}/{_flops} kickers={_kickerJumps}/{_kickerLaunches} longJumps={_longJumps}/{_jumpRecords}/{_longestJump:0.0}m " +
                 $"crashes={_crashFx.Hits} hardest={_crashFx.HardestHit / PixelsPerMeter * 3.6f:0}km/h sparks={_crashFx.SparkBursts} " +
                 $"cans={_cansTaken} heaps={_heapsTaken} smashed={_heapsSmashed} bestStreak={_bestStreak} " +
                 $"dash={(_dash.Visible ? $"{_dash.ShownKmh:0}km/h" : "hidden")} lines={_lines.Intensity:0.00} peakLines={_peakLines:0.00} wind={_sfx.WindLevel:0.00} gear={_car.Gearbox.Gear}/{_car.Gearbox.Gears} revs={_car.Gearbox.Rpm:0.00} enginePitch={_sfx.LastEnginePitch:0.00} shifts={_car.Gearbox.Upshifts}/{_car.Gearbox.Downshifts} topGear={_car.Gearbox.TopGear} limiter={_car.Gearbox.Limiter} pops={_car.Backfires} turbo={(_car.HasTurbo ? $"peak {_car.TurboPeak:0.00} full {_car.TurboFullSeconds:0.0}s blowoffs {_car.TurboBlowOffs} now {_car.TurboBoost:0.00}" : "none")} starts={_perfectStarts}/{_goodStarts}/{_boggedStarts} sprints={_sprintsRun} sprintBest={GameSession.SprintBest:0.00} stuck={_stuckCount} rocks={_car.RockPulses} rockedFree={_rockFrees} longestStuck={_longestStuck:0.0}s flips={_backflips}/{_frontflips}/{_flipsCrashed} maxAirRot={_maxAirRot:0} rollOvers={_rollOvers} ice={_spray.IceSeconds:0.0}s slip={_spray.SlipSeconds:0.0}s bite={_spray.BiteSeconds:0.0}s trip={tripNow} reports={_tripsReported} deepest={_deepestThisRun:0.0}m crystalsMined={_crystalsMined} card={(_card.Showing ? _card.ShownScore.ToString() : "hidden")} {NightSummary()} {BirdSummary()} {BatSummary()} {DevilSummary()} {HookSummary()} {VeinSummary()} {LuckySummary()} {FlightSummary()} {HopSummary()} {NextPartSummary()} {HomeSummary()} {PlantSummary()} pauses={_pauses} | " +
                 string.Join(" ", System.Array.ConvertAll(GameSession.Items, i => $"{i}={GameSession.Count(i)}")));
    }
}
