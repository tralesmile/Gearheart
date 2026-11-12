using Gearheart.Vehicles;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// The visible part of the car's lights at night: a faint haze of light in the air along each beam
/// (the PointLight2D only lights what it falls on, so a beam into the sky would be invisible), a bright
/// flare on each lamp, and a red glow on the tail-light. Lives on the glint layer above the darkness,
/// drawn with additive blending.
/// </summary>
public partial class HeadlightBeams : Node2D
{
    public SoftVehicle Car;

    private const float Length = 360f;
    private const float HalfAngle = 0.27f;   // radians: a little narrower than the light cone
    private const int Segments = 8;

    public override void _Ready() => Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        if (!IsInstanceValid(Car)) return;
        Beam(Car.HeadBeam, new Color(1f, 0.93f, 0.75f), 0.2f);
        Beam(Car.ReverseBeam, new Color(0.9f, 0.92f, 1f), 0.14f);
        var tail = Car.TailLamp;
        if (tail.Strength > 0.02f)
        {
            float a = tail.Strength * (tail.Braking ? 1f : 0.6f);
            DrawCircle(tail.Pos, 9f, new Color(1f, 0.15f, 0.08f, 0.25f * a));
            DrawCircle(tail.Pos, 3.5f, new Color(1f, 0.35f, 0.25f, 0.8f * a));
        }
    }

    private void Beam((Vector2 Pos, Vector2 Dir, float Strength) beam, Color color, float haze)
    {
        if (beam.Strength < 0.02f) return;
        var points = new Vector2[Segments + 2];
        var colors = new Color[Segments + 2];
        points[0] = beam.Pos;
        colors[0] = new Color(color, haze * beam.Strength);
        for (int i = 0; i <= Segments; i++)
        {
            float angle = Mathf.Lerp(-HalfAngle, HalfAngle, i / (float)Segments);
            points[i + 1] = beam.Pos + beam.Dir.Rotated(angle) * Length;
            colors[i + 1] = new Color(color, 0f);
        }
        DrawPolygon(points, colors);
        // The lamp itself flares.
        DrawCircle(beam.Pos, 10f, new Color(color, 0.22f * beam.Strength));
        DrawCircle(beam.Pos, 4f, new Color(color, 0.85f * beam.Strength));
    }
}
