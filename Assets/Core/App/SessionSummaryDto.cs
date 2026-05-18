using System.Collections.Generic;
using Newtonsoft.Json;

namespace Host.App
{
    /// Shape-compatible mirror of the Python `metrics.py` SessionSummary dict
    /// (dossier/06_data_schema.md). Used by the Results browser to render summaries
    /// returned from the backend's /api/sessions/{id} endpoint.
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class SessionSummaryDto
    {
        [JsonProperty("session_id")] public string SessionId;
        [JsonProperty("participant_id")] public string ParticipantId;
        [JsonProperty("scenario")] public string Scenario;
        [JsonProperty("mode")] public string Mode;
        [JsonProperty("vendor")] public string Vendor;
        [JsonProperty("started_utc")] public string StartedUtc;
        [JsonProperty("duration_seconds")] public double DurationSeconds;

        [JsonProperty("calibration")] public CalibrationBlock Calibration;
        [JsonProperty("step_outcomes")] public List<StepOutcome> StepOutcomes;
        [JsonProperty("global")] public GlobalBlock Global;

        [JsonObject(MemberSerialization.OptIn)]
        public sealed class CalibrationBlock
        {
            [JsonProperty("initial_tre_mm")] public double? InitialTreMm;
            [JsonProperty("recalibration_count")] public int RecalibrationCount;
            [JsonProperty("min_tre_mm")] public double? MinTreMm;
            [JsonProperty("max_tre_mm")] public double? MaxTreMm;
        }

        [JsonObject(MemberSerialization.OptIn)]
        public sealed class StepOutcome
        {
            [JsonProperty("step")] public int Step;
            [JsonProperty("first_entered_at_s")] public double FirstEnteredAtS;
            [JsonProperty("completed_at_s")] public double CompletedAtS;
            [JsonProperty("duration_s")] public double DurationS;
            [JsonProperty("errors")] public Dictionary<string, int> Errors;
            [JsonProperty("reset_offers")] public int ResetOffers;
            [JsonProperty("reset_accepts")] public int ResetAccepts;
        }

        [JsonObject(MemberSerialization.OptIn)]
        public sealed class GlobalBlock
        {
            [JsonProperty("total_errors")] public int TotalErrors;
            [JsonProperty("trajectory_smoothness_score")] public double TrajectorySmoothnessScore;
            [JsonProperty("path_efficiency")] public double? PathEfficiency;
            [JsonProperty("task_completion")] public bool TaskCompletion;
        }
    }
}
