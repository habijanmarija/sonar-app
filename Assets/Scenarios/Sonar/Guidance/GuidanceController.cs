using System;
using Sonar.Scenario;
using Host.Telemetry;
using Sonar.Tracking;
using UnityEngine;

namespace Sonar.Guidance
{
    /// Phase 1 guidance pipeline. Per Update:
    ///   1. Read DrillTipPose from the configured IDrillTipSource.
    ///   2. Evaluate against the active PlannedTrajectory.
    ///   3. Per-metric sustain-red detection fires `error` events through SessionRunner.
    ///   4. Step-completion rules drive StepGraphRunner.Advance() + step_transition emit.
    ///   5. Latest EvaluationFrame + Hint surface via events for the visualization layer.
    [AddComponentMenu("SONAR/Guidance/Guidance Controller")]
    public sealed class GuidanceController : MonoBehaviour
    {
        [SerializeField] string _scenarioResourceName = "bone_drilling_L2_v1";
        [SerializeField] MonoBehaviour _drillTipSourceBehaviour; // anything implementing IDrillTipSource
        [SerializeField] SessionRunner _sessionRunner;
        [SerializeField] SessionMode _mode = SessionMode.Novice;

        [Header("Step completion (tunable)")]
        [SerializeField, Range(0.1f, 5f)] float _stepGreenDwellSeconds = 0.5f;

        public SessionMode Mode => _mode;
        public ThresholdProfile ActiveThresholds { get; private set; }

        public ScenarioConfig Config { get; private set; }
        public StepGraphRunner Runner { get; private set; }
        public EvaluationFrame Latest { get; private set; }
        public Gate LatestWorstGate { get; private set; } = Gate.Green;
        public string LatestHint { get; private set; } = "";

        public event Action<EvaluationFrame> OnFrame;
        public event Action<int, int> OnStepTransition;
        public event Action<string, string> OnErrorFired;

        IDrillTipSource _source;
        AdaptiveHintProvider _hints;
        GateTransitionDetector _lateralDet;
        GateTransitionDetector _angularDet;
        GateTransitionDetector _depthDet;
        StepRecognizer _recognizer;
        StepDivergenceMonitor _divergence;
        float _greenSince;
        bool _sessionBegun;

        void Awake()
        {
            Config = ScenarioLoader.LoadFromResources(_scenarioResourceName);
            if (Config == null)
            {
                enabled = false;
                return;
            }
            ActiveThresholds = Config.ThresholdsFor(_mode);
            Runner = new StepGraphRunner(Config);
            Runner.OnStepTransition += HandleStepTransition;

            _hints = AdaptiveHintProvider.LoadFromResources();
            _lateralDet = new GateTransitionDetector();
            _angularDet = new GateTransitionDetector();
            _depthDet = new GateTransitionDetector();
            _recognizer = new StepRecognizer(Config.Trajectory);
            _divergence = new StepDivergenceMonitor();
            _divergence.OnDivergence += HandleDivergence;

            _source = _drillTipSourceBehaviour as IDrillTipSource;
            if (_source == null)
                Debug.LogError("[Guidance] _drillTipSourceBehaviour does not implement IDrillTipSource");
        }

        /// Call once the SessionRunner is connected. Emits a user_action recording
        /// the session mode (so post-hoc analysis knows which threshold profile applied),
        /// then the initial step_transition for step 1.
        public void BeginSession()
        {
            if (_sessionBegun) return;
            _sessionBegun = true;
            if (_sessionRunner != null && Runner != null)
            {
                _sessionRunner.EmitUserAction("mode_selected", new System.Collections.Generic.Dictionary<string, object>
                {
                    ["mode"] = _mode.ToString(),
                });
                _sessionRunner.EmitStepTransition(Runner.CurrentStep, Config.TotalSteps);
            }
            _greenSince = Time.unscaledTime;
        }

        /// Allow demo / Editor menu code to set the mode before Awake runs.
        public void SetMode(SessionMode mode)
        {
            _mode = mode;
            if (Config != null) ActiveThresholds = Config.ThresholdsFor(mode);
        }

        void Update()
        {
            if (_source == null || !_sessionBegun) return;

            var tip = _source.Current;
            var frame = TrajectoryEvaluator.Evaluate(in tip, Config.Trajectory, ActiveThresholds);
            Latest = frame;
            LatestWorstGate = frame.Worst;
            OnFrame?.Invoke(frame);

            float t = Time.unscaledTime;

            if (_lateralDet.Push(frame.LateralGate, t)) Fire("trajectory_deviation", "lateral");
            if (_angularDet.Push(frame.AngularGate, t)) Fire("trajectory_deviation", "angular");
            if (_depthDet.Push(frame.DepthGate, t))
                Fire(frame.DepthErrorM > 0 ? "excessive_depth" : "insufficient_depth", "depth");

            int recognised = _recognizer.Push(in tip);
            _divergence.Push(Runner.CurrentStep, recognised, t);

            UpdateStepProgression(frame, t);
        }

        void HandleDivergence(int runnerStep, int recognisedStep)
        {
            var detail = $"runner={runnerStep} recognised={recognisedStep}";
            if (_sessionRunner != null)
                _sessionRunner.EmitUserAction("step_divergence", new System.Collections.Generic.Dictionary<string, object>
                {
                    ["runner_step"] = runnerStep,
                    ["recognised_step"] = recognisedStep,
                });
            Debug.Log($"[Guidance] step divergence: {detail}");
        }

        void UpdateStepProgression(in EvaluationFrame frame, float t)
        {
            var step = Runner.CurrentDefinition;
            if (step == null) return;

            // Phase 1 completion rule: all three metrics in Green, sustained for the
            // greater of step.HoldSeconds and _stepGreenDwellSeconds.
            bool allGreen = frame.LateralGate == Gate.Green
                            && frame.AngularGate == Gate.Green
                            && frame.DepthGate == Gate.Green;

            if (!allGreen)
            {
                _greenSince = t;
                return;
            }

            float required = Mathf.Max(step.HoldSeconds, _stepGreenDwellSeconds);
            if (t - _greenSince >= required && !Runner.IsFinalStep)
            {
                Runner.Advance();
                _greenSince = t;
                _lateralDet.Reset();
                _angularDet.Reset();
                _depthDet.Reset();
            }
        }

        void Fire(string errorCode, string detailTag)
        {
            if (!Runner.IsErrorLegalForCurrentStep(errorCode))
                return;
            var hint = _hints?.Get(Runner.CurrentStep, errorCode) ?? "";
            LatestHint = hint;
            OnErrorFired?.Invoke(errorCode, hint);

            if (_sessionRunner != null)
                _sessionRunner.EmitError(errorCode, $"{detailTag} step={Runner.CurrentStep}");
        }

        void HandleStepTransition(int newStep, int total)
        {
            OnStepTransition?.Invoke(newStep, total);
            _hints?.OnStepChanged(newStep);
            if (_sessionRunner != null)
                _sessionRunner.EmitStepTransition(newStep, total);
        }

        public void SetDrillTipSource(IDrillTipSource source) => _source = source;
        public void SetSessionRunner(SessionRunner runner) => _sessionRunner = runner;
    }
}
