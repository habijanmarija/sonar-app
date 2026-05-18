using Sonar.Scenario;
using Sonar.Tracking;
using UnityEngine;

namespace Sonar.Guidance
{
    /// Rule-based inference of "what step the user is actually doing" from the live drill-tip
    /// pose stream alone. Independent of StepGraphRunner.CurrentStep; comparing the two
    /// catches drift between the runner state and reality (e.g. user has finished inserting
    /// but the runner is still parked on step 4 because the green-dwell wasn't met).
    ///
    /// Returned step numbers match the 8-step graph in dossier/01_task_model.md.
    public sealed class StepRecognizer
    {
        readonly PlannedTrajectory _trajectory;
        readonly float _entryProximityM;
        readonly float _atDepthToleranceM;
        readonly float _alignedAngleDeg;
        readonly float _movingSpeedMps;

        Vector3 _lastPosition;
        float _lastTime;
        bool _initialised;

        public int LastRecognisedStep { get; private set; } = 1;
        public float DepthAlongMetresPerSecond { get; private set; }

        public StepRecognizer(
            PlannedTrajectory trajectory,
            float entryProximityM = 0.005f,
            float atDepthToleranceM = 0.003f,
            float alignedAngleDeg = 10f,
            float movingSpeedMps = 0.005f)
        {
            _trajectory = trajectory;
            _entryProximityM = entryProximityM;
            _atDepthToleranceM = atDepthToleranceM;
            _alignedAngleDeg = alignedAngleDeg;
            _movingSpeedMps = movingSpeedMps;
        }

        /// Push the latest pose; returns the recognised step (1..8).
        public int Push(in DrillTipPose tip)
        {
            if (!tip.IsValid || _trajectory == null) return LastRecognisedStep;

            float depthAlong = _trajectory.DepthAlong(tip.Position);

            if (_initialised)
            {
                float dt = Mathf.Max(1e-4f, tip.UnscaledTime - _lastTime);
                float lastDepth = _trajectory.DepthAlong(_lastPosition);
                DepthAlongMetresPerSecond = (depthAlong - lastDepth) / dt;
            }
            else
            {
                _initialised = true;
            }
            _lastPosition = tip.Position;
            _lastTime = tip.UnscaledTime;

            float lateral = _trajectory.LateralError(tip.Position);
            float angular = _trajectory.AngularErrorDeg(tip.Axis);
            float distanceFromEntry = Vector3.Distance(tip.Position, _trajectory.Entry);
            float depthError = depthAlong - _trajectory.PlannedDepthM;
            bool aligned = angular < _alignedAngleDeg;
            bool atEntry = distanceFromEntry < _entryProximityM;
            bool atTarget = Mathf.Abs(depthError) < _atDepthToleranceM;
            bool advancing = DepthAlongMetresPerSecond > _movingSpeedMps && lateral < _entryProximityM * 2f;
            bool retreating = DepthAlongMetresPerSecond < -_movingSpeedMps;

            // Order matters: more-specific predicates first.
            int recognised;
            if (atTarget && Mathf.Abs(DepthAlongMetresPerSecond) < _movingSpeedMps) recognised = 6;
            else if (advancing) recognised = 5;
            else if (retreating) recognised = 7;
            else if (atEntry && aligned) recognised = 4;
            else if (atEntry) recognised = 3;
            else if (distanceFromEntry > 0.05f) recognised = 1;
            else recognised = LastRecognisedStep;

            LastRecognisedStep = recognised;
            return recognised;
        }
    }
}
