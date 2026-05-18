using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Host.Settings
{
    /// Persistent host settings. Lives in Application.persistentDataPath/settings.json.
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class AppSettings
    {
        [JsonProperty("backend_url")] public string BackendUrl = "ws://127.0.0.1:8765";
        [JsonProperty("rest_url")] public string RestUrl = "http://127.0.0.1:8000";
        [JsonProperty("default_mode")] public string DefaultMode = "Novice";
        [JsonProperty("participant_id_strategy")] public string ParticipantIdStrategy = "sequential";
        [JsonProperty("auto_open_results_after_session")] public bool AutoOpenResults = true;

        const string Filename = "settings.json";

        public static AppSettings Load()
        {
            try
            {
                var path = SettingsPath;
                if (!File.Exists(path)) return new AppSettings();
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Settings] load failed; using defaults: {e.Message}");
                return new AppSettings();
            }
        }

        public void Save()
        {
            try
            {
                var path = SettingsPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Settings] save failed: {e.Message}");
            }
        }

        static string SettingsPath =>
            Path.Combine(Application.persistentDataPath, Filename);
    }
}
