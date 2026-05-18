using System;
using System.Collections.Generic;
using Host.App;
using Host.Participant;
using Host.Registration;
using UnityEngine;
using UnityEngine.UIElements;

namespace Host.UI.Calibration
{
    /// Wires the calibration UI Toolkit panel to PointSetRegistration.Solve.
    /// Caller supplies (a) reference landmarks in phantom model-space and (b) a delegate
    /// that returns the current measured tip position. This keeps the controller agnostic
    /// of where the tip pose comes from (real controller, scripted simulator, etc).
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("SONAR Host/UI/Calibration Controller")]
    public sealed class CalibrationController : MonoBehaviour
    {
        public event Action<CalibrationResult> OnAccepted;
        public event Action OnRetry;

        [SerializeField, Range(0.001f, 0.01f)] float _gateMetres = 0.002f;

        Vector3[] _referenceLandmarks;
        Func<Vector3> _currentTip;
        readonly List<Vector3> _samples = new();
        RegistrationResult _latest;

        UIDocument _doc;
        Label _sampleCount;
        Label _tre;
        Label _validation;
        VisualElement _landmarks;
        Button _sampleBtn;
        Button _acceptBtn;

        /// Configure before enabling. Reference landmarks come from phantom/assets/landmarks_model_space.json;
        /// supply them after the host loads the active scenario's phantom version.
        public void Configure(Vector3[] referenceLandmarks, Func<Vector3> currentTip)
        {
            _referenceLandmarks = referenceLandmarks ?? Array.Empty<Vector3>();
            _currentTip = currentTip;
        }

        void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            var root = _doc.rootVisualElement;
            _sampleCount = root.Q<Label>("sample-count");
            _tre = root.Q<Label>("tre");
            _validation = root.Q<Label>("validation");
            _landmarks = root.Q<VisualElement>("landmarks");
            _sampleBtn = root.Q<Button>("sample");
            _acceptBtn = root.Q<Button>("accept");

            _sampleBtn.clicked += HandleSample;
            _acceptBtn.clicked += HandleAccept;
            root.Q<Button>("retry").clicked += HandleRetry;

            RefreshLandmarkList();
            RefreshStatus();
        }

        void HandleSample()
        {
            if (_referenceLandmarks == null || _referenceLandmarks.Length == 0)
            {
                _validation.text = "No reference landmarks configured.";
                return;
            }
            if (_samples.Count >= _referenceLandmarks.Length)
            {
                _validation.text = "All landmarks sampled. Accept or retry.";
                return;
            }
            if (_currentTip == null)
            {
                _validation.text = "No tip source supplied to controller.";
                return;
            }
            _samples.Add(_currentTip());
            _validation.text = "";

            if (_samples.Count == _referenceLandmarks.Length)
                TrySolve();
            RefreshLandmarkList();
            RefreshStatus();
        }

        void HandleRetry()
        {
            _samples.Clear();
            _latest = null;
            _validation.text = "";
            RefreshLandmarkList();
            RefreshStatus();
            OnRetry?.Invoke();
        }

        void HandleAccept()
        {
            if (_latest == null || _latest.RmseMeters > _gateMetres)
            {
                _validation.text = $"TRE above {_gateMetres * 1000f:0.0} mm gate; resample worst landmark.";
                return;
            }
            var calib = new CalibrationResult
            {
                TreMm = _latest.RmseMeters * 1000.0,
                MeetsGate = true,
                PhantomToWorldPositionRaw = new[] { _latest.Translation.x, _latest.Translation.y, _latest.Translation.z },
                PhantomToWorldRotationRaw = new[] { _latest.Rotation.x, _latest.Rotation.y, _latest.Rotation.z, _latest.Rotation.w },
                LandmarkCount = _latest.LandmarkCount,
                RetryCount = 0,
            };
            if (AppLifecycle.Instance != null && AppLifecycle.Instance.ActiveSession != null)
                AppLifecycle.Instance.ActiveSession.Calibration = calib;
            OnAccepted?.Invoke(calib);
        }

        void TrySolve()
        {
            try
            {
                _latest = PointSetRegistration.Solve(_referenceLandmarks, _samples.ToArray());
            }
            catch (Exception e)
            {
                _validation.text = $"Solver failed: {e.Message}";
                _latest = null;
            }
        }

        void RefreshStatus()
        {
            int total = _referenceLandmarks?.Length ?? 0;
            _sampleCount.text = $"Sampled: {_samples.Count} / {total}";
            _tre.text = _latest != null
                ? $"TRE: {_latest.RmseMeters * 1000f:0.00} mm"
                : "TRE: —";
            bool canAccept = _latest != null && _latest.RmseMeters <= _gateMetres;
            _acceptBtn.SetEnabled(canAccept);
        }

        void RefreshLandmarkList()
        {
            _landmarks.Clear();
            int total = _referenceLandmarks?.Length ?? 0;
            for (int i = 0; i < total; i++)
            {
                bool sampled = i < _samples.Count;
                string label;
                string cls;
                if (!sampled)
                {
                    label = $"Landmark {i + 1}: pending";
                    cls = "landmark-pending";
                }
                else if (_latest != null && i < _latest.PerLandmarkResidualsMeters.Count)
                {
                    float r = _latest.PerLandmarkResidualsMeters[i];
                    label = $"Landmark {i + 1}: {r * 1000f:0.00} mm residual";
                    cls = r <= _gateMetres ? "landmark-accepted" : "landmark-over-budget";
                }
                else
                {
                    label = $"Landmark {i + 1}: sampled";
                    cls = "landmark-accepted";
                }
                var lbl = new Label(label);
                lbl.AddToClassList(cls);
                _landmarks.Add(lbl);
            }
        }
    }
}
