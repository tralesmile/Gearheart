using Gearheart.Core;
using Gearheart.Physics;
using Gearheart.Vehicles;
using Gearheart.World;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// Driving your design. Two modes:
/// - Free Drive: rolling hills, crates to smash, ore rocks to mine with a drill.
/// - Hill Climb: a rising, ever steeper track; the clock runs from the start line to the finish.
/// </summary>
public partial class DriveScene : Node2D
{
    private const float PixelsPerMeter = 64f;
    private const float StartX = 300f;
    private const float FinishX = 22000f;   // about 340 m after the start
    private const float StuckSpeed = 0.5f * PixelsPerMeter;  // below 0.5 m/s ...
    private const float StuckSeconds = 4f;                    // ... for this long = stuck

    private static readonly float[] CrateStacks = { 1500f, 4200f };

    private enum RaceState { WaitingForStart, Running, Finished, Stuck }

    private bool _isRace;
    private TestTerrain _terrain;
    private OreField _ore;
    private SoftPhysicsWorld _world;
    private SoftVehicle _player;
    private Camera2D _camera;
    private Label _hud;
    private Label _banner;
    private Label _toast;

    private RaceState _race;
    private float _raceTime;
    private float _stuckTime;
    private float _toastTime;
    private float _elapsed;
    private float _nextLog;

    public override void _Ready()
    {
        GameSession.ApplyCommandLine();
        _isRace = GameSession.Mode == DriveMode.HillClimb;
        RenderingServer.SetDefaultClearColor(_isRace ? new Color(0.55f, 0.7f, 0.85f) : new Color(0.45f, 0.65f, 0.85f));

        _terrain = new TestTerrain
        {
            Name = "Terrain",
            Style = _isRace ? TerrainStyle.HillClimb : TerrainStyle.Rolling,
            Length = _isRace ? 24000 : 12000,
            Seed = _isRace ? 11 : 7,
        };
        AddChild(_terrain);

        _ore = new OreField { Name = "Ore" };
        AddChild(_ore);
        if (!_isRace) _ore.Scatter(_terrain, 700f, 11500f, 650f, 99, CrateStacks);
        _ore.Mined += OnMined;

        _world = new SoftPhysicsWorld
        {
            Name = "SoftPhysicsWorld",
            Terrain = _terrain,
            Mineable = _ore,
        };
        AddChild(_world);

        if (_isRace)
        {
            AddFlag(StartX, "START", new Color(0.95f, 0.95f, 0.95f));
            AddFlag(FinishX, "FINISH", new Color(0.95f, 0.8f, 0.2f));
        }
        else
        {
            foreach (float x in CrateStacks) SpawnCrates(x);
        }

        _camera = new Camera2D
        {
            Zoom = new Vector2(0.8f, 0.8f),
            PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 6f,
        };
        AddChild(_camera);
        _camera.MakeCurrent();

        BuildHud();
        SpawnPlayer();
        GD.Print($"[drive] mode={GameSession.Mode} design=\"{GameSession.CurrentDesign.Name}\" autodrive={GameSession.AutoDrive} rocks={_ore.RocksLeft}");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

        switch (key.PhysicalKeycode)
        {
            case Key.R: SpawnPlayer(); break;
            case Key.G: GetTree().ChangeSceneToFile(GameSession.GarageScene); break;
            case Key.Escape: GetTree().Quit(); break;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _elapsed += dt;
        if (!IsInstanceValid(_player)) return;

        if (_isRace) UpdateRace(dt);

        if (GameSession.AutoDrive && _elapsed >= _nextLog)
        {
            _nextLog += 2f;
            Vector2 c = _player.CenterPosition;
            GD.Print($"[autodrive] t={_elapsed:0.0}s x={c.X / PixelsPerMeter:0}m y={-c.Y / PixelsPerMeter:0}m " +
                     $"speed={_player.Speed / PixelsPerMeter * 3.6f:0}km/h fuel={_player.Fuel:0} damage={_player.Damage:0} " +
                     $"race={_race} time={_raceTime:0.0} rocks={_ore.RocksLeft} | {GameSession.MaterialsText()}");
            if (!_isRace)
            {
                foreach (var tip in _player.DrillTips)
                    GD.Print($"   [debug] tip=({tip.X:0},{tip.Y:0}) ground={_terrain.SurfaceY(tip.X):0} {_ore.DebugNearest(tip)}");
                GD.Print($"   [debug] pitch={_player.PitchDegrees():0}deg drilling={_player.IsDrilling} {_player.DebugSummary()}");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_player)) return;
        _camera.Position = _player.CenterPosition;

        float kmh = _player.Speed / PixelsPerMeter * 3.6f;
        string text = $"Speed {kmh:0} km/h   Fuel {_player.Fuel:0}/{_player.FuelCapacity:0}   Damage {_player.Damage:0}   FPS {Engine.GetFramesPerSecond():0}";

        if (_isRace)
        {
            var best = GameSession.BestTime(GameSession.HillClimbTrack);
            float toGo = Mathf.Max(0f, (FinishX - _player.CenterPosition.X) / PixelsPerMeter);
            text += $"\nTime {GameSession.FormatTime(_raceTime)}   Best {(best != null ? GameSession.FormatTime(best.Seconds) : "--")}   To finish {toGo:0} m";
        }
        else
        {
            text += $"\n{GameSession.MaterialsText()}   Rocks left {_ore.RocksLeft}";
            if (_player.IsDrilling) text += "\nDRILLING: ease off the throttle, press E to keep the nose down";
        }

        text += "\nD/A or arrows: drive   Q/E: tilt   Space: handbrake   R: repair and restart   G: garage   Esc: quit";
        _hud.Text = text;

        if (_toastTime > 0f)
        {
            _toastTime -= (float)delta;
            _toast.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(_toastTime, 0f, 1f));
        }
    }

    // ---- Race --------------------------------------------------------------------------------

    private void UpdateRace(float dt)
    {
        float x = _player.CenterPosition.X;
        switch (_race)
        {
            case RaceState.WaitingForStart:
                if (x >= StartX)
                {
                    _race = RaceState.Running;
                    _raceTime = 0f;
                    SetBanner("GO!", 1f);
                }
                break;

            case RaceState.Running:
                _raceTime += dt;
                if (x >= FinishX)
                {
                    _race = RaceState.Finished;
                    bool record = GameSession.SubmitTime(GameSession.HillClimbTrack, _raceTime, GameSession.CurrentDesign.Name);
                    SetBanner($"FINISH  {GameSession.FormatTime(_raceTime)}{(record ? "\nNEW RECORD!" : "")}\nR: race again   G: garage", 0f);
                    GD.Print($"[race] finished in {_raceTime:0.00}s record={record}");
                    break;
                }

                _stuckTime = _player.Speed < StuckSpeed ? _stuckTime + dt : 0f;
                if (_stuckTime > StuckSeconds)
                {
                    _race = RaceState.Stuck;
                    string reason = _player.Fuel <= 0f ? "Out of fuel" : "Stuck";
                    SetBanner($"{reason} after {(x - StartX) / PixelsPerMeter:0} m\nR: try again   G: garage", 0f);
                    GD.Print($"[race] {reason.ToLowerInvariant()} at {(x - StartX) / PixelsPerMeter:0}m after {_raceTime:0.0}s");
                }
                break;
        }
    }

    // ---- Spawning ----------------------------------------------------------------------------

    private void SpawnPlayer()
    {
        if (IsInstanceValid(_player)) _player.QueueFree();

        float spawnX = _isRace ? 120f : 200f;
        _player = new SoftVehicle
        {
            Name = "Player",
            World = _world,
            Design = GameSession.CurrentDesign.Clone(),
            Spawn = new Vector2(spawnX, _terrain.SurfaceY(spawnX) - 110f),
            PlayerControlled = !GameSession.AutoDrive,
            AutoThrottle = GameSession.AutoDrive ? 1f : 0f,
        };
        AddChild(_player);

        _race = RaceState.WaitingForStart;
        _raceTime = 0f;
        _stuckTime = 0f;
        SetBanner(_isRace ? "Hill Climb: reach the finish as fast as you can" : "", 3f);
    }

    private void SpawnCrates(float x)
    {
        const float size = 32f;
        float ground = _terrain.SurfaceY(x);
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 4 - row; col++)
            {
                var crate = new RigidBody2D
                {
                    Mass = 0.3f,
                    Position = new Vector2(x + col * (size + 2f) + row * (size / 2f), ground - 40f - row * (size + 2f)),
                };
                crate.AddToGroup(SoftPhysicsWorld.LooseObjectsGroup);
                crate.AddChild(new CollisionShape2D { Name = "Shape", Shape = new RectangleShape2D { Size = new Vector2(size, size) } });
                crate.AddChild(new Polygon2D
                {
                    Polygon = Util.Shapes.Rect(new Vector2(size, size)),
                    Color = new Color(0.7f, 0.5f, 0.25f),
                });
                AddChild(crate);
            }
        }
    }

    private void AddFlag(float x, string text, Color color)
    {
        float ground = _terrain.SurfaceY(x);
        var flag = new Node2D { Position = new Vector2(x, ground) };
        flag.AddChild(new Line2D { Points = new[] { Vector2.Zero, new Vector2(0f, -170f) }, Width = 4f, DefaultColor = new Color(0.2f, 0.2f, 0.2f) });
        flag.AddChild(new Polygon2D { Polygon = new[] { new Vector2(0f, -170f), new Vector2(70f, -150f), new Vector2(0f, -130f) }, Color = color });
        var label = new Label { Text = text, Position = new Vector2(-30f, -210f) };
        label.AddThemeFontSizeOverride("font_size", 24);
        flag.AddChild(label);
        AddChild(flag);
    }

    // ---- HUD ---------------------------------------------------------------------------------

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        _hud = new Label { Position = new Vector2(16f, 16f) };
        _hud.AddThemeFontSizeOverride("font_size", 18);
        _hud.AddThemeColorOverride("font_outline_color", Colors.Black);
        _hud.AddThemeConstantOverride("outline_size", 4);
        layer.AddChild(_hud);

        _banner = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _banner.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _banner.AddThemeFontSizeOverride("font_size", 40);
        _banner.AddThemeColorOverride("font_outline_color", Colors.Black);
        _banner.AddThemeConstantOverride("outline_size", 8);
        layer.AddChild(_banner);

        _toast = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _toast.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _toast.Position += new Vector2(-200f, 120f);
        _toast.CustomMinimumSize = new Vector2(400f, 0f);
        _toast.AddThemeFontSizeOverride("font_size", 28);
        _toast.AddThemeColorOverride("font_outline_color", Colors.Black);
        _toast.AddThemeConstantOverride("outline_size", 6);
        layer.AddChild(_toast);
    }

    private void SetBanner(string text, float seconds)
    {
        _banner.Text = text;
        if (seconds > 0f)
        {
            GetTree().CreateTimer(seconds).Timeout += () =>
            {
                if (IsInstanceValid(_banner) && _banner.Text == text) _banner.Text = "";
            };
        }
    }

    private void OnMined(OreType ore, int amount)
    {
        GameSession.AddMaterial(ore, amount);
        _toast.Text = $"+{amount} {ore}";
        _toast.AddThemeColorOverride("font_color", OreField.ColorOf(ore).Lightened(0.3f));
        _toastTime = 2f;
        GD.Print($"[mining] +{amount} {ore} (total {GameSession.MaterialCount(ore)})");
    }
}
