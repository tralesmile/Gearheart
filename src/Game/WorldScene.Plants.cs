using Gearheart.Core;
using Gearheart.World;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// Roadside plants the car runs over: bushes, flowers and grass tufts in the meadow, cacti in The Wastes
/// and snowy shrubs in the Frozen North burst into leaves, petals, cactus chunks or snow, with a rustle,
/// and leave a stump or stubble that grows back later (see <see cref="Flora"/>). A cactus thumps the car
/// a little. Plants flattened close together chain into a MOWER count, popped at x3, x5, x10 and every 10th. Pure feel: no loot, no score.
/// </summary>
public partial class WorldScene
{
    /// <summary>Seconds between two plants for the mower chain to go on.</summary>
    private const float MowWindow = 2.5f;

    /// <summary>Share of its speed the car keeps when it hits a cactus (a bush or flowers cost nothing).</summary>
    private const float CactusKeep = 0.97f;

    private int _mowChain, _bestMow, _plantsLogged, _mowPops;
    private float _lastMowAt = -10f;
    private float _regrowTick;
    private readonly int[] _flattened = new int[6];   // by PlantKind

    private static readonly Color LeafGreen = new(0.28f, 0.58f, 0.24f), LeafLight = new(0.45f, 0.72f, 0.3f);
    private static readonly Color CactusGreen = new(0.3f, 0.6f, 0.34f), CactusPulp = new(0.78f, 0.87f, 0.62f);
    private static readonly Color SnowWhite = new(0.95f, 0.97f, 1f), PineGreen = new(0.17f, 0.4f, 0.3f);
    private static readonly Color Twig = new(0.42f, 0.31f, 0.2f), MowColor = new(0.7f, 1f, 0.5f);
    private static readonly Color[] Petals =
    {
        new(0.98f, 0.86f, 0.31f), new(0.94f, 0.43f, 0.59f), new(0.98f, 0.98f, 0.98f), new(0.63f, 0.51f, 0.94f),
    };

    /// <summary>Every physics frame: the car flattens what it touches; plants grow back out of sight.</summary>
    private void UpdatePlants(float dt)
    {
        _regrowTick -= dt;
        if (_regrowTick <= 0f)
        {
            _regrowTick = 1f;
            Flora.Regrow(_world, Focus.X);
        }
        if (!_driving) return;

        float kmh = _car.Speed / PixelsPerMeter * 3.6f;
        if (kmh < 4f) return;   // a car creeping into a bush just sits in it
        Rect2 box = _car.Bounds();
        int x0 = Mathf.FloorToInt(box.Position.X / Ts) - 1, x1 = Mathf.FloorToInt(box.End.X / Ts) + 1;
        for (int tx = x0; tx <= x1; tx++)
        {
            int cell = Flora.DecorCell(_world, tx);
            PlantKind kind = Flora.KindOf(cell);
            if (kind == PlantKind.None || Flora.Flattened(_world, tx)) continue;
            Rect2 plant = Flora.Box(_world, tx, kind);
            if (!plant.Intersects(box)) continue;
            // The bounds are a box round a tilted car; make sure some part of it really is down at the plant.
            if (!TouchesPlant(plant)) continue;
            FlattenPlant(tx, kind, plant, kmh);
        }
    }

    private bool TouchesPlant(Rect2 plant)
    {
        Vector2 mid = plant.GetCenter();
        foreach (Vector2 p in new[] { mid, plant.Position, new Vector2(plant.End.X, plant.Position.Y), new Vector2(mid.X, plant.End.Y - 2f) })
            if (_car.ContainsPoint(p)) return true;
        foreach (var (c, r) in _car.WheelCircles)
        {
            Vector2 near = c.Clamp(plant.Position, plant.End);
            if (near.DistanceSquaredTo(c) <= r * r) return true;
        }
        return false;
    }

    private void FlattenPlant(int tx, PlantKind kind, Rect2 plant, float kmh)
    {
        Flora.Flatten(_world, tx);
        _flattened[(int)kind]++;
        float dir = Mathf.Sign(_car.CenterVelocity.X);
        Vector2 at = plant.GetCenter();
        // A tyre throws what it runs over up and back; the bits fly from behind the wheel where you can see them.
        float push = 140f + kmh * 4f;

        switch (kind)
        {
            case PlantKind.Bush:
                Leaves(at, new[] { LeafGreen, LeafLight, new Color(0.19f, 0.44f, 0.18f) }, 18, push, dir, 260f);
                Leaves(at, new[] { Twig, new Color(0.86f, 0.24f, 0.27f) }, 6, push * 0.8f, dir, 600f);
                _sfx.Play("rustle", -9f, 0.04f, 0.95f + 0.1f * GD.Randf());
                _crashFx.AddTrauma(0.03f);
                break;
            case PlantKind.SnowShrub:
                Leaves(at, new[] { SnowWhite, new Color(0.86f, 0.91f, 0.97f) }, 20, push, dir, 160f, 0.92f);
                Leaves(at, new[] { PineGreen, Twig }, 6, push * 0.8f, dir, 600f);
                _sfx.Play("rustle", -10f, 0.04f, 0.8f + 0.1f * GD.Randf());
                _crashFx.AddTrauma(0.03f);
                break;
            case PlantKind.Flowers:
                Leaves(at, Petals, 16, push * 0.9f, dir, 110f, 1.6f);
                Leaves(at, new[] { LeafLight }, 5, push * 0.7f, dir, 400f);
                _sfx.Play("rustle", -14f, 0.04f, 1.3f + 0.15f * GD.Randf());
                break;
            case PlantKind.Cactus:
                Leaves(at, new[] { CactusGreen, new Color(0.41f, 0.72f, 0.43f) }, 14, push * 1.1f, dir, 700f, 1f, 7f);
                Leaves(at, new[] { CactusPulp, new Color(0.94f, 0.47f, 0.63f) }, 8, push * 1.2f, dir, 500f);
                _sfx.Play("thud", -15f, 0.05f, 1.7f);
                _sfx.Play("rustle", -12f, 0.04f, 0.7f);
                _crashFx.AddTrauma(0.07f);
                if (!_car.Airborne) _car.Slow(CactusKeep);
                break;
            default:   // a grass tuft: a puff of blades, no sound (there are a lot of them)
                Leaves(at, new[] { LeafGreen, LeafLight }, 5, push * 0.6f, dir, 300f, 0.8f, 3f);
                break;
        }

        if (kind != PlantKind.Tuft)
        {
            _mowChain = _elapsed - _lastMowAt <= MowWindow ? _mowChain + 1 : 1;
            _lastMowAt = _elapsed;
            _bestMow = Mathf.Max(_bestMow, _mowChain);
            // Milestones only: the meadow has a plant every few metres, so a pop per plant was a smear of text.
            if (_mowChain is 3 or 5 or 10 || (_mowChain > 10 && _mowChain % 10 == 0))
            {
                _pops.Show(new Vector2(at.X, _car.Bounds().Position.Y - 18f), $"MOWER x{_mowChain}{(_mowChain >= 10 ? "!" : "")}", MowColor,
                           11f + Mathf.Min(_mowChain / 5, 4), 1.3f, 34f);
                _mowPops++;
            }
        }

        if (GameSession.AutoPlay && kind != PlantKind.Tuft && _plantsLogged < 40)
        {
            _plantsLogged++;
            GD.Print($"[plants] flattened {kind.ToString().ToLowerInvariant()} at x={tx} ({TileWorld.BiomeAt(tx)}) at {kmh:0} km/h chain x{_mowChain}{(kind == PlantKind.Cactus && !_car.Airborne ? $" -> {_car.Speed / PixelsPerMeter * 3.6f:0} km/h" : "")} frame={Engine.GetFramesDrawn()}");
        }
    }

    /// <summary>
    /// A one-shot spray of leaves (or petals, chunks, snow): thrown up and back off the tyre, drifting
    /// down slowly with drag, each bit picking one of <paramref name="colors"/>.
    /// </summary>
    private void Leaves(Vector2 at, Color[] colors, int amount, float speed, float forward, float gravity, float life = 1.2f, float size = 5f)
    {
        var offsets = new float[colors.Length];
        for (int i = 0; i < colors.Length; i++) offsets[i] = i / (float)colors.Length;
        var ramp = new Gradient { Offsets = offsets, Colors = colors, InterpolationMode = Gradient.InterpolationModeEnum.Constant };
        var burst = new CpuParticles2D
        {
            Position = at,
            OneShot = true, Emitting = true, Amount = amount, Lifetime = life, Explosiveness = 0.95f,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle, EmissionRectExtents = new Vector2(8f, 6f),
            Direction = new Vector2(-forward * 0.55f, -1f).Normalized(), Spread = 55f,
            InitialVelocityMin = speed * 0.35f, InitialVelocityMax = speed, Gravity = new Vector2(0f, gravity),
            DampingMin = 40f, DampingMax = 120f,
            AngularVelocityMin = -540f, AngularVelocityMax = 540f,
            ScaleAmountMin = size * 0.6f, ScaleAmountMax = size * 1.2f,
            ColorInitialRamp = ramp,
        };
        burst.ZIndex = 20;   // over the car's wheels, which hide the plant at the moment it goes
        AddChild(burst);
        GetTree().CreateTimer(life + 0.4f).Timeout += () =>
        {
            if (IsInstanceValid(burst)) burst.QueueFree();
        };
    }

    private string PlantSummary() =>
        $"plants={_flattened[(int)PlantKind.Bush]}/{_flattened[(int)PlantKind.Flowers]}/{_flattened[(int)PlantKind.Cactus]}/{_flattened[(int)PlantKind.SnowShrub]}/{_flattened[(int)PlantKind.Tuft]} " +
        $"bestMow={_bestMow} mowPops={_mowPops} flatNow={Flora.FlatCount} regrown={Flora.Regrown}";

    /// <summary>Census of the plants along the road near the garage, for the bot log.</summary>
    private void LogPlantCensus()
    {
        if (!GameSession.AutoPlay) return;
        var counts = new int[6];
        int mid = (TileWorld.PadFrom + TileWorld.PadTo) / 2;
        for (int tx = mid - 800; tx <= mid + 800; tx++)
            counts[(int)Flora.KindOf(Flora.DecorCell(_world, tx))]++;
        GD.Print($"[plants] within 200 m of the garage: bushes={counts[(int)PlantKind.Bush]} flowers={counts[(int)PlantKind.Flowers]} " +
                 $"cacti={counts[(int)PlantKind.Cactus]} snowShrubs={counts[(int)PlantKind.SnowShrub]} tufts={counts[(int)PlantKind.Tuft]}");
    }
}
