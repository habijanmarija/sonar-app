using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Sonar.Guidance
{
    /// Loads `Resources/Hints.json` and looks up hint text by (step, error_code).
    /// Phase 1: deterministic lookup. Phase 2: same surface, swap implementation for
    /// adaptive selection based on per-participant error history.
    public sealed class HintProvider
    {
        readonly IDictionary<string, string> _hints;

        HintProvider(IDictionary<string, string> hints)
        {
            _hints = hints ?? new Dictionary<string, string>();
        }

        /// Returns hint text for (step, errorCode), or empty if no hint configured.
        public string Get(int step, string errorCode)
        {
            var key = $"{step}:{errorCode}";
            return _hints.TryGetValue(key, out var v) ? v : string.Empty;
        }

        public static HintProvider LoadFromResources(string resourcePath = "Hints")
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[Guidance] Resources/{resourcePath}.json not found; hints will be empty.");
                return new HintProvider(null);
            }
            return FromJson(asset.text);
        }

        public static HintProvider FromJson(string json)
        {
            var raw = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            return new HintProvider(raw);
        }
    }
}
