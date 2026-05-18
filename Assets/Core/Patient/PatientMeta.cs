using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Host.Patients
{
    /// Catalog entry for a patient case. Loaded eagerly by PatientDirectoryLoader so
    /// PatientSelector can list cases without loading their full data.
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class PatientMeta
    {
        [JsonProperty("patient_id")] public string PatientId;
        [JsonProperty("pseudonym")] public string Pseudonym;
        [JsonProperty("age_band")] public string AgeBand;
        [JsonProperty("sex")] public string Sex;
        [JsonProperty("indication")] public string Indication;
        [JsonProperty("history")] public string History;
        [JsonProperty("captured_utc")] public DateTime CapturedUtc;

        /// Folder containing the patient's data on disk; relative or absolute.
        [JsonProperty("data_root")] public string DataRoot;

        /// DICOM series listed in the manifest (one or more — e.g. CT + MR).
        [JsonProperty("dicom_series", NullValueHandling = NullValueHandling.Ignore)]
        public List<DicomSeriesRef> DicomSeries;

        /// Mesh files (STL/OBJ/etc.) referenced by the manifest.
        [JsonProperty("meshes", NullValueHandling = NullValueHandling.Ignore)]
        public List<MeshRef> Meshes;

        /// Preset views the user can snap the camera to.
        [JsonProperty("views", NullValueHandling = NullValueHandling.Ignore)]
        public List<ViewPreset> Views;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class DicomSeriesRef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("modality")] public string Modality;  // "CT" | "MR" | ...
        [JsonProperty("path")] public string Path;          // relative to PatientMeta.DataRoot
        [JsonProperty("description")] public string Description;
        /// "dicom" (default; Phase F+) | "png_sequence" (Phase E test fixture).
        [JsonProperty("format")] public string Format = "dicom";
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class MeshRef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;          // human-readable, e.g. "liver"
        [JsonProperty("path")] public string Path;          // relative to PatientMeta.DataRoot
        [JsonProperty("format")] public string Format;      // "stl" | "obj" | "ply" | "blender_json"
        [JsonProperty("color")] public string Color;        // "#RRGGBB"
        [JsonProperty("opacity")] public float Opacity = 1f;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ViewPreset
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("position")] public float[] PositionRaw;
        [JsonProperty("rotation_euler")] public float[] RotationEulerRaw;
    }
}
