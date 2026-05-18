using UnityEngine;

namespace Host.Patients.Loaders
{
    /// STL loader for Host.Patients. Delegates the actual parsing to whatever STL reader
    /// is in the project. Phase A keeps this as a thin shim with a placeholder until
    /// we wire it to `Sonar.Phantom.StlReader` (cross-asmdef ref) or extract the reader
    /// into a shared `Host.Mesh` asmdef.
    public sealed class StlMeshLoader : IMeshLoader
    {
        public string Format => "stl";

        public Mesh Load(string path)
        {
            // TODO(Phase A polish): wire to Sonar.Phantom.StlReader.Read(path) by either
            // (a) referencing Sonar.Phantom from Host.Patients (creates a cross-domain dep),
            // or (b) moving StlReader into a new Host.Mesh asmdef shared between Patient
            // and Phantom workstreams. Option (b) is cleaner; do it when Phase B needs
            // real meshes loaded. Until then this returns null and logs.
            Debug.LogWarning($"[Patient] StlMeshLoader.Load not wired yet (path: {path}). Move Sonar.Phantom.StlReader into Host.Mesh.");
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => MeshLoaderRegistry.Register(new StlMeshLoader());
    }
}
