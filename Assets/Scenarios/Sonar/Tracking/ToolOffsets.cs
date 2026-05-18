using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Sonar.Tracking
{
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ToolOffsetEntry
    {
        [JsonProperty("position_m")] public float[] PositionRaw;
        [JsonProperty("rotation_quat")] public float[] RotationRaw;

        public Vector3 Position =>
            PositionRaw != null && PositionRaw.Length == 3
                ? new Vector3(PositionRaw[0], PositionRaw[1], PositionRaw[2])
                : Vector3.zero;

        public Quaternion Rotation =>
            RotationRaw != null && RotationRaw.Length == 4
                ? new Quaternion(RotationRaw[0], RotationRaw[1], RotationRaw[2], RotationRaw[3])
                : Quaternion.identity;
    }

    public sealed class ToolOffsets
    {
        readonly IDictionary<string, ToolOffsetEntry> _offsets;
        public string Version { get; }

        ToolOffsets(string version, IDictionary<string, ToolOffsetEntry> offsets)
        {
            Version = version;
            _offsets = offsets;
        }

        public bool TryGet(string key, out ToolOffsetEntry entry) =>
            _offsets.TryGetValue(key, out entry);

        public static ToolOffsets LoadFromResources(string resourcePath = "ToolOffsets")
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[Tracking] Resources/{resourcePath}.json not found; tool tip will fall back to identity offset.");
                return new ToolOffsets("missing", new Dictionary<string, ToolOffsetEntry>());
            }
            return FromJson(asset.text);
        }

        public static ToolOffsets FromJson(string json)
        {
            var raw = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, ToolOffsetEntry>>>(json);
            if (raw == null || raw.Count == 0)
                return new ToolOffsets("empty", new Dictionary<string, ToolOffsetEntry>());

            var version = raw.Keys.GetEnumerator();
            version.MoveNext();
            var v = version.Current ?? "unknown";
            return new ToolOffsets(v, raw[v]);
        }
    }
}
