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
    private bool _crashDropped;
    private float _pickTimer;
    private int _puffs;
    private readonly Label[] _counts = new Label[GameSession.Items.Length];
    private Vector2 _garageDoor;
    private bool _driving;
    private float _toastTime, _elapsed, _nextLog, _saveTimer;
    private float _airTime, _airStartSpeed, _boostSeconds, _topSpeed, _longestAir;

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

        _physics = new SoftPhysicsWorld { Name = "SoftPhysicsWorld", Terrain = _world, Mineable = _world };
        AddChild(_physics);

        _crashFx = new CrashFx { Name = "CrashFx", Log = GameSession.AutoPlay, GroundColor = GroundColorAt };
        AddChild(_crashFx);
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

        _sfx = new Sfx { Name = "Sfx" };
        AddChild(_sfx);
        _player.Placed += () => _sfx.Play("place");
        _crashFx.Camera = _camera;
        _crashFx.Sfx = _sfx;
        _crashFx.Toast = text =>
        {
            if (_toastTime < 1.2f) Toast(text);   // never hide a fresh stunt call-out
        };

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
        _car.AutoThrottle = GameSession.AutoPlay ? (GameSession.DriveLeft ? -1f : 1f) : 0f;
        if (GameSession.AutoPlay) _car.DrillEnabled = true;
        _car.AutoBoost = GameSession.AutoPlay && _car.HasBooster;
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
        if (_driving) TrackStunts(dt);

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
        _view.Focus = focus;
        // Pull the camera back at speed so jumps and what is ahead stay in view.
        float driveZoom = DriveZoom - 0.4f * Mathf.Clamp((_car.Speed - 500f) / 1000f, 0f, 1f);
        float zoom = Mathf.Lerp(_camera.Zoom.X, _driving ? driveZoom : FootZoom, 1f - Mathf.Exp(-2f * dt));
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
        (_backdrop.Desert, _backdrop.Frozen) = TileWorld.BiomeBlend(focus.X);

        _crashFx.Enabled = _driving;
        _sfx.UpdateCar(_driving, _car.Speed, _car.Throttle, _car.IsDrilling && _car.DrillEnabled, _car.Boosting);
        _pickTimer -= dt;
        if (!_driving && _player.IsMining && _pickTimer <= 0f)
        {
            _sfx.Play("pick", -10f);
            _pickTimer = 0.22f;
        }

        UpdateHud(dt);
    }

    // ---- Mining results ---------------------------------------------------------------------

    private void OnTileBroken(int x, int y, Tile tile)
    {
        _sfx?.Play("break", -9f, 0.06f);
        Puff(x, y, tile);

        var (item, amount) = TileWorld.Drop(tile);
        if (item == null) return;
        if (item != "Dirt" && item != "Stone") _sfx?.Play("pickup", -8f, 0.1f);
        GameSession.Add(item, amount);
        if (item != "Dirt" && item != "Stone") Toast($"+{amount} {item}");
        if (GameSession.AutoPlay && item != "Dirt" && item != "Stone") GD.Print($"[mining] +{amount} {item} at ({x},{y})");
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
            _ => new Color(0.47f, 0.49f, 0.52f),
        };

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

    private static string BiomeName(float worldX) => TileWorld.BiomeAt(Mathf.FloorToInt(worldX / Ts)) switch
    {
        Biome.Desert => "The Wastes",
        Biome.Frozen => "Frozen North",
        _ => "Home Meadow",
    };

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
            _hint.Text = "A/D: drive   Q/E: tilt (also rolls you back over)   Space: handbrake   F: get out" +
                         (_car.HasDrill ? $"   X: drill {(_car.DrillEnabled ? "ON" : "OFF")}" : "") +
                         (_car.HasBooster ? "   Shift: BOOST" : "") +
                         (NearGarage() ? "   G: garage   R: repair" : "");
            _status.Text = $"{(_car.Boosting ? "BOOST!   " : "")}{BiomeName(_car.CenterPosition.X)}   {_car.Speed / PixelsPerMeter * 3.6f:0} km/h   Fuel {_car.Fuel:0}/{_car.FuelCapacity:0}   Damage {_car.Damage:0}";
            _status.Modulate = _car.Boosting ? new Color(1f, 0.8f, 0.4f) : Colors.White;
        }
        else
        {
            string hint = "A/D: walk   Space: jump   Hold left mouse: mine   Right mouse: place block   1/2: choose block";
            if (NearCar()) hint += "\nF: get in the car";
            if (NearGarage()) hint += "\nG: open the garage (build and upgrade your car)";
            _hint.Text = hint;
            _status.Modulate = Colors.White;
            _status.Text = $"{BiomeName(_player.Position.X)}   Depth {Mathf.Max(0f, (_player.Position.Y / Ts - _world.SurfaceRow(Mathf.FloorToInt(_player.Position.X / Ts))) * 0.25f):0} m";
        }

        if (_toastTime > 0f)
        {
            _toastTime -= dt;
            _toast.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(_toastTime, 0f, 1f));
        }
    }

    // ---- Stunts -----------------------------------------------------------------------------

    /// <summary>Times jumps and boosts: a long jump gets a cheer on screen (and a line in the bot log).</summary>
    private void TrackStunts(float dt)
    {
        if (_car.Boosting) _boostSeconds += dt;
        _topSpeed = Mathf.Max(_topSpeed, _car.Speed);

        if (_car.Airborne)
        {
            if (_airTime == 0f) _airStartSpeed = _car.Speed;
            _airTime += dt;
            return;
        }
        if (_airTime > 0.9f)
        {
            _longestAir = Mathf.Max(_longestAir, _airTime);
            string praise = _airTime > 2.5f ? "MONSTER AIR" : _airTime > 1.6f ? "HUGE AIR" : "BIG AIR";
            Toast($"{praise}!  {_airTime:0.0} s");
            _sfx.Play("pickup", -6f);
            if (GameSession.AutoPlay)
                GD.Print($"[stunt] {praise.ToLowerInvariant()}: airborne {_airTime:0.00}s, took off at {_airStartSpeed / PixelsPerMeter * 3.6f:0} km/h, damage={_car.Damage:0}");
        }
        _airTime = 0f;
    }

    // ---- Automated playtest -----------------------------------------------------------------
    // --autoplay: walk, dig down by hand, then get in the car and drive (and drill) to the right.

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

        if (!_driving && !GameSession.CrashTest)
        {
            Vector2 feet = _player.Position + new Vector2(0f, Player.HalfHeight);
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
            _car.AutoTilt = dig && _car.PitchDegrees() < 30f ? 1f : dig ? 0.001f : 0f;
            _car.AutoThrottle = (dig ? 0.6f : 1f) * (GameSession.DriveLeft ? -1f : 1f);
        }

        if (_elapsed < _nextLog) return;
        _nextLog += 2f;
        Vector2 p = _player.Position, c = _car.CenterPosition;
        int surface = _world.SurfaceRow(Mathf.FloorToInt(c.X / Ts));
        GD.Print($"[autoplay] t={_elapsed:0}s {(_driving ? "driving" : "on foot")} biome={TileWorld.BiomeAt(Mathf.FloorToInt(c.X / Ts))} grip={_world.GripAt(c + new Vector2(0f, 60f)):0.00} player=({p.X / Ts:0.0},{p.Y / Ts:0.0}) grounded={_player.Grounded} " +
                 $"car=({c.X / Ts:0.0},{c.Y / Ts:0.0}) surfaceRow={surface} depth={(c.Y / Ts - surface) * 0.25f:0.0}m speed={_car.Speed / PixelsPerMeter * 3.6f:0}km/h pitch={_car.PitchDegrees():0} " +
                 $"fuel={_car.Fuel:0} damage={_car.Damage:0} drilling={_car.IsDrilling} boosting={_car.Boosting} boostTotal={_boostSeconds:0.0}s " +
                 $"top={_topSpeed / PixelsPerMeter * 3.6f:0}km/h longestAir={_longestAir:0.0}s " +
                 $"crashes={_crashFx.Hits} hardest={_crashFx.HardestHit / PixelsPerMeter * 3.6f:0}km/h sparks={_crashFx.SparkBursts} | " +
                 string.Join(" ", System.Array.ConvertAll(GameSession.Items, i => $"{i}={GameSession.Count(i)}")));
    }
}
