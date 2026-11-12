using System;

namespace Gearheart.Game;

/// <summary>
/// The gate sprint: a timed dash over <see cref="Distance"/> metres from a standing start on the garage
/// pad, like a drag strip. The clock starts the last moment the car was standing still and stops when it
/// is <see cref="Distance"/> metres (sideways) from where it stood, in either direction. Launch control
/// (hold Space + throttle, let go in the green) is how you get a good time; a plain start works too.
/// Pure logic: the world scene feeds it positions and shows the result.
/// </summary>
public sealed class GateSprint
{
    public const float Distance = 20f;      // metres
    public const float GiveUpAfter = 12f;   // seconds: a sprint that takes longer is not a sprint
    public const float RestKmh = 0.5f;      // below this the car counts as standing still

    private float _restAt = -1f, _restX, _startAt, _startX;

    /// <summary>A sprint is under way (the car left the line and has not reached the distance yet).</summary>
    public bool Running { get; private set; }

    /// <summary>Standing still on the pad, ready: the next move off starts the clock.</summary>
    public bool Armed => !Running && _restAt >= 0f;

    /// <summary>Seconds since the start (while running), or the finished time (after <see cref="Update"/> returned Finished).</summary>
    public float Time { get; private set; }

    /// <summary>Metres covered so far (while running).</summary>
    public float Covered { get; private set; }

    /// <summary>When the clock started (game seconds), for matching it to a launch.</summary>
    public float StartedAt => _startAt;

    public enum Result { None, Started, Finished, GaveUp }

    /// <param name="now">Game time in seconds.</param>
    /// <param name="xMeters">The car's sideways position in metres (any fixed origin).</param>
    /// <param name="kmh">The car's speed.</param>
    /// <param name="onPad">The car is on the garage pad (where a sprint may start).</param>
    /// <param name="driving">The player is in the car.</param>
    public Result Update(float now, float xMeters, float kmh, bool onPad, bool driving)
    {
        if (!driving)
        {
            bool wasRunning = Running;
            Running = false;
            _restAt = -1f;
            return wasRunning ? Result.GaveUp : Result.None;
        }

        bool started = false;
        if (!Running)
        {
            if (onPad && kmh < RestKmh)
            {
                _restAt = now;
                _restX = xMeters;
                return Result.None;
            }
            if (_restAt < 0f) return Result.None;   // rolling, but it never stood still on the pad
            Running = true;
            _startAt = _restAt;
            _startX = _restX;
            _restAt = -1f;
            started = true;
        }

        Time = now - _startAt;
        Covered = Math.Abs(xMeters - _startX);
        if (Covered >= Distance)
        {
            Running = false;
            return Result.Finished;
        }
        if (Time > GiveUpAfter || (onPad && kmh < RestKmh && Time > 0.3f))
        {
            // Too slow, or it stopped again on the pad: the next move off is a fresh start.
            Running = false;
            if (onPad && kmh < RestKmh)
            {
                _restAt = now;
                _restX = xMeters;
            }
            return Result.GaveUp;
        }
        return started ? Result.Started : Result.None;
    }
}
