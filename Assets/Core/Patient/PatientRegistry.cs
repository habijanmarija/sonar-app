using System;
using System.Collections.Generic;
using UnityEngine;

namespace Host.Patients
{
    /// Manages the active Patient + the catalog of available cases.
    /// Only one Patient can be active at a time (mirrors imhotep's single-patient model).
    /// Catalog is populated by PatientDirectoryLoader at app boot or on demand.
    public static class PatientRegistry
    {
        static readonly List<PatientMeta> _catalog = new();
        static Patient _active;

        public static IReadOnlyList<PatientMeta> Catalog => _catalog;
        public static Patient Active => _active;

        /// Replace the catalog (typically called once at boot after PatientDirectoryLoader scans).
        public static void SetCatalog(IEnumerable<PatientMeta> entries)
        {
            _catalog.Clear();
            if (entries != null) _catalog.AddRange(entries);
        }

        /// Load a patient. Caller is responsible for invoking the mesh + DICOM loaders
        /// in the desired order; this method takes the populated Patient and publishes it.
        public static void Activate(Patient p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            UnloadActive();
            _active = p;
            PatientEventSystem.RaiseLoaded(_active);
        }

        public static void UnloadActive()
        {
            if (_active == null) return;
            var prior = _active;
            _active = null;
            try { PatientEventSystem.RaiseUnloaded(prior); }
            catch (Exception e) { Debug.LogError($"[PatientRegistry] unload handler threw: {e.Message}"); }
            prior.Dispose();
        }

        public static PatientMeta Find(string patientId)
        {
            foreach (var m in _catalog)
                if (m.PatientId == patientId) return m;
            return null;
        }
    }
}
