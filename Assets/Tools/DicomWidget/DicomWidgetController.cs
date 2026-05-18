using System;
using System.Collections.Generic;
using System.Linq;
using Host.Patients;
using UnityEngine;
using UnityEngine.UIElements;

namespace Host.Tools.DicomWidget
{
    /// 2D slice viewer for the active patient's image series. Renders one slice at a
    /// time as the background image of a VisualElement, navigable by a SliderInt and
    /// a dropdown to switch between series (CT / MR / ...).
    ///
    /// Source-format agnostic: relies on the ImageSeries handles already populated by
    /// PatientLoadService. Whether the slices came from PNG sequences (today) or real
    /// DICOM (Phase F via fo-dicom) is opaque to this controller.
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("SONAR Host/Tools/DICOM Widget Controller")]
    public sealed class DicomWidgetController : MonoBehaviour
    {
        public event Action OnClose;

        UIDocument _doc;
        Label _emptyMessage;
        VisualElement _content;
        DropdownField _seriesPicker;
        VisualElement _sliceImage;
        Label _sliceLabel;
        SliderInt _sliceSlider;

        readonly List<ImageSeries> _series = new();
        int _activeIndex = -1;

        void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            var root = _doc.rootVisualElement;
            _emptyMessage = root.Q<Label>("empty-message");
            _content = root.Q<VisualElement>("content");
            _seriesPicker = root.Q<DropdownField>("series-picker");
            _sliceImage = root.Q<VisualElement>("slice-image");
            _sliceLabel = root.Q<Label>("slice-label");
            _sliceSlider = root.Q<SliderInt>("slice-slider");

            root.Q<Button>("close").clicked += () => { OnClose?.Invoke(); gameObject.SetActive(false); };
            _seriesPicker.RegisterValueChangedCallback(_ => SelectSeries(_seriesPicker.index));
            _sliceSlider.RegisterValueChangedCallback(evt => ShowSlice(evt.newValue));

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

        void HandlePatientLoaded(Patient p)
        {
            _series.Clear();
            if (p?.Series != null)
            {
                foreach (var s in p.Series.Values)
                    if (s is ImageSeries imgSeries && imgSeries.SliceCount > 0)
                        _series.Add(imgSeries);
            }

            if (_series.Count == 0)
            {
                _emptyMessage.text = "No image series in this patient.";
                _emptyMessage.style.display = DisplayStyle.Flex;
                _content?.RemoveFromClassList("visible");
                return;
            }

            _emptyMessage.style.display = DisplayStyle.None;
            _content.AddToClassList("visible");

            _seriesPicker.choices = _series
                .Select(s => $"{s.Id} ({s.Modality}, {s.SliceCount} slices)")
                .ToList();
            _seriesPicker.SetValueWithoutNotify(_seriesPicker.choices[0]);
            SelectSeries(0);
        }

        void HandlePatientUnloaded(Patient _)
        {
            _series.Clear();
            _activeIndex = -1;
            if (_emptyMessage != null)
            {
                _emptyMessage.text = "No series loaded.";
                _emptyMessage.style.display = DisplayStyle.Flex;
            }
            _content?.RemoveFromClassList("visible");
        }

        void SelectSeries(int index)
        {
            if (index < 0 || index >= _series.Count) return;
            _activeIndex = index;
            var s = _series[index];
            _sliceSlider.lowValue = 0;
            _sliceSlider.highValue = Mathf.Max(0, s.SliceCount - 1);
            _sliceSlider.SetValueWithoutNotify(0);
            ShowSlice(0);
        }

        void ShowSlice(int slice)
        {
            if (_activeIndex < 0 || _activeIndex >= _series.Count) return;
            var s = _series[_activeIndex];
            var tex = s.Slice(slice);
            if (tex == null) return;
            _sliceImage.style.backgroundImage = new StyleBackground(tex);
            _sliceLabel.text = $"Slice {slice + 1} / {s.SliceCount}";
        }
    }
}
