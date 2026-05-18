using UnityEngine;

namespace Sonar.Tracking
{
    /// Wires a Unity XR Interaction Toolkit Right-Hand Controller's pose to a
    /// ControllerDrillTipSource without requiring the asmdef to take a hard dependency
    /// on com.unity.xr.interaction.toolkit. The controller transform is supplied by
    /// the user dragging the Right Hand Controller GameObject into the Inspector slot.
    ///
    /// Purpose: keep the cross-cutting wiring simple in the Editor — one drag-and-drop
    /// to connect XR Origin's right-hand controller → drill-tip pipeline.
    [AddComponentMenu("SONAR/Tracking/XR Controller Tip Bridge")]
    public sealed class XrControllerTipBridge : MonoBehaviour
    {
        [Tooltip("Drag the XR Origin's Right Hand Controller GameObject (or its model transform) here.")]
        [SerializeField] Transform _rightHandController;

        [Tooltip("Drag the ControllerDrillTipSource that should receive the controller's pose.")]
        [SerializeField] ControllerDrillTipSource _drillTipSource;

        [Tooltip("Auto-find ControllerDrillTipSource on this GameObject or its children if not assigned.")]
        [SerializeField] bool _autoFindDrillTip = true;

        void Awake()
        {
            if (_drillTipSource == null && _autoFindDrillTip)
                _drillTipSource = GetComponentInChildren<ControllerDrillTipSource>();

            if (_drillTipSource == null)
                Debug.LogError("[XrControllerTipBridge] No ControllerDrillTipSource assigned or found. Drag one into the Inspector slot.");
            if (_rightHandController == null)
                Debug.LogWarning("[XrControllerTipBridge] No right-hand controller transform assigned. Tip source will report Invalid until set.");

            if (_drillTipSource != null && _rightHandController != null)
                _drillTipSource.SetControllerTransform(_rightHandController);
        }

        public void SetControllerTransform(Transform t)
        {
            _rightHandController = t;
            if (_drillTipSource != null) _drillTipSource.SetControllerTransform(t);
        }
    }
}
