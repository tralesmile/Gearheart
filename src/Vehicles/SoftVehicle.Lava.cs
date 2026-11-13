using System;
using Godot;

namespace Gearheart.Vehicles;

/// <summary>
/// The magma sea (see World.Lava). Molten rock is much denser than a car: every node under the lava
/// line is pushed up harder than gravity pulls it down and its motion is damped, so the car sinks in
/// about half way, bobs and floats, while it cooks (damage grows each second it sits in it). The push
/// is a velocity change per node, so a car half in the lava tips a little, like a boat.
/// </summary>
public partial class SoftVehicle
{
    /// <summary>World position → is it lava? Set by the world scene; null means no lava anywhere.</summary>
    public Func<Vector2, bool> IsLava { get; set; }

    public const float LavaLift = 2.3f;     // upward push, in gravities, on a node under the surface
    private const float LavaDrag = 3.5f;    // 1/s: how fast lava soaks up a node's speed
    public const float LavaBurn = 2.5f;     // damage per second with the whole car under

    /// <summary>Share of the car's nodes in lava at the last substep (0..1).</summary>
    public float LavaShare { get; private set; }
    public bool InLava => LavaShare > 0f;

    /// <summary>Damage taken from lava so far (for the log).</summary>
    public float LavaDamage { get; private set; }

    private void ApplyLava(float dt)
    {
        if (IsLava == null || dt <= 0f)
        {
            LavaShare = 0f;
            return;
        }
        int under = 0;
        float lift = LavaLift * Gravity * dt * dt;
        float keep = Mathf.Exp(-LavaDrag * dt);
        for (int i = 0; i < Pos.Length; i++)
        {
            if (!IsLava(Pos[i] + Origin)) continue;
            under++;
            Vector2 step = (Pos[i] - Prev[i]) * keep;
            Prev[i] = Pos[i] - step;
            Pos[i].Y -= lift;
        }
        LavaShare = Pos.Length > 0 ? (float)under / Pos.Length : 0f;
        if (under == 0) return;
        float burn = LavaBurn * Mathf.Min(1f, LavaShare * 2f) * dt;
        Damage += burn;
        LavaDamage += burn;
    }
}
