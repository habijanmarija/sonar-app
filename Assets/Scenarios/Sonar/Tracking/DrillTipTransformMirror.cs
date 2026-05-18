using UnityEngine;

namespace Sonar.Tracking
{
    /// Mirrors an IDrillTipSource.Current into this GameObject's Transform every LateUpdate.
    /// Plug into Host.Telemetry.SessionRunner.SetDrillTipTransform(...) so the telemetry pump
    /// reads the smoothed pose without having to know about the source implementation.
    [AddComponentMenu("SONAR/Tracking/Drill Tip Transform Mirror")]
    public sealed class DrillTipTransformMirror : MonoBehaviour
    {
        [SerializeField] MonoBehaviour _source;
        IDrillTipSource _drill;

        public void SetSource(IDrillTipSource s) => _drill = s;

        void Awake()
        {
            _drill = _source as IDrillTipSource;
            if (_drill == null && _source != null)
                Debug.LogError($"[Tracking] {_source.GetType().Name} on '{name}' does not implement IDrillTipSource.");
        }

        void LateUpdate()
        {
            if (_drill == null) return;
            var tip = _drill.Current;
            if (!tip.IsValid) return;
            transform.position = tip.Position;
            transform.rotation = Quaternion.LookRotation(tip.Axis);
        }
    }
}
