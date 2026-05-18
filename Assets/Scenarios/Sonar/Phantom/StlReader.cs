using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Sonar.Phantom
{
    /// Minimal STL parser. Auto-detects binary vs. ASCII from the file header.
    /// Returns a Unity Mesh in left-handed coordinates (sign-flipped X) so the result
    /// composes naturally with phantom-space landmark coords produced by `phantom/02_segmentation.md`.
    public static class StlReader
    {
        public static Mesh Read(string path)
        {
            if (!File.Exists(path)) return null;
            var bytes = File.ReadAllBytes(path);
            return IsBinary(bytes) ? ReadBinary(bytes) : ReadAscii(Encoding.ASCII.GetString(bytes));
        }

        // Binary STL: 80-byte header + uint32 triangle count + (12 floats + uint16) per tri.
        // ASCII STL: starts with "solid " and contains "facet normal" / "vertex" tokens.
        // The "binary file starts with 'solid '" gotcha is real — we check the total length
        // against the binary triangle-count math to disambiguate.
        static bool IsBinary(byte[] bytes)
        {
            if (bytes.Length < 84) return false;
            uint triCount = System.BitConverter.ToUInt32(bytes, 80);
            long expected = 84L + triCount * 50L;
            return expected == bytes.Length;
        }

        static Mesh ReadBinary(byte[] bytes)
        {
            uint triCount = System.BitConverter.ToUInt32(bytes, 80);
            var verts = new List<Vector3>((int)triCount * 3);
            var tris = new List<int>((int)triCount * 3);
            int offset = 84;
            for (uint i = 0; i < triCount; i++)
            {
                offset += 12; // skip normal
                for (int v = 0; v < 3; v++)
                {
                    float x = -System.BitConverter.ToSingle(bytes, offset); // RH → LH
                    float y = System.BitConverter.ToSingle(bytes, offset + 4);
                    float z = System.BitConverter.ToSingle(bytes, offset + 8);
                    verts.Add(new Vector3(x, y, z));
                    offset += 12;
                }
                offset += 2; // skip attribute byte count
                int baseIdx = (int)i * 3;
                tris.Add(baseIdx);
                tris.Add(baseIdx + 2); // flip winding to compensate for X mirror
                tris.Add(baseIdx + 1);
            }
            return Build(verts, tris);
        }

        static Mesh ReadAscii(string text)
        {
            var verts = new List<Vector3>(1024);
            var tris = new List<int>(1024);
            int triIndex = 0;
            using var reader = new StringReader(text);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (!line.StartsWith("vertex", System.StringComparison.OrdinalIgnoreCase)) continue;
                var parts = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) continue;
                float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var x);
                float.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var y);
                float.TryParse(parts[3], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var z);
                verts.Add(new Vector3(-x, y, z));
                if (verts.Count % 3 == 0)
                {
                    tris.Add(triIndex);
                    tris.Add(triIndex + 2);
                    tris.Add(triIndex + 1);
                    triIndex += 3;
                }
            }
            return Build(verts, tris);
        }

        static Mesh Build(List<Vector3> verts, List<int> tris)
        {
            var mesh = new Mesh { name = "RuntimePhantom" };
            if (verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
