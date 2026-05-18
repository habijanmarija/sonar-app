using Host.Platform;
using UnityEngine;

namespace Sonar.Tracking
{
    /// Phase 1 drill-tip source: composes the controller transform with a printed-jig offset
    /// loaded from Resources/ToolOffsets.json, then smooths the result.
    /// Pair with DrillTipTransformMirror to publish the result as a Unity Transform.
    [AddComponentMenu("SONAR/Tracking/Controller Drill Tip Source")]
    public sealed class ControllerDrillTipSource : MonoBehaviour, IDrillTipSource
    {
        [SerializeField] Transform _controllerTransform;
        [SerializeField] string _offsetKeyOverride;
        [SerializeField] bool _useSmoothing = true;
        [SerializeField] Vector3 _fallbackOffsetMetres = new(0f, -0.08f, 0.02f);

        PoseSmoother _smoother;
        ToolOffsetEntry _offset;
        DrillTipPose _current = DrillTipPose.Invalid;

        public DrillTipPose Current => _current;
        public void SetControllerTransform(Transform t) => _controllerTransform = t;

        void Awake()
        {
            _smoother = new PoseSmoother();

            var offsets = ToolOffsets.LoadFromResources();
            var key = string.IsNullOrEmpty(_offsetKeyOverride)
                ? DefaultOffsetKey()
                : _offsetKeyOverride;
            if (!offsets.TryGet(key, out _offset))
            {
                Debug.LogWarning($"[Tracking] no tool offset for key '{key}'; using fallback offset.");
                _offset = new ToolOffsetEntry
                {
                    PositionRaw = new[] { _fallbackOffsetMetres.x, _fallbackOffsetMetres.y, _fallbackOffsetMetres.z },
                    RotationRaw = new[] { 0f, 0f, 0f, 1f },
                };
            }
        }

        void Update()
        {
            if (_controllerTransform == null)
            {
                _current = DrillTipPose.Invalid;
                return;
            }

            var rawWorldPosition = _controllerTransform.TransformPoint(_offset.Position);
            var rawWorldAxis = _controllerTransform.rotation * (_offset.Rotation * Vector3.forward);

            Vector3 pos, axis;
            if (_useSmoothing)
            {
                _smoother.Push(rawWorldPosition, rawWorldAxis, Time.unscaledTime);
                pos = _smoother.SmoothedPosition;
                axis = _smoother.SmoothedAxis;
            }
            else
            {
                pos = rawWorldPosition;
                axis = rawWorldAxis.normalized;
            }

            _current = new DrillTipPose(pos, axis, Time.unscaledTime);
        }

        string DefaultOffsetKey()
        {
            var vendor = XrPlatform.Current?.Vendor ?? XrVendor.EditorFlat;
            return vendor switch
            {
                XrVendor.MetaQuest => "Meta/QuestTouchPlus_R",
                XrVendor.ViveFocus => "HTC/FocusVision_R",
                _ => "Editor/Flat_R",
            };
        }
    }
}
