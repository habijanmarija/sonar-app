using System.IO;
using UnityEngine;

namespace Sonar.Phantom
{
    /// Loads an STL produced by the `phantom/02_segmentation.md` pipeline from disk at
    /// runtime. Default search root is `Application.persistentDataPath/phantom/`.
    /// Use when iterating on a printed phantom without rebuilding the Unity app — drop
    /// the new STL into the search directory and re-enter the scene.
    [AddComponentMenu("SONAR/Phantom/Runtime Phantom Loader")]
    public sealed class RuntimePhantomLoader : MonoBehaviour, IPhantomSource
    {
        [SerializeField] string _filename = "verseNNN_L2_v1.stl";
        [SerializeField] string _subdirectory = "phantom";

        public string SourceId => _filename;

        public Mesh LoadMesh()
        {
            var path = Path.Combine(Application.persistentDataPath, _subdirectory, _filename);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[Phantom] STL not found at {path}; place an STL there or change _filename.");
                return null;
            }
            try
            {
                var mesh = StlReader.Read(path);
                if (mesh != null)
                    Debug.Log($"[Phantom] loaded {path} ({mesh.vertexCount} verts).");
                return mesh;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Phantom] failed to parse {path}: {e.Message}");
                return null;
            }
        }
    }
}
