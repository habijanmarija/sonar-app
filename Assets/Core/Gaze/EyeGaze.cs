using UnityEngine;

namespace Host.Gaze
{
    /// World-space eye gaze ray + validity flag. Validity false means the headset cannot
    /// report a current gaze (uncalibrated, blink, hardware not present, etc.).
    public readonly struct EyeGaze
    {
        public readonly Vector3 Origin;
        public readonly Vector3 Direction;
        public readonly float TimeSeconds;
        public readonly bool IsValid;

        public EyeGaze(Vector3 origin, Vector3 direction, float timeSeconds, bool isValid = true)
        {
            Origin = origin;
            Direction = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.forward;
            TimeSeconds = timeSeconds;
            IsValid = isValid;
        }

        public static EyeGaze Invalid =>
            new EyeGaze(Vector3.zero, Vector3.forward, 0f, isValid: false);
    }

    public interface IEyeGazeSource
    {
        EyeGaze Current { get; }
    }
}
