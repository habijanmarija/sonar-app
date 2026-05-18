using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Host.Platform;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Host.Telemetry
{
    [AddComponentMenu("SONAR/Telemetry/Session Runner")]
    public sealed class SessionRunner : MonoBehaviour
    {
        [Header("Server")]
        [SerializeField] string _serverUrl = "ws://127.0.0.1:8765";
        [SerializeField] bool _autoConnect = true;

        [Header("Session")]
        [SerializeField] string _scenario = "bone_drilling";
        [SerializeField, Range(1f, 90f)] float _poseUpdateHz = 30f;

        [Header("Tracked Transforms (optional)")]
        [Tooltip("Head pose source. Falls back to Camera.main when null.")]
        [SerializeField] Transform _headTransform;
        [SerializeField] Transform _controllerTransform;
        [SerializeField] Transform _drillTipTransform;

        ITelemetrySink _sink;
        string _sessionId;
        Stopwatch _deviceClock;
        float _poseInterval;
        float _poseTimer;
        bool _sessionActive;

        public string SessionId => _sessionId;
        public bool IsConnected => _sink?.IsConnected ?? false;
        public int DroppedEventCount => _sink?.DroppedEventCount ?? 0;

        public void SetHeadTransform(Transform t) => _headTransform = t;
        public void SetControllerTransform(Transform t) => _controllerTransform = t;
        public void SetDrillTipTransform(Transform t) => _drillTipTransform = t;

        async void OnEnable()
        {
            if (!_autoConnect) return;
            await StartSessionAsync(destroyCancellationToken);
        }

        async void OnDisable()
        {
            await EndSessionAsync();
        }

        public async Task StartSessionAsync(CancellationToken ct)
        {
            if (_sessionActive)
            {
                Debug.LogWarning("[SessionRunner] StartSessionAsync called while session already active.");
                return;
            }

            _sessionId = Guid.NewGuid().ToString("N");
            _deviceClock = Stopwatch.StartNew();
            _poseInterval = 1f / Mathf.Max(1f, _poseUpdateHz);
            _poseTimer = 0f;
            _sink = new WebSocketTelemetrySink();

            try
            {
                await _sink.ConnectAsync(new Uri(_serverUrl), ct);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SessionRunner] connect failed ({_serverUrl}): {e.Message}");
                _sink = null;
                return;
            }

            _sessionActive = true;
            EmitSessionStart();
            Debug.Log($"[SessionRunner] session {_sessionId} started against {_serverUrl} on {XrPlatform.Current.Vendor}");
        }

        public async Task EndSessionAsync()
        {
            if (!_sessionActive || _sink == null) return;

            Enqueue(TelemetryEventType.SessionEnd);

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _sink.DisconnectAsync(cts.Token);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SessionRunner] disconnect: {e.Message}");
            }

            _sessionActive = false;
            _sink = null;
            _deviceClock?.Stop();
            Debug.Log($"[SessionRunner] session {_sessionId} ended (dropped={DroppedEventCount})");
        }

        void Update()
        {
            if (!_sessionActive) return;

            _poseTimer += Time.unscaledDeltaTime;
            if (_poseTimer < _poseInterval) return;
            _poseTimer = 0f;

            var evt = NewEvent(TelemetryEventType.PoseUpdate);
            var head = _headTransform != null ? _headTransform : (Camera.main != null ? Camera.main.transform : null);
            if (head != null) evt.HeadPose = TelemetryPose.From(head);
            if (_controllerTransform != null) evt.ControllerPose = TelemetryPose.From(_controllerTransform);
            if (_drillTipTransform != null) evt.DrillTipPose = TelemetryPose.From(_drillTipTransform);
            _sink.Enqueue(evt);
        }

        public void EmitStepTransition(int currentStep, int totalSteps)
        {
            var evt = NewEvent(TelemetryEventType.StepTransition);
            evt.CurrentStep = currentStep;
            evt.TotalSteps = totalSteps;
            _sink?.Enqueue(evt);
        }

        public void EmitError(string code, string detail)
        {
            var evt = NewEvent(TelemetryEventType.Error);
            evt.ErrorCode = code;
            evt.ErrorDetail = detail;
            _sink?.Enqueue(evt);
        }

        public void EmitCalibration(double targetRegistrationErrorMm)
        {
            var evt = NewEvent(TelemetryEventType.Calibration);
            evt.TargetRegistrationErrorMm = targetRegistrationErrorMm;
            _sink?.Enqueue(evt);
        }

        public void EmitUserAction(string action, IDictionary<string, object> data = null)
        {
            var evt = NewEvent(TelemetryEventType.UserAction);
            var payload = data != null ? new Dictionary<string, object>(data) : new Dictionary<string, object>();
            payload["action"] = action;
            evt.CustomData = payload;
            _sink?.Enqueue(evt);
        }

        void Enqueue(string eventType) => _sink?.Enqueue(NewEvent(eventType));

        void EmitSessionStart()
        {
            var platform = XrPlatform.Current;
            var evt = NewEvent(TelemetryEventType.SessionStart);
            evt.CustomData = new Dictionary<string, object>
            {
                ["vendor"] = platform.Vendor.ToString(),
                ["runtime"] = platform.RuntimeName,
                ["has_passthrough"] = platform.HasPassthrough,
                ["has_hand_tracking"] = platform.HasHandTracking,
                ["has_eye_tracking"] = platform.HasEyeTracking,
                ["unity_version"] = Application.unityVersion,
                ["device_model"] = SystemInfo.deviceModel,
            };
            _sink?.Enqueue(evt);
        }

        TelemetryEvent NewEvent(string eventType) => new()
        {
            SessionId = _sessionId,
            TimestampUtc = DateTime.UtcNow.ToString("o"),
            DeviceTimestampMs = _deviceClock?.ElapsedMilliseconds ?? 0,
            EventType = eventType,
            Scenario = _scenario,
        };
    }
}
