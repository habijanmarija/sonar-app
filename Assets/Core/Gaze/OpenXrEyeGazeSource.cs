using Host.Platform;
using UnityEngine;

namespace Host.Gaze
{
    /// OpenXR eye-gaze source — concrete vendor wiring is intentionally a TODO because
    /// Quest 3 (XR_EXT_eye_gaze_interaction) and Focus Vision (VIVE OpenXR + Tobii)
    /// have diverged in 1.x release timing. This script:
    ///   1. Refuses to enable if IXrPlatform.HasEyeTracking is false (returns Invalid).
    ///   2. Falls back to head pose as a coarse gaze proxy until the OpenXR ext is wired.
    ///
    /// When wiring the real ext: subscribe to the OpenXR eye-gaze input action and
    /// replace HeadPoseFallback with the pose returned by the action's ReadValue.
    [AddComponentMenu("SONAR/Gaze/OpenXR Eye Gaze Source")]
    public sealed class OpenXrEyeGazeSource : MonoBehaviour, IEyeGazeSource
    {
        [SerializeField] Transform _headTransform;
        EyeGaze _current = EyeGaze.Invalid;
        public EyeGaze Current => _current;

        void Update()
        {
            if (XrPlatform.Current == null || !XrPlatform.Current.HasEyeTracking)
            {
                _current = EyeGaze.Invalid;
                return;
            }

            // TODO(Phase 3): replace with OpenXR XR_EXT_eye_gaze_interaction action read.
            // For now, the head's forward vector is a coarse stand-in so downstream UX
            // (e.g. "what is the user looking at?") has *something* to work with on
            // headsets that *do* report HasEyeTracking but whose runtime action wiring
            // is not yet complete.
            var head = _headTransform != null ? _headTransform : (Camera.main != null ? Camera.main.transform : null);
            if (head == null) { _current = EyeGaze.Invalid; return; }
            _current = new EyeGaze(head.position, head.forward, Time.unscaledTime);
        }
    }
}
