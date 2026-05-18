using System;
using System.Collections.Generic;
using Host.Visualization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Host.Tools.Segmentation
{
    /// Drives the multi-segment segmentation panel. Owns a SegmentationDoc with
    /// 0..N intensity-range segments; one is active and drives the threshold
    /// sliders, colour swatches, and the brush mask.
    ///
    /// Workstation reacts to two events:
    ///   - OnDocChanged → persist the doc to disk
    ///   - OnActiveSegmentChanged → flush the previous segment's mask, then
    ///     restore the new segment's mask onto the VolumeRenderer
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("SONAR Host/Tools/Segmentation Controller")]
    public sealed class SegmentationController : MonoBehaviour
    {
        public event Action OnClose;
        public event Action<SegmentationDoc> OnDocChanged;
        public event Action<string, string> OnActiveSegmentChanged; // (oldId, newId)

        UIDocument _doc;
        Label _emptyMessage;
        VisualElement _content;
        Toggle _enabled;
        Toggle _brushEnabled;
        Toggle _brushErase;
        Slider _low, _high;
        SliderInt _brushRadius;
        Label _lowValue, _highValue, _brushRadiusValue, _activeSegmentLabel;
        Slider _globalOpacity, _activeOpacity;
        Label _globalOpacityValue, _activeOpacityValue;
        VisualElement _segmentsList;
        TextField _newSegmentName;
        Button _segmentAdd, _brushClear, _closeBtn;
        Button _colRed, _colGreen, _colYellow, _colBlue;

        VolumeRenderer _renderer;
        SegmentationDoc _doc_model;
        BrushPlacer _brushPlacer;

        // Cycle of default colours used when adding new segments.
        static readonly Color[] DefaultColors =
        {
            new(1.00f, 0.42f, 0.29f),
            new(0.36f, 0.82f, 0.48f),
            new(0.96f, 0.82f, 0.25f),
            new(0.29f, 0.62f, 1.00f),
            new(0.80f, 0.45f, 0.95f),
            new(0.45f, 0.95f, 0.95f),
        };

        Segment Active
        {
            get
            {
                if (_doc_model == null) return null;
                foreach (var s in _doc_model.Segments)
                    if (s.Id == _doc_model.ActiveSegmentId) return s;
                return null;
            }
        }

        void OnEnable()
        {
            BindUi();
            RebuildSegmentList();
            ApplyActiveToUi();
        }

        void BindUi()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            var root = _doc?.rootVisualElement;
            if (root == null) return;
            _emptyMessage = root.Q<Label>("empty-message");
            _content = root.Q<VisualElement>("content");
            _enabled = root.Q<Toggle>("enabled");
            _brushEnabled = root.Q<Toggle>("brush-enabled");
            _brushErase = root.Q<Toggle>("brush-erase");
            _low = root.Q<Slider>("low");
            _high = root.Q<Slider>("high");
            _brushRadius = root.Q<SliderInt>("brush-radius");
            _lowValue = root.Q<Label>("low-value");
            _highValue = root.Q<Label>("high-value");
            _brushRadiusValue = root.Q<Label>("brush-radius-value");
            _activeSegmentLabel = root.Q<Label>("active-segment-label");
            _segmentsList = root.Q<VisualElement>("segments-list");
            _newSegmentName = root.Q<TextField>("new-segment-name");
            _segmentAdd = root.Q<Button>("segment-add");
            _brushClear = root.Q<Button>("brush-clear");
            _closeBtn = root.Q<Button>("close");
            _globalOpacity = root.Q<Slider>("global-opacity");
            _globalOpacityValue = root.Q<Label>("global-opacity-value");
            _activeOpacity = root.Q<Slider>("active-opacity");
            _activeOpacityValue = root.Q<Label>("active-opacity-value");
            _colRed = root.Q<Button>("color-red");
            _colGreen = root.Q<Button>("color-green");
            _colYellow = root.Q<Button>("color-yellow");
            _colBlue = root.Q<Button>("color-blue");

            if (_enabled == null) return;

            _enabled.UnregisterValueChangedCallback(OnEnabledChanged);
            _enabled.RegisterValueChangedCallback(OnEnabledChanged);
            _low.UnregisterValueChangedCallback(OnLowChanged);
            _low.RegisterValueChangedCallback(OnLowChanged);
            _high.UnregisterValueChangedCallback(OnHighChanged);
            _high.RegisterValueChangedCallback(OnHighChanged);
            if (_brushEnabled != null) { _brushEnabled.UnregisterValueChangedCallback(OnBrushEnabledChanged); _brushEnabled.RegisterValueChangedCallback(OnBrushEnabledChanged); }
            if (_brushErase != null)   { _brushErase.UnregisterValueChangedCallback(OnBrushEraseChanged);     _brushErase.RegisterValueChangedCallback(OnBrushEraseChanged); }
            if (_brushRadius != null)  { _brushRadius.UnregisterValueChangedCallback(OnBrushRadiusChanged);   _brushRadius.RegisterValueChangedCallback(OnBrushRadiusChanged); }

            if (_colRed != null)    { _colRed.clicked    -= ApplyColorRed;    _colRed.clicked    += ApplyColorRed; }
            if (_colGreen != null)  { _colGreen.clicked  -= ApplyColorGreen;  _colGreen.clicked  += ApplyColorGreen; }
            if (_colYellow != null) { _colYellow.clicked -= ApplyColorYellow; _colYellow.clicked += ApplyColorYellow; }
            if (_colBlue != null)   { _colBlue.clicked   -= ApplyColorBlue;   _colBlue.clicked   += ApplyColorBlue; }

            if (_brushClear != null) { _brushClear.clicked -= ClearMask;       _brushClear.clicked += ClearMask; }
            if (_segmentAdd != null) { _segmentAdd.clicked -= HandleAddSegment; _segmentAdd.clicked += HandleAddSegment; }
            if (_closeBtn != null)   { _closeBtn.clicked   -= HandleClose;       _closeBtn.clicked   += HandleClose; }

            if (_globalOpacity != null)
            {
                _globalOpacity.UnregisterValueChangedCallback(OnGlobalOpacityChanged);
                _globalOpacity.RegisterValueChangedCallback(OnGlobalOpacityChanged);
            }
            if (_activeOpacity != null)
            {
                _activeOpacity.UnregisterValueChangedCallback(OnActiveOpacityChanged);
                _activeOpacity.RegisterValueChangedCallback(OnActiveOpacityChanged);
            }
        }

        void OnGlobalOpacityChanged(ChangeEvent<float> evt)
        {
            if (_doc_model == null) return;
            _doc_model.GlobalOpacity = evt.newValue;
            if (_globalOpacityValue != null) _globalOpacityValue.text = evt.newValue.ToString("0.00");
            RaiseDocChanged();
        }

        void OnActiveOpacityChanged(ChangeEvent<float> evt)
        {
            var seg = Active; if (seg == null) return;
            seg.Opacity = evt.newValue;
            if (_activeOpacityValue != null) _activeOpacityValue.text = evt.newValue.ToString("0.00");
            RaiseDocChanged();
        }

        // ── Public API used by the workstation ──────────────────────────────

        public void Bind(VolumeRenderer renderer, SegmentationDoc doc)
        {
            _renderer = renderer;
            _doc_model = doc ?? new SegmentationDoc();
            _doc_model.Segments ??= new List<Segment>();
            if (string.IsNullOrEmpty(_doc_model.ActiveSegmentId) && _doc_model.Segments.Count > 0)
                _doc_model.ActiveSegmentId = _doc_model.Segments[0].Id;
            BindUi();
            if (_globalOpacity != null) _globalOpacity.SetValueWithoutNotify(Mathf.Clamp01(_doc_model.GlobalOpacity));
            if (_globalOpacityValue != null) _globalOpacityValue.text = _doc_model.GlobalOpacity.ToString("0.00");
            RebuildSegmentList();
            ApplyActiveToUi();
        }

        public void BindBrush(BrushPlacer placer)
        {
            _brushPlacer = placer;
            if (_brushPlacer != null && _renderer != null) _brushPlacer.Bind(_renderer);
            if (_brushPlacer != null) _brushPlacer.SetRadius(_brushRadius?.value ?? 4);
        }

        public void Unbind()
        {
            _renderer = null;
            _doc_model = null;
            UpdateAvailability();
        }

        /// Workstation calls this after restoring the brush mask for the active
        /// segment, in case the available-state visuals depend on having a volume.
        public void SyncAfterMaskSwap() => UpdateAvailability();

        // ── Segment list management ─────────────────────────────────────────

        void RebuildSegmentList()
        {
            if (_segmentsList == null || _doc_model == null) return;
            _segmentsList.Clear();
            foreach (var seg in _doc_model.Segments)
            {
                var local = seg; // capture
                var row = new VisualElement();
                row.AddToClassList("segment-row");
                if (seg.Id == _doc_model.ActiveSegmentId) row.AddToClassList("active");
                if (!seg.Enabled) row.AddToClassList("hidden-segment");
                row.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    // Only activate if the click hit the row itself (not the rename
                    // textfield or any button). UIElements bubbles events.
                    if (evt.target is TextField || evt.target is Button) return;
                    SetActiveSegment(local.Id);
                });

                var swatch = new VisualElement();
                swatch.AddToClassList("segment-row-color");
                swatch.style.backgroundColor = local.Color;
                row.Add(swatch);

                var name = new TextField { value = local.Label };
                name.AddToClassList("segment-row-name");
                name.RegisterValueChangedCallback(evt =>
                {
                    local.Label = evt.newValue;
                    RaiseDocChanged();
                    if (local.Id == _doc_model.ActiveSegmentId) UpdateActiveLabel();
                });
                row.Add(name);

                // Eye toggle — quickly hide/show a segment without changing
                // active or threshold. Re-uses the existing Segment.Enabled
                // flag, so palette + mesh + ortho-overlay all respect it.
                var eye = new Button(() =>
                {
                    local.Enabled = !local.Enabled;
                    if (local.Id == _doc_model.ActiveSegmentId) PushActiveToRenderer();
                    RaiseDocChanged();
                    RebuildSegmentList();
                }) { text = local.Enabled ? "●" : "○" };
                eye.AddToClassList("segment-row-eye");
                if (!local.Enabled) eye.AddToClassList("segment-row-eye-off");
                eye.tooltip = local.Enabled ? "Hide segment" : "Show segment";
                row.Add(eye);

                var del = new Button(() => HandleDeleteSegment(local.Id)) { text = "✕" };
                del.AddToClassList("segment-row-delete");
                row.Add(del);

                _segmentsList.Add(row);
            }
        }

        void HandleAddSegment()
        {
            if (_doc_model == null) return;
            string baseLabel = string.IsNullOrWhiteSpace(_newSegmentName?.value) ? "Region" : _newSegmentName.value.Trim();
            var seg = new Segment
            {
                Id = $"seg_{Guid.NewGuid().ToString("N").Substring(0, 8)}",
                Label = baseLabel,
                Enabled = true,
                Low = 0.40f,
                High = 0.60f,
                CreatedUtc = DateTime.UtcNow,
            };
            seg.Color = DefaultColors[_doc_model.Segments.Count % DefaultColors.Length];
            _doc_model.Segments.Add(seg);
            RaiseDocChanged();
            RebuildSegmentList();
            SetActiveSegment(seg.Id);
            if (_newSegmentName != null) _newSegmentName.value = "Region";
        }

        void HandleDeleteSegment(string segmentId)
        {
            if (_doc_model == null) return;
            int idx = _doc_model.Segments.FindIndex(s => s.Id == segmentId);
            if (idx < 0) return;
            bool removingActive = segmentId == _doc_model.ActiveSegmentId;
            _doc_model.Segments.RemoveAt(idx);
            if (removingActive)
            {
                string oldId = segmentId;
                string newId = _doc_model.Segments.Count > 0 ? _doc_model.Segments[0].Id : null;
                _doc_model.ActiveSegmentId = newId;
                OnActiveSegmentChanged?.Invoke(oldId, newId);
            }
            RaiseDocChanged();
            RebuildSegmentList();
            ApplyActiveToUi();
            // Workstation deletes the mask file separately via the doc-changed handler.
        }

        void SetActiveSegment(string segmentId)
        {
            if (_doc_model == null || segmentId == _doc_model.ActiveSegmentId) return;
            string oldId = _doc_model.ActiveSegmentId;
            _doc_model.ActiveSegmentId = segmentId;
            OnActiveSegmentChanged?.Invoke(oldId, segmentId);
            RaiseDocChanged();
            RebuildSegmentList();
            ApplyActiveToUi();
        }

        void ApplyActiveToUi()
        {
            UpdateAvailability();
            UpdateActiveLabel();
            var seg = Active;
            if (seg == null)
            {
                if (_enabled != null) _enabled.SetValueWithoutNotify(false);
                if (_low != null) _low.SetValueWithoutNotify(0.4f);
                if (_high != null) _high.SetValueWithoutNotify(0.6f);
                if (_lowValue != null) _lowValue.text = "—";
                if (_highValue != null) _highValue.text = "—";
                PushZeroToRenderer();
                return;
            }
            if (_enabled != null) _enabled.SetValueWithoutNotify(seg.Enabled);
            if (_low != null) _low.SetValueWithoutNotify(seg.Low);
            if (_high != null) _high.SetValueWithoutNotify(seg.High);
            if (_lowValue != null) _lowValue.text = seg.Low.ToString("0.00");
            if (_highValue != null) _highValue.text = seg.High.ToString("0.00");
            if (_activeOpacity != null) _activeOpacity.SetValueWithoutNotify(Mathf.Clamp01(seg.Opacity));
            if (_activeOpacityValue != null) _activeOpacityValue.text = seg.Opacity.ToString("0.00");
            PushActiveToRenderer();
        }

        void UpdateActiveLabel()
        {
            if (_activeSegmentLabel == null) return;
            var seg = Active;
            _activeSegmentLabel.text = seg != null ? $"Active: {seg.Label}" : "Active: —";
        }

        // ── Control handlers (modify Active segment) ────────────────────────

        void OnEnabledChanged(ChangeEvent<bool> evt)
        {
            var seg = Active; if (seg == null) return;
            seg.Enabled = evt.newValue;
            PushActiveToRenderer();
            RaiseDocChanged();
        }

        void OnLowChanged(ChangeEvent<float> evt)
        {
            var seg = Active; if (seg == null) return;
            seg.Low = evt.newValue;
            if (_lowValue != null) _lowValue.text = evt.newValue.ToString("0.00");
            PushActiveToRenderer();
            RaiseDocChanged();
        }

        void OnHighChanged(ChangeEvent<float> evt)
        {
            var seg = Active; if (seg == null) return;
            seg.High = evt.newValue;
            if (_highValue != null) _highValue.text = evt.newValue.ToString("0.00");
            PushActiveToRenderer();
            RaiseDocChanged();
        }

        void OnBrushEnabledChanged(ChangeEvent<bool> evt)
        {
            if (_brushPlacer == null) return;
            if (evt.newValue) _brushPlacer.Begin();
            else _brushPlacer.End();
        }

        void OnBrushEraseChanged(ChangeEvent<bool> evt) => _brushPlacer?.SetErasing(evt.newValue);

        void OnBrushRadiusChanged(ChangeEvent<int> evt)
        {
            if (_brushRadiusValue != null) _brushRadiusValue.text = evt.newValue.ToString();
            _brushPlacer?.SetRadius(evt.newValue);
        }

        void ApplyColorRed()    => ApplyColor(new Color(1.00f, 0.42f, 0.29f));
        void ApplyColorGreen()  => ApplyColor(new Color(0.36f, 0.82f, 0.48f));
        void ApplyColorYellow() => ApplyColor(new Color(0.96f, 0.82f, 0.25f));
        void ApplyColorBlue()   => ApplyColor(new Color(0.29f, 0.62f, 1.00f));

        void ApplyColor(Color c)
        {
            var seg = Active; if (seg == null) return;
            seg.Color = c;
            PushActiveToRenderer();
            RebuildSegmentList(); // refresh swatch
            RaiseDocChanged();
        }

        void ClearMask() => _renderer?.ClearMask();

        void HandleClose() { OnClose?.Invoke(); gameObject.SetActive(false); }

        // ── Renderer sync ───────────────────────────────────────────────────

        void PushActiveToRenderer()
        {
            if (_renderer == null) return;
            var seg = Active;
            if (seg == null) { PushZeroToRenderer(); return; }
            _renderer.SetSegmentEnabled(seg.Enabled);
            _renderer.SetSegmentRange(seg.Low, seg.High);
            _renderer.SetSegmentColor(seg.Color);
            _renderer.SetMaskColor(seg.Color);
        }

        void PushZeroToRenderer()
        {
            _renderer?.SetSegmentEnabled(false);
        }

        void RaiseDocChanged() => OnDocChanged?.Invoke(_doc_model);

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
