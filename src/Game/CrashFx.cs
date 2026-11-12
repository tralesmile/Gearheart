using System;
using Gearheart.Core;
using Gearheart.Vehicles;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// Crash feedback for the player's car: screen shake, sparks where metal scrapes the ground,
/// a dust burst on landings, flying bolts when beams snap, and thud / crunch / snap sounds
/// scaled to how hard the hit was. Attach to a scene, set <see cref="Camera"/> and
/// <see cref="Sfx"/>, then call <see cref="Watch"/> with the car.
/// </summary>
public partial class CrashFx : Node2D
{
    private const float MaxShake = 22f;        // screen pixels at full trauma
    private const float TraumaDecay = 1.5f;    // per second
    private const int MaxBursts = 10;

    public Camera2D Camera { get; set; }
    public Sfx Sfx { get; set; }

    /// <summary>Colour of the ground at a point (for the dust burst).</summary>
    public Func<Vector2, Color> GroundColor { get; set; } = _ => new Color(0.55f, 0.45f, 0.35f);

    /// <summary>Shows a short message on screen (wheel lost, big crash).</summary>
    public Action<string> Toast { get; set; }

    /// <summary>Writes [crash] lines to the log (bot runs).</summary>
    public bool Log { get; set; }

    /// <summary>When false (for example while the player is on foot), hits make no shake or toast.</summary>
    public bool Enabled { get; set; } = true;

    public int Hits { get; private set; }
    public float HardestHit { get; private set; }
    public int SparkBursts { get; private set; }

    private SoftVehicle _car;
    private float _trauma, _time;
    private int _bursts;

    public void Watch(SoftVehicle car)
    {
        if (_car != null && IsInstanceValid(_car)) _car.Crashed -= OnCrash;
        _car = car;
        if (_car != null) _car.Crashed += OnCrash;
    }

    public override void _ExitTree()
    {
        if (_car != null && IsInstanceValid(_car)) _car.Crashed -= OnCrash;
    }

    /// <summary>A small puff of dust (rocking a stuck car, a wheel thumping down).</summary>
    public void Dust(Vector2 at, Vector2 normal, Color color, int amount, float speed)
    {
        if (Enabled) Burst(at, normal, color, amount, speed, 0.6f, 2.5f, 5f, 200f, 70f, dust: true);
    }

    /// <summary>Adds screen shake (0..1). Shake grows with the square of trauma, so small knocks stay subtle.</summary>
    public void AddTrauma(float amount) => _trauma = Mathf.Clamp(_trauma + amount, 0f, 1f);

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _trauma = Mathf.Max(0f, _trauma - TraumaDecay * dt);
        if (Camera == null) return;

        float shake = _trauma * _trauma * MaxShake / Mathf.Max(0.1f, Camera.Zoom.X);
        // Smooth pseudo-noise: a few unrelated sines per axis.
        float nx = Mathf.Sin(_time * 47f) * 0.6f + Mathf.Sin(_time * 83f + 1.3f) * 0.4f;
        float ny = Mathf.Sin(_time * 53f + 2.1f) * 0.6f + Mathf.Sin(_time * 71f + 0.4f) * 0.4f;
        Camera.Offset = new Vector2(nx, ny) * shake;
    }

    private void OnCrash(CrashEvent e)
    {
        Hits++;
        HardestHit = Mathf.Max(HardestHit, e.Strength);
        float kmh = e.Strength / 64f * 3.6f;
        // 0 at a light knock, 1 at a brutal one. The body hitting the ground hurts sooner (13 to
        // 50 km/h) than landing on the wheels, which the suspension softens (21 to 74 km/h).
        float power = e.BodyHit
            ? Mathf.Clamp((e.Strength - 230f) / 670f, 0f, 1f)
            : Mathf.Clamp((e.Strength - 380f) / 940f, 0f, 1f);

        if (Enabled)
        {
            AddTrauma((e.BodyHit ? 0.15f + 0.7f * power : 0.1f + 0.6f * power) + 0.12f * Mathf.Min(e.Snapped, 4) + (e.WheelLost ? 0.3f : 0f));
            if (e.WheelLost) Toast?.Invoke("LOST A WHEEL!");
            else if (e.Snapped >= 3 || (e.BodyHit && power > 0.6f)) Toast?.Invoke(e.Snapped >= 3 ? "WRECKED!" : "CRASH!");
        }

        // Dust kicked up wherever the car hits the ground.
        Burst(e.Point, e.Normal, GroundColor(e.Point - e.Normal * 4f), 10 + (int)(20 * power), 60f + 180f * power, 0.75f, 3f, 7f, 260f, 80f, dust: true);

        // Metal on the ground (or a snapping beam) throws sparks.
        if (e.BodyHit || e.Snapped > 0)
        {
            SparkBursts++;
            Burst(e.Point, e.Normal, new Color(1f, 0.85f, 0.35f), 18 + (int)(30 * power) + 6 * Mathf.Min(e.Snapped, 4), 180f + 360f * power, 0.5f, 2.5f, 4.5f, 700f, 75f, dust: false);
        }
        if (e.Snapped > 0)
            Burst(e.Point, e.Normal, new Color(0.38f, 0.39f, 0.42f), 4 + 2 * Mathf.Min(e.Snapped, 4), 140f + 160f * power, 1.0f, 3f, 5f, 900f, 60f, dust: false);

        if (Sfx != null)
        {
            // Harder hits are louder and lower.
            float pitch = 1.15f - 0.35f * power;
            Sfx.Play(e.BodyHit ? "crunch" : "thud", -16f + 14f * power, 0.12f, pitch);
            if (e.Snapped > 0 || e.WheelLost) Sfx.Play("snap", -8f + 4f * power, 0.15f);
        }

        if (Log)
            GD.Print($"[crash] hit {kmh:0} km/h ({e.Strength:0} px/s) {(e.BodyHit ? "body" : "wheels")} snapped={e.Snapped} dent={e.Dent:0.0} " +
                     $"wheelLost={e.WheelLost} at=({e.Point.X:0},{e.Point.Y:0}) frame={Engine.GetFramesDrawn()} shake={_trauma:0.00}");
    }

    private void Burst(Vector2 at, Vector2 normal, Color color, int amount, float speed, float life,
                       float sizeMin, float sizeMax, float gravity, float spread, bool dust)
    {
        if (_bursts >= MaxBursts) return;
        _bursts++;
        var fade = new Gradient
        {
            Offsets = dust ? new[] { 0f, 1f } : new[] { 0f, 0.3f, 1f },
            Colors = dust
                ? new[] { new Color(color, 0.85f), new Color(color, 0f) }
                : new[] { new Color(1f, 1f, 0.9f, 1f), color, new Color(0.9f, 0.3f, 0.1f, 0f) },
        };
        var p = new CpuParticles2D
        {
            Position = at + normal * 2f,
            OneShot = true, Emitting = true, Amount = Mathf.Max(1, amount), Lifetime = life, Explosiveness = 1f,
            Direction = normal, Spread = spread, InitialVelocityMin = speed * 0.4f, InitialVelocityMax = speed,
            Gravity = new Vector2(0f, gravity), ScaleAmountMin = sizeMin, ScaleAmountMax = sizeMax,
            ColorRamp = fade, DampingMin = dust ? 60f : 0f, DampingMax = dust ? 120f : 0f,
            ZIndex = 5,
        };
        AddChild(p);
        GetTree().CreateTimer(life + 0.3).Timeout += () =>
        {
            _bursts--;
            if (IsInstanceValid(p)) p.QueueFree();
        };
    }
}
