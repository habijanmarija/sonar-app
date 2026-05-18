using System;
using System.Collections.Generic;
using Host.Patients;
using UnityEngine;
using UnityEngine.UIElements;

namespace Host.Tools.OpacityControl
{
    /// Per-organ opacity sliders for the active patient. Subscribes to PatientEventSystem;
    /// rebuilds the slider list on PatientLoaded; clears on PatientUnloaded. Pushes opacity
    /// changes through an externally supplied callback so the workstation can update both
    /// the OrganMesh.Opacity field and the spawned MeshRenderer's material.
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("SONAR Host/Tools/Opacity Control Controller")]
    public sealed class OpacityControlController : MonoBehaviour
    {
        public event Action OnClose;

        /// Callback invoked when a slider value changes. Args: (organId, opacity in [0,1]).
        public Action<string, float> OnOpacityChanged;

        UIDocument _doc;
        Label _emptyMessage;
        VisualElement _list;
        readonly Dictionary<string, Label> _valueLabels = new();

        void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            var root = _doc.rootVisualElement;
            _emptyMessage = root.Q<Label>("empty-message");
            _list = root.Q<VisualElement>("sliders");
            root.Q<Button>("close").clicked += () => { OnClose?.Invoke(); gameObject.SetActive(false); };

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

        void Rebuild(Patient p)
        {
            _list.Clear();
            _valueLabels.Clear();
            if (p == null || p.Meshes == null || p.Meshes.Count == 0)
            {
                Clear(null);
                return;
            }
            _emptyMessage.style.display = DisplayStyle.None;

            foreach (var organ in p.Meshes.Values)
            {
                var row = new VisualElement();
                row.AddToClassList("organ-row");

                var header = new VisualElement();
                header.AddToClassList("organ-row-header");
                var nameLabel = new Label(organ.Name);
                nameLabel.AddToClassList("organ-row-name");
                var valueLabel = new Label($"{organ.Opacity * 100f:0}%");
                valueLabel.AddToClassList("organ-row-value");
                header.Add(nameLabel);
                header.Add(valueLabel);
                row.Add(header);

                var slider = new Slider(0f, 1f) { value = organ.Opacity };
                slider.RegisterValueChangedCallback(evt =>
                {
                    organ.Opacity = evt.newValue;
                    valueLabel.text = $"{evt.newValue * 100f:0}%";
                    OnOpacityChanged?.Invoke(organ.Id, evt.newValue);
                });
                row.Add(slider);

                _valueLabels[organ.Id] = valueLabel;
                _list.Add(row);
            }
        }

        void Clear(Patient _)
        {
            _list.Clear();
            _valueLabels.Clear();
            _emptyMessage.style.display = DisplayStyle.Flex;
        }
    }
}
