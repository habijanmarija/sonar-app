using Sonar.Guidance;
using UnityEngine;

namespace Sonar.Visualization
{
    /// World-space 3D text that surfaces the latest hint above the trajectory.
    /// Uses Unity's legacy TextMesh — always available, no TMP setup required.
    [AddComponentMenu("SONAR/Visualization/Hint Label")]
    public sealed class HintLabel : MonoBehaviour
    {
        [SerializeField] GuidanceController _guidance;
        [SerializeField] float _heightOffsetM = 0.08f;
        [SerializeField, Range(0.0005f, 0.01f)] float _characterSizeM = 0.002f;

        TextMesh _text;
        Vector3 _anchor;

        void Awake()
        {
            var go = new GameObject("HintText");
            go.transform.SetParent(transform, worldPositionStays: false);
            _text = go.AddComponent<TextMesh>();
            _text.fontSize = 96;
            _text.characterSize = _characterSizeM;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.color = new Color(1f, 1f, 1f, 0.95f);
            _text.richText = false;
            _text.text = "";
        }

        void Start()
        {
            if (_guidance != null && _guidance.Config != null)
                _anchor = _guidance.Config.Trajectory.Entry;
        }

        void LateUpdate()
        {
            if (_guidance == null) return;
            _text.text = _guidance.LatestHint ?? "";
            _text.transform.position = _anchor + Vector3.up * _heightOffsetM;
            if (Camera.main != null)
                _text.transform.rotation = Quaternion.LookRotation(
                    _text.transform.position - Camera.main.transform.position);
        }
    }
}
