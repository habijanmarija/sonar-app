using Sonar.Guidance;
using Sonar.Scenario;
using UnityEngine;

namespace Sonar.Visualization
{
    /// Renders the planned trajectory as a translucent line from entry to entry + max_depth * axis,
    /// plus a small marker at the entry point. Colour is keyed off the live worst-of-three gate.
    [RequireComponent(typeof(LineRenderer))]
    [AddComponentMenu("SONAR/Visualization/Trajectory Overlay")]
    public sealed class TrajectoryOverlay : MonoBehaviour
    {
        [SerializeField] GuidanceController _guidance;
        [SerializeField, Range(0.001f, 0.02f)] float _lineWidth = 0.004f;
        [SerializeField] Color _greenColour = new(0.30f, 0.95f, 0.40f, 0.85f);
        [SerializeField] Color _yellowColour = new(1.00f, 0.85f, 0.20f, 0.85f);
        [SerializeField] Color _redColour = new(0.95f, 0.30f, 0.30f, 0.85f);

        LineRenderer _line;
        GameObject _entryMarker;
        Material _entryMaterial;

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.material = new Material(FindUnlitShader());
            _line.startWidth = _lineWidth;
            _line.endWidth = _lineWidth;
            _line.positionCount = 2;

            _entryMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _entryMarker.name = "EntryMarker";
            _entryMarker.transform.SetParent(transform, worldPositionStays: false);
            _entryMarker.transform.localScale = Vector3.one * 0.006f;
            DestroyImmediate(_entryMarker.GetComponent<Collider>());
            _entryMaterial = new Material(FindUnlitShader()) { color = _greenColour };
            _entryMarker.GetComponent<MeshRenderer>().sharedMaterial = _entryMaterial;
        }

        /// Pick a shader that renders correctly in URP, with Built-in pipeline fallbacks.
        /// "Universal Render Pipeline/Particles/Unlit" supports vertex colors so
        /// LineRenderer.startColor/endColor work without per-frame material edits.
        static Shader FindUnlitShader()
        {
            return Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }

        void LateUpdate()
        {
            if (_guidance == null || _guidance.Config == null) return;

            var t = _guidance.Config.Trajectory;
            var start = t.Entry;
            var end = t.Entry + t.Axis * t.MaxDepthM;
            _line.SetPosition(0, start);
            _line.SetPosition(1, end);
            _entryMarker.transform.position = start;

            var c = ColourFor(_guidance.LatestWorstGate);
            _line.startColor = c;
            _line.endColor = c;
            _entryMaterial.color = c;
        }

        Color ColourFor(Gate gate) => gate switch
        {
            Gate.Green => _greenColour,
            Gate.Yellow => _yellowColour,
            _ => _redColour,
        };

        void OnDestroy()
        {
            if (_entryMaterial != null) Destroy(_entryMaterial);
        }
    }
}
