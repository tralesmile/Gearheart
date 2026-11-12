using System;
using System.Collections.Generic;
using Gearheart.Vehicles;
using Godot;

namespace Gearheart.Game;

/// <summary>
/// Tyres on snow and ice: plain tyres that spin or slide throw a fan of pale ice mist,
/// studded tyres kick up crisp white chips as they bite. Also tells the player, once, that
/// Spiked Tyres exist when their wheels are spinning helplessly on ice, and cheers the first
/// time the spikes bite. Call <see cref="Track"/> every physics tick.
/// </summary>
public partial class TyreSpray : Node2D
{
    private const int MaxChips = 320;
    private const float SlipLimit = 80f;      // px/s of tyre slip before a plain tyre sprays
    private const float BiteSpeed = 60f;      // px/s of rim speed before studs throw chips

    /// <summary>True when the ground at a point is ice (not just snow).</summary>
    public Func<Vector2, bool> IsIce { get; set; } = _ => false;

    /// <summary>Shows a short message on screen; returns false when another call-out is still being read.</summary>
    public Func<string, bool> Toast { get; set; }

    /// <summary>Writes [ice] lines to the log (bot runs).</summary>
    public bool Log { get; set; }

    /// <summary>Seconds with any wheel on ice, sliding on snow or ice, and studs biting (for the bot log).</summary>
    public float IceSeconds { get; private set; }
    public float SlipSeconds { get; private set; }
    public float BiteSeconds { get; private set; }
    public int ChipsThrown { get; private set; }

    private struct Chip
    {
        public Vector2 Pos, Vel;
        public float Life, MaxLife, Size;
        public Color Color;
    }

    private readonly List<Chip> _chips = new();
    private readonly RandomNumberGenerator _rng = new();
    private float _emit, _slipRun, _biteRun;
    private bool _slipHinted, _biteCheered;
    private string _pending;       // a hint waiting for the screen to be free
    private float _pendingFor;

    public TyreSpray()
    {
        ZIndex = 5;
        _rng.Seed = 4242;   // same spray in every bot run
    }

    public void Track(SoftVehicle car, float dt, bool driving)
    {
        if (!driving || car == null)
        {
            _slipRun = _biteRun = 0f;
            return;
        }

        bool onIce = false, slipping = false, biting = false, slippingOnIce = false, bitingOnIce = false;
        foreach (var c in car.WheelContacts)
        {
            if (!c.OnFrozen) continue;
            bool ice = IsIce(c.Point - c.Normal * 6f);
            onIce |= ice;
            Vector2 tangent = new(-c.Normal.Y, c.Normal.X);
            float back = -Mathf.Sign(Mathf.Abs(c.RimSpeed) > 20f ? c.RimSpeed : -c.Slip);   // chips fly out behind the tyre

            if (c.Biting && Mathf.Abs(c.RimSpeed) > BiteSpeed)
            {
                biting = true;
                bitingOnIce |= ice;
                float rate = 50f * Mathf.Clamp(Mathf.Abs(c.RimSpeed) / 450f, 0.25f, 1f);
                Emit(c.Point, tangent * back, c.Normal, rate * dt, 100f + Mathf.Abs(c.RimSpeed) * 0.25f, new Color(0.97f, 0.99f, 1f), 3f, 4.5f);
            }
            else if (Mathf.Abs(c.Slip) > SlipLimit)
            {
                slipping = true;
                slippingOnIce |= ice;
                float rate = 110f * Mathf.Clamp(Mathf.Abs(c.Slip) / 300f, 0.25f, 1f);
                Emit(c.Point, tangent * back, c.Normal, rate * dt, 70f + Mathf.Abs(c.Slip) * 0.35f, new Color(0.86f, 0.95f, 1f, 0.95f), 2.5f, 5f);
            }
        }

        if (onIce) IceSeconds += dt;
        if (slipping) SlipSeconds += dt;
        if (biting) BiteSeconds += dt;

        // Plain tyres spinning on ice for a moment: tell the player the fix, once.
        // (Wheels bounce off the ground on bumpy ice, so slipping ticks come and go: count them, forget slowly.)
        _slipRun = slippingOnIce && !car.HasStuds ? _slipRun + dt : Mathf.Max(0f, _slipRun - dt * 0.2f);
        if (!_slipHinted && _slipRun > 0.6f)
        {
            _slipHinted = true;
            Queue("ICE! Your tyres can't grip\nSpiked Tyres bite here (garage)");
        }
        _biteRun = bitingOnIce ? _biteRun + dt : 0f;
        if (!_biteCheered && _biteRun > 0.4f)
        {
            _biteCheered = true;
            Queue("SPIKES BITE!");
        }

        // Show the waiting hint as soon as the screen is free (a trip-goal call-out may be up),
        // but drop it if it has gone stale.
        if (_pending != null)
        {
            _pendingFor += dt;
            if (Toast != null && Toast(_pending))
            {
                if (Log) GD.Print($"[ice] hint shown after {_pendingFor:0.0}s: \"{_pending.Replace('\n', ' ')}\" frame={Engine.GetFramesDrawn()}");
                _pending = null;
            }
            else if (_pendingFor > 5f)
            {
                if (Log) GD.Print($"[ice] hint dropped (screen busy): \"{_pending.Replace('\n', ' ')}\"");
                _pending = null;
            }
        }
    }

    private void Queue(string text)
    {
        _pending = text;
        _pendingFor = 0f;
    }

    private void Emit(Vector2 at, Vector2 back, Vector2 normal, float count, float speed, Color color, float minSize, float maxSize)
    {
        _emit += count;
        while (_emit >= 1f)
        {
            _emit -= 1f;
            if (_chips.Count >= MaxChips) _chips.RemoveAt(0);
            Vector2 dir = (back * _rng.RandfRange(0.6f, 1.1f) + normal * _rng.RandfRange(0.5f, 1.2f)).Normalized();
            float life = _rng.RandfRange(0.35f, 0.7f);
            _chips.Add(new Chip
            {
                Pos = at + new Vector2(_rng.RandfRange(-4f, 4f), _rng.RandfRange(-3f, 1f)),
                Vel = dir * speed * _rng.RandfRange(0.5f, 1.15f),
                Life = life,
                MaxLife = life,
                Size = _rng.RandfRange(minSize, maxSize),
                Color = color,
            });
            ChipsThrown++;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        for (int i = _chips.Count - 1; i >= 0; i--)
        {
            Chip c = _chips[i];
            c.Life -= dt;
            if (c.Life <= 0f)
            {
                _chips.RemoveAt(i);
                continue;
            }
            c.Vel += new Vector2(0f, 520f) * dt;
            c.Vel *= 1f - 1.5f * dt;
            c.Pos += c.Vel * dt;
            _chips[i] = c;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var c in _chips)
        {
            float t = c.Life / c.MaxLife;
            Color col = c.Color;
            col.A *= Mathf.Clamp(t * 1.6f, 0f, 1f);
            float s = c.Size * (0.6f + 0.4f * t);
            // A dark rim so pale chips read against snow, ice and sky.
            DrawRect(new Rect2(c.Pos - new Vector2(s + 2f, s + 2f) / 2f, new Vector2(s + 2f, s + 2f)), new Color(0.22f, 0.32f, 0.45f, col.A * 0.7f));
            DrawRect(new Rect2(c.Pos - new Vector2(s, s) / 2f, new Vector2(s, s)), col);
        }
    }
}
