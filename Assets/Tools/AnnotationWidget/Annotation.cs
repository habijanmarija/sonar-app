using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Host.Tools.AnnotationWidget
{
    /// One annotation on the active patient. Position is in patient-space (relative to
    /// the PatientContent root). Persisted as JSON under `<patient>/annotations.json`.
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class Annotation
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("label")] public string Label;
        [JsonProperty("color")] public string ColorHex;
        [JsonProperty("position_m")] public float[] PositionRaw;
        [JsonProperty("created_utc")] public DateTime CreatedUtc;
        [JsonProperty("author")] public string Author;

        public Vector3 Position
        {
            get => PositionRaw != null && PositionRaw.Length == 3
                ? new Vector3(PositionRaw[0], PositionRaw[1], PositionRaw[2])
                : Vector3.zero;
            set => PositionRaw = new[] { value.x, value.y, value.z };
        }

        public Color Color
        {
            get
            {
                if (string.IsNullOrEmpty(ColorHex) || !ColorUtility.TryParseHtmlString(ColorHex, out var c))
                    return new Color(1f, 0.85f, 0.2f, 1f);
                return c;
            }
        }

        public static Annotation New(Vector3 position, string label, string colorHex = "#FFD633", string author = "")
        {
            return new Annotation
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                Label = label ?? "",
                ColorHex = colorHex,
                PositionRaw = new[] { position.x, position.y, position.z },
                CreatedUtc = DateTime.UtcNow,
                Author = author ?? "",
            };
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class AnnotationCollection
    {
        [JsonProperty("patient_id")] public string PatientId;
        [JsonProperty("annotations")] public System.Collections.Generic.List<Annotation> Annotations = new();
    }
}
