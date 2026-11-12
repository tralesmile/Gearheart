using Godot;
using Gearheart.World;

namespace Gearheart.Game;

/// <summary>
/// The driving camera's brain. On the ground it looks ahead the way the car is going, so the road
/// in front (the next kicker, a heap, a drop) is on screen sooner than the road behind. On a big
/// jump it works out where the car will come down (a plain ballistic arc against the tiles), eases
/// out and slides over so both the car and the landing spot are in view, and draws the car's
/// shadow on the ground there. On touchdown it eases back in.
/// <para>
/// It only moves the camera; nothing in the physics or the bots depends on it. Call
/// <see cref="Step"/> every frame; read <see cref="Target"/> and <see cref="Zoom"/>.
/// </para>
/// </summary>
public partial class AirCam : Node2D
{
    private const float Ts = TileWorld.TileSize;
    private const float Gravity = 980f;            // matches SoftVehicle
    private const float Probe = 44f;               // px below the body centre that touches down first (about the tyres)
    private const float LeadPerSpeed = 0.22f;      // s of look-ahead (px of lead per px/s)
    private const float LeadMax = 220f, LeadMaxDown = 90f, LeadMaxUp = 60f;
    private const float BigAir = 0.7f;             // s of predicted flight before the air cam engages
    private const float AirZoomMin = 0.85f;
    private const float MarginX = 300f, MarginY = 230f;   // screen px kept around car and landing

    public TileWorld World { get; set; }
    /// <summary>The Camera2D's position smoothing speed: its lag behind a moving target is cancelled.</summary>
    public float Smoothing { get; set; } = 7f;

    /// <summary>Where the camera should point this frame (world px).</summary>
    public Vector2 Target { get; private set; }
    /// <summary>The zoom the camera should ease toward this frame.</summary>
    public float Zoom { get; private set; } = 1.5f;
    /// <summary>0 = plain chase cam, 1 = fully framing the jump.</summary>
    public float Blend => _air;
    /// <summary>True while a predicted landing spot exists.</summary>
    public bool HasLanding => _hasLanding;
    public Vector2 Landing => _landing;
    public float TimeToLand => _timeToLand;
    public Vector2 Lead => _lead;

    private Vector2 _lead, _vel;
    private float _air, _timeToLand, _flight, _shadow;
    private bool _hasLanding;
    private Vector2 _landing;

    /// <summary>
    /// One frame. <paramref name="baseZoom"/> is the chase zoom (already pulled back for speed);
    /// <paramref name="airTime"/> is how long the car has been off the ground (0 on the ground).
    /// </summary>
    public void Step(float dt, bool driving, Vector2 car, Vector2 velocity, bool flying, float airTime,
                     float baseZoom, float currentZoom, Vector2 viewport)
    {
        if (!driving)
        {
            _lead = _lead.Lerp(Vector2.Zero, 1f - Mathf.Exp(-4f * dt));
            _vel = Vector2.Zero;
            _air = 0f;
            _hasLanding = false;
            Target = car;
            Zoom = baseZoom;
            Fade(dt);
            return;
        }

        // A smoothed velocity: bumps and bounces must not shake the view.
        _vel = _vel.Lerp(velocity, 1f - Mathf.Exp(-5f * dt));

        // Look ahead along the way the car is going. Slow to follow, so bumps don't wobble the view.
        var wantLead = new Vector2(
            Mathf.Clamp(velocity.X * LeadPerSpeed, -LeadMax, LeadMax),
            Mathf.Clamp(velocity.Y * 0.12f, -LeadMaxUp, LeadMaxDown));
        if (flying) wantLead.Y = 0f;   // the arc is framed below; don't chase the bounce
        _lead = _lead.Lerp(wantLead, 1f - Mathf.Exp(-1.3f * dt));

        // Where will it come down?
        _hasLanding = false;
        if (flying && airTime > 0.08f) _hasLanding = Predict(car, velocity, out _landing, out _timeToLand);
        _flight = _hasLanding ? airTime + _timeToLand : 0f;
        bool big = _hasLanding && _flight >= BigAir;
        float rate = big ? 3.2f : (flying ? 0.8f : 2.2f);   // in quickly; out quickly once down
        _air = Mathf.Lerp(_air, big ? 1f : 0f, 1f - Mathf.Exp(-rate * dt));

        Vector2 chase = car + _lead;
        Vector2 target = chase;
        float zoom = baseZoom;
        if (_hasLanding || _air > 0.01f)
        {
            if (_hasLanding) _frameLanding = _landing;
            Vector2 span = (_frameLanding - car).Abs();
            float fit = Mathf.Min(viewport.X / (span.X + 2f * MarginX), viewport.Y / (span.Y + 2f * MarginY));
            float airZoom = Mathf.Clamp(fit, AirZoomMin, baseZoom);
            zoom = Mathf.Lerp(baseZoom, airZoom, _air);
            target = chase.Lerp((car + _frameLanding) * 0.5f, _air);
        }

        // Whatever happens, keep the car on screen (judged at the zoom the camera has right now).
        Vector2 half = viewport / (2f * Mathf.Max(0.1f, currentZoom));
        Vector2 room = new(Mathf.Max(0f, half.X - 150f), Mathf.Max(0f, half.Y - 110f));
        // The camera eases toward its target and so trails a moving car by speed / smoothing;
        // aim that far ahead so the look-ahead is really ahead at 90 km/h too.
        target += _vel / Mathf.Max(1f, Smoothing) * (1f - 0.5f * _air);
        Vector2 off = target - car;
        off = new Vector2(Mathf.Clamp(off.X, -room.X, room.X), Mathf.Clamp(off.Y, -room.Y, room.Y));
        Target = car + off;
        Zoom = zoom;
        Fade(dt);
    }

    private Vector2 _frameLanding;

    /// <summary>
    /// Steps a ballistic arc from the car (gravity only; the booster and air drag are left out) until
    /// the bottom of the car meets a solid tile. Up to 3.5 s ahead.
    /// </summary>
    private bool Predict(Vector2 car, Vector2 v, out Vector2 landing, out float time)
    {
        landing = car;
        time = 0f;
        if (World == null) return false;
        const float step = 1f / 40f;
        Vector2 p = car + new Vector2(0f, Probe);
        if (Hit(p)) return false;   // still touching (just took off from a lip): wait a frame
        for (int i = 1; i <= 140; i++)
        {
            Vector2 next = p + v * step + new Vector2(0f, 0.5f * Gravity * step * step);
            v.Y += Gravity * step;
            if (Hit(next))
            {
                // Settle on the tile top under the hit for a clean shadow.
                float gy = World.GroundY(next.X, p.Y);
                landing = new Vector2(next.X, Mathf.Min(gy, next.Y + Ts));
                time = i * step;
                return true;
            }
            p = next;
        }
        return false;
    }

    private bool Hit(Vector2 p)
    {
        int tx = Mathf.FloorToInt(p.X / Ts), ty = Mathf.FloorToInt(p.Y / Ts);
        if (ty < 0) return false;
        if (World.Solid(tx, ty)) return true;
        float ramp = World.RampY(tx, ty, p.X);
        return !float.IsNaN(ramp) && p.Y >= ramp;
    }

    private void Fade(float dt)
    {
        float want = _hasLanding && _flight >= BigAir ? 1f : 0f;
        _shadow = Mathf.Lerp(_shadow, want, 1f - Mathf.Exp(-(want > 0f ? 6f : 10f) * dt));
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_shadow < 0.02f) return;
        // A soft dark ellipse on the ground, smaller and fainter the farther the car is from it.
        float near = Mathf.Clamp(1f - _timeToLand / 1.6f, 0.25f, 1f);
        float w = 40f + 34f * near, h = 5f + 3f * near;
        var c = new Color(0.05f, 0.03f, 0.02f, 0.6f * _shadow * (0.5f + 0.5f * near));
        Vector2 at = _frameLanding + new Vector2(0f, -1f);
        for (int i = 0; i < 3; i++)
        {
            float k = 1f - i * 0.28f;
            DrawSetTransform(at, 0f, new Vector2(w * k / h, 1f));
            DrawCircle(Vector2.Zero, h, new Color(c, c.A * (0.45f + 0.3f * i)));
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
