using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace Host.Patients.Loaders
{
    /// Minimal MHA (MetaImage) reader. Parses the plain-text header up to
    /// `ElementDataFile = LOCAL`, optionally zlib-decompresses the trailing
    /// payload, then normalises the voxel data to R8 bytes. Volume axes are
    /// transposed so the largest-spacing axis becomes the slice direction
    /// (D in the returned dimensions) — typical MR scans have thicker slices
    /// than in-plane pixels, so this lines the data up with what humans expect.
    ///
    /// Supports:
    ///   ElementType   MET_SHORT, MET_USHORT, MET_UCHAR, MET_FLOAT
    ///   CompressedData True (zlib) or False
    ///   NDims         3
    public static class MhaReader
    {
        public struct Volume
        {
            public int W, H, D;        // dimensions after axis transpose (D = slice axis)
            public byte[] Bytes;        // normalised R8 voxels, length W * H * D
            public Vector3 SpacingMm;   // physical spacing after transpose (.z is slice thickness)
            /// Unit vectors in cube-local space pointing toward the patient's
            /// anterior / superior / right anatomical directions. Derived from
            /// the MHA `AnatomicalOrientation` (e.g. "RAI") combined with our
            /// axis transpose. Zero on all three = unknown.
            public Vector3 AnatomyAnteriorDir;
            public Vector3 AnatomySuperiorDir;
            public Vector3 AnatomyRightDir;
        }

        public static bool TryRead(string path, out Volume vol, out string error)
        {
            vol = default;
            error = null;
            if (!File.Exists(path)) { error = $"file not found: {path}"; return false; }

            byte[] all;
            try { all = File.ReadAllBytes(path); }
            catch (Exception e) { error = $"read failed: {e.Message}"; return false; }

            int payloadStart = FindPayloadStart(all);
            if (payloadStart < 0) { error = "couldn't find 'ElementDataFile = LOCAL' header terminator"; return false; }

            var fields = ParseFields(Encoding.ASCII.GetString(all, 0, payloadStart));
            if (!fields.TryGetValue("DimSize", out var dimStr)) { error = "missing DimSize"; return false; }
            if (!fields.TryGetValue("ElementType", out var elemType)) { error = "missing ElementType"; return false; }
            string spaceStr = fields.TryGetValue("ElementSpacing", out var s) ? s : "1 1 1";
            bool compressed = fields.TryGetValue("CompressedData", out var c) && c.Equals("True", StringComparison.OrdinalIgnoreCase);
            string ndimsStr = fields.TryGetValue("NDims", out var n) ? n : "3";
            if (ndimsStr.Trim() != "3") { error = $"only NDims=3 supported (got {ndimsStr})"; return false; }

            var dims = ParseInts(dimStr);
            var spacing = ParseFloats(spaceStr);
            if (dims.Length != 3 || spacing.Length != 3) { error = "DimSize / ElementSpacing must have 3 values"; return false; }

            byte[] payload;
            try
            {
                if (compressed) payload = DecompressZlib(all, payloadStart, all.Length - payloadStart);
                else
                {
                    payload = new byte[all.Length - payloadStart];
                    Buffer.BlockCopy(all, payloadStart, payload, 0, payload.Length);
                }
            }
            catch (Exception e) { error = $"decompression failed: {e.Message}"; return false; }

            byte[] r8;
            try { r8 = ConvertToR8(payload, elemType.Trim()); }
            catch (Exception e) { error = $"voxel conversion failed: {e.Message}"; return false; }

            long expected = (long)dims[0] * dims[1] * dims[2];
            if (r8.Length != expected) { error = $"voxel count {r8.Length} ≠ DimSize product {expected}"; return false; }

            // MHA DimSize order is (X, Y, Z); the on-disk byte order iterates X
            // fastest, then Y, then Z. Pick the slice axis as the largest-spacing
            // one (typical for MR — thicker slices than in-plane pixels) and
            // transpose so it ends up as our D dimension.
            int sliceMhaAxis = ArgMax(spacing);   // 0, 1, or 2 in MHA order
            r8 = TransposeMhaToWHD(r8, dims, sliceMhaAxis, out int newW, out int newH, out int newD);
            float spW = sliceMhaAxis == 0 ? spacing[1] : spacing[0];
            float spH = sliceMhaAxis == 2 ? spacing[1] : spacing[2];
            float spD = spacing[sliceMhaAxis];

            string anatomyOrient = fields.TryGetValue("AnatomicalOrientation", out var ao) ? ao.Trim() : null;
            var (anteriorDir, superiorDir, rightDir) = ComputeAnatomyDirs(anatomyOrient, sliceMhaAxis);

            vol = new Volume
            {
                W = newW, H = newH, D = newD,
                Bytes = r8,
                SpacingMm = new Vector3(spW, spH, spD),
                AnatomyAnteriorDir = anteriorDir,
                AnatomySuperiorDir = superiorDir,
                AnatomyRightDir = rightDir,
            };
            return true;
        }

        /// Map the MHA's `AnatomicalOrientation` (e.g. "RAI" = +X→right,
        /// +Y→anterior, +Z→inferior) into unit vectors in cube-local space
        /// after we've transposed the slice axis to D. Returns zero vectors if
        /// the field is missing or malformed.
        static (Vector3 a, Vector3 s, Vector3 r) ComputeAnatomyDirs(string orientation, int sliceMhaAxis)
        {
            if (string.IsNullOrEmpty(orientation) || orientation.Length != 3)
                return (Vector3.zero, Vector3.zero, Vector3.zero);

            // After transpose: MHA axes (X, Y, Z) map to cube-local in this order.
            int axW, axH;
            switch (sliceMhaAxis)
            {
                case 0:  axW = 1; axH = 2; break;
                case 1:  axW = 0; axH = 2; break;
                default: axW = 0; axH = 1; break;
            }
            Vector3 CubeLocalFor(int mhaAxis, float sign)
            {
                if (mhaAxis == sliceMhaAxis) return sign * new Vector3(0, 0, 1); // D
                if (mhaAxis == axW)          return sign * new Vector3(1, 0, 0); // W
                if (mhaAxis == axH)          return sign * new Vector3(0, 1, 0); // H
                return Vector3.zero;
            }
            char Opposite(char c) => c switch
            {
                'R' => 'L', 'L' => 'R',
                'A' => 'P', 'P' => 'A',
                'S' => 'I', 'I' => 'S',
                _ => '\0',
            };
            Vector3 DirOf(char anatomyLetter)
            {
                for (int i = 0; i < 3; i++)
                {
                    char c = char.ToUpperInvariant(orientation[i]);
                    if (c == anatomyLetter) return CubeLocalFor(i, +1f);
                    if (Opposite(c) == anatomyLetter) return CubeLocalFor(i, -1f);
                }
                return Vector3.zero;
            }
            return (DirOf('A'), DirOf('S'), DirOf('R'));
        }

        // ── helpers ─────────────────────────────────────────────────────────

        static int FindPayloadStart(byte[] all)
        {
            // Locate "ElementDataFile = LOCAL\n" (or with \r\n). Return index of the
            // first byte AFTER that line terminator.
            var key = "ElementDataFile";
            int searchEnd = Math.Min(all.Length, 8192); // header is always small
            for (int i = 0; i < searchEnd - key.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < key.Length; j++)
                    if (all[i + j] != (byte)key[j]) { match = false; break; }
                if (!match) continue;
                // Walk to end of line (\n).
                int eol = i;
                while (eol < all.Length && all[eol] != (byte)'\n') eol++;
                if (eol < all.Length) return eol + 1;
                return -1;
            }
            return -1;
        }

        static Dictionary<string, string> ParseFields(string headerText)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawLine in headerText.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string k = line.Substring(0, eq).Trim();
                string v = line.Substring(eq + 1).Trim();
                if (k.Length > 0) dict[k] = v;
            }
            return dict;
        }

        static int[] ParseInts(string s) => Array.ConvertAll(s.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries), int.Parse);
        static float[] ParseFloats(string s) => Array.ConvertAll(s.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries), x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture));
        static int ArgMax(float[] a) { int idx = 0; for (int i = 1; i < a.Length; i++) if (a[i] > a[idx]) idx = i; return idx; }

        static byte[] DecompressZlib(byte[] src, int offset, int count)
        {
            // MHA's compressed data is zlib-framed (RFC 1950): 2-byte header
            // followed by raw deflate, then a 4-byte adler32 trailer. We strip
            // the 2-byte header and feed the rest to DeflateStream which only
            // handles raw deflate.
            if (count < 6) throw new InvalidDataException("zlib payload too short");
            using var ms = new MemoryStream(src, offset + 2, count - 2);
            using var def = new DeflateStream(ms, CompressionMode.Decompress);
            using var output = new MemoryStream();
            def.CopyTo(output);
            return output.ToArray();
        }

        static byte[] ConvertToR8(byte[] raw, string elementType)
        {
            switch (elementType)
            {
                case "MET_UCHAR":
                case "MET_CHAR":
                    return raw;
                case "MET_SHORT":
                {
                    int count = raw.Length / 2;
                    short min = short.MaxValue, max = short.MinValue;
                    for (int i = 0; i < count; i++)
                    {
                        short v = (short)(raw[i * 2] | (raw[i * 2 + 1] << 8));
                        if (v < min) min = v;
                        if (v > max) max = v;
                    }
                    int span = Math.Max(max - min, 1);
                    var result = new byte[count];
                    for (int i = 0; i < count; i++)
                    {
                        short v = (short)(raw[i * 2] | (raw[i * 2 + 1] << 8));
                        result[i] = (byte)Math.Clamp((v - min) * 255 / span, 0, 255);
                    }
                    return result;
                }
                case "MET_USHORT":
                {
                    int count = raw.Length / 2;
                    ushort min = ushort.MaxValue, max = 0;
                    for (int i = 0; i < count; i++)
                    {
                        ushort v = (ushort)(raw[i * 2] | (raw[i * 2 + 1] << 8));
                        if (v < min) min = v;
                        if (v > max) max = v;
                    }
                    int span = Math.Max(max - min, 1);
                    var result = new byte[count];
                    for (int i = 0; i < count; i++)
                    {
                        ushort v = (ushort)(raw[i * 2] | (raw[i * 2 + 1] << 8));
                        result[i] = (byte)Math.Clamp((v - min) * 255 / span, 0, 255);
                    }
                    return result;
                }
                case "MET_FLOAT":
                {
                    int count = raw.Length / 4;
                    float min = float.MaxValue, max = float.MinValue;
                    for (int i = 0; i < count; i++)
                    {
                        float v = BitConverter.ToSingle(raw, i * 4);
                        if (v < min) min = v;
                        if (v > max) max = v;
                    }
                    float span = Math.Max(max - min, 1e-6f);
                    var result = new byte[count];
                    for (int i = 0; i < count; i++)
                    {
                        float v = BitConverter.ToSingle(raw, i * 4);
                        result[i] = (byte)Math.Clamp((int)((v - min) / span * 255f), 0, 255);
                    }
                    return result;
                }
                default:
                    throw new NotSupportedException($"MHA ElementType not supported: {elementType}");
            }
        }

        /// Reorder the on-disk MHA bytes (axis order X,Y,Z, X-fastest) so the
        /// chosen `sliceMhaAxis` becomes the slowest-varying axis (D in W,H,D).
        /// Returns a new byte[] in the WHD layout the rest of the workstation
        /// expects, plus the new dimensions.
        static byte[] TransposeMhaToWHD(byte[] src, int[] mhaDims, int sliceMhaAxis,
                                        out int W, out int H, out int D)
        {
            int sx = mhaDims[0], sy = mhaDims[1], sz = mhaDims[2];
            // Pick the other two MHA axes (in stable order) as in-plane W & H.
            int axW, axH;
            switch (sliceMhaAxis)
            {
                case 0: axW = 1; axH = 2; break; // slice along X → plane is Y×Z
                case 1: axW = 0; axH = 2; break; // slice along Y → plane is X×Z
                default: axW = 0; axH = 1; break; // slice along Z → plane is X×Y
            }
            int[] dimsByAxis = { sx, sy, sz };
            W = dimsByAxis[axW]; H = dimsByAxis[axH]; D = dimsByAxis[sliceMhaAxis];
            int[] strides = { 1, sx, sx * sy }; // bytes-per-step along each MHA axis
            var dst = new byte[(long)W * H * D];
            int strideS = strides[sliceMhaAxis];
            int strideW = strides[axW];
            int strideH = strides[axH];
            for (int d = 0; d < D; d++)
            for (int h = 0; h < H; h++)
            for (int w = 0; w < W; w++)
                dst[((long)d * H + h) * W + w] = src[(long)d * strideS + (long)h * strideH + (long)w * strideW];
            return dst;
        }
    }
}
