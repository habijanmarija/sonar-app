using System.Collections.Generic;
using UnityEngine;

namespace Sonar.Analytics
{
    /// Trajectory smoothness from RMS jerk along the drill-tip pose stream.
    /// Higher score = smoother motion. Definition mirrors dossier/06_data_schema.md.
    ///
    /// Jerk = derivative of acceleration. Computed via finite differences on the
    /// position stream. RMS jerk is inverted + normalised to a 0..1 score against
    /// a configurable reference jerk (default 50 m/s³ → score ~0.5).
    public static class SmoothnessScorer
    {
        public const float DefaultReferenceJerkMetresPerS3 = 50f;

        /// Score in [0, 1]; 1 = perfectly smooth (zero jerk), 0 = highly jerky (≫ reference).
        /// Returns 0 if fewer than 4 samples (jerk needs at least 4 points).
        public static float Score(IReadOnlyList<PoseSample> samples,
                                  float referenceJerk = DefaultReferenceJerkMetresPerS3)
        {
            if (samples == null || samples.Count < 4) return 0f;

            double sumSq = 0;
            int n = 0;

            for (int i = 3; i < samples.Count; i++)
            {
                float dt1 = samples[i - 2].TimeSeconds - samples[i - 3].TimeSeconds;
                float dt2 = samples[i - 1].TimeSeconds - samples[i - 2].TimeSeconds;
                float dt3 = samples[i].TimeSeconds - samples[i - 1].TimeSeconds;
                if (dt1 < 1e-4f || dt2 < 1e-4f || dt3 < 1e-4f) continue;

                var v1 = (samples[i - 2].Position - samples[i - 3].Position) / dt1;
                var v2 = (samples[i - 1].Position - samples[i - 2].Position) / dt2;
                var v3 = (samples[i].Position - samples[i - 1].Position) / dt3;
                var a1 = (v2 - v1) / dt2;
                var a2 = (v3 - v2) / dt3;
                var jerk = (a2 - a1) / dt3;

                sumSq += jerk.sqrMagnitude;
                n++;
            }

            if (n == 0) return 0f;
            double rms = System.Math.Sqrt(sumSq / n);
            float refJ = Mathf.Max(1f, referenceJerk);
            return (float)(refJ / (refJ + rms));
        }
    }
}
