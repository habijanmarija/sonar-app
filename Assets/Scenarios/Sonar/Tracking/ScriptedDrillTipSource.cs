using UnityEngine;

namespace Sonar.Tracking
{
    /// IDrillTipSource backed by an externally set pose; used by demo / smoke drivers
    /// that drive the drill from a scripted coroutine on Linux Editor flat-mode.
    [AddComponentMenu("SONAR/Tracking/Scripted Drill Tip Source")]
    public sealed class ScriptedDrillTipSource : MonoBehaviour, IDrillTipSource
    {
        DrillTipPose _current = DrillTipPose.Invalid;
        public DrillTipPose Current => _current;

        public void Set(Vector3 position, Vector3 axis)
        {
            _current = new DrillTipPose(position, axis, Time.unscaledTime);
        }
    }
}
