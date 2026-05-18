using System;
using System.IO;
using Host.Patients;
using Newtonsoft.Json;
using UnityEngine;

namespace Host.Tools.Segmentation
{
    /// JSON load/save for per-patient segmentation parameters. v1 supports one segment;
    /// future multi-segment support extends SegmentationDoc to a list.
    public static class SegmentationStore
    {
        public const string Filename = "segmentation.json";

        public static string PathFor(Patient patient)
        {
            if (patient == null || string.IsNullOrEmpty(patient.Meta?.DataRoot)) return null;
            return Path.Combine(patient.Meta.DataRoot, Filename);
        }

        public static SegmentationDoc Load(Patient patient)
        {
            var path = PathFor(patient);
            if (path == null || !File.Exists(path))
                return new SegmentationDoc { PatientId = patient?.Meta?.PatientId, Segments = new System.Collections.Generic.List<Segment>() };
            try
            {
                var json = File.ReadAllText(path);
                var doc = JsonConvert.DeserializeObject<SegmentationDoc>(json) ?? new SegmentationDoc();
                doc.Segments ??= new System.Collections.Generic.List<Segment>();
                // Migrate v1 single-segment doc → multi-segment list. Dropping the old
                // field on write keeps the file format from carrying both forever.
                if (doc.Segment != null && doc.Segments.Count == 0)
                {
                    if (string.IsNullOrEmpty(doc.Segment.Id)) doc.Segment.Id = "seg1";
                    doc.Segments.Add(doc.Segment);
                    doc.ActiveSegmentId ??= doc.Segment.Id;
                }
                doc.Segment = null;
                if (string.IsNullOrEmpty(doc.ActiveSegmentId) && doc.Segments.Count > 0)
                    doc.ActiveSegmentId = doc.Segments[0].Id;
                return doc;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Segmentation] failed to load {path}: {e.Message}");
                return new SegmentationDoc { PatientId = patient?.Meta?.PatientId, Segments = new System.Collections.Generic.List<Segment>() };
            }
        }

        public static bool Save(Patient patient, SegmentationDoc doc)
        {
            var path = PathFor(patient);
            if (path == null || doc == null) return false;
            try
            {
                doc.PatientId = patient.Meta.PatientId;
                doc.Segment = null; // never write the v1 field back out
                var json = JsonConvert.SerializeObject(doc, Formatting.Indented);
                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Segmentation] failed to save {path}: {e.Message}");
                return false;
            }
        }
    }
}
