using Godot;

namespace Gearheart.Game;

// Bot log for the air cam: is the landing predicted where the car really comes down, how far does
// the camera ease out, and does the car stay on screen the whole jump?
public partial class WorldScene
{
    private bool _acTracking, _acWasFlying;
    private float _acFirstX, _acLastX, _acFirstT, _acWidest, _acPeakBlend, _acLogTimer;
    private int _acOffscreen, _acJumps, _acLeadLogs;

    private void LogAirCam(bool flying)
    {
        int frame = (int)Engine.GetFramesDrawn();
        Vector2 car = _car.CenterPosition;
        Vector2 vp = GetViewportRect().Size;
        Vector2 onScreen = (car - _camera.GetScreenCenterPosition()) * _camera.Zoom.X;
        bool visible = Mathf.Abs(onScreen.X) < vp.X / 2f - 30f && Mathf.Abs(onScreen.Y) < vp.Y / 2f - 30f;

        if (flying && !_acTracking && _airCam.HasLanding && _airTime + _airCam.TimeToLand >= 0.7f && _acJumps < 12)
        {
            _acTracking = true;
            _acFirstX = _airCam.Landing.X;
            _acFirstT = _airTime + _airCam.TimeToLand;
            _acWidest = _camera.Zoom.X;
            _acPeakBlend = 0f;
            _acOffscreen = 0;
            GD.Print($"[aircam] big air at x={car.X / Ts:0}: {_car.Speed / PixelsPerMeter * 3.6f:0} km/h, landing predicted at x={_acFirstX / Ts:0} ({(_acFirstX - car.X) / PixelsPerMeter:0.0} m ahead) after {_acFirstT:0.00} s of air, zoom {_camera.Zoom.X:0.00} -> {_airCam.Zoom:0.00} frame={frame}");
        }
        if (_acTracking)
        {
            if (_airCam.HasLanding) _acLastX = _airCam.Landing.X;
            _acWidest = Mathf.Min(_acWidest, _camera.Zoom.X);
            _acPeakBlend = Mathf.Max(_acPeakBlend, _airCam.Blend);
            if (!visible) _acOffscreen++;
            if (!flying && _acWasFlying)
            {
                _acTracking = false;
                _acJumps++;
                float err1 = (car.X - _acFirstX) / PixelsPerMeter, err2 = (car.X - _acLastX) / PixelsPerMeter;
                GD.Print($"[aircam] touchdown at x={car.X / Ts:0} ({(_bodyDown ? "on the body" : "wheels")}): first guess off by {err1:+0.0;-0.0} m, last guess off by {err2:+0.0;-0.0} m; widest zoom {_acWidest:0.00}, blend peaked {_acPeakBlend:0.00}, car off screen {_acOffscreen} frames frame={frame}");
            }
        }
        _acWasFlying = flying;

        // Now and then on the ground at speed: how far ahead does the camera look?
        _acLogTimer -= (float)GetProcessDeltaTime();
        if (_driving && !flying && _acLeadLogs < 6 && _acLogTimer <= 0f && _car.Speed > 20f / 3.6f * PixelsPerMeter)
        {
            _acLogTimer = 6f;
            _acLeadLogs++;
            GD.Print($"[aircam] driving {_car.Speed / PixelsPerMeter * 3.6f:0} km/h ({(_car.CenterVelocity.X >= 0 ? "right" : "left")}): looking {_airCam.Lead.X / PixelsPerMeter:+0.0;-0.0} m ahead, car {onScreen.X:+0;-0} px from screen centre, zoom {_camera.Zoom.X:0.00}, on screen {(visible ? "yes" : "NO")} frame={frame}");
        }
    }
}
