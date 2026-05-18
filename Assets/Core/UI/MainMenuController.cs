using System;
using Host.App;
using Host.Participant;
using Host.Scenarios;
using Host.Tools.Notifications;
using Host.UI.Calibration;
using Host.UI.Intake;
using Host.UI.Results;
using Host.UI.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace Host.UI
{
    /// Top-level UI orchestrator for the main-menu scene. Owns four UIDocument GameObjects
    /// (menu / intake / calibration / results / settings) and swaps between them. Routes
    /// the intake → calibration → launch flow into AppLifecycle.
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("SONAR Host/UI/Main Menu Controller")]
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] ParticipantIntakeController _intake;
        [SerializeField] CalibrationController _calibration;
        [SerializeField] ResultsController _results;
        [SerializeField] SettingsController _settings;

        [Header("Calibration reference data")]
        [Tooltip("Five reference landmarks in phantom-space. Override from a JSON asset for production; defaults shown are placeholders.")]
        [SerializeField] Vector3[] _referenceLandmarks = new Vector3[]
        {
            new(0.012f,  0.000f,  0.045f),
            new(0.038f,  0.005f,  0.020f),
            new(-0.036f, 0.005f,  0.019f),
            new(0.001f,  0.012f, -0.030f),
            new(0.024f,  0.008f,  0.000f),
        };

        UIDocument _doc;
        VisualElement _menuRoot;
        IScenarioModule _pendingScenario;

        /// Index into _referenceLandmarks for the flat-mode mock tip source.
        /// Each Sample press advances this; resets on Configure.
        int _mockSampleIndex;

        void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _menuRoot = _doc.rootVisualElement;

            RenderScenarios();
            _menuRoot.Q<Button>("settings").clicked += () => Switch(MenuState.Settings);
            _menuRoot.Q<Button>("results").clicked += () => Switch(MenuState.Results);
            _menuRoot.Q<Button>("quit").clicked += HandleQuit;

            if (_intake != null) _intake.OnContinue += HandleIntakeContinue;
            if (_intake != null) _intake.OnCancel += () => Switch(MenuState.Menu);
            if (_calibration != null) _calibration.OnAccepted += HandleCalibrationAccepted;
            if (_calibration != null) _calibration.OnRetry += () => NotificationSystem.Show("Calibration reset.", NotificationSeverity.Info, 2f);
            if (_results != null) _results.OnClose += () => Switch(MenuState.Menu);
            if (_settings != null) _settings.OnClose += () => Switch(MenuState.Menu);

            Switch(MenuState.Menu);
        }

        void RenderScenarios()
        {
            var list = _menuRoot.Q<VisualElement>("scenarios-list");
            list.Clear();
            foreach (var module in ScenarioRegistry.All)
            {
                var card = new VisualElement();
                card.AddToClassList("scenario-card");
                bool isStub = string.IsNullOrEmpty(module.SceneName);
                if (isStub) card.AddToClassList("stub");

                var name = new Label(module.DisplayName);
                name.AddToClassList("scenario-name");
                card.Add(name);

                var desc = new Label(module.Description);
                desc.AddToClassList("scenario-desc");
                card.Add(desc);

                var meta = new Label($"Modes: {string.Join(", ", module.SupportedModes)}  |  passthrough: {(module.Requirements.NeedsPassthrough ? "yes" : "no")}");
                meta.AddToClassList("scenario-meta");
                card.Add(meta);

                var launchBtn = new Button(() => LaunchScenario(module))
                {
                    text = isStub ? "Not implemented yet" : "Start session"
                };
                launchBtn.AddToClassList("scenario-launch");
                launchBtn.SetEnabled(!isStub);
                card.Add(launchBtn);

                list.Add(card);
            }
        }

        void HandleQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void LaunchScenario(IScenarioModule module)
        {
            if (string.IsNullOrEmpty(module.SceneName))
            {
                NotificationSystem.Show($"{module.DisplayName} is not yet implemented.", NotificationSeverity.Warning);
                return;
            }
            _pendingScenario = module;
            Switch(MenuState.Intake);
        }

        void HandleIntakeContinue(ParticipantSession session)
        {
            if (_pendingScenario != null) session.ScenarioId = _pendingScenario.Id;
            ParticipantStore.Save(session);
            if (AppLifecycle.Instance != null) AppLifecycle.Instance.GetType();  // keep ref alive

            // Flat-mode mock tip source: returns the next reference landmark + 0.3 mm
            // Gaussian-ish noise so the Horn solver still has work to do but TRE ≈ 0,
            // allowing the operator to click Accept and proceed. Real-drill wiring on
            // headset builds replaces this delegate with an IDrillTipSource reference.
            _mockSampleIndex = 0;
            _calibration?.Configure(_referenceLandmarks, () =>
            {
                if (_referenceLandmarks == null || _referenceLandmarks.Length == 0)
                    return Vector3.zero;
                var idx = Mathf.Clamp(_mockSampleIndex, 0, _referenceLandmarks.Length - 1);
                _mockSampleIndex++;
                return _referenceLandmarks[idx] + new Vector3(
                    UnityEngine.Random.Range(-0.0003f, 0.0003f),
                    UnityEngine.Random.Range(-0.0003f, 0.0003f),
                    UnityEngine.Random.Range(-0.0003f, 0.0003f));
            });
            Switch(MenuState.Calibration);
        }

        void HandleCalibrationAccepted(CalibrationResult result)
        {
            if (_pendingScenario == null || AppLifecycle.Instance == null)
            {
                NotificationSystem.Show("Internal state lost — please restart.", NotificationSeverity.Error);
                return;
            }
            var session = AppLifecycle.Instance.ActiveSession ?? ParticipantSession.New("unknown", "Novice", _pendingScenario.Id);
            session.Calibration = result;
            AppLifecycle.Instance.LaunchScenario(_pendingScenario, session);
        }

        void Switch(MenuState state)
        {
            _menuRoot.style.display = state == MenuState.Menu ? DisplayStyle.Flex : DisplayStyle.None;
            if (_intake != null) _intake.gameObject.SetActive(state == MenuState.Intake);
            if (_calibration != null) _calibration.gameObject.SetActive(state == MenuState.Calibration);
            if (_results != null) _results.gameObject.SetActive(state == MenuState.Results);
            if (_settings != null) _settings.gameObject.SetActive(state == MenuState.Settings);
        }

        enum MenuState { Menu, Intake, Calibration, Results, Settings }
    }
}
