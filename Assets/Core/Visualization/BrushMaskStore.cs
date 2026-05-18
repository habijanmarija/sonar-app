using System;
using System.IO;
using Host.Patients;
using UnityEngine;

namespace Host.Visualization
{
    /// On-disk persistence for the brush-painted segmentation mask. One file per
    /// (patient, series) pair under
    ///   Application.persistentDataPath/patients/<patient_id>/masks/<series_id>.bin
    ///
    /// File layout (little-endian):
    ///   int32 version (currently 1)
    ///   int32 W, H, D
    ///   byte  voxels[W*H*D]   — R8 mask values (0 = empty, 255 = painted)
    public static class BrushMaskStore
    {
        const int FormatVersion = 1;

        public static string PathFor(Patient p, string seriesId, string segmentId = null)
        {
            var dir = Path.Combine(Application.persistentDataPath, "patients", p.Meta.PatientId, "masks");
            Directory.CreateDirectory(dir);
            var filename = string.IsNullOrEmpty(segmentId) ? $"{seriesId}.bin" : $"{seriesId}__{segmentId}.bin";
            return Path.Combine(dir, filename);
        }

        public static bool Save(Patient p, string seriesId, string segmentId, int w, int h, int d, byte[] bytes)
        {
            if (p == null || string.IsNullOrEmpty(seriesId) || bytes == null) return false;
            try
            {
                var path = PathFor(p, seriesId, segmentId);
                using var fs = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
                using var bw = new BinaryWriter(fs);
                bw.Write(FormatVersion);
                bw.Write(w);
                bw.Write(h);
                bw.Write(d);
                bw.Write(bytes);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BrushMaskStore] Save failed for {p.Meta.PatientId}/{seriesId}/{segmentId}: {e.Message}");
                return false;
            }
        }

        public static bool TryLoad(Patient p, string seriesId, string segmentId, out int w, out int h, out int d, out byte[] bytes)
        {
            w = h = d = 0; bytes = null;
            if (p == null || string.IsNullOrEmpty(seriesId)) return false;
            var path = PathFor(p, seriesId, segmentId);
            // v1 fallback: pre-multi-segment files lived at <series_id>.bin with no
            // segment_id suffix. Pick that up if the segment-specific file doesn't
            // exist yet, so older saved masks aren't orphaned by the migration.
            if (!File.Exists(path))
            {
                var legacy = PathFor(p, seriesId, null);
                if (legacy == path || !File.Exists(legacy)) return false;
                path = legacy;
            }
            try
            {
                using var fs = File.OpenRead(path);
                using var br = new BinaryReader(fs);
                int version = br.ReadInt32();
                if (version != FormatVersion) return false;
                w = br.ReadInt32();
                h = br.ReadInt32();
                d = br.ReadInt32();
                long expected = (long)w * h * d;
                if (expected <= 0 || expected > int.MaxValue) return false;
                bytes = br.ReadBytes((int)expected);
                return bytes.Length == expected;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BrushMaskStore] Load failed for {p.Meta.PatientId}/{seriesId}/{segmentId}: {e.Message}");
                return false;
            }
        }

        public static bool Delete(Patient p, string seriesId, string segmentId)
        {
            try
            {
                var path = PathFor(p, seriesId, segmentId);
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BrushMaskStore] Delete failed for {p.Meta.PatientId}/{seriesId}/{segmentId}: {e.Message}");
                return false;
            }
        }
    }
}
