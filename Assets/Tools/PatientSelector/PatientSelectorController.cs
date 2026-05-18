using System;
using Host.Patients;
using Host.Tools.Notifications;
using UnityEngine;
using UnityEngine.UIElements;

namespace Host.Tools.PatientSelector
{
    /// Scans PatientDirectoryLoader on enable, renders catalog as clickable rows.
    /// Click a row → PatientLoadService.Load → PatientRegistry.Activate → fires
    /// PatientEventSystem.PatientLoaded. Cross-tool subscribers (PatientBriefing,
    /// view controls, etc.) react via the event bus.
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("SONAR Host/Tools/Patient Selector Controller")]
    public sealed class PatientSelectorController : MonoBehaviour
    {
        public event Action<Patient> OnPatientPicked;
        public event Action OnClose;

        UIDocument _doc;
        VisualElement _list;
        Label _emptyMessage;
        Label _rootPath;

        void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            var root = _doc.rootVisualElement;
            _list = root.Q<VisualElement>("patients-list");
            _emptyMessage = root.Q<Label>("empty-message");
            _rootPath = root.Q<Label>("root-path");

            root.Q<Button>("refresh").clicked += Refresh;
            root.Q<Button>("close").clicked += () => OnClose?.Invoke();

            Refresh();
        }

        void Refresh()
        {
            _rootPath.text = $"Patient directory: {PatientDirectoryLoader.Root}";

            var catalog = PatientDirectoryLoader.Scan();
            PatientRegistry.SetCatalog(catalog);

            _list.Clear();
            if (catalog.Count == 0)
            {
                _emptyMessage.AddToClassList("visible");
                return;
            }
            _emptyMessage.RemoveFromClassList("visible");

            foreach (var meta in catalog)
            {
                var row = BuildRow(meta);
                _list.Add(row);
            }
        }

        VisualElement BuildRow(PatientMeta meta)
        {
            var row = new Button(() => HandlePick(meta));
            row.AddToClassList("patient-row");

            var idLabel = new Label(meta.PatientId);
            idLabel.AddToClassList("patient-id");
            row.Add(idLabel);

            var pseudonymLabel = new Label(meta.Pseudonym ?? "");
            pseudonymLabel.AddToClassList("patient-pseudonym");
            row.Add(pseudonymLabel);

            int meshCount = meta.Meshes?.Count ?? 0;
            int seriesCount = meta.DicomSeries?.Count ?? 0;
            var metaLabel = new Label(
                $"{meta.AgeBand ?? "—"}  |  {meshCount} mesh(es), {seriesCount} DICOM series  |  {meta.Indication ?? ""}");
            metaLabel.AddToClassList("patient-meta");
            row.Add(metaLabel);

            return row;
        }

        void HandlePick(PatientMeta meta)
        {
            var patient = PatientLoadService.Load(meta);
            if (patient == null)
            {
                NotificationSystem.Show($"Failed to load {meta.PatientId}.", NotificationSeverity.Error);
                return;
            }
            NotificationSystem.Show($"Loaded {meta.PatientId} ({patient.Meshes.Count} meshes).", NotificationSeverity.Info, 2.5f);
            OnPatientPicked?.Invoke(patient);
        }
    }
}
