using UnityEngine;

namespace Sonar.Phantom
{
    /// Produces a runtime phantom mesh + its phantom-space origin transform.
    /// Phase 3 ships RuntimePhantomLoader (STL from disk). DICOM runtime import is
    /// scoped out of Phase 3 — the `phantom/` workstream produces STLs from 3D Slicer
    /// and ships them with the build; runtime DICOM parsing is a separate research
    /// vertical (volume sampling, isosurface extraction) not justified here.
    public interface IPhantomSource
    {
        /// Returns the loaded mesh, or null on failure. Implementations should log
        /// the failure cause; callers can fall back to a built-in placeholder.
        Mesh LoadMesh();

        /// Human-readable identifier for telemetry custom_data; e.g. STL filename.
        string SourceId { get; }
    }
}
