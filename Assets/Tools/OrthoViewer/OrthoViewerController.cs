using System;
using System.Collections.Generic;
using Host.Tools.Segmentation;
using Host.Visualization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Host.Tools.OrthoViewer
{
    /// Three-pane orthogonal viewer: axial / sagittal / coronal cuts through
    /// the active volume, with the segmentation palette composited per pixel.
    ///
    /// Axes use texture-space names (W, H, D in the VolumeRenderer's mask)
    /// so the panel is consistent regardless of the patient's scan orientation.
    /// The workstation `Bind`s a VolumeRenderer + the segment palette; the
    /// renderer's `OnMaskChanged` triggers a slice refresh so brush strokes
    /// show up immediately in the 2D views.
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("SONAR Host/Tools/Ortho Viewer Controller")]
    public sealed class OrthoViewerController : MonoBehaviour
    {
        public event Action OnClose;
        /// Fired when the user drag-paints on a 2D slice. axis: 0=YZ, 1=XZ, 2=XY.
        /// Workstation handles the actual mask write so brush state (segment id,
        /// erase mode, radius) stays in one place (BrushPlacer / Segmentation).
        public event Action<Vector3Int, int> OnPaintRequest;

        UIDocument _doc;
        Label _emptyMessage;
        VisualElement _content;
        VisualElement _axialView, _sagittalView, _coronalView;
        SliderInt _axialSlider, _sagittalSlider, _coronalSlider;
        Label _axialIndex, _sagittalIndex, _coronalIndex;
        Button _closeBtn;

        VolumeRenderer _renderer;
        Color[] _palette = new Color[32];
        Texture2D _axialTex, _sagittalTex, _coronalTex;
        // The single 3D voxel position the three panes share. Each pane shows
        // its 2D slice at this position's axis index, and renders a horizontal
        // + vertical crosshair at the other two axes. Click on a pane → set
        // those two axes of focus; the slider on that pane keeps its own axis.
        Vector3Int _focus = new(0, 0, 0);
        // Overlay crosshair lines per pane — placed on top of the slice texture
        // so they stay 2 screen-pixels thick regardless of voxel resolution.
        VisualElement _axialH, _axialV, _sagittalH, _sagittalV, _coronalH, _coronalV;

        /// One screw to project onto each pane. EntryVoxel / TipVoxel are
        /// voxel-space coordinates (0..W-1, 0..H-1, 0..D-1); Color is the
        /// owning segment's colour so the line/dots match the 3D mesh.
        public struct ScrewProjection
        {
            public Vector3Int EntryVoxel;
            public Vector3Int TipVoxel;
            public Color Color;
        }

        /// Per-screw overlay set — three panes × (entry dot, tip dot, line).
        sealed class ScrewOverlay
        {
            public ScrewProjection Spec;
            public VisualElement AxialEntry, AxialTip, AxialLine;
            public VisualElement SagittalEntry, SagittalTip, SagittalLine;
            public VisualElement CoronalEntry, CoronalTip, CoronalLine;
        }
        readonly List<ScrewOverlay> _overlays = new();
        BrushPlacer _brush;
        bool _drawing;
        int _drawingAxis;
        // Display knobs (apply to slice extraction, not the source data):
        //   brightness: additive offset in [-0.5, 0.5]
        //   contrast:   multiplier (1 + contrast) centred at 0.5, in [-1, 3]
        //   smooth:     toggles Texture2D filter between Bilinear and Point
        float _brightness = 0f;
        float _contrast = 0f;
        bool _smooth = false;
        Slider _brightnessSlider, _contrastSlider;
        Label _brightnessValue, _contrastValue;
        Toggle _smoothToggle;

        void OnEnable()
        {
            // BindUi (re)attaches to UI elements every time the panel shows.
            // ResetSliders re-applies the volume's dimensions to the slider range —
            // critical because Bind() runs while the panel is still hidden (so the
            // sliders were null then) and the UXML default range is 0..0.
            BindUi();
            if (_renderer != null)
            {
                ResetSliders();
                RefreshAllSlices();
            }
            UpdateAvailability();
        }

        void OnDisable()
        {
            if (_renderer != null) _renderer.OnMaskChanged -= HandleMaskChanged;
        }

        void BindUi()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            var root = _doc?.rootVisualElement;
            if (root == null) return;
            _emptyMessage = root.Q<Label>("empty-message");
            _content = root.Q<VisualElement>("content");
            _axialView = root.Q<VisualElement>("axial-view");
            _sagittalView = root.Q<VisualElement>("sagittal-view");
            _coronalView = root.Q<VisualElement>("coronal-view");
            _axialSlider = root.Q<SliderInt>("axial-slider");
            _sagittalSlider = root.Q<SliderInt>("sagittal-slider");
            _coronalSlider = root.Q<SliderInt>("coronal-slider");
            _axialIndex = root.Q<Label>("axial-index");
            _sagittalIndex = root.Q<Label>("sagittal-index");
            _coronalIndex = root.Q<Label>("coronal-index");
            _closeBtn = root.Q<Button>("close");
            _brightnessSlider = root.Q<Slider>("brightness");
            _contrastSlider = root.Q<Slider>("contrast");
            _brightnessValue = root.Q<Label>("brightness-value");
            _contrastValue = root.Q<Label>("contrast-value");
            _smoothToggle = root.Q<Toggle>("smooth");
            if (_axialSlider == null) return;

            // Force horizontal pane layout + explicit view dimensions in code
            // so USS quirks (cross-version differences, .panel inheritance) can't
            // collapse the slice boxes.
            var grid = root.Q<VisualElement>(className: "ortho-grid");
            if (grid != null) grid.style.flexDirection = FlexDirection.Row;
            foreach (var pane in root.Query<VisualElement>(className: "ortho-pane").ToList())
            {
                pane.style.flexDirection = FlexDirection.Column;
                pane.style.width = 340;
                pane.style.marginRight = 8;
            }
            foreach (var view in new[] { _axialView, _sagittalView, _coronalView })
            {
                if (view == null) continue;
                // Width follows the pane (flex-grow), height stays fixed at 400.
                // Using percent width prevents the 3 × 400 px slice boxes from
                // overflowing the panel when the screen is narrower than ~1240 px.
                view.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
                view.style.height = 400;
                view.style.flexShrink = 1;
                view.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 1f));
            }
            // Build the per-pane crosshair overlay lines once. Re-creating on
            // every Bind would stack duplicates; we look up by name first.
            _axialH    = GetOrAddCrosshairLine(_axialView, "x-h", horizontal: true);
            _axialV    = GetOrAddCrosshairLine(_axialView, "x-v", horizontal: false);
            _sagittalH = GetOrAddCrosshairLine(_sagittalView, "x-h", horizontal: true);
            _sagittalV = GetOrAddCrosshairLine(_sagittalView, "x-v", horizontal: false);
            _coronalH  = GetOrAddCrosshairLine(_coronalView, "x-h", horizontal: true);
            _coronalV  = GetOrAddCrosshairLine(_coronalView, "x-v", horizontal: false);

            // Recompute pixel-based line geometry whenever a pane resizes
            // (window resize, panel toggled open the first time, etc).
            _axialView?.UnregisterCallback<GeometryChangedEvent>(OnViewGeometryChanged);
            _sagittalView?.UnregisterCallback<GeometryChangedEvent>(OnViewGeometryChanged);
            _coronalView?.UnregisterCallback<GeometryChangedEvent>(OnViewGeometryChanged);
            _axialView?.RegisterCallback<GeometryChangedEvent>(OnViewGeometryChanged);
            _sagittalView?.RegisterCallback<GeometryChangedEvent>(OnViewGeometryChanged);
            _coronalView?.RegisterCallback<GeometryChangedEvent>(OnViewGeometryChanged);

            UpdateScrewMarkers();

            _axialSlider.UnregisterValueChangedCallback(OnAxialChanged);
            _axialSlider.RegisterValueChangedCallback(OnAxialChanged);
            _sagittalSlider.UnregisterValueChangedCallback(OnSagittalChanged);
            _sagittalSlider.RegisterValueChangedCallback(OnSagittalChanged);
            _coronalSlider.UnregisterValueChangedCallback(OnCoronalChanged);
            _coronalSlider.RegisterValueChangedCallback(OnCoronalChanged);

            // Pointer drag handling. PointerDown decides between "navigate" and
            // "paint" based on whether brush mode is active. Re-registering is
            // safe because UnregisterCallback compares delegate identity.
            BindPointerHandlers(_axialView,    2);
            BindPointerHandlers(_sagittalView, 0);
            BindPointerHandlers(_coronalView,  1);

            if (_brightnessSlider != null)
            {
                _brightnessSlider.UnregisterValueChangedCallback(OnBrightnessChanged);
                _brightnessSlider.RegisterValueChangedCallback(OnBrightnessChanged);
                _brightnessSlider.SetValueWithoutNotify(_brightness);
            }
            if (_contrastSlider != null)
            {
                _contrastSlider.UnregisterValueChangedCallback(OnContrastChanged);
                _contrastSlider.RegisterValueChangedCallback(OnContrastChanged);
                _contrastSlider.SetValueWithoutNotify(_contrast);
            }
            if (_smoothToggle != null)
            {
                _smoothToggle.UnregisterValueChangedCallback(OnSmoothChanged);
                _smoothToggle.RegisterValueChangedCallback(OnSmoothChanged);
                _smoothToggle.SetValueWithoutNotify(_smooth);
            }

            if (_closeBtn != null) { _closeBtn.clicked -= HandleClose; _closeBtn.clicked += HandleClose; }
        }

        void OnBrightnessChanged(ChangeEvent<float> evt)
        {
            _brightness = evt.newValue;
            if (_brightnessValue != null) _brightnessValue.text = evt.newValue.ToString("0.00");
            RefreshAllSlices();
        }

        void OnContrastChanged(ChangeEvent<float> evt)
        {
            _contrast = evt.newValue;
            if (_contrastValue != null) _contrastValue.text = evt.newValue.ToString("0.00");
            RefreshAllSlices();
        }

        void OnSmoothChanged(ChangeEvent<bool> evt)
        {
            _smooth = evt.newValue;
            // Live-update existing textures without recreating them.
            var f = _smooth ? FilterMode.Bilinear : FilterMode.Point;
            if (_axialTex != null) _axialTex.filterMode = f;
            if (_sagittalTex != null) _sagittalTex.filterMode = f;
            if (_coronalTex != null) _coronalTex.filterMode = f;
        }

        public void Bind(VolumeRenderer renderer)
        {
            if (_renderer != null) _renderer.OnMaskChanged -= HandleMaskChanged;
            _renderer = renderer;
            if (_renderer != null) _renderer.OnMaskChanged += HandleMaskChanged;
            BindUi();
            ResetSliders();
            RefreshAllSlices();
            UpdateAvailability();
        }

        public void SetPalette(Color[] palette)
        {
            _palette = palette ?? new Color[32];
            RefreshAllSlices();
        }

        /// Workstation hands in its BrushPlacer so the ortho viewer knows when
        /// "brush mode" is on (drag-paint) vs off (click-to-navigate).
        public void BindBrush(BrushPlacer brush) { _brush = brush; }

        void ResetSliders()
        {
            if (_renderer == null || !_renderer.HasMask) return;
            var (w, h, d) = _renderer.MaskDimensions;
            ApplyRange(_axialSlider, d, d / 2);
            ApplyRange(_sagittalSlider, w, w / 2);
            ApplyRange(_coronalSlider, h, h / 2);
            // Initial focus = volume centre so crosshairs are visible from frame 1.
            _focus = new Vector3Int(_sagittalSlider.value, _coronalSlider.value, _axialSlider.value);
        }

        // Set a slider's range to [0, size-1] and pick a default value only when
        // the current value is outside the new range — preserves user position
        // across hide/show.
        static void ApplyRange(SliderInt slider, int size, int defaultValue)
        {
            if (slider == null) return;
            int hi = Mathf.Max(0, size - 1);
            slider.lowValue = 0;
            slider.highValue = hi;
            if (slider.value < 0 || slider.value > hi)
                slider.SetValueWithoutNotify(Mathf.Clamp(defaultValue, 0, hi));
        }

        void HandleMaskChanged() => RefreshAllSlices();

        void OnAxialChanged(ChangeEvent<int> evt)    { _focus.z = evt.newValue; RefreshAxial(evt.newValue); RefreshOthersIfCrosshairAxisChanged(axis: 2); }
        void OnSagittalChanged(ChangeEvent<int> evt) { _focus.x = evt.newValue; RefreshSagittal(evt.newValue); RefreshOthersIfCrosshairAxisChanged(axis: 0); }
        void OnCoronalChanged(ChangeEvent<int> evt)  { _focus.y = evt.newValue; RefreshCoronal(evt.newValue); RefreshOthersIfCrosshairAxisChanged(axis: 1); }

        // Crosshair on each pane depends on two axes of the focus point. When
        // an axis changes (slider move OR pane click), every pane that draws a
        // crosshair on that axis has to redraw.
        //   Axial pane crosshair = (x, y)         → redraw on x or y change
        //   Sagittal pane crosshair = (y, z)      → redraw on y or z change
        //   Coronal pane crosshair = (x, z)       → redraw on x or z change
        void RefreshOthersIfCrosshairAxisChanged(int axis)
        {
            if (axis == 0) { RefreshAxial(_axialSlider?.value ?? 0); RefreshCoronal(_coronalSlider?.value ?? 0); }
            if (axis == 1) { RefreshAxial(_axialSlider?.value ?? 0); RefreshSagittal(_sagittalSlider?.value ?? 0); }
            if (axis == 2) { RefreshSagittal(_sagittalSlider?.value ?? 0); RefreshCoronal(_coronalSlider?.value ?? 0); }
        }

        // ── Pointer handling (navigate + drag-paint) ─────────────────────────

        // The view's pixel (0,0) is top-left in UI Toolkit. Our slice textures
        // were filled with SetPixels32 (bottom-up), so we invert Y to map.
        Vector2 NormalizedPaneClick(VisualElement view, Vector2 localPos)
        {
            var r = view.contentRect;
            float u = r.width  > 0 ? Mathf.Clamp01(localPos.x / r.width)  : 0f;
            float v = r.height > 0 ? Mathf.Clamp01(1f - localPos.y / r.height) : 0f;
            return new Vector2(u, v);
        }

        void BindPointerHandlers(VisualElement view, int axis)
        {
            if (view == null) return;
            view.UnregisterCallback<PointerDownEvent>(OnPanePointerDown);
            view.UnregisterCallback<PointerMoveEvent>(OnPanePointerMove);
            view.UnregisterCallback<PointerUpEvent>(OnPanePointerUp);
            view.userData = axis; // remember which axis this pane represents
            view.RegisterCallback<PointerDownEvent>(OnPanePointerDown);
            view.RegisterCallback<PointerMoveEvent>(OnPanePointerMove);
            view.RegisterCallback<PointerUpEvent>(OnPanePointerUp);
        }

        bool BrushActive => _brush != null && _brush.Active;

        void OnPanePointerDown(PointerDownEvent evt)
        {
            if (_renderer == null || !_renderer.HasMask) return;
            if (evt.currentTarget is not VisualElement view) return;
            int axis = view.userData is int a ? a : -1;
            if (axis < 0) return;
            if (BrushActive)
            {
                _drawing = true;
                _drawingAxis = axis;
                view.CapturePointer(evt.pointerId);
                PaintAtPane(view, axis, evt.localPosition);
                evt.StopPropagation();
            }
            else
            {
                NavigateAtPane(view, axis, evt.localPosition);
            }
        }

        void OnPanePointerMove(PointerMoveEvent evt)
        {
            if (!_drawing) return;
            if (evt.currentTarget is not VisualElement view) return;
            int axis = view.userData is int a ? a : -1;
            if (axis != _drawingAxis) return;
            PaintAtPane(view, axis, evt.localPosition);
        }

        void OnPanePointerUp(PointerUpEvent evt)
        {
            if (!_drawing) return;
            if (evt.currentTarget is not VisualElement view) return;
            _drawing = false;
            view.ReleasePointer(evt.pointerId);
        }

        Vector3Int PaneVoxel(VisualElement view, int axis, Vector2 localPos)
        {
            var n = NormalizedPaneClick(view, localPos);
            var (w, h, d) = _renderer.MaskDimensions;
            int v3x = _focus.x, v3y = _focus.y, v3z = _focus.z;
            switch (axis)
            {
                case 0: // sagittal pane: nx=Y, ny=Z, X fixed
                    v3y = Mathf.Clamp(Mathf.RoundToInt(n.x * (h - 1)), 0, h - 1);
                    v3z = Mathf.Clamp(Mathf.RoundToInt(n.y * (d - 1)), 0, d - 1);
                    break;
                case 1: // coronal pane: nx=X, ny=Z, Y fixed
                    v3x = Mathf.Clamp(Mathf.RoundToInt(n.x * (w - 1)), 0, w - 1);
                    v3z = Mathf.Clamp(Mathf.RoundToInt(n.y * (d - 1)), 0, d - 1);
                    break;
                default: // axial pane: nx=X, ny=Y, Z fixed
                    v3x = Mathf.Clamp(Mathf.RoundToInt(n.x * (w - 1)), 0, w - 1);
                    v3y = Mathf.Clamp(Mathf.RoundToInt(n.y * (h - 1)), 0, h - 1);
                    break;
            }
            return new Vector3Int(v3x, v3y, v3z);
        }

        void NavigateAtPane(VisualElement view, int axis, Vector2 localPos)
        {
            var v = PaneVoxel(view, axis, localPos);
            // Update slider values for the two axes this pane navigates.
            switch (axis)
            {
                case 0: _focus.y = v.y; _focus.z = v.z; _coronalSlider?.SetValueWithoutNotify(v.y); _axialSlider?.SetValueWithoutNotify(v.z); break;
                case 1: _focus.x = v.x; _focus.z = v.z; _sagittalSlider?.SetValueWithoutNotify(v.x); _axialSlider?.SetValueWithoutNotify(v.z); break;
                default: _focus.x = v.x; _focus.y = v.y; _sagittalSlider?.SetValueWithoutNotify(v.x); _coronalSlider?.SetValueWithoutNotify(v.y); break;
            }
            RefreshAllSlices();
        }

        void PaintAtPane(VisualElement view, int axis, Vector2 localPos)
        {
            var v = PaneVoxel(view, axis, localPos);
            OnPaintRequest?.Invoke(v, axis);
        }

        void RefreshAllSlices()
        {
            if (_renderer == null || !_renderer.HasMask) return;
            RefreshAxial(_axialSlider?.value ?? 0);
            RefreshSagittal(_sagittalSlider?.value ?? 0);
            RefreshCoronal(_coronalSlider?.value ?? 0);
        }

        // ── 2D slice extraction ──────────────────────────────────────────────

        void RefreshAxial(int z)
        {
            if (_renderer == null || !_renderer.HasMask) return;
            var (w, h, d) = _renderer.MaskDimensions;
            z = Mathf.Clamp(z, 0, d - 1);
            EnsureTex(ref _axialTex, w, h);
            FillAxial(_axialTex, _renderer.VolumeBytes, _renderer.MaskBytes, w, h, d, z);
            ApplyTex(_axialView, _axialTex);
            if (_axialIndex != null) _axialIndex.text = $"{z + 1}/{d}";
        }

        void RefreshSagittal(int x)
        {
            if (_renderer == null || !_renderer.HasMask) return;
            var (w, h, d) = _renderer.MaskDimensions;
            x = Mathf.Clamp(x, 0, w - 1);
            EnsureTex(ref _sagittalTex, h, d);
            FillSagittal(_sagittalTex, _renderer.VolumeBytes, _renderer.MaskBytes, w, h, d, x);
            ApplyTex(_sagittalView, _sagittalTex);
            if (_sagittalIndex != null) _sagittalIndex.text = $"{x + 1}/{w}";
        }

        void RefreshCoronal(int y)
        {
            if (_renderer == null || !_renderer.HasMask) return;
            var (w, h, d) = _renderer.MaskDimensions;
            y = Mathf.Clamp(y, 0, h - 1);
            EnsureTex(ref _coronalTex, w, d);
            FillCoronal(_coronalTex, _renderer.VolumeBytes, _renderer.MaskBytes, w, h, d, y);
            ApplyTex(_coronalView, _coronalTex);
            if (_coronalIndex != null) _coronalIndex.text = $"{y + 1}/{h}";
        }

        void EnsureTex(ref Texture2D t, int w, int h)
        {
            if (t != null && t.width == w && t.height == h)
            {
                t.filterMode = _smooth ? FilterMode.Bilinear : FilterMode.Point;
                return;
            }
            if (t != null) Destroy(t);
            t = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = _smooth ? FilterMode.Bilinear : FilterMode.Point,
            };
        }

        // Build a thin rectangle line that gets rotated to match the entry→tip
        // direction in pane pixel space.
        static VisualElement BuildScrewLine(VisualElement view, Color color)
        {
            if (view == null) return null;
            var line = new VisualElement();
            line.style.position = Position.Absolute;
            line.style.height = 3;
            var c = color; c.a = 0.9f;
            line.style.backgroundColor = new StyleColor(c);
            line.style.transformOrigin = new StyleTransformOrigin(new TransformOrigin(new Length(0, LengthUnit.Pixel), new Length(1.5f, LengthUnit.Pixel), 0));
            line.style.display = DisplayStyle.None;
            line.pickingMode = PickingMode.Ignore;
            view.Add(line);
            return line;
        }

        // Build a small coloured dot as a child of `view`.
        static VisualElement BuildDot(VisualElement view, Color color, Color borderColor)
        {
            if (view == null) return null;
            const float size = 12f;
            var dot = new VisualElement();
            dot.style.position = Position.Absolute;
            dot.style.width = size;
            dot.style.height = size;
            dot.style.backgroundColor = new StyleColor(color);
            var radius = new StyleLength(size * 0.5f);
            dot.style.borderTopLeftRadius = radius;
            dot.style.borderTopRightRadius = radius;
            dot.style.borderBottomLeftRadius = radius;
            dot.style.borderBottomRightRadius = radius;
            dot.style.borderTopWidth = 1; dot.style.borderRightWidth = 1;
            dot.style.borderBottomWidth = 1; dot.style.borderLeftWidth = 1;
            dot.style.borderTopColor = dot.style.borderRightColor =
            dot.style.borderBottomColor = dot.style.borderLeftColor = new StyleColor(borderColor);
            dot.style.translate = new StyleTranslate(new Translate(-size * 0.5f, -size * 0.5f, 0));
            dot.style.display = DisplayStyle.None;
            dot.pickingMode = PickingMode.Ignore;
            view.Add(dot);
            return dot;
        }

        // Build (or fetch) a 2-px overlay line as a child of `view`. Horizontal
        // line spans the pane horizontally and is positioned by `top`; vertical
        // does the inverse. The slice texture is rendered as the view's
        // backgroundImage, so these elements draw cleanly on top.
        static VisualElement GetOrAddCrosshairLine(VisualElement view, string name, bool horizontal)
        {
            if (view == null) return null;
            var existing = view.Q<VisualElement>(name);
            if (existing != null) return existing;
            var line = new VisualElement { name = name };
            line.style.position = Position.Absolute;
            line.style.backgroundColor = new StyleColor(new Color(1f, 0.86f, 0f, 0.95f));
            if (horizontal)
            {
                line.style.left = 0; line.style.right = 0; line.style.height = 2;
            }
            else
            {
                line.style.top = 0; line.style.bottom = 0; line.style.width = 2;
            }
            line.pickingMode = PickingMode.Ignore; // don't intercept the pane's click
            view.Add(line);
            return line;
        }

        void ApplyTex(VisualElement view, Texture2D tex)
        {
            if (view == null || tex == null) return;
            view.style.backgroundImage = new StyleBackground(tex);
        }

        Color32 BlendVoxel(byte volumeR, byte maskIdx)
        {
            // Apply brightness/contrast to the underlying grayscale before
            // blending with the segment colour: bias around 0.5, then add
            // brightness offset, then clamp.
            float vf = volumeR / 255f;
            vf = (vf - 0.5f) * (1f + _contrast) + 0.5f + _brightness;
            vf = Mathf.Clamp01(vf);
            byte gray = (byte)Mathf.Clamp((int)(vf * 255f), 0, 255);
            var c = new Color32(gray, gray, gray, 255);
            if (maskIdx == 0 || maskIdx >= _palette.Length) return c;
            var seg = _palette[maskIdx];
            if (seg.a <= 0.001f) return c;
            float a = Mathf.Clamp01(seg.a);
            byte r = (byte)Mathf.Clamp((int)((1 - a) * gray + a * seg.r * 255), 0, 255);
            byte g = (byte)Mathf.Clamp((int)((1 - a) * gray + a * seg.g * 255), 0, 255);
            byte b = (byte)Mathf.Clamp((int)((1 - a) * gray + a * seg.b * 255), 0, 255);
            return new Color32(r, g, b, 255);
        }

        // Axial: D slice index `z` → outputs (W * H) pixels indexed by (x, y).
        // Crosshair is drawn as VisualElement overlay (UpdateCrosshairs), not into
        // the texture, so the line is 2 px wide regardless of slice resolution.
        void FillAxial(Texture2D tex, byte[] vol, byte[] mask, int W, int H, int D, int z)
        {
            var pixels = new Color32[W * H];
            int basez = z * W * H;
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int src = basez + y * W + x;
                pixels[y * W + x] = BlendVoxel(vol != null ? vol[src] : (byte)0, mask != null ? mask[src] : (byte)0);
            }
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            UpdateCrosshairs();
        }

        // Sagittal: W slice index `x` → outputs (H * D) pixels indexed by (y, z).
        void FillSagittal(Texture2D tex, byte[] vol, byte[] mask, int W, int H, int D, int x)
        {
            var pixels = new Color32[H * D];
            for (int z = 0; z < D; z++)
            for (int y = 0; y < H; y++)
            {
                int src = z * W * H + y * W + x;
                pixels[z * H + y] = BlendVoxel(vol != null ? vol[src] : (byte)0, mask != null ? mask[src] : (byte)0);
            }
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            UpdateCrosshairs();
        }

        // Coronal: H slice index `y` → outputs (W * D) pixels indexed by (x, z).
        void FillCoronal(Texture2D tex, byte[] vol, byte[] mask, int W, int H, int D, int y)
        {
            var pixels = new Color32[W * D];
            for (int z = 0; z < D; z++)
            for (int x = 0; x < W; x++)
            {
                int src = z * W * H + y * W + x;
                pixels[z * W + x] = BlendVoxel(vol != null ? vol[src] : (byte)0, mask != null ? mask[src] : (byte)0);
            }
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            UpdateCrosshairs();
        }

        // Re-position the six overlay crosshair lines based on the current focus.
        // Each pane has two axes; we map the focus voxel coord to a [0, 1]
        // fraction of the pane's drawable area, accounting for the slice
        // texture's bottom-up Y convention (Unity SetPixels32) vs the UI's
        // top-down layout.
        void UpdateCrosshairs()
        {
            if (_renderer == null || !_renderer.HasMask) return;
            var (w, h, d) = _renderer.MaskDimensions;
            // Axial pane: vertical = focus.x along W, horizontal = focus.y along H (Y-flipped).
            PlaceLine(_axialV, isHorizontal: false, frac: SafeFrac(_focus.x, w));
            PlaceLine(_axialH, isHorizontal: true,  frac: 1f - SafeFrac(_focus.y, h));
            // Sagittal pane: vertical = focus.y along H, horizontal = focus.z along D (Y-flipped).
            PlaceLine(_sagittalV, isHorizontal: false, frac: SafeFrac(_focus.y, h));
            PlaceLine(_sagittalH, isHorizontal: true,  frac: 1f - SafeFrac(_focus.z, d));
            // Coronal pane: vertical = focus.x along W, horizontal = focus.z along D (Y-flipped).
            PlaceLine(_coronalV, isHorizontal: false, frac: SafeFrac(_focus.x, w));
            PlaceLine(_coronalH, isHorizontal: true,  frac: 1f - SafeFrac(_focus.z, d));
        }

        static float SafeFrac(int idx, int size) => size > 1 ? Mathf.Clamp01((float)idx / (size - 1)) : 0f;

        static void PlaceLine(VisualElement line, bool isHorizontal, float frac)
        {
            if (line == null) return;
            if (isHorizontal) line.style.top  = new StyleLength(Length.Percent(frac * 100f));
            else              line.style.left = new StyleLength(Length.Percent(frac * 100f));
        }

        void OnViewGeometryChanged(GeometryChangedEvent _) => UpdateScrewMarkers();

        // ── Multi-screw overlay management ──────────────────────────────────

        /// Replace the set of projected screws. Workstation rebuilds and pushes
        /// after every placement / clear so the panes always show the current
        /// state.
        public void SetScrews(IReadOnlyList<ScrewProjection> screws)
        {
            DestroyAllOverlays();
            if (screws != null)
            {
                foreach (var s in screws)
                {
                    if (_axialView == null || _sagittalView == null || _coronalView == null) break;
                    var overlay = new ScrewOverlay { Spec = s };
                    var entryDotColor = new Color(0.2f, 1f, 0.4f, 1f);
                    var tipDotColor   = new Color(1f, 0.4f, 0.2f, 1f);
                    overlay.AxialLine    = BuildScrewLine(_axialView,    s.Color);
                    overlay.AxialEntry   = BuildDot(_axialView,    entryDotColor, Color.black);
                    overlay.AxialTip     = BuildDot(_axialView,    tipDotColor,   Color.black);
                    overlay.SagittalLine = BuildScrewLine(_sagittalView, s.Color);
                    overlay.SagittalEntry= BuildDot(_sagittalView, entryDotColor, Color.black);
                    overlay.SagittalTip  = BuildDot(_sagittalView, tipDotColor,   Color.black);
                    overlay.CoronalLine  = BuildScrewLine(_coronalView,  s.Color);
                    overlay.CoronalEntry = BuildDot(_coronalView,  entryDotColor, Color.black);
                    overlay.CoronalTip   = BuildDot(_coronalView,  tipDotColor,   Color.black);
                    _overlays.Add(overlay);
                }
            }
            UpdateScrewMarkers();
        }

        public void ClearScrews() => SetScrews(System.Array.Empty<ScrewProjection>());

        void DestroyAllOverlays()
        {
            foreach (var o in _overlays)
            {
                Detach(o.AxialEntry);   Detach(o.AxialTip);   Detach(o.AxialLine);
                Detach(o.SagittalEntry); Detach(o.SagittalTip); Detach(o.SagittalLine);
                Detach(o.CoronalEntry); Detach(o.CoronalTip); Detach(o.CoronalLine);
            }
            _overlays.Clear();

            static void Detach(VisualElement v)
            {
                if (v != null && v.parent != null) v.parent.Remove(v);
            }
        }

        void UpdateScrewMarkers()
        {
            if (_renderer == null || !_renderer.HasMask) { foreach (var o in _overlays) HideOverlay(o); return; }
            var (w, h, d) = _renderer.MaskDimensions;
            foreach (var o in _overlays)
            {
                // Axial: x ← vx, y ← vy (Y-flipped for UI top-down)
                float axEx = SafeFrac(o.Spec.EntryVoxel.x, w), axEy = 1f - SafeFrac(o.Spec.EntryVoxel.y, h);
                float axTx = SafeFrac(o.Spec.TipVoxel.x,   w), axTy = 1f - SafeFrac(o.Spec.TipVoxel.y,   h);
                PlaceDot(o.AxialEntry, axEx, axEy);
                PlaceDot(o.AxialTip,   axTx, axTy);
                PlaceScrewLine(_axialView, o.AxialLine, axEx, axEy, axTx, axTy);
                // Sagittal: x ← vy, y ← vz
                float sgEx = SafeFrac(o.Spec.EntryVoxel.y, h), sgEy = 1f - SafeFrac(o.Spec.EntryVoxel.z, d);
                float sgTx = SafeFrac(o.Spec.TipVoxel.y,   h), sgTy = 1f - SafeFrac(o.Spec.TipVoxel.z,   d);
                PlaceDot(o.SagittalEntry, sgEx, sgEy);
                PlaceDot(o.SagittalTip,   sgTx, sgTy);
                PlaceScrewLine(_sagittalView, o.SagittalLine, sgEx, sgEy, sgTx, sgTy);
                // Coronal: x ← vx, y ← vz
                float coEx = SafeFrac(o.Spec.EntryVoxel.x, w), coEy = 1f - SafeFrac(o.Spec.EntryVoxel.z, d);
                float coTx = SafeFrac(o.Spec.TipVoxel.x,   w), coTy = 1f - SafeFrac(o.Spec.TipVoxel.z,   d);
                PlaceDot(o.CoronalEntry, coEx, coEy);
                PlaceDot(o.CoronalTip,   coTx, coTy);
                PlaceScrewLine(_coronalView, o.CoronalLine, coEx, coEy, coTx, coTy);
            }
        }

        static void HideOverlay(ScrewOverlay o)
        {
            Hide(o.AxialEntry); Hide(o.AxialTip); Hide(o.AxialLine);
            Hide(o.SagittalEntry); Hide(o.SagittalTip); Hide(o.SagittalLine);
            Hide(o.CoronalEntry); Hide(o.CoronalTip); Hide(o.CoronalLine);
        }

        // Position a screw line as a rotated rectangle from (fx1, fy1) to
        // (fx2, fy2) in the pane's content rect (fractions in [0, 1]).
        static void PlaceScrewLine(VisualElement view, VisualElement line, float fx1, float fy1, float fx2, float fy2)
        {
            if (view == null || line == null) return;
            var rect = view.contentRect;
            if (rect.width <= 0 || rect.height <= 0) { Hide(line); return; }
            float x1 = fx1 * rect.width, y1 = fy1 * rect.height;
            float x2 = fx2 * rect.width, y2 = fy2 * rect.height;
            float dx = x2 - x1, dy = y2 - y1;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length < 1f) { Hide(line); return; }
            float angleDeg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            line.style.display = DisplayStyle.Flex;
            line.style.left = x1;
            line.style.top = y1 - 1.5f; // centre the 3-px line vertically on its endpoint
            line.style.width = length;
            line.style.rotate = new StyleRotate(new Rotate(new Angle(angleDeg, AngleUnit.Degree)));
        }

        static void PlaceDot(VisualElement dot, float fx, float fy)
        {
            if (dot == null) return;
            dot.style.display = DisplayStyle.Flex;
            dot.style.left = new StyleLength(Length.Percent(fx * 100f));
            dot.style.top  = new StyleLength(Length.Percent(fy * 100f));
        }

        static void Hide(VisualElement v) { if (v != null) v.style.display = DisplayStyle.None; }

        void UpdateAvailability()
        {
            bool ok = _renderer != null && _renderer.HasVolume;
            if (_emptyMessage != null) _emptyMessage.style.display = ok ? DisplayStyle.None : DisplayStyle.Flex;
            // The content stays flex-laid-out at all times — empty-message just
            // hides itself when a volume loads, instead of toggling content's
            // display. Toggling display:none was hiding the per-pane row layout.
        }

        void HandleClose() { OnClose?.Invoke(); gameObject.SetActive(false); }
    }
}
