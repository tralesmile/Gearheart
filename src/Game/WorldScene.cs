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
    private StashGlints _glints;
    private RoadsidePickups _pickups;
    private bool _canHinted;
    private int _cansTaken, _heapsTaken, _heapsSmashed;
    private int _smashStreak, _bestStreak;
    private float _lastSmashAt = -99f;
    private bool _smashToast;
    private const float StreakWindow = 2.5f;     // seconds between smashes that still chain
    private Dashboard _dash;
    private SpeedLines _lines;
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

    // Trips: leave the garage grounds, come back, get a report card with a score and a best to beat.
    private const float TripLeave = 18f, TripHome = 8f;   // metres from the middle of the garage pad
    private Trip _trip;
    private TripCard _card;
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

        // Glints over buried stashes, on their own layer so the underground darkness does not hide them.
        var glintLayer = new CanvasLayer { Layer = 1, FollowViewportEnabled = true };
        AddChild(glintLayer);
        _glints = new StashGlints { Name = "StashGlints", World = _world, Focus = _player.Position };
        glintLayer.AddChild(_glints);

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
        LogStashes();
        LogPickups();
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
        CheckPickups();
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
        _glints.Focus = focus;
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
        if (TileWorld.IsStash(tile))
        {
            OpenStash(x, y, tile);
            return;
        }

        var (item, amount) = TileWorld.Drop(tile);
        if (item == null) return;
        if (item != "Dirt" && item != "Stone") _sfx?.Play("pickup", -8f, 0.1f);
        GameSession.Add(item, amount);
        if (item != "Dirt" && item != "Stone") Toast($"+{amount} {item}");
        if (GameSession.AutoPlay && item != "Dirt" && item != "Stone") GD.Print($"[mining] +{amount} {item} at ({x},{y})");
    }

    /// <summary>The big find: a buried stash pays out a bundle of materials with a fanfare and a gold fountain.</summary>
    private void OpenStash(int x, int y, Tile tile)
    {
        var loot = TileWorld.StashLoot(tile, x, y);
        foreach (var (item, amount) in loot) GameSession.Add(item, amount);
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
            foreach (var (item, amount) in loot) GameSession.Add(item, amount);
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

        _toast = Outlined(new Label { HorizontalAlignment = HorizontalAlignment.Center }, 28);
        _toast.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _toast.Position += new Vector2(-200f, 110f);
        _toast.CustomMinimumSize = new Vector2(400f, 0f);
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
        _smashToast = false;
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
            // Speed, fuel and damage live on the dashboard now; the status line keeps where you are.
            float kmh = _car.Speed / PixelsPerMeter * 3.6f;
            _status.Text = $"{(_car.Boosting ? "BOOST!   " : "")}{BiomeName(_car.CenterPosition.X)}{TripStatus()}";
            _status.Modulate = _car.Boosting ? new Color(1f, 0.8f, 0.4f) : Colors.White;
            _dash.Visible = true;
            _dash.Set(kmh, _car.Fuel, _car.FuelCapacity, _car.Damage, _car.Boosting);
            _lines.Set(_carVx / PixelsPerMeter * 3.6f, _carDir, _car.Boosting);
            _peakLines = Mathf.Max(_peakLines, _lines.Intensity);
            if (GameSession.AutoPlay && !_linesLogged && _lines.Intensity > 0.6f)
            {
                _linesLogged = true;
                GD.Print($"[speed] streaks at {_lines.Intensity:0.00} at {_carVx / PixelsPerMeter * 3.6f:0} km/h sideways, wind {_sfx.WindLevel:0.00}, boosting={_car.Boosting} frame={Engine.GetFramesDrawn()}");
            }
        }
        else
        {
            string hint = "A/D: walk   Space: jump   Hold left mouse: mine   Right mouse: place block   1/2: choose block";
            if (NearCar()) hint += "\nF: get in the car";
            if (NearGarage()) hint += "\nG: open the garage (build and upgrade your car)";
            _hint.Text = hint;
            _dash.Visible = false;
            _lines.Set(0f, _carDir, false);
            _status.Modulate = Colors.White;
            _status.Text = $"{BiomeName(_player.Position.X)}{TripStatus()}   Depth {Mathf.Max(0f, (_player.Position.Y / Ts - _world.SurfaceRow(Mathf.FloorToInt(_player.Position.X / Ts))) * 0.25f):0} m" +
                           (GameSession.StashesFound > 0 ? $"   Stashes found {GameSession.StashesFound}" : "");
        }

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
        };
        foreach (string item in Trip.HaulItems) _trip.MaterialsAtStart[item] = GameSession.Count(item);
        _farthestCheered = false;
        if (GameSession.AutoPlay) GD.Print($"[trip] start: left the garage grounds {(_driving ? "driving" : "on foot")} at t={_elapsed:0.0}s");
    }

    private void EndTrip()
    {
        Trip t = _trip;
        _trip = null;
        t.Seconds = _elapsed - t.StartedAt;
        t.Heaps = _heapsTaken - t.HeapsAtStart;
        t.Cans = _cansTaken - t.CansAtStart;
        t.Crashes = _crashFx.Hits - t.CrashesAtStart;
        t.RollOvers = _rollOvers - t.RollOversAtStart;
        t.Stashes = GameSession.StashesFound - t.StashesAtStart;
        foreach (string item in Trip.HaulItems) t.Haul[item] = Mathf.Max(0, GameSession.Count(item) - t.MaterialsAtStart.GetValueOrDefault(item));
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
                     $"farthest={t.Farthest:0}m side={(t.FarthestSide < 0 ? "west" : "east")} reached=\"{BiomeName(t.FarthestX)}\" time={t.Seconds:0.0}s top={t.TopKmh:0}km/h air={t.LongestAir:0.0}s " +
                     $"heaps={t.Heaps} streak={t.BestStreak} cans={t.Cans} stashes={t.Stashes} haul=\"{t.HaulText()}\" crashes={t.Crashes} rollOvers={t.RollOvers} " +
                     $"records=[{records}] frame={Engine.GetFramesDrawn()}");
            foreach (TripRow r in rows) GD.Print($"[trip]   {r.Label}: {r.Value} -> +{r.Points}{(r.Record ? " NEW BEST" : "")}");
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

        // Lying on the roof also has no wheels on the ground: that is not air time worth a cheer.
        if (_car.SelfRighting)
        {
            _airTime = 0f;
            return;
        }
        if (_car.Airborne)
        {
            if (_airTime == 0f) _airStartSpeed = _car.Speed;
            _airTime += dt;
            return;
        }
        if (_airTime > 0.3f) _trip?.NoteAir(_airTime);
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

        if (!_driving && !GameSession.CrashTest && !GameSession.TripTest)
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
            _car.AutoTilt = dig && _car.PitchDegrees() < 30f ? 1f : dig ? 0.001f : 0f;
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
                 $"top={_topSpeed / PixelsPerMeter * 3.6f:0}km/h longestAir={_longestAir:0.0}s " +
                 $"crashes={_crashFx.Hits} hardest={_crashFx.HardestHit / PixelsPerMeter * 3.6f:0}km/h sparks={_crashFx.SparkBursts} " +
                 $"cans={_cansTaken} heaps={_heapsTaken} smashed={_heapsSmashed} bestStreak={_bestStreak} " +
                 $"dash={(_dash.Visible ? $"{_dash.ShownKmh:0}km/h" : "hidden")} lines={_lines.Intensity:0.00} peakLines={_peakLines:0.00} wind={_sfx.WindLevel:0.00} rollOvers={_rollOvers} trip={tripNow} reports={_tripsReported} card={(_card.Showing ? _card.ShownScore.ToString() : "hidden")} | " +
                 string.Join(" ", System.Array.ConvertAll(GameSession.Items, i => $"{i}={GameSession.Count(i)}")));
    }
}
