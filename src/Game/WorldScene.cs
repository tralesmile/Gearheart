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
    private SoftPhysicsWorld _physics;
    private SoftVehicle _car;
    private Player _player;
    private Camera2D _camera;
    private CanvasModulate _darkness;
    private PointLight2D _lamp;
    private Label _hint, _toast, _status;
    private readonly Label[] _counts = new Label[GameSession.Items.Length];
    private Vector2 _garageDoor;
    private bool _driving;
    private float _toastTime, _elapsed, _nextLog, _saveTimer;

    public override void _Ready()
    {
        GameSession.ApplyCommandLine();
        _world = GameSession.World;
        _world.TileBroken += OnTileBroken;

        var sky = new CanvasLayer { Layer = -10 };
        AddChild(sky);
        _backdrop = new Backdrop();
        sky.AddChild(_backdrop);

        AddChild(new TileWorldView { Name = "Tiles", World = _world });

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

        _physics = new SoftPhysicsWorld { Name = "SoftPhysicsWorld", Terrain = _world, Mineable = _world };
        AddChild(_physics);

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
            PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 7f,
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

        BuildHud();
        GD.Print($"[world] ready: design=\"{GameSession.CurrentDesign.Name}\" autoplay={GameSession.AutoPlay} " +
                 $"player=({_player.Position.X / Ts:0},{_player.Position.Y / Ts:0}) tiles");
    }

    public override void _ExitTree()
    {
        if (_world != null) _world.TileBroken -= OnTileBroken;
        GameSession.SaveAll();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) GameSession.SaveAll();
    }

    private void SpawnCar()
    {
        if (IsInstanceValid(_car)) _car.QueueFree();
        float x = (TileWorld.PadFrom + 30) * Ts;
        _car = new SoftVehicle
        {
            Name = "Car",
            World = _physics,
            Design = GameSession.CurrentDesign.Clone(),
            Spawn = new Vector2(x, _world.GroundY(x, (TileWorld.BaseSurface - 12) * Ts) - 75f),
            Parked = true,
            DrillEnabled = false,
        };
        AddChild(_car);
    }

    // ---- Input ------------------------------------------------------------------------------

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

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
            case Key.X:
                if (_driving)
                {
                    _car.DrillEnabled = !_car.DrillEnabled;
                    Toast(_car.DrillEnabled ? "Drill ON" : "Drill OFF");
                }
                break;
            case Key.Key1: _player.SelectedBlock = "Dirt"; break;
            case Key.Key2: _player.SelectedBlock = "Stone"; break;
            case Key.Escape:
                GameSession.SaveAll();
                GetTree().Quit();
                break;
        }
    }

    private Vector2 Focus => _driving ? _car.CenterPosition : _player.Position;
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
        _car.AutoThrottle = GameSession.AutoPlay ? 1f : 0f;
        if (GameSession.AutoPlay) _car.DrillEnabled = true;
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
        _camera.Position = focus;
        float zoom = Mathf.Lerp(_camera.Zoom.X, _driving ? DriveZoom : FootZoom, 1f - Mathf.Exp(-3f * dt));
        _camera.Zoom = new Vector2(zoom, zoom);

        // Darkness grows with depth below the surface.
        float depth = focus.Y / Ts - _world.SurfaceRow(Mathf.FloorToInt(focus.X / Ts));
        float under = Mathf.Clamp((depth - 3f) / 12f, 0f, 1f);
        _darkness.Color = Colors.White.Lerp(new Color(0.16f, 0.17f, 0.24f), under);
        _lamp.Position = focus;
        _lamp.Energy = under * 1.15f;
        _lamp.TextureScale = _driving ? 1.6f : 1.1f;
        _backdrop.CameraPosition = _camera.GetScreenCenterPosition();
        _backdrop.Underground = under;

        UpdateHud(dt);
    }

    // ---- Mining results ---------------------------------------------------------------------

    private void OnTileBroken(int x, int y, Tile tile)
    {
        var (item, amount) = TileWorld.Drop(tile);
        if (item == null) return;
        GameSession.Add(item, amount);
        if (item != "Dirt" && item != "Stone") Toast($"+{amount} {item}");
        if (GameSession.AutoPlay && item != "Dirt" && item != "Stone") GD.Print($"[mining] +{amount} {item} at ({x},{y})");
    }

    // ---- HUD --------------------------------------------------------------------------------

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        _hint = Outlined(new Label { Position = new Vector2(16f, 12f) }, 18);
        layer.AddChild(_hint);

        _status = Outlined(new Label { HorizontalAlignment = HorizontalAlignment.Right }, 20);
        _status.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _status.Position += new Vector2(-436f, 12f);
        _status.CustomMinimumSize = new Vector2(420f, 0f);
        layer.AddChild(_status);

        _toast = Outlined(new Label { HorizontalAlignment = HorizontalAlignment.Center }, 28);
        _toast.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _toast.Position += new Vector2(-200f, 110f);
        _toast.CustomMinimumSize = new Vector2(400f, 0f);
        layer.AddChild(_toast);

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
            slot.AddChild(new TextureRect
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
    }

    private static Label Outlined(Label label, int size)
    {
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 5);
        return label;
    }

    private void Toast(string text)
    {
        _toast.Text = text;
        _toastTime = 2f;
    }

    private void UpdateHud(float dt)
    {
        for (int i = 0; i < _counts.Length; i++)
        {
            string item = GameSession.Items[i];
            bool selected = !_driving && item == _player.SelectedBlock;
            _counts[i].Text = $"{GameSession.Count(item)}{(i < 2 ? $" [{i + 1}]" : "")}";
            _counts[i].Modulate = selected ? new Color(1f, 0.9f, 0.4f) : Colors.White;
        }

        if (_driving)
        {
            _hint.Text = "A/D: drive   Q/E: tilt   Space: handbrake   F: get out" +
                         (_car.HasDrill ? $"   X: drill {(_car.DrillEnabled ? "ON" : "OFF")}" : "") +
                         (NearGarage() ? "   G: garage   R: repair" : "");
            _status.Text = $"{_car.Speed / PixelsPerMeter * 3.6f:0} km/h   Fuel {_car.Fuel:0}/{_car.FuelCapacity:0}   Damage {_car.Damage:0}";
        }
        else
        {
            string hint = "A/D: walk   Space: jump   Hold left mouse: mine   Right mouse: place block   1/2: choose block";
            if (NearCar()) hint += "\nF: get in the car";
            if (NearGarage()) hint += "\nG: open the garage (build and upgrade your car)";
            _hint.Text = hint;
            _status.Text = $"Depth {Mathf.Max(0f, (_player.Position.Y / Ts - _world.SurfaceRow(Mathf.FloorToInt(_player.Position.X / Ts))) * 0.25f):0} m";
        }

        if (_toastTime > 0f)
        {
            _toastTime -= dt;
            _toast.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(_toastTime, 0f, 1f));
        }
    }

    // ---- Automated playtest -----------------------------------------------------------------
    // --autoplay: walk, dig down by hand, then get in the car and drive (and drill) to the right.

    private void RunBot()
    {
        if (!_driving)
        {
            Vector2 feet = _player.Position + new Vector2(0f, Player.HalfHeight);
            if (_elapsed < 3f)
                _player.Override = new PlayerInput { Move = 1f, Jump = Mathf.Abs(_player.Velocity.X) < 5f, Aim = feet };
            else if (_elapsed < 9f)
                _player.Override = new PlayerInput { Mine = true, Aim = feet + new Vector2(0f, 8f) };
            else if (_elapsed < 10f)
                _player.Override = new PlayerInput { Place = true, Aim = _player.Position + new Vector2(-Ts * 1.5f, 0f) };
            else
                EnterCar();
        }

        if (_elapsed < _nextLog) return;
        _nextLog += 2f;
        Vector2 p = _player.Position, c = _car.CenterPosition;
        int surface = _world.SurfaceRow(Mathf.FloorToInt(c.X / Ts));
        GD.Print($"[autoplay] t={_elapsed:0}s {(_driving ? "driving" : "on foot")} player=({p.X / Ts:0.0},{p.Y / Ts:0.0}) grounded={_player.Grounded} " +
                 $"car=({c.X / Ts:0.0},{c.Y / Ts:0.0}) surfaceRow={surface} speed={_car.Speed / PixelsPerMeter * 3.6f:0}km/h pitch={_car.PitchDegrees():0} " +
                 $"fuel={_car.Fuel:0} damage={_car.Damage:0} drilling={_car.IsDrilling} | " +
                 string.Join(" ", System.Array.ConvertAll(GameSession.Items, i => $"{i}={GameSession.Count(i)}")));
    }
}
