using UnityEngine;

namespace Sonar.Tracking
{
    /// Exponential moving average position + slerped axis with an outlier rejection rule.
    /// Pure C# so the smoothing characteristics are testable without Unity Play mode.
    public sealed class PoseSmoother
    {
        readonly float _positionAlpha;
        readonly float _axisAlpha;
        readonly float _maxJumpMetresPerSecond;

        Vector3 _smoothedPosition;
        Vector3 _smoothedAxis = Vector3.forward;
        float _lastTime;
        bool _initialised;

        public PoseSmoother(float positionAlpha = 0.4f, float axisAlpha = 0.3f, float maxJumpMetresPerSecond = 4f)
        {
            _positionAlpha = Mathf.Clamp01(positionAlpha);
            _axisAlpha = Mathf.Clamp01(axisAlpha);
            _maxJumpMetresPerSecond = Mathf.Max(0.1f, maxJumpMetresPerSecond);
        }

        public Vector3 SmoothedPosition => _smoothedPosition;
        public Vector3 SmoothedAxis => _smoothedAxis;

        /// Feed a new raw sample; returns the smoothed value. Samples that imply
        /// instantaneous speed > maxJumpMetresPerSecond are rejected (returned smoothed
        /// remains the previous accepted value).
        public bool Push(Vector3 rawPosition, Vector3 rawAxis, float unscaledTime)
        {
            if (!_initialised)
            {
                _smoothedPosition = rawPosition;
                _smoothedAxis = rawAxis.sqrMagnitude > 1e-8f ? rawAxis.normalized : Vector3.forward;
                _lastTime = unscaledTime;
                _initialised = true;
                return true;
            }

            float dt = Mathf.Max(1e-4f, unscaledTime - _lastTime);
            float instantaneousSpeed = (rawPosition - _smoothedPosition).magnitude / dt;
            if (instantaneousSpeed > _maxJumpMetresPerSecond)
                return false;

            _smoothedPosition = Vector3.Lerp(_smoothedPosition, rawPosition, _positionAlpha);
            var rawN = rawAxis.sqrMagnitude > 1e-8f ? rawAxis.normalized : _smoothedAxis;
            _smoothedAxis = Vector3.Slerp(_smoothedAxis, rawN, _axisAlpha).normalized;
            _lastTime = unscaledTime;
            return true;
        }

        public void Reset()
        {
            _initialised = false;
            _smoothedPosition = Vector3.zero;
            _smoothedAxis = Vector3.forward;
            _lastTime = 0f;
        }
    }
}
