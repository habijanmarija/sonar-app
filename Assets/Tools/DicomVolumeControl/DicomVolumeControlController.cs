using System;
using System.Collections.Generic;
using Host.Visualization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Host.Tools.DicomVolumeControl
{
    /// Threshold + density controls bound to a VolumeRenderer. Three presets set
    /// both windowing and tint for common tasks (soft tissue, bone, lung).
    /// (A manual color picker is editor-only in UI Toolkit; using preset buttons
    /// as the only tint source until UI Toolkit ships a runtime ColorField.)
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("SONAR Host/Tools/Dicom Volume Control Controller")]
    public sealed class DicomVolumeControlController : MonoBehaviour
    {
        public event Action OnClose;
        /// Workstation subscribes to this; arg is the series id the user picked from
        /// the dropdown. Workstation re-loads the volume from that series.
        public event Action<string> OnSeriesPicked;
        /// Fired when the Show volume / Show segments toggles change, in addition
        /// to the direct renderer.Set* call. Workstation listens to the second one
        /// so it can also hide/show the marching-cubes segment meshes.
        public event Action<bool> OnShowVolumeRequested;
        public event Action<bool> OnShowSegmentationRequested;

        UIDocument _doc;
        Label _emptyMessage;
        VisualElement _content;
        Slider _thresholdLow, _thresholdHigh, _density;
        Label _lowValue, _highValue, _densityValue;
        Button _presetSoft, _presetBone, _presetLung, _closeBtn;
        DropdownField _seriesPicker;
        Toggle _showVolume, _showSegmentation;

        VolumeRenderer _renderer;
        readonly List<string> _seriesIds = new();
        string _activeSeriesId;

        void OnEnable()
        {
            // Re-query every time: when UIDocument re-attaches the tree on enable it
            // may hand back fresh element instances; old refs become orphans whose
            // mutations don't show on screen. Idempotent: Unregister-then-Register
            // collapses duplicate handlers whether the tree is reused or rebuilt.
            BindUi();
            ApplyRendererToUi();
        }

        void BindUi()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            var root = _doc?.rootVisualElement;
            if (root == null) return;
            _emptyMessage = root.Q<Label>("empty-message");
            _content = root.Q<VisualElement>("content");
            _thresholdLow = root.Q<Slider>("threshold-low");
            _thresholdHigh = root.Q<Slider>("threshold-high");
            _density = root.Q<Slider>("density");
            _lowValue = root.Q<Label>("threshold-low-value");
            _highValue = root.Q<Label>("threshold-high-value");
            _densityValue = root.Q<Label>("density-value");
            _presetSoft = root.Q<Button>("preset-soft");
            _presetBone = root.Q<Button>("preset-bone");
            _presetLung = root.Q<Button>("preset-lung");
            _closeBtn = root.Q<Button>("close");
            _seriesPicker = root.Q<DropdownField>("series-picker");
            _showVolume = root.Q<Toggle>("show-volume");
            _showSegmentation = root.Q<Toggle>("show-segmentation");
            if (_thresholdLow == null || _thresholdHigh == null || _density == null) return;

            if (_showVolume != null)
            {
                _showVolume.UnregisterValueChangedCallback(OnShowVolumeChanged);
                _showVolume.RegisterValueChangedCallback(OnShowVolumeChanged);
            }
            if (_showSegmentation != null)
            {
                _showSegmentation.UnregisterValueChangedCallback(OnShowSegmentationChanged);
                _showSegmentation.RegisterValueChangedCallback(OnShowSegmentationChanged);
            }

            _thresholdLow.UnregisterValueChangedCallback(OnLowChanged);
            _thresholdLow.RegisterValueChangedCallback(OnLowChanged);
            _thresholdHigh.UnregisterValueChangedCallback(OnHighChanged);
            _thresholdHigh.RegisterValueChangedCallback(OnHighChanged);
            _density.UnregisterValueChangedCallback(OnDensityChanged);
            _density.RegisterValueChangedCallback(OnDensityChanged);

            if (_seriesPicker != null)
            {
                _seriesPicker.UnregisterValueChangedCallback(OnSeriesChanged);
                _seriesPicker.RegisterValueChangedCallback(OnSeriesChanged);
                RefreshSeriesDropdown();
            }

            if (_presetSoft != null) { _presetSoft.clicked -= ApplyPresetSoft; _presetSoft.clicked += ApplyPresetSoft; }
            if (_presetBone != null) { _presetBone.clicked -= ApplyPresetBone; _presetBone.clicked += ApplyPresetBone; }
            if (_presetLung != null) { _presetLung.clicked -= ApplyPresetLung; _presetLung.clicked += ApplyPresetLung; }
            if (_closeBtn != null)   { _closeBtn.clicked -= HandleClose;     _closeBtn.clicked += HandleClose; }
        }

        void OnSeriesChanged(ChangeEvent<string> evt)
        {
            int idx = _seriesPicker.index;
            if (idx < 0 || idx >= _seriesIds.Count) return;
            var pickedId = _seriesIds[idx];
            if (pickedId == _activeSeriesId) return;
            _activeSeriesId = pickedId;
            OnSeriesPicked?.Invoke(pickedId);
        }

        void RefreshSeriesDropdown()
        {
            if (_seriesPicker == null) return;
            var labels = new List<string>();
            for (int i = 0; i < _seriesIds.Count; i++) labels.Add(_seriesIds[i]);
            _seriesPicker.choices = labels;
            if (!string.IsNullOrEmpty(_activeSeriesId))
            {
                int idx = _seriesIds.IndexOf(_activeSeriesId);
                if (idx >= 0) _seriesPicker.SetValueWithoutNotify(labels[idx]);
            }
        }

        /// Workstation hands the list of selectable series + the one currently loaded.
        /// Labels in the dropdown are the series ids (short ascii) so they fit; we
        /// could swap in series descriptions later if needed.
        public void SetSeriesChoices(IReadOnlyList<string> seriesIds, string activeId)
        {
            _seriesIds.Clear();
            if (seriesIds != null) _seriesIds.AddRange(seriesIds);
            _activeSeriesId = activeId;
            RefreshSeriesDropdown();
        }

        void OnLowChanged(ChangeEvent<float> evt)     { _renderer?.SetThresholdLow(evt.newValue);  if (_lowValue != null) _lowValue.text = evt.newValue.ToString("0.00"); }
        void OnHighChanged(ChangeEvent<float> evt)    { _renderer?.SetThresholdHigh(evt.newValue); if (_highValue != null) _highValue.text = evt.newValue.ToString("0.00"); }
        void OnDensityChanged(ChangeEvent<float> evt) { _renderer?.SetDensityScale(evt.newValue);  if (_densityValue != null) _densityValue.text = evt.newValue.ToString("0.00"); }
        void OnShowVolumeChanged(ChangeEvent<bool> evt)       { _renderer?.SetShowVolume(evt.newValue); OnShowVolumeRequested?.Invoke(evt.newValue); }
        // "Show segments" controls the marching-cubes meshes the workstation
        // spawns per segment. The volumetric mask path inside the shader is
        // hard-disabled (see VolumeRenderer.Awake) so we don't double-render.
        void OnShowSegmentationChanged(ChangeEvent<bool> evt) { OnShowSegmentationRequested?.Invoke(evt.newValue); }

        void ApplyPresetSoft() => ApplyPreset(low: 0.30f, high: 0.85f, density: 1.5f, tint: new Color(1f, 0.92f, 0.85f));
        void ApplyPresetBone() => ApplyPreset(low: 0.70f, high: 1.00f, density: 3.0f, tint: new Color(1f, 0.98f, 0.94f));
        void ApplyPresetLung() => ApplyPreset(low: 0.05f, high: 0.30f, density: 0.8f, tint: new Color(0.85f, 0.92f, 1.0f));
        void HandleClose() { OnClose?.Invoke(); gameObject.SetActive(false); }

        /// Workstation calls this after spawning the VolumeRenderer for the active patient.
        public void Bind(VolumeRenderer renderer)
        {
            _renderer = renderer;
            // UI may not be bound yet if the panel hasn't been shown — OnEnable will
            // apply the cached renderer the next time the panel is opened.
            BindUi();
            ApplyRendererToUi();
        }

        void ApplyRendererToUi()
        {
            UpdateAvailability();
            if (_renderer == null) return;
            if (_thresholdLow != null) _thresholdLow.SetValueWithoutNotify(_renderer.ThresholdLow);
            if (_thresholdHigh != null) _thresholdHigh.SetValueWithoutNotify(_renderer.ThresholdHigh);
            if (_density != null) _density.SetValueWithoutNotify(_renderer.DensityScale);
            if (_lowValue != null) _lowValue.text = _renderer.ThresholdLow.ToString("0.00");
            if (_highValue != null) _highValue.text = _renderer.ThresholdHigh.ToString("0.00");
            if (_densityValue != null) _densityValue.text = _renderer.DensityScale.ToString("0.00");
        }

        public void Unbind()
        {
            _renderer = null;
            UpdateAvailability();
        }

        void ApplyPreset(float low, float high, float density, Color tint)
        {
            _thresholdLow.value = low;
            _thresholdHigh.value = high;
            _density.value = density;
            _renderer?.SetTint(tint);
        }

        void UpdateAvailability()
        {
            bool ok = _renderer != null && _renderer.HasVolume;
            if (_emptyMessage != null) _emptyMessage.style.display = ok ? DisplayStyle.None : DisplayStyle.Flex;
            if (_content != null)
            {
                if (ok) _content.AddToClassList("visible");
                else _content.RemoveFromClassList("visible");
            }
        }
    }
}
