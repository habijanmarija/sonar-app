using UnityEngine;

namespace Host.Patients.Loaders
{
    /// Loads a mesh from a file path. Implementations cover STL, OBJ, PLY,
    /// imhotep's BlenderJson format, etc. Format dispatch is done by MeshLoaderRegistry.
    public interface IMeshLoader
    {
        /// Lowercased format token used in PatientMeta.MeshRef.Format ("stl", "obj", ...).
        string Format { get; }

        /// Read the file at `path` and return a Unity Mesh, or null on failure.
        Mesh Load(string path);
    }
}
