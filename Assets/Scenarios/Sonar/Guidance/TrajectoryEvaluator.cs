using Sonar.Scenario;
using Sonar.Tracking;
using UnityEngine;

namespace Sonar.Guidance
{
    /// Per-frame derived metrics for one drill-tip sample against one planned trajectory.
    public readonly struct EvaluationFrame
    {
        public readonly float LateralM;
        public readonly float AngularDeg;
        public readonly float DepthAlongM;
        public readonly float DepthErrorM;
        public readonly Gate LateralGate;
        public readonly Gate AngularGate;
        public readonly Gate DepthGate;

        public EvaluationFrame(
            float lateralM, float angularDeg, float depthAlongM, float depthErrorM,
            Gate lateralGate, Gate angularGate, Gate depthGate)
        {
            LateralM = lateralM;
            AngularDeg = angularDeg;
            DepthAlongM = depthAlongM;
            DepthErrorM = depthErrorM;
            LateralGate = lateralGate;
            AngularGate = angularGate;
            DepthGate = depthGate;
        }

        /// Worst-of-three gate; useful for the tip marker colour.
        public Gate Worst =>
            LateralGate >= AngularGate
                ? (LateralGate >= DepthGate ? LateralGate : DepthGate)
                : (AngularGate >= DepthGate ? AngularGate : DepthGate);
    }

    public static class TrajectoryEvaluator
    {
        public static EvaluationFrame Evaluate(
            in DrillTipPose tip,
            PlannedTrajectory trajectory,
            ThresholdProfile thresholds)
        {
            if (!tip.IsValid || trajectory == null || thresholds == null)
                return new EvaluationFrame(0, 0, 0, 0, Gate.Red, Gate.Red, Gate.Red);

            float lateral = trajectory.LateralError(tip.Position);
            float angular = trajectory.AngularErrorDeg(tip.Axis);
            float depthAlong = trajectory.DepthAlong(tip.Position);
            float depthError = depthAlong - trajectory.PlannedDepthM;

            return new EvaluationFrame(
                lateralM: lateral,
                angularDeg: angular,
                depthAlongM: depthAlong,
                depthErrorM: depthError,
                lateralGate: thresholds.ClassifyLateral(lateral),
                angularGate: thresholds.ClassifyAngular(angular),
                depthGate: thresholds.ClassifyDepthError(depthError));
        }
    }
}
