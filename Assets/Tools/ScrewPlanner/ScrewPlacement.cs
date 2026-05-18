using System;
using System.Collections.Generic;
using System.IO;
using Host.Patients;
using Newtonsoft.Json;
using UnityEngine;

namespace Host.Tools.ScrewPlanner
{
    /// One persisted screw placement: entry + tip in world space, plus the
    /// target segment id, last computed coverage, and a timestamp. Stored
    /// per-patient at <patient>/screws.json next to annotations.json /
    /// segmentation.json. World coordinates are saved as-is — the volume cube
    /// is always placed at the workstation's origin so world == cube-relative
    /// across sessions on the same patient.
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ScrewPlacement
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("segment_id")] public string SegmentId;
        [JsonProperty("segment_label")] public string SegmentLabel;
        [JsonProperty("color_hex")] public string ColorHex;
        [JsonProperty("entry_m")] public float[] EntryRaw;
        [JsonProperty("tip_m")]   public float[] TipRaw;
        [JsonProperty("coverage_pct")] public float CoveragePct;
        [JsonProperty("placed_utc")] public DateTime PlacedUtc;

        public Vector3 Entry => new(EntryRaw[0], EntryRaw[1], EntryRaw[2]);
        public Vector3 Tip   => new(TipRaw[0],   TipRaw[1],   TipRaw[2]);
        public void SetEntry(Vector3 v) { EntryRaw = new[] { v.x, v.y, v.z }; }
        public void SetTip(Vector3 v)   { TipRaw   = new[] { v.x, v.y, v.z }; }

        public Color Color
        {
            get
            {
                if (string.IsNullOrEmpty(ColorHex) || !ColorUtility.TryParseHtmlString(ColorHex, out var c))
                    return new Color(0.8f, 0.8f, 0.8f, 1f);
                return c;
            }
            set => ColorHex = "#" + ColorUtility.ToHtmlStringRGB(value);
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ScrewPlacementDoc
    {
        [JsonProperty("patient_id")] public string PatientId;
        [JsonProperty("screws")] public List<ScrewPlacement> Screws = new();
    }

    public static class ScrewPlacementStore
    {
        public const string Filename = "screws.json";

        public static string PathFor(Patient patient)
        {
            if (patient == null || string.IsNullOrEmpty(patient.Meta?.DataRoot)) return null;
            return Path.Combine(patient.Meta.DataRoot, Filename);
        }

        public static ScrewPlacementDoc Load(Patient patient)
        {
            var path = PathFor(patient);
            if (path == null || !File.Exists(path))
                return new ScrewPlacementDoc { PatientId = patient?.Meta?.PatientId, Screws = new List<ScrewPlacement>() };
            try
            {
                var json = File.ReadAllText(path);
                var doc = JsonConvert.DeserializeObject<ScrewPlacementDoc>(json) ?? new ScrewPlacementDoc();
                doc.Screws ??= new List<ScrewPlacement>();
                return doc;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ScrewPlacement] failed to load {path}: {e.Message}");
                return new ScrewPlacementDoc { PatientId = patient?.Meta?.PatientId, Screws = new List<ScrewPlacement>() };
            }
        }

        public static bool Save(Patient patient, ScrewPlacementDoc doc)
        {
            var path = PathFor(patient);
            if (path == null || doc == null) return false;
            try
            {
                doc.PatientId = patient.Meta.PatientId;
                File.WriteAllText(path, JsonConvert.SerializeObject(doc, Formatting.Indented));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ScrewPlacement] failed to save {path}: {e.Message}");
                return false;
            }
        }
    }
}
