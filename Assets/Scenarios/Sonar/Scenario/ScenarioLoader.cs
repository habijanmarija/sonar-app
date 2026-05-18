using Newtonsoft.Json;
using UnityEngine;

namespace Sonar.Scenario
{
    public static class ScenarioLoader
    {
        const string ResourceFolder = "scenarios";

        /// Load and validate a scenario from a JSON string.
        public static ScenarioConfig FromJson(string json)
        {
            var cfg = JsonConvert.DeserializeObject<ScenarioConfig>(json);
            if (cfg == null) throw new ScenarioConfigException("JSON parsed to null");
            cfg.Validate();
            return cfg;
        }

        /// Load a scenario from `Resources/scenarios/<name>.json` at runtime.
        /// Tolerates both bare names ("bone_drilling_L2_v1") and prefixed names
        /// ("scenarios/bone_drilling_L2_v1") so saved scenes with either form work.
        /// Returns null and logs if the asset is missing.
        public static ScenarioConfig LoadFromResources(string scenarioName)
        {
            var bare = scenarioName != null && scenarioName.StartsWith(ResourceFolder + "/")
                ? scenarioName.Substring(ResourceFolder.Length + 1)
                : scenarioName;
            var asset = Resources.Load<TextAsset>($"{ResourceFolder}/{bare}");
            if (asset == null)
            {
                Debug.LogError($"[Scenario] Resources/{ResourceFolder}/{bare}.json not found");
                return null;
            }
            try
            {
                return FromJson(asset.text);
            }
            catch (ScenarioConfigException e)
            {
                Debug.LogError($"[Scenario] invalid {scenarioName}: {e.Message}");
                return null;
            }
        }
    }
}
