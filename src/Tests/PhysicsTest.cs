using System.Collections.Generic;
using Gearheart.Vehicles;
using Gearheart.World;
using Godot;

namespace Gearheart.Tests;

/// <summary>
/// Week 3-4 physics test scene: drive, crash, and benchmark 1, 4 or 8 vehicles.
/// Controls are shown on screen.
/// </summary>
public partial class PhysicsTest : Node2D
{
    private const float PixelsPerMeter = 64f;
    private static readonly Vector2 SpawnPoint = new(200f, -150f);

    private Vehicle _player;
    private readonly List<Vehicle> _bots = new();
    private Camera2D _camera;
    private Label _hud;

    public override void _Ready()
    {
        RenderingServer.SetDefaultClearColor(new Color(0.45f, 0.65f, 0.85f));

        AddChild(new TestTerrain { Name = "Terrain" });
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
            case Key.Key1: SetBotCount(0); break;
            case Key.Key4: SetBotCount(3); break;
            case Key.Key8: SetBotCount(7); break;
            case Key.Escape: GetTree().Quit(); break;
        }
    }

    public override void _Process(double delta)
    {
        bool hasPlayer = IsInstanceValid(_player) && _player.Chassis != null;
        if (hasPlayer)
            _camera.Position = _player.Chassis.GlobalPosition;

        float kmh = hasPlayer ? _player.Speed / PixelsPerMeter * 3.6f : 0f;
        float damage = hasPlayer ? _player.Damage : 0f;

        _hud.Text =
            $"FPS {Engine.GetFramesPerSecond():0}   Vehicles {1 + _bots.Count}   Speed {kmh:0} km/h   Damage {damage:0}\n" +
            "D/A or arrows: drive   Q/E: tilt   Space: handbrake   R: reset   1/4/8: vehicle count   Esc: quit";
    }

    private void SpawnPlayer()
    {
        if (IsInstanceValid(_player)) _player.QueueFree();
        _player = new Vehicle { Name = "Player", PlayerControlled = true, Position = SpawnPoint };
        AddChild(_player);
    }

    private void SetBotCount(int count)
    {
        foreach (var bot in _bots)
            if (IsInstanceValid(bot)) bot.QueueFree();
        _bots.Clear();

        for (int i = 0; i < count; i++)
        {
            var bot = new Vehicle
            {
                Name = $"Bot{i}",
                AutoThrottle = 0.7f,
                Position = new Vector2(450f + 220f * i, -300f),
                BodyColor = Color.FromHsv(0.08f * i + 0.5f, 0.6f, 0.85f),
            };
            AddChild(bot);
            _bots.Add(bot);
        }
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
                crate.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(size, size) } });
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
