using UnityEngine;

namespace Host.Patients.Loaders
{
    /// DICOM loader. Phase A: stub. Phase E: real implementation backed by fo-dicom
    /// (MIT-licensed, .NET-friendly). Add `Dicom.dll` to `Plugins/` or as a NuGet ref
    /// before activating this.
    ///
    /// Loading strategy when implemented:
    ///   1. Find all .dcm files in the series directory.
    ///   2. Read metadata, sort by ImagePositionPatient.
    ///   3. Allocate a Texture3D of the right format (R16 for CT, R8 typical for masks).
    ///   4. Stream pixel data slice-by-slice into the texture.
    ///   5. Return a DicomSeriesHandle wrapping the texture + the per-slice geometry.
    public static class DicomLoader
    {
        public static DicomSeriesHandle Load(string seriesDirectory, string id, string modality)
        {
            Debug.LogWarning(
                $"[Patient] DICOM loading not yet implemented (series: {seriesDirectory}). " +
                "Stub returning null. See IMHOTEP_PORT_PLAN.md Phase E.");
            return null;
        }
    }
}
