using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Host.Participant
{
    /// JSON-backed store for ParticipantSession records.
    /// Path: Application.persistentDataPath/participants/<id>.json
    /// One file per participant; cumulative across runs so the next session can resume IDs.
    public static class ParticipantStore
    {
        const string SubDirectory = "participants";

        static string Root => Path.Combine(Application.persistentDataPath, SubDirectory);

        public static void EnsureDirectory() => Directory.CreateDirectory(Root);

        public static string PathFor(string participantId) =>
            Path.Combine(Root, $"{participantId}.json");

        public static IReadOnlyList<string> ListParticipantIds()
        {
            EnsureDirectory();
            return Directory.GetFiles(Root, "*.json")
                .Select(p => Path.GetFileNameWithoutExtension(p))
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();
        }

        public static bool Exists(string participantId) =>
            File.Exists(PathFor(participantId));

        public static ParticipantSession Load(string participantId)
        {
            var path = PathFor(participantId);
            if (!File.Exists(path)) return null;
            try
            {
                return JsonConvert.DeserializeObject<ParticipantSession>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Participant] load failed for {participantId}: {e.Message}");
                return null;
            }
        }

        public static bool Save(ParticipantSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.ParticipantId))
            {
                Debug.LogError("[Participant] save called with null or unidentified session.");
                return false;
            }
            try
            {
                EnsureDirectory();
                File.WriteAllText(
                    PathFor(session.ParticipantId),
                    JsonConvert.SerializeObject(session, Formatting.Indented));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Participant] save failed: {e.Message}");
                return false;
            }
        }
    }
}
