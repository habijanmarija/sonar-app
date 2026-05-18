using UnityEngine;
using UnityEngine.InputSystem;

namespace Host.Gaze
{
    /// Mock IEyeGazeSource for Linux Editor flat-mode work: gaze direction is the
    /// camera-relative ray through the mouse cursor. Useful for iterating on gaze-driven
    /// UX without a headset.
    [AddComponentMenu("SONAR/Gaze/Editor Eye Gaze Source")]
    public sealed class EditorEyeGazeSource : MonoBehaviour, IEyeGazeSource
    {
        [SerializeField] Camera _camera;
        EyeGaze _current = EyeGaze.Invalid;
        public EyeGaze Current => _current;

        void Update()
        {
            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null) { _current = EyeGaze.Invalid; return; }
            var mouse = Mouse.current;
            if (mouse == null) { _current = EyeGaze.Invalid; return; }
            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            _current = new EyeGaze(ray.origin, ray.direction, Time.unscaledTime);
        }
    }
}
