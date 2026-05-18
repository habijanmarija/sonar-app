using System.Collections.Generic;
using Newtonsoft.Json;

namespace Sonar.Scenario
{
    /// One step in the Phase 1 SONAR 8-step graph (dossier/01_task_model.md).
    /// `Errors` enumerates the failure modes legal for this step; the runtime can
    /// emit only these `error_code`s while `current_step == Number`.
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class StepDefinition
    {
        [JsonProperty("number")] public int Number;
        [JsonProperty("name")] public string Name;
        [JsonProperty("hold_seconds")] public float HoldSeconds;
        [JsonProperty("errors")] public List<string> Errors;

        public bool AllowsError(string code) => Errors != null && Errors.Contains(code);
    }
}
