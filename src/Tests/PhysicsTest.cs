using System.Collections.Generic;
using Gearheart.Physics;
using Gearheart.Vehicles;
using Gearheart.World;
using Godot;

namespace Gearheart.Tests;

/// <summary>
/// Week 3-4 physics test scene: drive, crash, and benchmark 1, 4 or 8 vehicles,
/// switching between physics approach A (rigid parts) and B (soft body) with V.
/// Controls are shown on screen.
/// </summary>
public partial class PhysicsTest : Node2D
{
    private const float PixelsPerMeter = 64f;
    private static readonly Vector2 SpawnPoint = new(200f, -150f);

    private enum PhysicsMode { Rigid, Soft }

    private PhysicsMode _mode = PhysicsMode.Soft; // approach B chosen 2026-11-11; V switches to A for comparison
    private TestTerrain _terrain;
    private SoftPhysicsWorld _softWorld;
    private Node2D _player;
    private readonly List<Node2D> _bots = new();
    private Camera2D _camera;
    private Label _hud;

    public override void _Ready()
    {
        RenderingServer.SetDefaultClearColor(new Color(0.45f, 0.65f, 0.85f));

        _terrain = new TestTerrain { Name = "Terrain" };
        AddChild(_terrain);
        _softWorld = new SoftPhysicsWorld { Name = "SoftPhysicsWorld", Terrain = _terrain };
        AddChild(_softWorld);
        SpawnCrates();

        _camera = new Camera2D
        {
            Zoom = new Vector2(0.8f, 0.8f),
            PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 6f,
        };
        AddChild(_camera);
        _camera.MakeCurrent();

        var hudLayer = new CanvasLayer();
        AddChild(hudLayer);
        _hud = new Label { Position = new Vector2(16f, 16f) };
        hudLayer.AddChild(_hud);

        SpawnPlayer();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

        switch (key.PhysicalKeycode)
        {
            case Key.R: SpawnPlayer(); break;
            case Key.V: ToggleMode(); break;
            case Key.C: SpawnOncoming(); break;
            case Key.Key1: SetBotCount(0); break;
            case Key.Key4: SetBotCount(3); break;
            case Key.Key8: SetBotCount(7); break;
            case Key.Escape: GetTree().Quit(); break;
        }
    }

    public override void _Process(double delta)
    {
        var vehicle = IsInstanceValid(_player) ? _player as IVehicle : null;
        if (vehicle != null)
            _camera.Position = vehicle.CenterPosition;

        float kmh = vehicle != null ? vehicle.Speed / PixelsPerMeter * 3.6f : 0f;
        float damage = vehicle?.Damage ?? 0f;
        string physics = vehicle?.PhysicsName ?? "-";

        _hud.Text =
            $"Physics {physics}   FPS {Engine.GetFramesPerSecond():0}   Vehicles {1 + _bots.Count}   Speed {kmh:0} km/h   Damage {damage:0}\n" +
            "D/A or arrows: drive   Q/E: tilt   Space: handbrake   R: reset   V: switch physics A/B\n" +
            "C: oncoming car (head-on test)   1/4/8: vehicle count   Esc: quit";
    }

    private void ToggleMode()
    {
        _mode = _mode == PhysicsMode.Rigid ? PhysicsMode.Soft : PhysicsMode.Rigid;
        int bots = _bots.Count;
        SpawnPlayer();
        SetBotCount(bots);
    }

    private Node2D CreateVehicle(string name, Vector2 at, bool player, float autoThrottle, Color color)
    {
        if (_mode == PhysicsMode.Soft)
        {
            return new SoftVehicle
            {
                Name = name,
                World = _softWorld,
                Spawn = at,
                PlayerControlled = player,
                AutoThrottle = autoThrottle,
                BodyColor = color,
            };
        }

        return new Vehicle
        {
            Name = name,
            Position = at,
            PlayerControlled = player,
            AutoThrottle = autoThrottle,
            BodyColor = color,
        };
    }

    private void SpawnPlayer()
    {
        if (IsInstanceValid(_player)) _player.QueueFree();
        var color = _mode == PhysicsMode.Soft ? new Color(0.25f, 0.55f, 0.85f) : new Color(0.85f, 0.35f, 0.2f);
        _player = CreateVehicle("Player", SpawnPoint, true, 0f, color);
        AddChild(_player);
    }

    private void SetBotCount(int count)
    {
        foreach (var bot in _bots)
            if (IsInstanceValid(bot)) bot.QueueFree();
        _bots.Clear();

        for (int i = 0; i < count; i++)
        {
            var bot = CreateVehicle(
                $"Bot{i}",
                new Vector2(450f + 220f * i, -300f),
                false,
                0.7f,
                Color.FromHsv(0.08f * i + 0.5f, 0.6f, 0.85f));
            AddChild(bot);
            _bots.Add(bot);
        }
    }

    /// <summary>Spawns a car ahead of the player that drives toward it, for head-on crash tests.</summary>
    private void SpawnOncoming()
    {
        var vehicle = IsInstanceValid(_player) ? _player as IVehicle : null;
        Vector2 at = (vehicle?.CenterPosition ?? SpawnPoint) + new Vector2(900f, -250f);
        var oncoming = CreateVehicle($"Oncoming{_bots.Count}", at, false, -0.8f, new Color(0.9f, 0.8f, 0.2f));
        AddChild(oncoming);
        _bots.Add(oncoming);
    }

    private void SpawnCrates()
    {
        const float size = 32f;
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 4 - row; col++)
            {
                var crate = new RigidBody2D
                {
                    Mass = 0.3f,
                    Position = new Vector2(900f + col * (size + 2f) + row * (size / 2f), -80f - row * (size + 2f)),
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
}
