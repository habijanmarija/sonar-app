using System;
using System.Threading.Tasks;
using Host.App;
using Host.App.Backend;
using UnityEngine;
using UnityEngine.UIElements;

namespace Host.UI.Results
{
    /// Lists sessions from the backend and renders the SessionSummary for the selected one.
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("SONAR Host/UI/Results Controller")]
    public sealed class ResultsController : MonoBehaviour
    {
        public event Action OnClose;

        UIDocument _doc;
        BackendClient _client;
        VisualElement _sessionsList;
        VisualElement _detail;
        Label _detailEmpty;
        VisualElement _stepsList;
        Label _dSession, _dParticipant, _dMode, _dVendor, _dDuration, _dTre, _dErrors, _dSmoothness, _dPath, _dCompletion;

        void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            var root = _doc.rootVisualElement;
            _sessionsList = root.Q<VisualElement>("sessions-list");
            _detail = root.Q<VisualElement>("detail");
            _detailEmpty = root.Q<Label>("detail-empty");
            _stepsList = root.Q<VisualElement>("steps-list");
            _dSession = root.Q<Label>("d-session");
            _dParticipant = root.Q<Label>("d-participant");
            _dMode = root.Q<Label>("d-mode");
            _dVendor = root.Q<Label>("d-vendor");
            _dDuration = root.Q<Label>("d-duration");
            _dTre = root.Q<Label>("d-tre");
            _dErrors = root.Q<Label>("d-errors");
            _dSmoothness = root.Q<Label>("d-smoothness");
            _dPath = root.Q<Label>("d-path");
            _dCompletion = root.Q<Label>("d-completion");

            _client = new BackendClient(AppLifecycle.Instance?.Settings);
            root.Q<Button>("refresh").clicked += () => _ = Refresh();
            root.Q<Button>("close").clicked += () => OnClose?.Invoke();

            _ = Refresh();
        }

        async Task Refresh()
        {
            _sessionsList.Clear();
            var list = await _client.ListSessionsAsync();
            if (list == null || list.Sessions == null)
            {
                _sessionsList.Add(new Label("Backend unreachable. Check Settings → backend URL."));
                return;
            }
            if (list.Sessions.Length == 0)
            {
                _sessionsList.Add(new Label("No sessions yet."));
                return;
            }
            for (int i = list.Sessions.Length - 1; i >= 0; i--)
            {
                var id = list.Sessions[i];
                var btn = new Button(() => _ = ShowSummary(id)) { text = id };
                _sessionsList.Add(btn);
            }
        }

        async Task ShowSummary(string sessionId)
        {
            var s = await _client.GetSummaryAsync(sessionId);
            if (s == null)
            {
                _detailEmpty.text = $"Failed to load summary for {sessionId}.";
                _detail.RemoveFromClassList("visible");
                return;
            }
            _detailEmpty.text = "";
            _detail.AddToClassList("visible");

            _dSession.text = $"Session: {s.SessionId}";
            _dParticipant.text = $"Participant: {s.ParticipantId}";
            _dMode.text = $"Mode: {s.Mode}";
            _dVendor.text = $"Vendor: {s.Vendor}";
            _dDuration.text = $"Duration: {s.DurationSeconds:0.0} s";
            _dTre.text = s.Calibration?.InitialTreMm.HasValue == true
                ? $"Initial TRE: {s.Calibration.InitialTreMm.Value:0.00} mm  (max {s.Calibration.MaxTreMm:0.00})"
                : "Initial TRE: —";
            _dErrors.text = $"Total errors: {s.Global?.TotalErrors ?? 0}";
            _dSmoothness.text = $"Smoothness: {s.Global?.TrajectorySmoothnessScore:0.000}";
            _dPath.text = s.Global?.PathEfficiency.HasValue == true
                ? $"Path efficiency: {s.Global.PathEfficiency.Value:0.000}"
                : "Path efficiency: —";
            _dCompletion.text = $"Task completion: {(s.Global?.TaskCompletion == true ? "yes" : "no")}";

            _stepsList.Clear();
            if (s.StepOutcomes != null)
            {
                foreach (var step in s.StepOutcomes)
                {
                    string errSummary = "";
                    if (step.Errors != null && step.Errors.Count > 0)
                    {
                        var parts = new System.Collections.Generic.List<string>();
                        foreach (var kv in step.Errors)
                            parts.Add($"{kv.Key}×{kv.Value}");
                        errSummary = " — errors: " + string.Join(", ", parts);
                    }
                    _stepsList.Add(new Label(
                        $"Step {step.Step}: {step.DurationS:0.0} s @ t={step.FirstEnteredAtS:0.0}s{errSummary}"));
                }
            }
        }
    }
}
