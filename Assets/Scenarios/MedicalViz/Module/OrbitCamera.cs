using UnityEngine;
using UnityEngine.InputSystem;

namespace MedicalViz.Module
{
    /// Right-mouse-drag to orbit around `_target`, middle-mouse-drag to pan,
    /// scroll to zoom. Left mouse stays free for the brush + UI interaction.
    [AddComponentMenu("SONAR Host/MedicalViz Orbit Camera")]
    public sealed class OrbitCamera : MonoBehaviour
    {
        [SerializeField] Transform _target;
        [SerializeField] float _orbitSpeed = 0.4f;
        [SerializeField] float _panSpeed = 0.0008f;
        [SerializeField] float _zoomSpeed = 0.08f;
        [SerializeField] float _minDistance = 0.05f;
        [SerializeField] float _maxDistance = 2.0f;

        float _distance;
        float _yaw;
        float _pitch;

        public void SetTarget(Transform target) => _target = target;

        void Start()
        {
            if (_target == null) return;
            var offset = transform.position - _target.position;
            _distance = offset.magnitude;
            _yaw = transform.eulerAngles.y;
            _pitch = transform.eulerAngles.x;
            // The cube is ~30 cm wide; default near-clip of 0.3 m would crop
            // the geometry the moment the user zooms in close. Pull it well in.
            var cam = GetComponent<Camera>();
            if (cam != null) cam.nearClipPlane = 0.01f;
            ApplyTransform();
        }

        void LateUpdate()
        {
            if (_target == null) return;
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                _yaw   += delta.x * _orbitSpeed;
                _pitch -= delta.y * _orbitSpeed;
                _pitch = Mathf.Clamp(_pitch, -85f, 85f);
            }
            if (mouse.middleButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                _target.position += -transform.right * delta.x * _panSpeed * _distance
                                  + -transform.up    * delta.y * _panSpeed * _distance;
            }
            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _distance *= 1f - Mathf.Sign(scroll) * _zoomSpeed;
                _distance = Mathf.Clamp(_distance, _minDistance, _maxDistance);
            }

            ApplyTransform();
        }

        void ApplyTransform()
        {
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = _target.position + rot * (Vector3.back * _distance);
            transform.rotation = rot;
        }
    }
}
