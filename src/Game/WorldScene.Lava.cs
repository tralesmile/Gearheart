using System.Collections.Generic;
using Gearheart.Core;
using Gearheart.World;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// The magma sea at the bottom of the world (see <see cref="Lava"/>): it glows, bubbles and rumbles.
/// On foot, falling in spits you back out towards the nearest shore with a sizzle, and every dip burns
/// one of your best ores. The car floats in it, half sunk, cooking (damage) until you drive out. The
/// ore just above it is rich: one tile in four is a lucky strike (see <see cref="LuckyOre"/>).
/// </summary>
public partial class WorldScene
{
    private LavaView _lavaView;
    private float _scorchCooldown, _carLavaTimer, _carSizzle, _carLavaLog;
    private bool _lavaHinted, _carWasInLava;
    private int _scorches, _orebBurnt, _carDips, _carLavaLines;
    private float _lavaLevel, _peakLava;
    private float _scorchLandWatch = -1f;
    private Vector2 _scorchFrom;
    private float _lastBurnAt = -99f;
    private int _crustX = int.MinValue, _crusts;
    private float _crustAt;

    // Bot (--lavatest)
    private bool _lavaPlaced, _lavaCarDropped, _lavaWalkBack, _lavaDone;
    private int _lavaLakeFrom, _lavaLakeTo, _lavaCarLakeFrom, _lavaCarLakeTo, _lavaCarDropX = -1;
    private float _lavaPlacedAt, _lavaCarAt;

    private static readonly string[] BurnOrder = { "Crystal", "Copper", "Iron", "Scrap" };
    private const float BurnGap = 3f;      // s between burnt ores
    private const int CrustWidth = 3;      // a pool this narrow (a shaft you dug) crusts over after a dip

    private void SetupLava(CanvasLayer glintLayer)
    {
        _lavaView = new LavaView { Name = "Lava", World = _world, Focus = _player.Position };
        glintLayer.AddChild(_lavaView);
        if (!GameSession.AutoPlay) return;
        int mid = (TileWorld.PadFrom + TileWorld.PadTo) / 2;
        var lakes = Lava.Lakes(_world);
        int near = 0, widest = 0;
        float nearest = float.MaxValue;
        foreach (var (from, to) in lakes)
        {
            widest = Mathf.Max(widest, to - from + 1);
            float d = Mathf.Min(Mathf.Abs(from - mid), Mathf.Abs(to - mid)) * 0.25f;
            nearest = Mathf.Min(nearest, d);
            if (d <= 100f) near++;
        }
        int lucky = 0, richOre = 0;
        for (int y = Lava.Row - Lava.RichBand; y < TileWorld.Height - 2; y++)
            for (int x = 0; x < TileWorld.Width; x++)
            {
                Tile t = _world.Get(x, y);
                if (!LuckyOre.IsOreTile(t)) continue;
                richOre++;
                if (LuckyOre.IsLucky(t, x, y)) lucky++;
            }
        GD.Print($"[lava] sea: line at row {Lava.Row} ({(Lava.Row - TileWorld.BaseSurface) * 0.25f:0.0} m under the pad), {Lava.Count(_world)} lava tiles, " +
                 $"{lakes.Count} lakes open to the air (widest {widest * 0.25f:0.0} m, {near} within 100 m of the garage, nearest {nearest:0} m); " +
                 $"ore in the hot band: {richOre} tiles, {lucky} lucky ({(richOre > 0 ? 100f * lucky / richOre : 0f):0}%)");
    }

    /// <summary>Physics tick: the player in lava, the car in lava, and the first sight of it.</summary>
    private void UpdateLava(float dt)
    {
        _scorchCooldown -= dt;
        if (_crustX != int.MinValue && _elapsed >= _crustAt && _player.Position.Y + Player.HalfHeight < Lava.Row * Ts - 2f) Crust();
        if (!_driving && _player.Active)
        {
            Vector2 feet = _player.Position + new Vector2(0f, Player.HalfHeight - 4f);
            bool inLava = Lava.AtPoint(_world, feet) || Lava.AtPoint(_world, _player.Position);
            if (inLava)
            {
                // Lava is dense: it floats you at the surface rather than letting you sink, so each scorch
                // launches from the top and clears the lip. (The throw itself sets a stronger upward speed.)
                _player.Velocity.Y = Mathf.Min(_player.Velocity.Y, -260f);
                if (_scorchCooldown <= 0f) Scorch();
            }
            if (_scorchLandWatch >= 0f && GameSession.AutoPlay)
            {
                _scorchLandWatch += dt;
                if (_player.Grounded && !inLava && _scorchLandWatch > 0.1f)
                {
                    int tx = Mathf.FloorToInt(_player.Position.X / Ts);
                    GD.Print($"[lava] landed after {_scorchLandWatch:0.00}s, {(_player.Position.X - _scorchFrom.X) / PixelsPerMeter:+0.0;-0.0} m sideways, " +
                             $"{(_scorchFrom.Y - _player.Position.Y) / PixelsPerMeter:0.0} m above the dip, on {(Lava.At(_world, tx, Mathf.FloorToInt((_player.Position.Y + Player.HalfHeight + 4f) / Ts)) ? "LAVA" : "rock")} " +
                             $"tile ({tx},{Mathf.FloorToInt(_player.Position.Y / Ts)}) frame={Engine.GetFramesDrawn()}");
                    _scorchLandWatch = -1f;
                }
                else if (_scorchLandWatch > 4f) _scorchLandWatch = -1f;
            }
        }

        // The car: steam, a sizzle now and then, and a call-out the first time it goes in.
        bool carIn = _car.InLava;
        if (carIn)
        {
            _carLavaTimer += dt;
            _carSizzle -= dt;
            if (!_carWasInLava)
            {
                _carDips++;
                _lavaView.Splash(new Vector2(_car.CenterPosition.X, Lava.Row * Ts), Mathf.Clamp(_car.CenterVelocity.Length() / 500f, 0.4f, 1f));
                _sfx.Play("sizzle", -4f, 0.2f, 0.85f);
                _carSizzle = 0.9f;
                if (_driving && _toastTime < 1.2f) Toast("THE CAR IS COOKING!\ndrive out of the lava");
                if (GameSession.AutoPlay)
                    GD.Print($"[lava] car in: dip {_carDips} at {_car.CenterVelocity.Length() / PixelsPerMeter * 3.6f:0} km/h, tile ({_car.CenterPosition.X / Ts:0},{_car.CenterPosition.Y / Ts:0}) " +
                             $"damage {_car.Damage:0.0} frame={Engine.GetFramesDrawn()}");
            }
            if (_carSizzle <= 0f)
            {
                _carSizzle = 0.8f;
                _sfx.Play("sizzle", -12f, 0.3f, 0.7f);
            }
            if (((int)(_carLavaTimer * 8f) & 1) == 0) _lavaView.Smoke(new Vector2(_car.CenterPosition.X + (float)GD.RandRange(-40, 40), Lava.Row * Ts + 2f));
            if (GameSession.AutoPlay && _carLavaTimer - _carLavaLog >= 0.5f && _carLavaLines < 14)
            {
                _carLavaLog = _carLavaTimer;
                _carLavaLines++;
                GD.Print($"[lava] car t={_carLavaTimer:0.0}s share={_car.LavaShare:0.00} centre {(_car.CenterPosition.Y - Lava.Row * Ts) / PixelsPerMeter:+0.00;-0.00} m below the line " +
                         $"vy={_car.CenterVelocity.Y / PixelsPerMeter:0.0} m/s pitch={_car.PitchDegrees():0} damage={_car.Damage:0.0} (lava {_car.LavaDamage:0.0}) x={_car.CenterPosition.X / Ts:0.0} frame={Engine.GetFramesDrawn()}");
            }
        }
        else if (_carWasInLava && GameSession.AutoPlay)
            GD.Print($"[lava] car out after {_carLavaTimer:0.0}s in lava, damage {_car.Damage:0.0} (lava {_car.LavaDamage:0.0}) x={_car.CenterPosition.X / Ts:0.0} frame={Engine.GetFramesDrawn()}");
        if (!carIn) _carLavaTimer = _carLavaLog = 0f;
        _carWasInLava = carIn;

        // First sight: a title card, like the Deep's.
        Vector2 focus = Focus;
        if (!_lavaHinted && _lavaView.SurfaceShown > 0 && Mathf.Abs(focus.Y / Ts - Lava.Row) < 14f && _toastTime < 1.2f && !_card.Showing)
        {
            _lavaHinted = true;
            _toast.Text = "MAGMA SEA\nit burns, but the rock above it is rich";
            _toastTime = 3.5f;
            _smashToast = _goalToast = false;
            _sfx.Play("record", -9f, 0.5f, 0.6f);
            if (GameSession.AutoPlay) GD.Print($"[lava] first sight: {_lavaView.SurfaceShown} surface tiles on screen, focus {(Lava.Row - focus.Y / Ts) * 0.25f:0.0} m above the line frame={Engine.GetFramesDrawn()}");
        }
    }

    /// <summary>On foot in lava: thrown up and towards the nearest shore, one ore burnt, steam and a hiss.</summary>
    private void Scorch()
    {
        _scorches++;
        _scorchCooldown = 0.45f;
        Vector2 p = _player.Position;
        int tx = Mathf.FloorToInt(p.X / Ts);
        // The nearest column where the lake ends (rock at the line), left or right.
        int shoreDir = 0, shoreDist = 0;
        for (int d = 1; d <= 24 && shoreDir == 0; d++)
        {
            if (!Lava.At(_world, tx + d, Lava.Row)) { shoreDir = 1; shoreDist = d; }
            else if (!Lava.At(_world, tx - d, Lava.Row)) { shoreDir = -1; shoreDist = d; }
        }
        if (shoreDir == 0) shoreDir = _player.Velocity.X < 0f ? -1 : 1;
        // A low, shore-ward hop: up just enough to clear the lip (about 1.1 m), strong sideways so you skip
        // onto the shore instead of launching into the cavern ceiling and dropping straight back in. A wide
        // lake takes several hops (an ore is lost at most every 3 s), each one landing you nearer the edge.
        float up = 380f;                                                  // about 1.5 m, clears the lip
        float flight = 2f * up / 1250f;                                   // Player.Gravity; time to fall back to this height
        float vx = Mathf.Clamp((shoreDist * Ts + 20f) / flight, 260f, 620f) * shoreDir;
        // A pool only a few tiles wide (usually your own shaft into the magma) crusts over once you are out,
        // so you cannot bounce in it forever.
        int width = LakeWidthAt(tx);
        if (width > 0 && width <= CrustWidth)
        {
            _crustX = tx;
            _crustAt = _elapsed + 0.3f;
        }
        _player.Velocity = new Vector2(vx, -up);
        _player.Stagger = flight + 0.3f;
        _scorchFrom = p;
        _scorchLandWatch = 0f;

        string burnt = null;
        // One ore per real dip: falling straight back in within 3 s costs nothing more.
        if (_elapsed - _lastBurnAt >= BurnGap)
        foreach (string item in BurnOrder)
        {
            if (GameSession.Count(item) <= 0) continue;
            GameSession.Add(item, -1);
            burnt = item;
            _orebBurnt++;
            _lastBurnAt = _elapsed;
            break;
        }
        var at = new Vector2(p.X, Lava.Row * Ts);
        _lavaView.Splash(at, 0.7f);
        _sfx.Play("sizzle", -3f, 0.15f);
        _pops.Show(p - new Vector2(0f, 46f), _scorches == 1 ? "HOT HOT HOT!" : "HOT!", new Color(1f, 0.55f, 0.15f), 16f, 1.3f, 40f);
        if (burnt != null) _pops.Show(p - new Vector2(0f, 28f), $"-1 {burnt} (burnt)", new Color(1f, 0.4f, 0.3f), 12f, 1.5f, 34f);
        if (_toastTime < 1.2f && _scorches == 1) Toast("LAVA!\nevery dip burns one of your ores");
        if (GameSession.AutoPlay)
            GD.Print($"[lava] scorch #{_scorches}: at tile ({tx},{Mathf.FloorToInt(p.Y / Ts)}) burnt={burnt ?? "nothing"} left: crystal={GameSession.Count("Crystal")} copper={GameSession.Count("Copper")} " +
                     $"iron={GameSession.Count("Iron")} scrap={GameSession.Count("Scrap")}; shore {(shoreDir > 0 ? "east" : "west")} {shoreDist * 0.25f:0.0} m, thrown vx={vx / PixelsPerMeter:0.0} m/s up={up / PixelsPerMeter:0.0} m/s frame={Engine.GetFramesDrawn()}");
    }

    /// <summary>Width in tiles of the lake top through column x (0 if x is not over open lava at the line).</summary>
    private int LakeWidthAt(int x)
    {
        if (!Lava.At(_world, x, Lava.Row)) return 0;
        int from = x, to = x;
        while (from - x < 40 && Lava.At(_world, from - 1, Lava.Row)) from--;
        while (to - x < 40 && Lava.At(_world, to + 1, Lava.Row)) to++;
        return to - from + 1;
    }

    /// <summary>The narrow pool the player just left cools: its top two rows of lava turn to slate.</summary>
    private void Crust()
    {
        int x0 = _crustX, n = 0;
        _crustX = int.MinValue;
        int w = LakeWidthAt(x0);
        if (w == 0 || w > CrustWidth) return;
        int from = x0;
        while (from > x0 - CrustWidth && Lava.At(_world, from - 1, Lava.Row)) from--;
        for (int x = from; x < from + CrustWidth; x++)
            for (int y = Lava.Row; y <= Lava.Row + 1; y++)
                if (Lava.At(_world, x, y))
                {
                    _world.Set(x, y, Tile.Slate);
                    n++;
                }
        if (n == 0) return;
        _crusts++;
        var at = new Vector2((x0 + 0.5f) * Ts, Lava.Row * Ts);
        _lavaView.Splash(at, 0.3f);
        _sfx.Play("sizzle", -10f, 0.1f, 0.6f);
        _pops.Show(at - new Vector2(0f, 20f), "it crusts over", new Color(0.9f, 0.6f, 0.45f), 11f, 1.4f, 30f);
        if (GameSession.AutoPlay) GD.Print($"[lava] crust: {n} lava tiles at column {x0} cooled to slate (pool {CrustWidth} or fewer wide) frame={Engine.GetFramesDrawn()}");
    }

    /// <summary>Every frame: the view follows the focus and the rumble follows how close the lake is.</summary>
    private void UpdateLavaView(Vector2 focus, float dt)
    {
        _lavaView.Focus = focus;
        int fx = Mathf.FloorToInt(focus.X / Ts);
        float fy = focus.Y / Ts;
        float best = float.MaxValue;
        if (fy > Lava.Row - 30)
            for (int dx = -26; dx <= 26; dx++)
                if (Lava.IsSurface(_world, fx + dx, Lava.Row))
                {
                    best = Mathf.Min(best, new Vector2(dx, fy - Lava.Row).Length());
                    if (best < 2f) break;
                }
        float target = best == float.MaxValue ? 0f : Mathf.Clamp(1f - (best - 3f) / 22f, 0f, 1f);
        _lavaLevel = Mathf.MoveToward(_lavaLevel, target, dt * 1.5f);
        _peakLava = Mathf.Max(_peakLava, _lavaLevel);
        _sfx.UpdateLava(_lavaLevel);
    }

    private string LavaSummary() =>
        $"lava: scorches={_scorches} oreBurnt={_orebBurnt} crusts={_crusts} carDips={_carDips} carLavaDamage={_car.LavaDamage:0.0} rumble={_peakLava:0.00} hinted={_lavaHinted}";

    // ---- Test bot (--lavatest) ------------------------------------------------------------------

    /// <summary>
    /// --lavatest: drop the mechanic (carrying 2 Copper and 3 Iron) over the lava lake nearest the garage and
    /// see him thrown out; after 4 s walk him back in for a second dip; after 8 s drop the car into the widest
    /// lake near the garage, let it float for 3 s, then floor it to see whether it can drive out.
    /// </summary>
    private void RunLavaBot()
    {
        if (_lavaDone) return;
        int mid = (TileWorld.PadFrom + TileWorld.PadTo) / 2;
        if (!_lavaPlaced)
        {
            if (_elapsed < 0.3f) return;
            _lavaPlaced = true;
            var lakes = Lava.Lakes(_world, mid - 600, mid + 600);
            lakes.Sort((a, b) => Mathf.Abs((a.From + a.To) / 2 - mid).CompareTo(Mathf.Abs((b.From + b.To) / 2 - mid)));
            Vector2? spot = null;
            foreach (var (from, to) in lakes)
            {
                if (to - from + 1 < 3) continue;
                spot = SpotAbove((from + to) / 2, 3);
                if (spot == null) continue;
                (_lavaLakeFrom, _lavaLakeTo) = (from, to);
                break;
            }
            // The car's lake: the widest lake that has a clear drop column (11 tiles wide, 8 rows tall) over
            // open lava, anywhere within it (a wide lake can have rock pillars poking up, so scan for a gap).
            int bestW = 0;
            foreach (var (from, to) in lakes)
            {
                if (to - from + 1 <= bestW) continue;
                int drop = CarDropColumn(from, to);
                if (drop < 0) continue;
                bestW = to - from + 1;
                (_lavaCarLakeFrom, _lavaCarLakeTo, _lavaCarDropX) = (from, to, drop);
            }
            if (spot is not Vector2 s)
            {
                GD.Print($"[lava] test: NO lake of {lakes.Count} within 150 m has room above it");
                _lavaDone = true;
                return;
            }
            GameSession.Add("Copper", 2);
            GameSession.Add("Iron", 3);
            PutPlayer(s);
            _lavaPlacedAt = _elapsed;
            GD.Print($"[lava] test: player dropped {(Lava.Row * Ts - s.Y - Player.HalfHeight) / PixelsPerMeter:0.0} m above a lake {(_lavaLakeTo - _lavaLakeFrom + 1) * 0.25f:0.0} m wide " +
                     $"(tiles {_lavaLakeFrom}-{_lavaLakeTo}, {((_lavaLakeFrom + _lavaLakeTo) / 2 - mid) * 0.25f:0} m from the garage) carrying copper={GameSession.Count("Copper")} iron={GameSession.Count("Iron")}; " +
                     $"car lake {(_lavaCarLakeTo - _lavaCarLakeFrom + 1) * 0.25f:0.0} m wide at tiles {_lavaCarLakeFrom}-{_lavaCarLakeTo} frame={Engine.GetFramesDrawn()}");
            return;
        }

        float t = _elapsed - _lavaPlacedAt;
        if (!_lavaCarDropped)
        {
            // Walk back into the lake once, 4 s in, then stand still.
            float lakeMid = (_lavaLakeFrom + _lavaLakeTo + 1) * 0.5f * Ts;
            if (t > 4f && _scorches < 2 && !_lavaWalkBack)
            {
                _lavaWalkBack = true;
                GD.Print($"[lava] test: walking back into the lake ({(lakeMid - _player.Position.X) / PixelsPerMeter:+0.0;-0.0} m) frame={Engine.GetFramesDrawn()}");
            }
            bool walk = _lavaWalkBack && _scorches < 2 && t < 7.5f;
            float dir = Mathf.Sign(lakeMid - _player.Position.X);
            _player.Override = new PlayerInput
            {
                Move = walk ? dir : 0f,
                Jump = walk && _player.Grounded && Mathf.Abs(_player.Velocity.X) < 5f,
                Aim = _player.Position + new Vector2(dir * 40f, 0f),
            };
            if (t < 8f || _lavaCarDropX < 0) return;
            _lavaCarDropped = true;
            int cx = _lavaCarDropX;
            var at = new Vector2((cx + 0.5f) * Ts, (Lava.Row - 5) * Ts);
            SpawnCar(at: at);
            EnterCar();
            _car.DrillEnabled = false;
            _car.AutoThrottle = 0f;
            _lavaCarAt = _elapsed;
            _camera.Position = at;
            _airCam.Snap(at);
            GD.Print($"[lava] test: car dropped 1.5 m over the middle of a {(_lavaCarLakeTo - _lavaCarLakeFrom + 1) * 0.25f:0.0} m lake (tile {cx}), damage {_car.Damage:0.0} frame={Engine.GetFramesDrawn()}");
            return;
        }

        float ct = _elapsed - _lavaCarAt;
        if (ct > 3f && _car.AutoThrottle == 0f)
        {
            // Floor it towards the nearer shore.
            int cx = Mathf.FloorToInt(_car.CenterPosition.X / Ts);
            float dir = cx - _lavaCarLakeFrom < _lavaCarLakeTo - cx ? -1f : 1f;
            _car.AutoThrottle = dir;
            GD.Print($"[lava] test: throttle {(dir > 0 ? "east" : "west")} towards the nearer shore, damage {_car.Damage:0.0} share={_car.LavaShare:0.00} frame={Engine.GetFramesDrawn()}");
        }
        if (ct > 9f)
        {
            _lavaDone = true;
            GD.Print($"[lava] test: done. {LavaSummary()} car in lava now={_car.InLava} damage={_car.Damage:0.0} frame={Engine.GetFramesDrawn()}");
        }
    }

    /// <summary>
    /// A column inside [from,to] where a car can be dropped: 11 tiles of open air (5 each side), 8 rows tall
    /// above the line, and open lava below it. Returns the column, or -1 if the lake has no such gap.
    /// </summary>
    private int CarDropColumn(int from, int to)
    {
        // Prefer a roomy gap (9 wide, 6 tall); fall back to a tighter one so a lake is rarely rejected.
        foreach (var (halfW, rows) in new[] { (4, 6), (3, 5) })
            for (int x = from + halfW; x <= to - halfW; x++)
            {
                if (!Lava.At(_world, x, Lava.Row)) continue;
                bool clear = true;
                for (int y = Lava.Row - rows; y < Lava.Row && clear; y++)
                    for (int dx = -halfW; dx <= halfW; dx++)
                        if (_world.Solid(x + dx, y)) { clear = false; break; }
                if (clear) return x;
            }
        return -1;
    }

    /// <summary>A free spot for the player <paramref name="rows"/> or more rows above the lava line at column x (or null).</summary>
    private Vector2? SpotAbove(int x, int rows)
    {
        for (int k = rows; k <= rows + 8; k++)
        {
            var s = new Vector2((x + 0.5f) * Ts, (Lava.Row - k) * Ts - Player.HalfHeight);
            if (!_player.Blocked(s.X, s.Y) && !_player.Blocked(s.X, s.Y + (k - 1) * Ts * 0.5f)) return s;
        }
        return null;
    }
}
