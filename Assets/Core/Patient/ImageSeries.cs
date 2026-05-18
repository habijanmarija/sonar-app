using System;
using System.Collections.Generic;
using UnityEngine;

namespace Host.Patients
{
    /// In-memory 2D image series: an ordered list of slice Texture2Ds plus per-slice
    /// metadata. Used by the DicomWidget to render axial / sagittal / coronal views.
    /// PNG sequences populate this directly; real DICOM goes through fo-dicom in Phase F.
    public sealed class ImageSeries : DicomSeriesHandle
    {
        readonly List<Texture2D> _slices = new();

        public IReadOnlyList<Texture2D> Slices => _slices;
        public int SliceCount => _slices.Count;
        public Texture2D Slice(int i) => (i >= 0 && i < _slices.Count) ? _slices[i] : null;

        /// Patient-space spacing between slice planes. PNG sequences default to 1 mm;
        /// real DICOM uses SliceThickness / Spacing Between Slices from the DICOM header.
        public float SliceSpacingMm = 1.0f;

        /// Physical voxel size in mm: (.x = in-plane X, .y = in-plane Y, .z = slice).
        /// Used by VolumeRenderer to scale the cube to the actual anatomical
        /// proportions instead of rendering as a unit cube. Zero on any axis
        /// means "unknown" (PNG previews) and the renderer falls back to its
        /// SerializeField cube size.
        public Vector3 VoxelSpacingMm = Vector3.zero;

        /// Unit vectors in cube-local space pointing toward the patient's
        /// anterior / superior / right anatomical directions. Populated from
        /// the MHA `AnatomicalOrientation` field. Zero = unknown (PNG previews).
        public Vector3 AnatomyAnteriorDir = Vector3.zero;
        public Vector3 AnatomySuperiorDir = Vector3.zero;
        public Vector3 AnatomyRightDir = Vector3.zero;

        /// Window/level defaults; clinical CT typically uses W=400 L=40 for soft tissue,
        /// W=1500 L=-600 for lung. PNG sequences leave them at sensible neutrals.
        public float DefaultWindow = 1f;
        public float DefaultLevel = 0.5f;

        public ImageSeries(string id, string modality) : base(id, modality) { }

        public void AddSlice(Texture2D slice)
        {
            if (slice != null) _slices.Add(slice);
        }

        public override void Dispose()
        {
            foreach (var t in _slices)
                if (t != null) UnityEngine.Object.Destroy(t);
            _slices.Clear();
        }
    }
}
