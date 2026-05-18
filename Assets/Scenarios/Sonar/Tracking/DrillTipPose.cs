using UnityEngine;

namespace Sonar.Tracking
{
    /// Snapshot of the drill-tip pose in phantom-space at a given Time.unscaledTime.
    public readonly struct DrillTipPose
    {
        public readonly Vector3 Position;
        public readonly Vector3 Axis;
        public readonly float UnscaledTime;
        public readonly bool IsValid;

        public DrillTipPose(Vector3 position, Vector3 axis, float unscaledTime, bool isValid = true)
        {
            Position = position;
            Axis = axis.sqrMagnitude > 1e-8f ? axis.normalized : Vector3.forward;
            UnscaledTime = unscaledTime;
            IsValid = isValid;
        }

        public static DrillTipPose Invalid =>
            new DrillTipPose(Vector3.zero, Vector3.forward, 0f, isValid: false);
    }
}
