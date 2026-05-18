using System;
using Host.Patients;
using UnityEngine;
using UnityEngine.UIElements;

namespace Host.Tools.PatientBriefing
{
    /// Read-only briefing for the active Patient. Subscribes to PatientEventSystem
    /// so it auto-renders when a patient is loaded, and clears on unload.
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("SONAR Host/Tools/Patient Briefing Controller")]
    public sealed class PatientBriefingController : MonoBehaviour
    {
        public event Action OnClose;
        public event Action OnBackToPatients;

        UIDocument _doc;
        Label _emptyMessage;
        VisualElement _content;
        Label _idLabel, _pseudonymLabel, _ageLabel, _sexLabel, _capturedLabel, _indicationLabel, _historyLabel, _dataLabel;

        void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            var root = _doc.rootVisualElement;
            _emptyMessage = root.Q<Label>("empty-message");
            _content = root.Q<VisualElement>("content");
            _idLabel = root.Q<Label>("patient-id");
            _pseudonymLabel = root.Q<Label>("pseudonym");
            _ageLabel = root.Q<Label>("age-band");
            _sexLabel = root.Q<Label>("sex");
            _capturedLabel = root.Q<Label>("captured");
            _indicationLabel = root.Q<Label>("indication");
            _historyLabel = root.Q<Label>("history");
            _dataLabel = root.Q<Label>("data-summary");

            root.Q<Button>("close").clicked += () => { OnClose?.Invoke(); gameObject.SetActive(false); };
            var backBtn = root.Q<Button>("back-to-patients");
            if (backBtn != null) backBtn.clicked += () => OnBackToPatients?.Invoke();

            PatientEventSystem.PatientLoaded += Render;
            PatientEventSystem.PatientUnloaded += Clear;

            // Render whatever's already active (handles the case where this panel enables
            // after a patient was loaded by PatientSelector earlier).
            if (PatientRegistry.Active != null) Render(PatientRegistry.Active);
            else Clear(null);
        }

        void OnDisable()
        {
            PatientEventSystem.PatientLoaded -= Render;
            PatientEventSystem.PatientUnloaded -= Clear;
        }

        void Render(Patient p)
        {
            var m = p?.Meta;
            if (m == null) { Clear(null); return; }

            _emptyMessage.style.display = DisplayStyle.None;
            _content.AddToClassList("visible");

            _idLabel.text = m.PatientId ?? "—";
            _pseudonymLabel.text = m.Pseudonym ?? "—";
            _ageLabel.text = m.AgeBand ?? "—";
            _sexLabel.text = m.Sex ?? "—";
            _capturedLabel.text = m.CapturedUtc == default ? "—" : m.CapturedUtc.ToString("yyyy-MM-dd");
            _indicationLabel.text = m.Indication ?? "—";
            _historyLabel.text = string.IsNullOrEmpty(m.History) ? "—" : m.History;

            int meshes = p.Meshes?.Count ?? 0;
            int series = p.Series?.Count ?? 0;
            _dataLabel.text = $"{meshes} mesh{(meshes == 1 ? "" : "es")} loaded, {series} DICOM series";
        }

        void Clear(Patient _)
        {
            _emptyMessage.style.display = DisplayStyle.Flex;
            _content.RemoveFromClassList("visible");
        }
    }
}
