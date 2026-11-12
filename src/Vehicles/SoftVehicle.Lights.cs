using Godot;

namespace Gearheart.Vehicles;

/// <summary>
/// Headlights and tail-lights. A cone of warm light shines out of the front of the frontmost body
/// block and a small red glow sits on the rearmost one (brighter while braking). The scene sets
/// <see cref="Lights"/> (0..1) from the time of day and depth: off by day, on at dusk, at night and
/// underground. The lenses are drawn on the body so you can see the lamps even by day.
/// Cars face right and back up to go left, so rolling backwards turns on a white reversing lamp
/// that shines a shorter cone out of the back: driving west at night you still see the road.
/// </summary>
public partial class SoftVehicle
{
    private const float HeadlightReach = 1.55f;   // texture scale of the cone: 512 px * 1.55 / 2 = about 400 px (6 m)

    private PointLight2D _headlight, _tailLight, _reverseLight;
    private float _reverseBlend;
    private static ImageTexture _coneTexture;
    private static GradientTexture2D _roundTexture;

    /// <summary>0 = lights off, 1 = full headlights. Set by the scene every frame.</summary>
    public float Lights { get; set; }

    /// <summary>True while the headlights are actually shining (the front block is still on the car).</summary>
    public bool HeadlightsOn => _headlight != null && _headlight.Visible && _headlight.Energy > 0.05f;

    private void CreateLights()
    {
        _headlight = new PointLight2D
        {
            Texture = ConeTexture(), TextureScale = HeadlightReach, Energy = 0f, Visible = false,
            Color = new Color(1f, 0.94f, 0.78f), ShadowEnabled = false,
        };
        AddChild(_headlight);
        _tailLight = new PointLight2D
        {
            Texture = RoundTexture(), TextureScale = 0.2f, Energy = 0f, Visible = false,
            Color = new Color(1f, 0.15f, 0.1f),
        };
        AddChild(_tailLight);
        _reverseLight = new PointLight2D
        {
            Texture = ConeTexture(), TextureScale = HeadlightReach * 0.85f, Energy = 0f, Visible = false,
            Color = new Color(0.95f, 0.95f, 1f),
        };
        AddChild(_reverseLight);
    }

    /// <summary>0..1 how much the reversing lamp is on (for the bot log).</summary>
    public float ReverseLamp => _reverseLight != null && _reverseLight.Visible ? _reverseBlend : 0f;

    private void UpdateLights()
    {
        if (_headlight == null) return;
        bool on = Lights > 0.02f;
        float backing = CenterVelocity.Dot(Forward());
        _reverseBlend = Mathf.MoveToward(_reverseBlend, backing < -60f ? 1f : backing > 20f ? 0f : _reverseBlend, 1f / 30f);
        _headlight.Visible = on && _frontCell != null;
        _tailLight.Visible = on && _rearCell != null;
        _reverseLight.Visible = on && _rearCell != null && _reverseBlend > 0.02f;
        if (!on) return;

        if (_frontCell != null)
        {
            int[] c = _frontCell.Corners;
            Vector2 forward = (Pos[c[1]] - Pos[c[0]]).Normalized();
            _headlight.Position = ToLocal(Pos[c[1]].Lerp(Pos[c[2]], 0.45f) + forward * 2f);
            _headlight.Rotation = forward.Angle() - GlobalRotation;
            _headlight.Energy = 1.35f * Lights;
        }
        if (_rearCell != null)
        {
            int[] c = _rearCell.Corners;
            _tailLight.Position = ToLocal(Pos[c[0]].Lerp(Pos[c[3]], 0.4f));
            bool braking = _input.Handbrake || (Mathf.Abs(_input.Throttle) > 0.3f && CenterVelocity.Dot(Forward()) * _input.Throttle < -40f);
            _tailLight.Energy = (braking ? 1.6f : 0.8f) * Lights;
            Vector2 back = (Pos[c[0]] - Pos[c[1]]).Normalized();
            _reverseLight.Position = ToLocal(Pos[c[0]].Lerp(Pos[c[3]], 0.55f) + back * 2f);
            _reverseLight.Rotation = back.Angle() - GlobalRotation;
            _reverseLight.Energy = 1.15f * Lights * _reverseBlend;
        }
    }

    /// <summary>The lamp lenses: a pale headlight on the front edge and a red tail-light on the back edge, glowing when on.</summary>
    private void DrawLightLenses()
    {
        if (_frontCell != null)
        {
            int[] c = _frontCell.Corners;
            Vector2 p = ToLocal(Pos[c[1]].Lerp(Pos[c[2]], 0.45f));
            Vector2 back = (Pos[c[0]] - Pos[c[1]]).Normalized() * 3f;
            DrawRect(new Rect2(p + back - new Vector2(2.5f, 2.5f), new Vector2(5f, 5f)),
                     new Color(0.95f, 0.92f, 0.7f).Lerp(new Color(1.6f, 1.5f, 1.2f), Lights));
        }
        if (_rearCell != null)
        {
            int[] c = _rearCell.Corners;
            Vector2 p = ToLocal(Pos[c[0]].Lerp(Pos[c[3]], 0.4f));
            Vector2 fwd = (Pos[c[1]] - Pos[c[0]]).Normalized() * 2.5f;
            DrawRect(new Rect2(p + fwd - new Vector2(2f, 2.5f), new Vector2(4f, 5f)),
                     new Color(0.6f, 0.08f, 0.06f).Lerp(new Color(1.6f, 0.25f, 0.15f), Lights));
        }
    }

    /// <summary>
    /// A 512 x 512 light texture with its apex at the centre: a beam about 40 degrees wide shining
    /// to the right (+X), soft at the edges and fading with distance, plus a small glow round the lamp.
    /// </summary>
    private static ImageTexture ConeTexture()
    {
        if (_coneTexture != null) return _coneTexture;
        const int size = 512;
        const float half = size / 2f;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - half, dy = y + 0.5f - half;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / half;
                float a = 0f;
                if (dx > 0f && d < 1f)
                {
                    float angle = Mathf.Abs(Mathf.Atan2(dy, dx));
                    float edge = 1f - Mathf.Clamp((angle - 0.26f) / 0.16f, 0f, 1f);   // full to 15 deg, gone by 24 deg
                    float fall = Mathf.Pow(1f - d, 1.2f);
                    float near = Mathf.Clamp(d / 0.08f, 0f, 1f);                       // soften the hard apex
                    a = edge * fall * (0.35f + 0.65f * near);
                }
                float glow = Mathf.Max(0f, 1f - d / 0.07f);
                a = Mathf.Clamp(a + 0.8f * glow * glow, 0f, 1f);
                image.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        _coneTexture = ImageTexture.CreateFromImage(image);
        return _coneTexture;
    }

    private static GradientTexture2D RoundTexture() => _roundTexture ??= new GradientTexture2D
    {
        Width = 256,
        Height = 256,
        Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new Vector2(0.5f, 0.5f),
        FillTo = new Vector2(1f, 0.5f),
        Gradient = new Gradient
        {
            Offsets = new[] { 0f, 1f },
            Colors = new[] { new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0f) },
        },
    };
}
