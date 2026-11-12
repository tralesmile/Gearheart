using System.Collections.Generic;
using Gearheart.Core;
using Gearheart.World;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// Vein runs: ore mined close together and quickly chains into "VEIN x2, x3 ..." with a tink that
/// climbs a scale, every 4th ore of a run pays one extra, and each broken ore sets the rest of its
/// vein sparkling so you can follow it. Also the --veintest bot.
/// </summary>
public partial class WorldScene
{
    private readonly VeinRun _vein = new();
    private MiningPops _pops;
    private bool _veinHinted;
    private int _veinRipples;

    // --veintest
    private bool _veinPlanted, _veinLonePlanted;
    private int _veinX, _veinY;
    private float _veinDoneAt = -1f;
    private int _veinTestRuns;

    // A major scale, so a long run climbs like a xylophone (semitones above the first note).
    private static readonly int[] VeinScale = { 0, 2, 4, 5, 7, 9, 11, 12, 14, 16, 17, 19 };

    private void SetupVeins(CanvasLayer glintLayer)
    {
        _pops = new MiningPops { Name = "MiningPops" };
        glintLayer.AddChild(_pops);
    }

    private static bool IsOre(Tile t) => t is Tile.ScrapOre or Tile.IronOre or Tile.CopperOre or Tile.Crystal;

    /// <summary>
    /// An ore tile broke (by pickaxe or drill): count it into the run, pop its amount, ring the rest of
    /// the vein. Returns the bonus amount to add (0 or 1).
    /// </summary>
    private int OnOreMined(int x, int y, Tile tile, string item, int amount)
    {
        var (chain, bonus) = _vein.Add(new Vector2I(x, y));
        var at = new Vector2((x + 0.5f) * Ts, (y + 0.5f) * Ts);
        Color color = TileColor(tile).Lightened(0.25f);

        _pops.Show(at, $"+{amount} {item}", color, 10f, 1.0f, 22f);
        if (chain >= 2)
        {
            float step = VeinScale[Mathf.Min(chain - 2, VeinScale.Length - 1)];
            _sfx?.Play("vein", -9f + Mathf.Min(chain, 6) * 0.5f, 0.02f, Mathf.Pow(2f, step / 12f));
            _pops.Show(at - new Vector2(0f, 13f), $"VEIN x{chain}", new Color(1f, 0.95f, 0.75f), 12f + Mathf.Min(chain, 8) * 0.6f, 1.2f, 30f);
        }
        if (bonus)
        {
            _sfx?.Play("record", -16f, 0.3f, 1.6f);
            _pops.Show(at - new Vector2(0f, 28f), $"+1 BONUS {item}", new Color(1f, 0.83f, 0.3f), 12f, 1.5f, 34f);
        }
        if (!_veinHinted && chain >= 2 && _toastTime < 1.2f && !_card.Showing && !_driving)
        {
            _veinHinted = true;
            Toast("VEIN RUN!\nkeep mining the ore next to it: every 4th pays a bonus");
        }

        // The rest of the vein sparkles, nearest first, so you see which way it runs.
        var rest = VeinTiles(x, y, 6, 24);
        if (rest.Count > 0)
        {
            _pops.Ripple(rest, color);
            _veinRipples++;
        }

        if (GameSession.AutoPlay)
            GD.Print($"[vein] {item} at ({x},{y}) chain={chain}{(bonus ? " BONUS +1" : "")} veinLeft={rest.Count} window={VeinRun.Window:0.0}s " +
                     $"{(_driving ? "drill" : "pickaxe")} {item}={GameSession.Count(item) + (bonus ? 1 : 0)} frame={Engine.GetFramesDrawn()}");
        return bonus ? 1 : 0;
    }

    /// <summary>Ore tiles connected (8 ways) to the tile at x,y, at most <paramref name="maxSteps"/> steps out, nearest first.</summary>
    private List<(Vector2I, int)> VeinTiles(int x, int y, int maxSteps, int max)
    {
        var found = new List<(Vector2I, int)>();
        var seen = new HashSet<Vector2I> { new(x, y) };
        var queue = new Queue<(Vector2I, int)>();
        queue.Enqueue((new Vector2I(x, y), 0));
        while (queue.Count > 0 && found.Count < max)
        {
            var (p, step) = queue.Dequeue();
            if (step >= maxSteps) continue;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var n = new Vector2I(p.X + dx, p.Y + dy);
                    if ((dx == 0 && dy == 0) || !seen.Add(n) || !IsOre(_world.Get(n.X, n.Y))) continue;
                    found.Add((n, step + 1));
                    queue.Enqueue((n, step + 1));
                    if (found.Count >= max) break;
                }
        }
        return found;
    }

    private void UpdateVeins(float dt)
    {
        _pops.TextScale = FootZoom / Mathf.Max(_camera.Zoom.X, 0.1f);
        _pops.Zoom = _camera.Zoom.X;
        int ended = _vein.Tick(dt);
        if (ended >= 3 && GameSession.AutoPlay)
            GD.Print($"[vein] run over: x{ended} (best {_vein.Best}, bonuses {_vein.Bonuses}) frame={Engine.GetFramesDrawn()}");
    }

    private string VeinSummary() =>
        $"vein={_vein.Chain} bestVein={_vein.Best} veinBonuses={_vein.Bonuses} veinRuns={_vein.Runs} ripples={_veinRipples} pops={_pops?.PopsShown ?? 0}";

    /// <summary>
    /// --veintest: stand the mechanic on open ground east of the pad, bury a seven-tile iron vein
    /// running down and to the right under their feet, and mine ore after ore (nearest first). Then,
    /// after the run has gone cold, a lone scrap tile next to them: it must start a new run at x1.
    /// </summary>
    private void RunVeinBot()
    {
        if (!_veinPlanted)
        {
            if (_elapsed < 0.3f) return;
            _veinPlanted = true;
            _veinX = TileWorld.PadTo + 14;
            _veinY = _world.SurfaceRow(_veinX);
            var at = new Vector2((_veinX + 0.5f) * Ts, _veinY * Ts - Player.HalfHeight - 0.5f);
            _player.Position = at;
            _player.Velocity = Vector2.Zero;
            _camera.Position = at;
            _airCam.Snap(at);
            var shape = new[] { (0, 1), (1, 1), (1, 2), (2, 2), (2, 3), (3, 3), (3, 4) };
            foreach (var (dx, dy) in shape) _world.Set(_veinX + dx, _veinY + dy, Tile.IronOre);
            GD.Print($"[vein] test: buried a {shape.Length}-tile iron vein at ({_veinX},{_veinY + 1})..({_veinX + 3},{_veinY + 4}), " +
                     $"Iron={GameSession.Count("Iron")} frame={Engine.GetFramesDrawn()}");
            return;
        }

        Vector2 me = _player.Position;
        Vector2I? ore = NearestOre(me, 88f);
        if (ore is Vector2I t)
        {
            _player.Override = new PlayerInput { Mine = true, Aim = new Vector2((t.X + 0.5f) * Ts, (t.Y + 0.5f) * Ts) };
        }
        else
        {
            _player.Override = new PlayerInput();
            if (_veinDoneAt < 0f)
            {
                _veinDoneAt = _elapsed;
                _veinTestRuns = 1;
                GD.Print($"[vein] test: vein dug out at t={_elapsed:0.0}s chain={_vein.Chain} best={_vein.Best} bonuses={_vein.Bonuses} Iron={GameSession.Count("Iron")} frame={Engine.GetFramesDrawn()}");
            }
            else if (!_veinLonePlanted && _elapsed > _veinDoneAt + VeinRun.Window + 1f)
            {
                _veinLonePlanted = true;
                int fx = Mathf.FloorToInt(me.X / Ts), fy = Mathf.FloorToInt((me.Y + Player.HalfHeight + 2f) / Ts);
                _world.Set(fx + 1, fy, Tile.ScrapOre);
                GD.Print($"[vein] test: the run went cold (chain={_vein.Chain}); a lone scrap tile at ({fx + 1},{fy}) frame={Engine.GetFramesDrawn()}");
            }
            else if (_veinLonePlanted && _veinTestRuns == 1)
            {
                _veinTestRuns = 2;
                GD.Print($"[vein] test: done. best={_vein.Best} bonuses={_vein.Bonuses} runs={_vein.Runs} Iron={GameSession.Count("Iron")} Scrap={GameSession.Count("Scrap")} " +
                         $"ripples={_veinRipples} pops={_pops.PopsShown} sparkles={_pops.SparklesShown} frame={Engine.GetFramesDrawn()}");
            }
        }

        if (_elapsed >= _nextLog)
            GD.Print($"[vein] t={_elapsed:0}s player=({me.X / Ts:0.0},{me.Y / Ts:0.0}) mining={_player.IsMining} {VeinSummary()} activePops={_pops.Active} frame={Engine.GetFramesDrawn()}");
    }

    /// <summary>The closest ore tile within a distance (pixels) of a point, if any.</summary>
    private Vector2I? NearestOre(Vector2 from, float within)
    {
        int r = Mathf.CeilToInt(within / Ts) + 1;
        int cx = Mathf.FloorToInt(from.X / Ts), cy = Mathf.FloorToInt(from.Y / Ts);
        Vector2I? best = null;
        float bestD = within;
        for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (!IsOre(_world.Get(x, y))) continue;
                float d = new Vector2((x + 0.5f) * Ts, (y + 0.5f) * Ts).DistanceTo(from);
                if (d <= bestD)
                {
                    bestD = d;
                    best = new Vector2I(x, y);
                }
            }
        return best;
    }
}
