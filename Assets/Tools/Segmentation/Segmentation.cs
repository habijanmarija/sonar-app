using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Host.Tools.Segmentation
{
    /// One intensity-range segment. Voxels whose volume intensity falls in [Low, High]
    /// get tinted with `ColorHex` in the volume render.
    /// Persisted as JSON at <patient>/segmentation.json.
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class Segment
    {
        [JsonProperty("id")] public string Id = "seg";
        [JsonProperty("label")] public string Label = "Region";
        [JsonProperty("enabled")] public bool Enabled = true;
        [JsonProperty("low")] public float Low = 0.4f;
        [JsonProperty("high")] public float High = 0.6f;
        [JsonProperty("color")] public string ColorHex = "#FF6B4A";
        /// Per-segment opacity multiplier in [0, 1]. Combined with the doc's
        /// GlobalOpacity to produce the alpha used by the 3D mesh material and
        /// the volumetric/ortho overlays.
        [JsonProperty("opacity")] public float Opacity = 1f;
        [JsonProperty("created_utc")] public DateTime CreatedUtc;

        public Color Color
        {
            get
            {
                if (string.IsNullOrEmpty(ColorHex) || !ColorUtility.TryParseHtmlString(ColorHex, out var c))
                    return new Color(1f, 0.42f, 0.29f, 1f);
                return c;
            }
            set => ColorHex = "#" + ColorUtility.ToHtmlStringRGB(value);
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class SegmentationDoc
    {
        [JsonProperty("patient_id")] public string PatientId;
        /// Legacy v1 single-segment slot — populated when reading old files, copied
        /// into Segments on first save. New writes leave this null.
        [JsonProperty("segment", NullValueHandling = NullValueHandling.Ignore)] public Segment Segment;
        [JsonProperty("segments")] public List<Segment> Segments = new();
        [JsonProperty("active_segment_id")] public string ActiveSegmentId;
        /// Master opacity multiplier applied to every segment's per-segment
        /// Opacity. Lets the user dim the whole segmentation overlay at once
        /// without touching individual rows. Defaults to 0.7 to preserve the
        /// previous hard-coded value.
        [JsonProperty("global_opacity")] public float GlobalOpacity = 0.7f;
    }
}
