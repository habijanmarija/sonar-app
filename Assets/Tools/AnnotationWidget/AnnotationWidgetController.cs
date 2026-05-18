using System;
using System.Collections.Generic;
using Host.Patients;
using UnityEngine;
using UnityEngine.UIElements;

namespace Host.Tools.AnnotationWidget
{
    /// UI controller for the annotation widget. Renders the list of annotations on the
    /// active patient, the "Add at click" entry row, and per-row delete buttons.
    ///
    /// Persistence + 3D marker spawning are not done here — the controller raises events
    /// that the workstation orchestrator handles. This keeps the tool self-contained
    /// and lets the workstation own the scene-level concerns (raycast target, marker root).
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("SONAR Host/Tools/Annotation Widget Controller")]
    public sealed class AnnotationWidgetController : MonoBehaviour
    {
        public event Action OnClose;

        /// Raised when user clicks "Add at click" with `label`. The orchestrator
        /// should start AnnotationPlacer.Begin() and, on hit, call AddAt(point, label).
        public event Action<string> OnAddRequested;

        /// Raised when user clicks a row's Delete. The orchestrator removes the marker,
        /// updates the store, and calls RebuildList() to refresh the panel.
        public event Action<string> OnDeleteRequested;

        UIDocument _doc;
        Label _status;
        TextField _newLabel;
        VisualElement _list;

        IReadOnlyList<Annotation> _annotations;

        void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            var root = _doc.rootVisualElement;
            _status = root.Q<Label>("status");
            _newLabel = root.Q<TextField>("new-label");
            _list = root.Q<VisualElement>("annotations-list");

            root.Q<Button>("add").clicked += HandleAdd;
            root.Q<Button>("close").clicked += () => { OnClose?.Invoke(); gameObject.SetActive(false); };

            PatientEventSystem.PatientLoaded += HandlePatientLoaded;
            PatientEventSystem.PatientUnloaded += HandlePatientUnloaded;

            if (PatientRegistry.Active != null) HandlePatientLoaded(PatientRegistry.Active);
            else HandlePatientUnloaded(null);
        }

        void OnDisable()
        {
            PatientEventSystem.PatientLoaded -= HandlePatientLoaded;
            PatientEventSystem.PatientUnloaded -= HandlePatientUnloaded;
        }

        /// Called by the orchestrator after add/delete/load. Replaces the rendered list.
        /// Safe to call before OnEnable: when the UI tree isn't bound yet, we just
        /// stash the annotations and rendering happens during OnEnable's first pass.
        public void RebuildList(IReadOnlyList<Annotation> annotations)
        {
            _annotations = annotations;
            if (_list == null) return; // OnEnable hasn't run yet
            _list.Clear();
            if (_annotations == null || _annotations.Count == 0)
            {
                if (_status != null) _status.text = "No annotations on this patient.";
                return;
            }
            if (_status != null) _status.text = $"{_annotations.Count} annotation(s).";
            foreach (var a in _annotations)
            {
                var row = new VisualElement();
                row.AddToClassList("annotation-row");

                var label = new Label(string.IsNullOrEmpty(a.Label) ? $"(unlabelled #{a.Id})" : a.Label);
                label.AddToClassList("annotation-row-label");
                row.Add(label);

                var del = new Button(() => OnDeleteRequested?.Invoke(a.Id)) { text = "✕" };
                del.AddToClassList("annotation-row-delete");
                row.Add(del);

                _list.Add(row);
            }
        }

        public void SetStatus(string text)
        {
            if (_status != null) _status.text = text;
        }

        void HandleAdd()
        {
            var label = _newLabel.value?.Trim() ?? "";
            if (string.IsNullOrEmpty(label)) label = "marker";
            OnAddRequested?.Invoke(label);
            _newLabel.value = "";
            if (_status != null) _status.text = "Click in the scene to place the annotation…";
        }

        void HandlePatientLoaded(Patient p)
        {
            // Render whatever the workstation pushed in via RebuildList; if the workstation
            // hasn't pushed yet, OnEnable's first-pass render shows the empty state.
            RebuildList(_annotations);
        }

        void HandlePatientUnloaded(Patient _)
        {
            _annotations = null;
            if (_status != null) _status.text = "No patient loaded.";
            if (_list != null) _list.Clear();
        }
    }
}
