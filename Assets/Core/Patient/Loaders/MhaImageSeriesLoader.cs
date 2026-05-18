using System.IO;
using UnityEngine;

namespace Host.Patients.Loaders
{
    /// Loads an ImageSeries from a single .mha (MetaImage) file. The MHA may be
    /// zlib-compressed and store any of {MET_UCHAR, MET_SHORT, MET_USHORT, MET_FLOAT}
    /// voxels; the reader normalises to R8 and slices the resulting volume along
    /// its largest-spacing axis so the workstation gets the same Texture2D-per-slice
    /// shape the PNG loader produces.
    ///
    /// Manifest entry:
    ///     { "id": "...", "modality": "MR",
    ///       "path": "case_t2.mha", "format": "mha_file" }
    /// `path` is resolved relative to the patient folder.
    public sealed class MhaImageSeriesLoader : IImageSeriesLoader
    {
        public string Format => "mha_file";

        public ImageSeries Load(string filePath, string id, string modality)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[MhaLoader] file not found: {filePath}");
                return null;
            }

            if (!MhaReader.TryRead(filePath, out var vol, out var err))
            {
                Debug.LogError($"[MhaLoader] {Path.GetFileName(filePath)} failed: {err}");
                return null;
            }
            Debug.Log($"[MhaLoader] loaded {Path.GetFileName(filePath)} → {vol.W}×{vol.H}×{vol.D} voxels, spacing {vol.SpacingMm.x:0.00}×{vol.SpacingMm.y:0.00}×{vol.SpacingMm.z:0.00} mm.");

            var series = new ImageSeries(id, modality)
            {
                SliceSpacingMm = vol.SpacingMm.z,
                VoxelSpacingMm = vol.SpacingMm,
                AnatomyAnteriorDir = vol.AnatomyAnteriorDir,
                AnatomySuperiorDir = vol.AnatomySuperiorDir,
                AnatomyRightDir = vol.AnatomyRightDir,
            };

            // Slice the volume into D Texture2D objects so it fits the existing
            // ImageSeries pipeline (used by both the volume renderer and the
            // slice-viewer panel). One slice = W×H bytes from the volume buffer.
            int sliceBytes = vol.W * vol.H;
            for (int z = 0; z < vol.D; z++)
            {
                var tex = new Texture2D(vol.W, vol.H, TextureFormat.R8, mipChain: false, linear: false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                // Voxel layout in vol.Bytes after the reader's transpose is
                // (D, H, W) outer-to-inner — so slice z starts at z*W*H.
                tex.SetPixelData(vol.Bytes, 0, z * sliceBytes);
                // Stays CPU-readable: VolumeRenderer.LoadFromImageSeries iterates
                // each slice via GetPixels32 to reassemble the Texture3D.
                tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                series.AddSlice(tex);
            }
            return series;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => ImageSeriesLoaderRegistry.Register(new MhaImageSeriesLoader());
    }
}
