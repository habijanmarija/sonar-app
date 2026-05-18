using System;

namespace Sonar.Guidance
{
    /// Watches whether StepRecognizer disagrees with StepGraphRunner.CurrentStep for
    /// longer than a sustain threshold. Fires once per sustained-disagreement episode
    /// and re-arms only when the two sides agree again.
    public sealed class StepDivergenceMonitor
    {
        readonly float _sustainSeconds;
        int _lastDisagreement;
        float _disagreementSince;
        bool _firedThisEpisode;

        public event Action<int, int> OnDivergence;   // (runnerStep, recognisedStep)

        public StepDivergenceMonitor(float sustainSeconds = 2f)
        {
            _sustainSeconds = sustainSeconds;
        }

        public void Push(int runnerStep, int recognisedStep, float unscaledTime)
        {
            if (runnerStep == recognisedStep)
            {
                _firedThisEpisode = false;
                _lastDisagreement = 0;
                return;
            }

            if (_lastDisagreement != recognisedStep)
            {
                _lastDisagreement = recognisedStep;
                _disagreementSince = unscaledTime;
                _firedThisEpisode = false;
                return;
            }

            if (!_firedThisEpisode && unscaledTime - _disagreementSince >= _sustainSeconds)
            {
                _firedThisEpisode = true;
                OnDivergence?.Invoke(runnerStep, recognisedStep);
            }
        }

        public void Reset()
        {
            _lastDisagreement = 0;
            _disagreementSince = 0;
            _firedThisEpisode = false;
        }
    }
}
