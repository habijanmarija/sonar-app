using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Sonar.Scenario
{
    /// Full Phase 1 scenario: one planned trajectory + the 8-step graph + thresholds.
    /// Phantom + landmark coordinates are produced separately under `phantom/assets/`
    /// and referenced by `phantom_version` for the runtime to refuse mismatches.
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ScenarioConfig
    {
        [JsonProperty("scenario")] public string Scenario = "bone_drilling";
        [JsonProperty("config_version")] public string ConfigVersion;
        [JsonProperty("phantom_version")] public string PhantomVersion;

        [JsonProperty("trajectory")] public PlannedTrajectory Trajectory;
        [JsonProperty("steps")] public List<StepDefinition> Steps;
        [JsonProperty("thresholds")] public ThresholdProfile Thresholds;

        /// Optional per-mode threshold overrides. If null/empty, all modes use `Thresholds`.
        /// Keys are SessionMode enum names (case-insensitive).
        [JsonProperty("mode_thresholds", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, ThresholdProfile> ModeThresholds;

        /// Returns the threshold profile for `mode`, falling back to `Thresholds` when
        /// no mode-specific override is configured.
        public ThresholdProfile ThresholdsFor(SessionMode mode)
        {
            if (ModeThresholds != null && ModeThresholds.TryGetValue(mode.ToString(), out var p) && p != null)
                return p;
            return Thresholds;
        }

        public int TotalSteps => Steps?.Count ?? 0;

        public StepDefinition Step(int number) =>
            Steps?.FirstOrDefault(s => s.Number == number);

        /// Throws if the config is internally inconsistent — call once at load time.
        public void Validate()
        {
            if (string.IsNullOrEmpty(Scenario))
                throw new ScenarioConfigException("scenario is empty");
            if (Trajectory == null)
                throw new ScenarioConfigException("trajectory missing");
            if (Trajectory.AxisRaw == null || Trajectory.AxisRaw.Length != 3)
                throw new ScenarioConfigException("trajectory.axis_unit must be a 3-vector");
            if (Trajectory.EntryRaw == null || Trajectory.EntryRaw.Length != 3)
                throw new ScenarioConfigException("trajectory.entry_m must be a 3-vector");
            if (Trajectory.Axis.sqrMagnitude < 1e-6f)
                throw new ScenarioConfigException("trajectory.axis_unit is zero");
            if (Trajectory.MinDepthM > Trajectory.MaxDepthM)
                throw new ScenarioConfigException("trajectory depth bounds inverted");
            if (Trajectory.PlannedDepthM < Trajectory.MinDepthM ||
                Trajectory.PlannedDepthM > Trajectory.MaxDepthM)
                throw new ScenarioConfigException("planned_depth_m outside [min, max]");
            if (Steps == null || Steps.Count == 0)
                throw new ScenarioConfigException("steps missing");

            var expected = 1;
            foreach (var s in Steps.OrderBy(s => s.Number))
            {
                if (s.Number != expected)
                    throw new ScenarioConfigException(
                        $"step numbers must be 1..N contiguous; got {s.Number}, expected {expected}");
                expected++;
            }
            if (Thresholds == null)
                throw new ScenarioConfigException("thresholds missing");
        }
    }

    public sealed class ScenarioConfigException : System.Exception
    {
        public ScenarioConfigException(string message) : base(message) { }
    }
}
