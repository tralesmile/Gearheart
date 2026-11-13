using Godot;

namespace Gearheart.Game;

/// <summary>
/// The garage at night: a lamp post beside the door, the shed's inside lit warm, and a searchlight on
/// the roof that sweeps a beam slowly across the sky, so coming home in the dark means driving toward
/// a light. <see cref="Level"/> (0 by day, 1 at night) is set by the world from the clock.
/// The post is drawn in the world (it darkens with the night like everything else); the glow, the
/// beam and the lit inside are drawn by <see cref="BeaconGlow"/> on the glint layer, added on top.
/// </summary>
public partial class BeaconPost : Node2D
{
    public const float Height = 168f;

    /// <summary>0 by day, 1 at night: the bulb is drawn lit.</summary>
    public float Level { get; set; }
    /// <summary>The roof searchlight, relative to the post's foot, and where it points (from straight up).</summary>
    public Vector2 Searchlight { get; set; }
    public float BeamAngle { get; set; }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        // Position is the foot of the post on the pad.
        var iron = new Color(0.22f, 0.23f, 0.26f);
        var rim = new Color(0.08f, 0.08f, 0.1f);
        DrawRect(new Rect2(-5f, -Height, 10f, Height), rim);
        DrawRect(new Rect2(-3f, -Height + 2f, 6f, Height - 2f), iron);
        DrawRect(new Rect2(-9f, -8f, 18f, 8f), rim);               // foot
        DrawRect(new Rect2(-3f, -Height, 26f, 5f), rim);           // arm toward the door side
        DrawRect(new Rect2(14f, -Height + 4f, 16f, 6f), rim);      // lamp hood
        var bulb = new Color(0.45f, 0.42f, 0.36f).Lerp(new Color(1f, 0.93f, 0.7f), Level);
        DrawRect(new Rect2(17f, -Height + 10f, 10f, 5f), bulb);

        // The searchlight on the roof: a bracket and a drum that turns with the beam.
        DrawRect(new Rect2(Searchlight + new Vector2(-6f, 0f), new Vector2(12f, 6f)), rim);
        DrawSetTransform(Searchlight, BeamAngle, Vector2.One);
        DrawRect(new Rect2(-7f, -14f, 14f, 14f), rim);
        DrawRect(new Rect2(-5f, -12f, 10f, 11f), iron);
        DrawRect(new Rect2(-6f, -16f, 12f, 3f), new Color(0.5f, 0.48f, 0.42f).Lerp(new Color(1f, 0.95f, 0.8f), Level));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    /// <summary>Where the bulb is, relative to the post's foot.</summary>
    public static Vector2 Bulb => new(22f, -Height + 13f);
}

/// <summary>The lit parts of the garage at night, drawn additively over the darkness.</summary>
public partial class BeaconGlow : Node2D
{
    public float Level { get; set; }
    /// <summary>World points: the lamp bulb, the searchlight on the roof, and the shed's inside.</summary>
    public Vector2 Bulb { get; set; }
    public Vector2 Searchlight { get; set; }
    public Rect2 Inside { get; set; }

    public const float BeamLength = 2600f;
    /// <summary>The beam's angle from straight up (radians, + leans east).</summary>
    public float BeamAngle { get; private set; }

    private float _time;

    public override void _Ready()
    {
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        // A slow sweep, about 18 s side to side and back, leaning up to 55 degrees each way.
        BeamAngle = 0.96f * Mathf.Sin(_time * 0.35f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Level <= 0.01f) return;
        float l = Level;
        var warm = new Color(1f, 0.78f, 0.42f);

        // The shed's inside: a warm wash, brighter toward the floor where the workbench is.
        var top = new Color(warm, 0.10f * l);
        var bottom = new Color(warm, 0.26f * l);
        var r = Inside;
        DrawPolygon(new[] { r.Position, r.Position + new Vector2(r.Size.X, 0f), r.End, r.Position + new Vector2(0f, r.Size.Y) },
                    new[] { top, top, bottom, bottom });

        // The lamp post's bulb: a halo and a cone down onto the pad.
        float flicker = 0.96f + 0.04f * Mathf.Sin(_time * 23f) * Mathf.Sin(_time * 7f);
        DrawCircle(Bulb, 34f, new Color(warm, 0.10f * l * flicker));
        DrawCircle(Bulb, 16f, new Color(warm, 0.22f * l * flicker));
        DrawCircle(Bulb, 6f, new Color(1f, 0.95f, 0.8f, 0.7f * l));
        var coneTop = new Color(warm, 0.16f * l * flicker);
        var coneEnd = new Color(warm, 0f);
        float drop = BeaconPost.Height - 14f;
        DrawPolygon(new[] { Bulb + new Vector2(-6f, 2f), Bulb + new Vector2(6f, 2f), Bulb + new Vector2(70f, drop), Bulb + new Vector2(-70f, drop) },
                    new[] { coneTop, coneTop, coneEnd, coneEnd });

        // The searchlight: a long beam fading out into the sky, and a hot spot at its lamp.
        Vector2 up = Vector2.Up.Rotated(BeamAngle);
        Vector2 origin = Searchlight + up * 15f;   // the drum's face
        Vector2 side = new(-up.Y, up.X);
        Vector2 far = origin + up * BeamLength;
        var beamNear = new Color(1f, 0.92f, 0.7f, 0.30f * l);
        var beamMid = new Color(1f, 0.9f, 0.65f, 0.12f * l);
        var beamFar = new Color(1f, 0.9f, 0.65f, 0f);
        Vector2 mid = origin + up * BeamLength * 0.35f;
        DrawPolygon(new[] { origin - side * 7f, origin + side * 7f, mid + side * 80f, mid - side * 80f },
                    new[] { beamNear, beamNear, beamMid, beamMid });
        DrawPolygon(new[] { mid - side * 80f, mid + side * 80f, far + side * 220f, far - side * 220f },
                    new[] { beamMid, beamMid, beamFar, beamFar });
        DrawCircle(origin, 12f, new Color(1f, 0.95f, 0.8f, 0.5f * l));
    }
}
