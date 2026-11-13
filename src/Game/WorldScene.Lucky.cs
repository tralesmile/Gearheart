using Gearheart.Core;
using Gearheart.World;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// Lucky strikes: about one ore tile in 16 gleams gold (see <see cref="LuckyOre"/>) and pays triple
/// when it breaks, with a gold fountain, a shock ring, a jackpot jingle and a little shake. Works
/// for the pickaxe and the drill. Also the world census and the --veintest check.
/// </summary>
public partial class WorldScene
{
    private LuckyGleams _lucky;
    private bool _luckyHinted, _luckyStruckOnce, _luckyCensus;
    private int _luckyStrikes, _luckyUnits;

    // --veintest: the lone scrap tile is made lucky.
    private bool _luckyTestPlanted;

    private void SetupLucky(CanvasLayer glintLayer)
    {
        _lucky = new LuckyGleams { Name = "LuckyGleams", World = _world, Focus = _player.Position };
        glintLayer.AddChild(_lucky);
    }

    private void UpdateLucky(Vector2 focus)
    {
        if (_lucky == null) return;
        _lucky.Focus = focus;
        if (!_luckyCensus && GameSession.AutoPlay) LuckyCensus();

        // The first lucky tile in plain sight gets a hint (on foot, when nothing else is being said).
        if (!_luckyHinted && !_driving && _lucky.Nearest < 150f && _toastTime <= 0f && !_card.Showing)
        {
            _luckyHinted = true;
            Toast("Something gleams gold...\nlucky ore pays triple");
            if (GameSession.AutoPlay)
                GD.Print($"[lucky] hint: lucky {_world.Get(_lucky.NearestTile.X, _lucky.NearestTile.Y)} at ({_lucky.NearestTile.X},{_lucky.NearestTile.Y}) " +
                         $"{_lucky.Nearest / Ts * 0.25f:0.0} m away frame={Engine.GetFramesDrawn()}");
        }
    }

    /// <summary>A lucky ore tile broke: pay the extra, and make it an event.</summary>
    private void OnLuckyStrike(int x, int y, Tile tile, string item, int amount)
    {
        int extra = amount * (LuckyOre.Multiplier - 1);
        GameSession.Add(item, extra);
        Vector2 at = TileCenter(x, y);
        FlyHome(at - new Vector2(0f, 8f), item, extra);
        _luckyStrikes++;
        _luckyUnits += amount + extra;

        _pops.Show(at - new Vector2(0f, 40f), "LUCKY STRIKE!", new Color(1f, 0.84f, 0.28f), 17f, 1.7f, 40f);
        _pops.Show(at - new Vector2(0f, 24f), $"x{LuckyOre.Multiplier}  +{amount + extra} {item}", new Color(1f, 0.95f, 0.7f), 13f, 1.6f, 36f);
        _sfx?.Play("lucky", -5f, 0.02f);
        _glints.Burst(at, false);
        _lucky.Ring(at);
        _crashFx.AddTrauma(0.35f);
        _birds?.Startle(at, 260f, "lucky");

        if (!_luckyStruckOnce && _toastTime < 1.2f && !_card.Showing)
        {
            _luckyStruckOnce = true;
            Toast("LUCKY STRIKE!\ngleaming gold ore pays triple: watch for the shine");
        }

        if (GameSession.AutoPlay)
            GD.Print($"[lucky] STRIKE {tile} at ({x},{y}) by {(_driving ? "drill" : "pickaxe")}: +{amount} +{extra} extra = {amount + extra} {item} " +
                     $"(now {GameSession.Count(item)}) strikes={_luckyStrikes} frame={Engine.GetFramesDrawn()}");
    }

    /// <summary>Bot log once per run: how many lucky tiles the world has near the garage, and the closest.</summary>
    private void LuckyCensus()
    {
        _luckyCensus = true;
        int ore = 0, lucky = 0, shallow = 0;
        string nearest = "none";
        float best = float.MaxValue;
        int pad = (TileWorld.PadFrom + TileWorld.PadTo) / 2;
        for (int x = pad - 200; x <= pad + 200; x++)
        {
            int s = _world.SurfaceRow(x);
            for (int y = s; y < s + 48; y++)
            {
                Tile t = _world.Get(x, y);
                if (!LuckyOre.IsOreTile(t)) continue;
                ore++;
                if (!LuckyOre.IsLucky(t, x, y)) continue;
                lucky++;
                if (y - s <= 12) shallow++;
                float d = Mathf.Abs(x - pad) + (y - s) * 2f;
                if (d < best)
                {
                    best = d;
                    nearest = $"{t} at ({x},{y}), {(x - pad) * 0.25f:0.0} m from the pad, {(y - s) * 0.25f:0.0} m down";
                }
            }
        }
        GD.Print($"[lucky] world within 50 m of the garage: {ore} ore tiles, {lucky} lucky ({(ore > 0 ? 100f * lucky / ore : 0):0.0}%), " +
                 $"{shallow} of them in the top 3 m; nearest: {nearest}");
    }

    private string LuckySummary() => $"lucky={_luckyStrikes} luckyUnits={_luckyUnits} gleams={_lucky?.Shown ?? 0}";

    /// <summary>--veintest: keep the buried test vein ordinary, so its numbers stay as they were.</summary>
    private static void SuppressLucky(int x, int y) => LuckyOre.Suppressed.Add((x, y));

    /// <summary>--veintest: the lone scrap tile is lucky.</summary>
    private void PlantLuckyTest(int x, int y)
    {
        _luckyTestPlanted = true;
        LuckyOre.Forced.Add((x, y));
        GD.Print($"[lucky] test: the lone scrap tile at ({x},{y}) is lucky; Scrap={GameSession.Count("Scrap")} frame={Engine.GetFramesDrawn()}");
    }
}
