using System;
using System.IO;
using Host.Patients;
using Newtonsoft.Json;
using UnityEngine;

namespace Host.Tools.AnnotationWidget
{
    /// JSON load/save for a patient's annotations. File path: <patient.DataRoot>/annotations.json.
    public static class AnnotationStore
    {
        public const string Filename = "annotations.json";

        public static string PathFor(Patient patient)
        {
            if (patient == null || string.IsNullOrEmpty(patient.Meta?.DataRoot)) return null;
            return Path.Combine(patient.Meta.DataRoot, Filename);
        }

        public static AnnotationCollection Load(Patient patient)
        {
            var path = PathFor(patient);
            if (path == null || !File.Exists(path))
                return new AnnotationCollection { PatientId = patient?.Meta?.PatientId };

            try
            {
                var json = File.ReadAllText(path);
                var collection = JsonConvert.DeserializeObject<AnnotationCollection>(json)
                                 ?? new AnnotationCollection();
                collection.Annotations ??= new System.Collections.Generic.List<Annotation>();
                return collection;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Annotation] failed to load {path}: {e.Message}");
                return new AnnotationCollection { PatientId = patient?.Meta?.PatientId };
            }
        }

        public static bool Save(Patient patient, AnnotationCollection collection)
        {
            var path = PathFor(patient);
            if (path == null || collection == null) return false;
            try
            {
                collection.PatientId = patient.Meta.PatientId;
                var json = JsonConvert.SerializeObject(collection, Formatting.Indented);
                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Annotation] failed to save {path}: {e.Message}");
                return false;
            }
        }
    }
}
