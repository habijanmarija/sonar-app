using System;
using Host.Patients;
using UnityEngine;
using UnityEngine.UIElements;

namespace Host.Tools.ViewControl
{
    /// Reads PatientMeta.Views from the active patient and renders a button per preset.
    /// Click → snap Camera.main to the preset's position + rotation. "Reset" returns to
    /// the camera pose recorded on this component's first OnEnable.
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("SONAR Host/Tools/View Control Controller")]
    public sealed class ViewControlController : MonoBehaviour
    {
        public event Action OnClose;

        UIDocument _doc;
        Label _emptyMessage;
        VisualElement _list;
        Vector3 _defaultPos;
        Quaternion _defaultRot;
        bool _defaultCaptured;

        void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            var root = _doc.rootVisualElement;
            _emptyMessage = root.Q<Label>("empty-message");
            _list = root.Q<VisualElement>("views-list");

            root.Q<Button>("close").clicked += () => { OnClose?.Invoke(); gameObject.SetActive(false); };
            root.Q<Button>("reset").clicked += ResetCamera;

            CaptureDefaultIfNeeded();

            PatientEventSystem.PatientLoaded += Rebuild;
            PatientEventSystem.PatientUnloaded += Clear;

            if (PatientRegistry.Active != null) Rebuild(PatientRegistry.Active);
            else Clear(null);
        }

        void OnDisable()
        {
            PatientEventSystem.PatientLoaded -= Rebuild;
            PatientEventSystem.PatientUnloaded -= Clear;
        }

        void CaptureDefaultIfNeeded()
        {
            if (_defaultCaptured) return;
            var cam = Camera.main;
            if (cam == null) return;
            _defaultPos = cam.transform.position;
            _defaultRot = cam.transform.rotation;
            _defaultCaptured = true;
        }

        void Rebuild(Patient p)
        {
            _list.Clear();
            var views = p?.Meta?.Views;
            if (views == null || views.Count == 0)
            {
                _emptyMessage.text = "No view presets defined in the manifest.";
                _emptyMessage.style.display = DisplayStyle.Flex;
                return;
            }
            _emptyMessage.style.display = DisplayStyle.None;

            foreach (var preset in views)
            {
                var btn = new Button(() => Snap(preset)) { text = preset.Name ?? preset.Id ?? "view" };
                btn.AddToClassList("view-button");
                _list.Add(btn);
            }
        }

        void Clear(Patient _)
        {
            _list.Clear();
            _emptyMessage.text = "No patient loaded.";
            _emptyMessage.style.display = DisplayStyle.Flex;
        }

        void Snap(ViewPreset preset)
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (preset.PositionRaw != null && preset.PositionRaw.Length == 3)
                cam.transform.position = new Vector3(preset.PositionRaw[0], preset.PositionRaw[1], preset.PositionRaw[2]);
            if (preset.RotationEulerRaw != null && preset.RotationEulerRaw.Length == 3)
                cam.transform.rotation = Quaternion.Euler(preset.RotationEulerRaw[0], preset.RotationEulerRaw[1], preset.RotationEulerRaw[2]);
        }

        void ResetCamera()
        {
            if (!_defaultCaptured) return;
            var cam = Camera.main;
            if (cam == null) return;
            cam.transform.position = _defaultPos;
            cam.transform.rotation = _defaultRot;
        }
    }
}
