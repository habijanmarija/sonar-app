using Newtonsoft.Json;
using UnityEngine;

namespace Sonar.Scenario
{
    /// One planned pedicle-screw trajectory in phantom-space (Unity left-handed metres).
    /// Entry + axis define an infinite ray; depth bounds gate steps 5–6.
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class PlannedTrajectory
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("vertebra")] public string Vertebra;
        [JsonProperty("side")] public string Side;

        [JsonProperty("entry_m")] public float[] EntryRaw;
        [JsonProperty("axis_unit")] public float[] AxisRaw;

        [JsonProperty("planned_depth_m")] public float PlannedDepthM;
        [JsonProperty("min_depth_m")] public float MinDepthM;
        [JsonProperty("max_depth_m")] public float MaxDepthM;

        public Vector3 Entry => new(EntryRaw[0], EntryRaw[1], EntryRaw[2]);
        public Vector3 Axis => new Vector3(AxisRaw[0], AxisRaw[1], AxisRaw[2]).normalized;

        /// Closest point along the planned ray to `tip` (phantom-space).
        public Vector3 ClosestPoint(Vector3 tip)
        {
            var d = Vector3.Dot(tip - Entry, Axis);
            return Entry + Axis * d;
        }

        /// Signed depth of `tip` along the planned axis from the entry point.
        public float DepthAlong(Vector3 tip) => Vector3.Dot(tip - Entry, Axis);

        /// Perpendicular distance from `tip` to the planned ray.
        public float LateralError(Vector3 tip) => Vector3.Distance(tip, ClosestPoint(tip));

        /// Angular error in degrees between `tipAxis` and the planned axis.
        public float AngularErrorDeg(Vector3 tipAxis) =>
            Vector3.Angle(tipAxis.normalized, Axis);
    }
}
