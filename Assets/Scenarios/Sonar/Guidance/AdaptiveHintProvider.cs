using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Sonar.Guidance
{
    /// HintProvider extension that supports a *list* of hints per (step, error_code) key
    /// and rotates through them on repeated firings at the same step. Reset on step
    /// transition so a new step starts at the first hint.
    ///
    /// JSON schema accepts both legacy strings and new arrays:
    ///   "3:entry_point_error": "Move closer."
    ///   "5:trajectory_deviation": [
    ///     "You are drifting laterally.",
    ///     "Return to the green corridor; reduce lateral angle.",
    ///     "Stop, recentre on the planned entry, and re-establish the trajectory."
    ///   ]
    public sealed class AdaptiveHintProvider
    {
        readonly IDictionary<string, List<string>> _hints;
        readonly IDictionary<string, int> _cursors = new Dictionary<string, int>();
        int _stepAnchor = 1;

        AdaptiveHintProvider(IDictionary<string, List<string>> hints)
        {
            _hints = hints ?? new Dictionary<string, List<string>>();
        }

        /// Call when StepGraphRunner advances; resets the per-key rotation cursor.
        public void OnStepChanged(int newStep)
        {
            _stepAnchor = newStep;
            _cursors.Clear();
        }

        public string Get(int step, string errorCode)
        {
            var key = $"{step}:{errorCode}";
            if (!_hints.TryGetValue(key, out var list) || list == null || list.Count == 0)
                return string.Empty;

            _cursors.TryGetValue(key, out var i);
            var hint = list[Mathf.Clamp(i, 0, list.Count - 1)];
            _cursors[key] = Mathf.Min(i + 1, list.Count - 1);
            return hint;
        }

        public static AdaptiveHintProvider LoadFromResources(string resourcePath = "Hints")
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[Guidance] Resources/{resourcePath}.json not found; hints will be empty.");
                return new AdaptiveHintProvider(null);
            }
            return FromJson(asset.text);
        }

        public static AdaptiveHintProvider FromJson(string json)
        {
            var parsed = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(json);
            var normalised = new Dictionary<string, List<string>>();
            if (parsed != null)
            {
                foreach (var kv in parsed)
                {
                    if (kv.Value.Type == JTokenType.String)
                        normalised[kv.Key] = new List<string> { kv.Value.Value<string>() };
                    else if (kv.Value.Type == JTokenType.Array)
                        normalised[kv.Key] = kv.Value.ToObject<List<string>>();
                }
            }
            return new AdaptiveHintProvider(normalised);
        }
    }
}
