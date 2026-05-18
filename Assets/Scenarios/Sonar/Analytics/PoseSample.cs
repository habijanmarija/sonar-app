using UnityEngine;

namespace Sonar.Analytics
{
    /// One timestamped drill-tip pose sample. Consumed by smoothness + path-efficiency
    /// scorers in this assembly; identical shape to the runtime DrillTipPose but with
    /// no Unity-runtime dependencies so the Python `metrics.py` port can mirror it.
    public readonly struct PoseSample
    {
        public readonly Vector3 Position;
        public readonly Vector3 Axis;
        public readonly float TimeSeconds;

        public PoseSample(Vector3 position, Vector3 axis, float timeSeconds)
        {
            Position = position;
            Axis = axis;
            TimeSeconds = timeSeconds;
        }
    }
}
