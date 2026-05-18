using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Host.Participant
{
    /// Per-trainee, per-run context that flows from the host shell into the loaded scenario.
    /// Created at participant intake; mutated by calibration; consumed by the scenario; copied
    /// into telemetry `custom_data` so post-hoc analysis can reconstruct what was happening.
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ParticipantSession
    {
        [JsonProperty("participant_id")] public string ParticipantId;
        [JsonProperty("mode")] public string Mode;       // "Novice" | "Intermediate" | "Expert"
        [JsonProperty("scenario_id")] public string ScenarioId;
        [JsonProperty("session_uuid")] public string SessionUuid;
        [JsonProperty("started_utc")] public DateTime StartedUtc;

        [JsonProperty("calibration", NullValueHandling = NullValueHandling.Ignore)]
        public CalibrationResult Calibration;

        [JsonProperty("intake", NullValueHandling = NullValueHandling.Ignore)]
        public ParticipantIntake Intake;

        [JsonProperty("custom_data")]
        public Dictionary<string, string> CustomData = new();

        public static ParticipantSession New(string participantId, string mode, string scenarioId) => new()
        {
            ParticipantId = participantId,
            Mode = mode,
            ScenarioId = scenarioId,
            SessionUuid = Guid.NewGuid().ToString("N"),
            StartedUtc = DateTime.UtcNow,
        };
    }

    /// Free-text intake form data collected before the session.
    /// Per `dossier/08_evaluation_plan.md` inclusion / exclusion rules.
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ParticipantIntake
    {
        [JsonProperty("age_band")] public string AgeBand;            // "18-25", "26-35", "36-50", "50+"
        [JsonProperty("handedness")] public string Handedness;       // "left" | "right" | "ambidextrous"
        [JsonProperty("prior_training")] public string PriorTraining; // "none" | "observer" | "trainee" | "expert"
        [JsonProperty("vision_corrected")] public bool VisionCorrected;
        [JsonProperty("vr_susceptible")] public bool VrSusceptible;
        [JsonProperty("photosensitive")] public bool Photosensitive;
        [JsonProperty("consent_given")] public bool ConsentGiven;
        [JsonProperty("consent_utc")] public DateTime ConsentUtc;
    }
}
