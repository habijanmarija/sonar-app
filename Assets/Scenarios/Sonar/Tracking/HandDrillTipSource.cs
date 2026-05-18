#if UNITY_XR_HANDS
using System.Collections.Generic;
using UnityEngine.XR.Hands;
#endif
using UnityEngine;

namespace Sonar.Tracking
{
    /// Hand-tracking drill-tip source. Used by trainers demonstrating without a controller
    /// (Phase 3 per SONAR plan + dossier/04_interaction_mapping.md hand columns).
    /// Tip position = right index fingertip; axis = vector from index proximal → index tip.
    /// Pinch (thumb-tip ↔ index-tip < 25 mm) latches `IsPinching` for confirm-style intents.
    [AddComponentMenu("SONAR/Tracking/Hand Drill Tip Source")]
    public sealed class HandDrillTipSource : MonoBehaviour, IDrillTipSource
    {
        [SerializeField] bool _useLeftHand = false;
        [SerializeField] float _pinchThresholdMetres = 0.025f;

        DrillTipPose _current = DrillTipPose.Invalid;
        public DrillTipPose Current => _current;
        public bool IsPinching { get; private set; }

#if UNITY_XR_HANDS
        XRHandSubsystem _subsystem;
        readonly List<XRHandSubsystem> _scratch = new();

        void Update()
        {
            if (_subsystem == null || !_subsystem.running)
            {
                SubsystemManager.GetSubsystems(_scratch);
                _subsystem = _scratch.Count > 0 ? _scratch[0] : null;
                if (_subsystem == null) { _current = DrillTipPose.Invalid; return; }
            }

            var hand = _useLeftHand ? _subsystem.leftHand : _subsystem.rightHand;
            if (!hand.isTracked) { _current = DrillTipPose.Invalid; IsPinching = false; return; }

            var indexTip = hand.GetJoint(XRHandJointID.IndexTip);
            var indexProximal = hand.GetJoint(XRHandJointID.IndexProximal);
            var thumbTip = hand.GetJoint(XRHandJointID.ThumbTip);

            if (!indexTip.TryGetPose(out var tipPose) ||
                !indexProximal.TryGetPose(out var proximalPose))
            {
                _current = DrillTipPose.Invalid;
                return;
            }

            var axis = (tipPose.position - proximalPose.position).normalized;
            _current = new DrillTipPose(tipPose.position, axis, Time.unscaledTime);

            if (thumbTip.TryGetPose(out var thumbPose))
                IsPinching = Vector3.Distance(tipPose.position, thumbPose.position) < _pinchThresholdMetres;
            else
                IsPinching = false;
        }
#else
        // Build without com.unity.xr.hands enabled: keep the script compilable so the
        // demo scene can reference it; Current stays Invalid at runtime.
        void Update() { _current = DrillTipPose.Invalid; }
#endif
    }
}
