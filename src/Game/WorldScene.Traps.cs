using System.Collections.Generic;
using Gearheart.Core;
using Gearheart.World;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// Speed traps: camera posts on flat stretches of road (<see cref="SpeedTrapPosts"/>). Driving past one
/// fires the camera — a white flash, a shutter click and a beep — and reads your speed over the car
/// ("SPEED TRAP 74 km/h"); beat the best on the post's sign for NEW BEST with the record chime.
/// Pure feel and a target on the flat stretches between kickers: no loot, no score on the trip report yet.
/// </summary>
public partial class WorldScene
{
    /// <summary>A pass slower than this is not a reading (rolling back and forth at the post).</summary>
    private const float TrapMinKmh = 5f;

    private SpeedTrapPosts _traps;
    private ColorRect _flashRect;
    private float _flashLeft;
    private float _trapPrevX = float.NaN;
    private readonly Dictionary<int, float> _trapLastAt = new();
    private int _trapHits, _trapRecords, _trapFirsts;
    private float _trapTop;

    private static readonly Color TrapWhite = new(0.95f, 0.97f, 1f), TrapGold = new(1f, 0.82f, 0.25f), TrapHot = new(1f, 0.45f, 0.2f);

    private void SetupTraps()
    {
        _traps = new SpeedTrapPosts
        {
            Name = "SpeedTraps", World = _world, Focus = TileWorld.PadCenter,
            Best = t => GameSession.TrapBest(t.Column),
        };
        AddChild(_traps);
        if (GameSession.TrapTest)
        {
            // Test: a slow best on the first trap east (to beat) and an unbeatable one on the second (to miss).
            foreach (SpeedTrap t in _traps.Traps)
                if (t.Side > 0 && t.Number <= 2) GameSession.PlantTrapBest(t.Column, t.Number == 1 ? 12f : 250f);
        }
        if (!GameSession.AutoPlay) return;
        int center = (TileWorld.PadFrom + TileWorld.PadTo) / 2;
        foreach (SpeedTrap t in _traps.Traps)
            GD.Print($"[trap] post {t.Name} at x={t.Column} ({(t.Column - center) * 0.25f:+0;-0} m, {TileWorld.BiomeAt(t.Column)}) best={GameSession.TrapBest(t.Column):0}");
    }

    /// <summary>The white screen flash sits on the HUD layer, under the labels.</summary>
    private void SetupTrapFlash(CanvasLayer hud)
    {
        _flashRect = new ColorRect { Color = new Color(1f, 1f, 1f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        hud.AddChild(_flashRect);
    }

    /// <summary>Every physics frame: did the car's middle cross a trap's line since the last frame?</summary>
    private void UpdateTraps()
    {
        if (_traps == null) return;
        if (!_driving)
        {
            _trapPrevX = float.NaN;
            return;
        }
        float x = _car.CenterPosition.X;
        if (!float.IsNaN(_trapPrevX))
        {
            foreach (SpeedTrap t in _traps.Traps)
            {
                float line = t.LineX;
                if ((_trapPrevX < line) == (x < line)) continue;
                if (_trapLastAt.TryGetValue(t.Column, out float last) && _elapsed - last < 3f) continue;
                float kmh = _car.Speed / PixelsPerMeter * 3.6f;
                if (kmh < TrapMinKmh) continue;
                _trapLastAt[t.Column] = _elapsed;
                FireTrap(t, kmh);
            }
        }
        _trapPrevX = x;
    }

    private void FireTrap(SpeedTrap t, float kmh)
    {
        float before = GameSession.SubmitTrap(t.Column, kmh);
        bool first = before <= 0f;
        bool record = !first && kmh > before;
        _trapHits++;
        _trapTop = Mathf.Max(_trapTop, kmh);
        if (record) _trapRecords++;
        if (first) _trapFirsts++;
        _traps.Flash(t, record || first);

        _sfx.Play("flash", -7f, 0.05f);
        if (record) _sfx.Play("record", -8f, 0.1f);
        _flashLeft = 0.18f;
        _crashFx.AddTrauma(record ? 0.12f : 0.04f);

        Color color = kmh >= 80f ? TrapHot : kmh >= 50f ? TrapGold : TrapWhite;
        float top = _car.Bounds().Position.Y;
        Vector2 at = new(_car.CenterPosition.X, top - 34f);
        _pops.Show(at, $"SPEED TRAP  {kmh:0} km/h", color, 15f + Mathf.Clamp((kmh - 40f) / 20f, 0f, 3f), 1.8f, 40f);
        string second = record ? $"NEW BEST!  +{kmh - before:0} km/h" : first ? "speed set: beat it next time" : $"best {before:0} km/h";
        _pops.Show(at + new Vector2(0f, 22f), second, record ? TrapGold : new Color(0.85f, 0.88f, 0.92f), record ? 13f : 10f, 1.8f, 40f);

        if (GameSession.AutoPlay)
            GD.Print($"[trap] FLASH {t.Name} x={t.Column} at {kmh:0.0} km/h airborne={_car.Airborne} dir={(_car.CenterVelocity.X > 0 ? "east" : "west")} " +
                     $"best before {before:0.0} -> {GameSession.TrapBest(t.Column):0.0} {(record ? "NEW BEST" : first ? "first" : "no record")} frame={Engine.GetFramesDrawn()}");
    }

    /// <summary>Every drawn frame: the posts follow the camera, the screen flash fades.</summary>
    private void UpdateTrapView(float dt)
    {
        if (_traps == null) return;
        _traps.Focus = Focus;
        _flashLeft = Mathf.Max(0f, _flashLeft - dt);
        if (_flashRect != null)
        {
            _flashRect.Size = GetViewportRect().Size;
            _flashRect.Color = new Color(1f, 1f, 1f, 0.32f * _flashLeft / 0.18f);
        }
    }

    private string TrapSummary() => $"traps={_trapHits} trapRecords={_trapRecords} trapFirsts={_trapFirsts} trapTop={_trapTop:0}";
}
