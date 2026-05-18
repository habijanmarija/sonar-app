using System.Collections.Generic;
using Sonar.Scenario;
using UnityEngine;

namespace Sonar.Analytics
{
    /// Composite skill score for one insertion attempt. Composes smoothness + path
    /// efficiency + an error-rate penalty. Returned bands match the dossier's
    /// novice/intermediate/expert thresholds and are tunable per study.
    public readonly struct SkillScore
    {
        public readonly float Smoothness;
        public readonly float PathEfficiency;
        public readonly float ErrorPenalty;
        public readonly float Composite;

        public SkillScore(float smoothness, float pathEfficiency, float errorPenalty, float composite)
        {
            Smoothness = smoothness;
            PathEfficiency = pathEfficiency;
            ErrorPenalty = errorPenalty;
            Composite = composite;
        }
    }

    public static class SkillScorer
    {
        /// errorCount across the insertion step; each error subtracts `errorPenaltyPerEvent`
        /// from the composite, clamped to [0, 1]. Phase 1 default 0.1.
        public static SkillScore Score(
            IReadOnlyList<PoseSample> samples,
            PlannedTrajectory trajectory,
            int errorCount,
            float smoothnessWeight = 0.45f,
            float pathWeight = 0.45f,
            float errorPenaltyPerEvent = 0.1f)
        {
            float smooth = SmoothnessScorer.Score(samples);
            float path = PathEfficiencyScorer.Score(samples, trajectory);
            float penalty = Mathf.Clamp01(errorCount * errorPenaltyPerEvent);
            float composite = Mathf.Clamp01(smoothnessWeight * smooth + pathWeight * path - penalty);
            return new SkillScore(smooth, path, penalty, composite);
        }
    }
}
