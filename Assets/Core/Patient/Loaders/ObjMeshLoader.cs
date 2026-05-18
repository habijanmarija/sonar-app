using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Host.Patients.Loaders
{
    /// Minimal OBJ parser sufficient for imhotep-style segmented organ meshes.
    /// Handles: v (vertex), vn (normal), f (triangle, with v/vt/vn forms).
    /// Skips: vt (texture coords), mtllib, usemtl, smoothing groups, polygons > 3.
    public sealed class ObjMeshLoader : IMeshLoader
    {
        public string Format => "obj";

        public Mesh Load(string path)
        {
            if (!File.Exists(path)) return null;

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();

            using var reader = new StreamReader(path);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0 || line[0] == '#') continue;
                var parts = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                switch (parts[0])
                {
                    case "v":
                        if (parts.Length >= 4 &&
                            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                            float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                            verts.Add(new Vector3(-x, y, z)); // RH → LH (mirror X)
                        break;
                    case "vn":
                        if (parts.Length >= 4 &&
                            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var nx) &&
                            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var ny) &&
                            float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var nz))
                            normals.Add(new Vector3(-nx, ny, nz));
                        break;
                    case "f":
                        // Only triangles. f "v/vt/vn" or "v//vn" or "v" — we take the v index.
                        if (parts.Length < 4) break;
                        int i0 = ParseFaceIndex(parts[1]);
                        int i1 = ParseFaceIndex(parts[2]);
                        int i2 = ParseFaceIndex(parts[3]);
                        if (i0 < 0 || i1 < 0 || i2 < 0) break;
                        // Flip winding to compensate for X mirror.
                        tris.Add(i0); tris.Add(i2); tris.Add(i1);
                        break;
                }
            }

            if (verts.Count == 0) return null;

            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
            if (verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            if (normals.Count == verts.Count) mesh.SetNormals(normals);
            else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static int ParseFaceIndex(string token)
        {
            var slash = token.IndexOf('/');
            var idxStr = slash >= 0 ? token.Substring(0, slash) : token;
            if (!int.TryParse(idxStr, out var idx)) return -1;
            return idx - 1; // OBJ indices are 1-based
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => MeshLoaderRegistry.Register(new ObjMeshLoader());
    }
}
