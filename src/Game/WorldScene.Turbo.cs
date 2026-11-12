using Gearheart.Core;
using Godot;

namespace Gearheart.Game;

/// <summary>The Turbocharger's sounds, call-out and bot log (the boost itself lives in SoftVehicle.Turbo).</summary>
public partial class WorldScene
{
    private bool _turboHinted;
    private int _turboFulls, _turboLogged;

    private void OnTurboFull()
    {
        if (!_driving) return;
        _turboFulls++;
        if (!_turboHinted)
        {
            _turboHinted = true;
            Toast("TURBO! Full boost: +40% torque\nLift off to hear it blow off");
        }
        if (GameSession.AutoPlay && _turboLogged < 12)
        {
            _turboLogged++;
            GD.Print($"[turbo] full boost #{_turboFulls} at {_car.Speed / PixelsPerMeter * 3.6f:0} km/h, gear {_car.Gearbox.Gear}, revs {_car.Gearbox.Rpm:0.00}, " +
                     $"launch armed={_car.LaunchArmed} whistle {_sfx.TurboWhistle:0.00} frame={Engine.GetFramesDrawn()}");
        }
    }

    private void OnTurboBlowOff(float boost)
    {
        if (!_driving) return;
        // Louder and a touch higher the more boost was vented.
        _sfx.Play("blowoff", -16f + 8f * boost, 0.25f, 0.9f + 0.2f * boost);
        if (GameSession.AutoPlay && _turboLogged < 12)
        {
            _turboLogged++;
            GD.Print($"[turbo] blow-off #{_car.TurboBlowOffs} boost {boost:0.00} at {_car.Speed / PixelsPerMeter * 3.6f:0} km/h, revs {_car.Gearbox.Rpm:0.00} frame={Engine.GetFramesDrawn()}");
        }
    }
}
