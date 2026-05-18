using System;
using Sonar.Scenario;

namespace Sonar.Guidance
{
    /// Drives current step number for the runtime. Advancing is initiated by the
    /// GuidanceController based on per-step success conditions (lateral green hold for
    /// `hold_seconds`, etc.). This class just enforces the step graph invariants and
    /// surfaces the OnStepTransition callback.
    public sealed class StepGraphRunner
    {
        readonly ScenarioConfig _config;
        public int CurrentStep { get; private set; } = 1;

        public event Action<int, int> OnStepTransition;

        public StepGraphRunner(ScenarioConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (_config.TotalSteps < 1)
                throw new ArgumentException("ScenarioConfig must have at least one step");
        }

        public bool IsFinalStep => CurrentStep >= _config.TotalSteps;
        public StepDefinition CurrentDefinition => _config.Step(CurrentStep);

        /// Advance to the next step; raises OnStepTransition with (newStep, totalSteps).
        /// No-op if already on the final step.
        public bool Advance()
        {
            if (IsFinalStep) return false;
            CurrentStep++;
            OnStepTransition?.Invoke(CurrentStep, _config.TotalSteps);
            return true;
        }

        public void Reset()
        {
            CurrentStep = 1;
        }

        /// Whether the given error code is legal for the current step (dossier rule).
        public bool IsErrorLegalForCurrentStep(string code) =>
            CurrentDefinition?.AllowsError(code) ?? false;
    }
}
