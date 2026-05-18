using System.Collections.Generic;
using Sonar.Scenario;
using UnityEngine;

namespace Sonar.Analytics
{
    /// Ratio of the ideal straight-line trajectory length to the realised drill-tip
    /// path length over the insertion step. 1.0 = perfectly straight along the
    /// planned axis; lower = more wandering. Dossier/06_data_schema.md `path_efficiency`.
    public static class PathEfficiencyScorer
    {
        /// Score in [0, 1]. Returns 0 if no motion was recorded.
        public static float Score(IReadOnlyList<PoseSample> samples, PlannedTrajectory trajectory)
        {
            if (samples == null || samples.Count < 2 || trajectory == null) return 0f;

            float realisedPath = 0f;
            for (int i = 1; i < samples.Count; i++)
                realisedPath += Vector3.Distance(samples[i - 1].Position, samples[i].Position);

            if (realisedPath < 1e-6f) return 0f;

            float idealPath = Mathf.Abs(
                trajectory.DepthAlong(samples[samples.Count - 1].Position)
                - trajectory.DepthAlong(samples[0].Position));

            return Mathf.Clamp01(idealPath / realisedPath);
        }
    }
}
