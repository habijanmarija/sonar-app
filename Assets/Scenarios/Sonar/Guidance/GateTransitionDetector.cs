using Sonar.Scenario;

namespace Sonar.Guidance
{
    /// Tracks how long a metric has been in Red. Fires an error event when the sustain
    /// time crosses `_redSustainSeconds`. Hysteresis prevents oscillation: once an error
    /// has fired, the next firing requires returning to Green and then re-entering Red.
    public sealed class GateTransitionDetector
    {
        readonly float _redSustainSeconds;
        Gate _last = Gate.Green;
        float _redEnteredAt;
        bool _firedThisDwell;

        public GateTransitionDetector(float redSustainSeconds = 0.3f)
        {
            _redSustainSeconds = redSustainSeconds;
        }

        /// Feed the latest gate + current time. Returns true exactly once per
        /// "entered red and stayed red" event, after the sustain threshold elapses.
        public bool Push(Gate gate, float unscaledTime)
        {
            if (gate != Gate.Red)
            {
                _last = gate;
                if (gate == Gate.Green) _firedThisDwell = false;
                return false;
            }

            if (_last != Gate.Red)
            {
                _redEnteredAt = unscaledTime;
                _last = Gate.Red;
                return false;
            }

            if (!_firedThisDwell && unscaledTime - _redEnteredAt >= _redSustainSeconds)
            {
                _firedThisDwell = true;
                return true;
            }
            return false;
        }

        public void Reset()
        {
            _last = Gate.Green;
            _redEnteredAt = 0f;
            _firedThisDwell = false;
        }
    }
}
