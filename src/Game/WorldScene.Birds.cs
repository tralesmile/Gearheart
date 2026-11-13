using System.Collections.Generic;
using Gearheart.Core;
using Gearheart.World;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// Birds along the road and on the garage roof (see <see cref="Birds"/>), and the horn (H, or Y on a
/// gamepad) that scares them off. Starting the engine flushes the sparrows off the garage roof; driving
/// at a flock sends it up in a flutter ahead of the bonnet. Nothing here touches physics or scoring.
/// </summary>
public partial class WorldScene
{
    private const float HornReach = 750f;          // px (about 12 m, a bit past the screen edge ahead)
    private const float EngineStartReach = 800f;
    private Birds _birds;
    private int _honks;
    private bool _botHonked, _padHornDown;
    private readonly Dictionary<string, int> _scatterCauses = new();

    private void SetupBirds(Vector2 garageAt)
    {
        // Perches on the garage roof (the shed is drawn at 2x; its roof line is 9 px down the sprite,
        // the sign's top edge is the top of the sprite).
        var roof = new[]
        {
            garageAt + new Vector2(16f, 18f),
            garageAt + new Vector2(42f, 18f),
            garageAt + new Vector2(118f, 0f),
        };
        _birds = new Birds { Name = "Birds", World = _world, Sfx = _sfx, Focus = _player.Position, RoofPerches = roof };
        _birds.Scattered += OnBirdsScattered;
        AddChild(_birds);   // after the car and the player: birds flying off pass in front of them

        if (!GameSession.AutoPlay) return;
        int sparrows = 0, crows = 0, buntings = 0;
        float firstEast = float.MaxValue, firstWest = float.MaxValue;
        foreach (var f in _birds.All)
        {
            if (f.Roof) continue;
            if (f.Kind == BirdKind.Crow) crows++;
            else if (f.Kind == BirdKind.Bunting) buntings++;
            else sparrows++;
            float m = (f.CenterX - TileWorld.PadCenter.X) / PixelsPerMeter;
            if (m > 0f) firstEast = Mathf.Min(firstEast, m);
            else firstWest = Mathf.Min(firstWest, -m);
        }
        GD.Print($"[birds] placed {_birds.All.Count} flocks: sparrows {sparrows}, crows {crows}, snow buntings {buntings}, garage roof {roof.Length} birds; " +
                 $"first flock {firstEast:0} m east, {firstWest:0} m west of the pad");
    }

    /// <summary>Every frame: the car (from further the faster it goes) or the walking player startles birds.</summary>
    private void UpdateBirds(Vector2 focus)
    {
        _birds.Focus = focus;
        _birds.Night = DayCycle.Night(GameSession.ClockHours);
        // About half a second of warning, so a flock is seen sitting on the road before it goes up
        // (36 km/h: 6 m; 90 km/h: 12 m).
        if (_driving) _birds.Startle(_car.CenterPosition, 140f + 0.38f * _car.Speed, "car");
        else _birds.Startle(_player.Position, 100f, "foot");

        // Gamepad horn: Y.
        bool pad = Input.IsJoyButtonPressed(0, JoyButton.Y);
        if (pad && !_padHornDown && _driving) Honk();
        _padHornDown = pad;
    }

    private void Honk()
    {
        _honks++;
        _sfx.Play("horn", -11f, 0.25f, Sfx.EnginePitch(_car.EngineSound));
        int before = _birds.Scatters;
        _birds.Startle(_car.CenterPosition, HornReach, "horn");
        _bats.Startle(_car.CenterPosition, HornReach, "horn", false);
        if (GameSession.AutoPlay)
            GD.Print($"[birds] honk #{_honks} at x={_car.CenterPosition.X / Ts:0} speed={_car.Speed / PixelsPerMeter * 3.6f:0}km/h scattered {_birds.Scatters - before} flock(s) frame={Engine.GetFramesDrawn()}");
    }

    private void OnBirdsScattered(Flock f, string cause, float distance)
    {
        _scatterCauses.TryGetValue(cause, out int n);
        _scatterCauses[cause] = n + 1;
        if (!GameSession.AutoPlay) return;
        GD.Print($"[birds] scatter: {(f.Roof ? "garage roof " : "")}{Birds.KindName(f.Kind)} x{f.Birds.Count} (flock {f.Index}, {(f.CenterX - TileWorld.PadCenter.X) / PixelsPerMeter:0} m from the pad) " +
                 $"cause={cause} nearest={distance / PixelsPerMeter:0.0}m {(_driving ? $"car {_car.Speed / PixelsPerMeter * 3.6f:0}km/h" : "on foot")} " +
                 $"night={DayCycle.Night(GameSession.ClockHours):0.00} frame={Engine.GetFramesDrawn()}");
    }

    /// <summary>Bots: honk once at the first flock seen 7–10 m ahead, so the horn is tested.</summary>
    private void RunHornBot()
    {
        if (_botHonked || !_driving || _elapsed - _enteredAt < 1f) return;
        int dir = GameSession.DriveLeft ? -1 : 1;
        Flock f = _birds.PerchedAhead(_car.CenterPosition, dir, 10f * PixelsPerMeter);
        if (f == null || Mathf.Abs(f.CenterX - _car.CenterPosition.X) < 7f * PixelsPerMeter) return;
        _botHonked = true;
        Honk();
    }

    private string BirdSummary()
    {
        var causes = new List<string>();
        foreach (var (k, v) in _scatterCauses) causes.Add($"{k}:{v}");
        return $"birds={_birds.Scatters} ({string.Join(",", causes)}) inAir={_birds.InAir} mostInAir={_birds.MostInAir} honks={_honks}";
    }
}
