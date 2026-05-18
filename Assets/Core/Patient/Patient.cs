using System.Collections.Generic;
using UnityEngine;

namespace Host.Patients
{
    /// The active loaded patient: meta + materialised meshes + DICOM series handles.
    /// Created by PatientRegistry.Load(meta); destroyed by PatientRegistry.Unload().
    /// Tools subscribe to PatientEventSystem to react to load/unload.
    public sealed class Patient
    {
        public PatientMeta Meta { get; }

        /// Materialised meshes keyed by MeshRef.Id. Populated by mesh loaders during Load.
        public readonly Dictionary<string, OrganMesh> Meshes = new();

        /// DICOM series handles keyed by DicomSeriesRef.Id. Populated by DICOM loader.
        public readonly Dictionary<string, DicomSeriesHandle> Series = new();

        public Patient(PatientMeta meta)
        {
            Meta = meta;
        }

        /// Cleanup: destroy Unity-side Mesh objects and unbind DICOM handles.
        /// Called by PatientRegistry; tools shouldn't call directly.
        public void Dispose()
        {
            foreach (var m in Meshes.Values)
                if (m.UnityMesh != null) Object.Destroy(m.UnityMesh);
            Meshes.Clear();

            foreach (var s in Series.Values)
                s?.Dispose();
            Series.Clear();
        }
    }

    public sealed class OrganMesh
    {
        public string Id;
        public string Name;
        public Mesh UnityMesh;
        public Color Color;
        public float Opacity;
    }

    /// Opaque handle for a loaded DICOM series. The underlying type depends on the
    /// loader implementation (Phase A: stub; Phase E: real fo-dicom Texture3D + slice list).
    public abstract class DicomSeriesHandle
    {
        public string Id { get; }
        public string Modality { get; }

        protected DicomSeriesHandle(string id, string modality)
        {
            Id = id;
            Modality = modality;
        }

        public abstract void Dispose();
    }
}
