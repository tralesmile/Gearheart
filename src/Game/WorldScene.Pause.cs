using Gearheart.Core;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// The pause menu (Esc / Start): the world stops, the menu shows volumes, fullscreen, the key list
/// and Save and quit. Esc used to quit the game outright; now it pauses. The trip report still takes
/// the first Esc (it closes the report), so the menu waits until the report is gone.
/// </summary>
public partial class WorldScene
{
    private PauseMenu _pause;
    private int _pauses;
    private bool _pauseBotPressed;
    private float _pausedAtElapsed;
    private Vector2 _pausedPlayer, _pausedCar;

    private void SetupPause()
    {
        var layer = new CanvasLayer { Layer = 20 };
        AddChild(layer);
        _pause = new PauseMenu { Name = "PauseMenu", CanOpen = () => !_card.Showing };
        layer.AddChild(_pause);
        _pause.Opened += () =>
        {
            _pauses++;
            _pausedAtElapsed = _elapsed;
            _pausedPlayer = _player.Position;
            _pausedCar = _car.CenterPosition;
            if (GameSession.PauseTest)
                GD.Print($"[pause] world stopped at t={_elapsed:0.00}s {(_driving ? "driving" : "on foot")} player=({_player.Position.X / Ts:0.0},{_player.Position.Y / Ts:0.0}) mining={_player.IsMining}");
        };
        _pause.Closed += () =>
        {
            if (!GameSession.PauseTest) return;
            GD.Print($"[pause] world resumed: t={_pausedAtElapsed:0.00}s -> {_elapsed:0.00}s, player moved {_player.Position.DistanceTo(_pausedPlayer):0.0} px, " +
                     $"car moved {_car.CenterPosition.DistanceTo(_pausedCar):0.0} px while paused");
        };
    }

    /// <summary>--pausetest: four seconds in (while the walk bot is mining) the bot presses a real Esc.</summary>
    private void RunPauseBot()
    {
        if (_pauseBotPressed || _elapsed < 4f) return;
        _pauseBotPressed = true;
        GD.Print($"[pause] bot presses Esc at t={_elapsed:0.00}s frame={Engine.GetFramesDrawn()}");
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Escape, Keycode = Key.Escape, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Escape, Keycode = Key.Escape, Pressed = false });
        Input.FlushBufferedEvents();
    }
}
