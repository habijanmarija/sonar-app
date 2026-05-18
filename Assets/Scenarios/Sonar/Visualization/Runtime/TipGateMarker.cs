using Sonar.Guidance;
using Sonar.Scenario;
using Sonar.Tracking;
using UnityEngine;

namespace Sonar.Visualization
{
    /// Small sphere positioned at the current drill-tip pose, colour-coded by worst gate.
    [AddComponentMenu("SONAR/Visualization/Tip Gate Marker")]
    public sealed class TipGateMarker : MonoBehaviour
    {
        [SerializeField] GuidanceController _guidance;
        [SerializeField] MonoBehaviour _drillTipSourceBehaviour;
        [SerializeField, Range(0.002f, 0.02f)] float _markerSizeMetres = 0.006f;
        [SerializeField] Color _greenColour = new(0.30f, 0.95f, 0.40f, 1.0f);
        [SerializeField] Color _yellowColour = new(1.00f, 0.85f, 0.20f, 1.0f);
        [SerializeField] Color _redColour = new(0.95f, 0.30f, 0.30f, 1.0f);

        IDrillTipSource _source;
        GameObject _sphere;
        Material _material;

        void Awake()
        {
            _source = _drillTipSourceBehaviour as IDrillTipSource;
            _sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphere.name = "TipMarker";
            _sphere.transform.SetParent(transform, worldPositionStays: false);
            _sphere.transform.localScale = Vector3.one * _markerSizeMetres;
            DestroyImmediate(_sphere.GetComponent<Collider>());
            _material = new Material(FindUnlitShader()) { color = _greenColour };
            _sphere.GetComponent<MeshRenderer>().sharedMaterial = _material;
        }

        static Shader FindUnlitShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }

        void LateUpdate()
        {
            if (_source == null) return;
            var tip = _source.Current;
            if (!tip.IsValid)
            {
                _sphere.SetActive(false);
                return;
            }
            _sphere.SetActive(true);
            _sphere.transform.position = tip.Position;

            var gate = _guidance != null ? _guidance.LatestWorstGate : Gate.Green;
            _material.color = gate switch
            {
                Gate.Green => _greenColour,
                Gate.Yellow => _yellowColour,
                _ => _redColour,
            };
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
