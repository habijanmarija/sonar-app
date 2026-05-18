using UnityEngine;

namespace Sonar.Phantom
{
    /// Placeholder for runtime DICOM → mesh import. Intentionally not implemented in
    /// the Phase 3 initial cut: a production-grade DICOM parser + isosurface extractor
    /// is a 2K+ LOC undertaking (parser + dataset reassembly + marching cubes + decimation).
    /// The practical input format for SONAR is **the STL emitted by the `phantom/`
    /// workstream**, loaded via RuntimePhantomLoader.
    ///
    /// When this is genuinely needed (Phase 4 candidate work):
    ///  - Use a third-party DICOM parser (fo-dicom is .NET-friendly, MIT licensed).
    ///  - Reassemble the volume from sorted slices.
    ///  - Threshold + marching cubes (e.g. SimpleITK or a custom implementation).
    ///  - Decimate to target ~50–200 K triangles for headset framerate.
    public sealed class DicomPhantomSource : IPhantomSource
    {
        public string SourceId => "dicom_runtime_not_implemented";

        public Mesh LoadMesh()
        {
            Debug.LogError(
                "[Phantom] Runtime DICOM import is intentionally not implemented. " +
                "Produce an STL from the phantom/ workstream and use RuntimePhantomLoader instead.");
            return null;
        }
    }
}
