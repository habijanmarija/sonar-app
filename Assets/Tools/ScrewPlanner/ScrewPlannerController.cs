using System;
using System.Collections.Generic;
using Host.Visualization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Host.Tools.ScrewPlanner
{
    /// SONAR's screw-placement training loop, slotted in next to the MedicalViz
    /// workstation. Reuses the SPIDER per-vertebra mask as ground truth: trainee
    /// picks a target segment (the currently active segment in the Segmentation
    /// panel), clicks an entry point + tip point in the 3D scene, and the
    /// controller computes the fraction of voxels along the screw that fall
    /// inside the target mask. Green / yellow / red gates mirror SONAR's
    /// existing TrajectoryEvaluator thresholds.
    ///
    /// Multi-screw: each completed placement is appended to a list and stays
    /// visible. "Place screw" starts a new placement; "Clear all" wipes them.
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("SONAR Host/Tools/Screw Planner Controller")]
    public sealed class ScrewPlannerController : MonoBehaviour
    {
        public event Action OnClose;
        /// Fired when a screw is placed or all screws are cleared. For placement
        /// (hasScrew=true), arg is the *latest* screw's endpoints — the ortho
        /// viewer uses these to project the most recent screw onto its panes.
        public event Action<Vector3, Vector3, bool> OnScrewChanged;
        /// Fired after any change to the placed-screws list (add or clear-all).
        /// The workstation listens to this to persist screws to disk.
        public event Action OnPlacementsChanged;

        [SerializeField] float _maxRayDistance = 10f;
        [SerializeField] float _screwLengthSamples = 64;   // number of voxel samples along the screw
        [SerializeField] float _screwRadiusMm = 1f;        // visual screw radius (no impact on score)

        UIDocument _doc;
        Label _emptyMessage, _targetLabel, _statusLabel, _coverage, _gate, _countLabel, _breachLabel, _trajectoryLabel, _canalGrade, _sonarMetrics;
        VisualElement _content, _screwsListVE;
        Button _placeBtn, _clearBtn, _closeBtn;
        Func<byte, string> _resolveLabel;
        // Cache: centroid of each segment's voxels in world space, keyed by byte.
        // Invalidated when the mask changes (paint stroke, segment add/delete, etc).
        readonly Dictionary<byte, Vector3> _centroidWorldCache = new();

        VolumeRenderer _renderer;
        Transform _patientContent;
        string _targetSegmentId;
        byte _targetByteValue;
        Color _targetColor = Color.gray;

        enum PlacementState { Idle, AwaitingEntry, AwaitingTip }
        PlacementState _state;

        sealed class PlacedScrew
        {
            public Vector3 Entry, Tip;
            public byte TargetByteValue;
            public Color Color;
            public string TargetLabel;
            public string SegmentId;
            public float CoveragePct;
            public GameObject Root;
            public GameObject Cylinder;
            public GameObject EntryMarker;
            public GameObject TipMarker;
            public Material CylinderMaterial;
        }

        readonly List<PlacedScrew> _placed = new();
        PlacedScrew _pending;   // currently being constructed (entry maybe set, tip not yet)
        // Live preview rubber-band while placing the tip — a thin LineRenderer
        // from the placed entry to the current mouse-hover hit point. Hides as
        // soon as placement commits or gets cancelled.
        GameObject _previewGo;
        LineRenderer _previewLine;
        Material _previewMaterial;

        void OnEnable()
        {
            BindUi();
            UpdateAvailability();
        }

        void OnDisable()
        {
            // Cancel any in-progress placement so a stale state doesn't capture
            // a click meant for navigation. Placed screws stay visible.
            CancelPending();
            RefreshStatus();
        }

        void BindUi()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            var root = _doc?.rootVisualElement;
            if (root == null) return;
            _emptyMessage = root.Q<Label>("empty-message");
            _content = root.Q<VisualElement>("content");
            _targetLabel = root.Q<Label>("target-label");
            _statusLabel = root.Q<Label>("status");
            _coverage = root.Q<Label>("coverage");
            _gate = root.Q<Label>("gate");
            _countLabel = root.Q<Label>("count");
            _breachLabel = root.Q<Label>("breach");
            _trajectoryLabel = root.Q<Label>("trajectory");
            _canalGrade = root.Q<Label>("canal-grade");
            _sonarMetrics = root.Q<Label>("sonar-metrics");
            _screwsListVE = root.Q<VisualElement>("screws-list");
            _placeBtn = root.Q<Button>("place");
            _clearBtn = root.Q<Button>("clear");
            _closeBtn = root.Q<Button>("close");
            if (_placeBtn != null) { _placeBtn.clicked -= HandlePlaceClicked; _placeBtn.clicked += HandlePlaceClicked; }
            if (_clearBtn != null) { _clearBtn.clicked -= ClearAll;            _clearBtn.clicked += ClearAll; }
            if (_closeBtn != null) { _closeBtn.clicked -= HandleClose;         _closeBtn.clicked += HandleClose; }
            RefreshStatus();
            RefreshCountLabel();
        }

        // ── Public API used by the workstation ──────────────────────────────

        public void Bind(VolumeRenderer renderer, Transform patientContent)
        {
            if (_renderer != null) _renderer.OnMaskChanged -= InvalidateCentroidCache;
            _renderer = renderer;
            if (_renderer != null) _renderer.OnMaskChanged += InvalidateCentroidCache;
            _patientContent = patientContent;
            _centroidWorldCache.Clear();
            BindUi();
            UpdateAvailability();
        }

        void InvalidateCentroidCache() => _centroidWorldCache.Clear();

        public void SetTarget(string segmentId, string segmentLabel, byte byteValue, Color color)
        {
            _targetSegmentId = segmentId;
            _targetByteValue = byteValue;
            _targetColor = color;
            BindUi();
            if (_targetLabel != null) _targetLabel.text = string.IsNullOrEmpty(segmentLabel) ? "Target: —" : $"Target: {segmentLabel}";
        }

        /// Workstation hands in a resolver byte→label so breach reports name the
        /// adjacent structure ("Disc 5", "Spinal canal") instead of "label 201".
        public void SetLabelResolver(Func<byte, string> resolver) { _resolveLabel = resolver; }

        // ── Placement state machine ─────────────────────────────────────────

        void HandlePlaceClicked()
        {
            if (_renderer == null || !_renderer.HasMask)
            {
                _state = PlacementState.Idle;
                RefreshStatus("No volume / mask loaded.");
                return;
            }
            if (_targetByteValue == 0)
            {
                _state = PlacementState.Idle;
                RefreshStatus("Pick a segment as Active in the Segmentation panel first.");
                return;
            }
            // Snap the target by capturing it now so subsequent segment switches
            // don't re-target an in-flight placement.
            _pending = new PlacedScrew
            {
                TargetByteValue = _targetByteValue,
                Color = _targetColor,
                TargetLabel = _targetLabel != null ? _targetLabel.text : "",
                SegmentId = _targetSegmentId,
            };
            _state = PlacementState.AwaitingEntry;
            RefreshStatus();
        }

        void Update()
        {
            if (_state == PlacementState.Idle) { HidePreview(); return; }
            if (Camera.main == null) return;
            var mouse = Mouse.current;
            if (mouse == null) return;

            // Right-click cancels an in-progress placement. Saves having to
            // commit a bad tip just to start over.
            if (mouse.rightButton.wasPressedThisFrame)
            {
                CancelPending();
                HidePreview();
                RefreshStatus("Placement cancelled.");
                return;
            }

            var cubeT = _renderer.VolumeCubeTransform;
            if (cubeT == null) return;

            // Rubber-band preview while waiting for tip. We raycast every frame
            // so the line tracks the cursor; it's cheap (one Physics.Raycast).
            if (_state == PlacementState.AwaitingTip && _pending != null)
            {
                var hoverRay = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
                if (TryPickInVolume(hoverRay, cubeT, out var hoverHit))
                    UpdatePreview(_pending.Entry, hoverHit.point);
            }

            if (!mouse.leftButton.wasPressedThisFrame) return;

            var ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
            if (!TryPickInVolume(ray, cubeT, out var hit)) return;

            if (_pending == null) { _state = PlacementState.Idle; return; }

            if (_state == PlacementState.AwaitingEntry)
            {
                _pending.Entry = hit.point;
                EnsureRoot(_pending, _placed.Count + 1);
                _pending.EntryMarker = SpawnMarker(_pending.Root.transform, hit.point, new Color(0.2f, 1f, 0.4f));
                _state = PlacementState.AwaitingTip;
                RefreshStatus();
            }
            else if (_state == PlacementState.AwaitingTip)
            {
                _pending.Tip = hit.point;
                EnsureRoot(_pending, _placed.Count + 1);
                _pending.TipMarker = SpawnMarker(_pending.Root.transform, hit.point, new Color(1f, 0.4f, 0.2f));
                BuildCylinderFor(_pending);
                var report = AnalyseScrew(_pending);
                _pending.CoveragePct = report.TargetPct;
                _placed.Add(_pending);
                var commit = _pending;
                _pending = null;
                _state = PlacementState.Idle;
                HidePreview();
                ShowCoverage(commit.CoveragePct);
                ShowBreach(report);
                ShowTrajectory(commit);
                ShowCanalGrade(commit);
                ShowSonarMetrics(commit);
                RefreshCountLabel();
                RefreshStatus($"Screw #{_placed.Count} placed. Click 'Place screw' to add another, 'Clear all' to reset.");
                OnScrewChanged?.Invoke(commit.Entry, commit.Tip, true);
                OnPlacementsChanged?.Invoke();
            }
        }

        void UpdatePreview(Vector3 entry, Vector3 hover)
        {
            if (_previewGo == null)
            {
                _previewGo = new GameObject("ScrewPreview");
                if (_patientContent != null) _previewGo.transform.SetParent(_patientContent, worldPositionStays: true);
                _previewLine = _previewGo.AddComponent<LineRenderer>();
                _previewLine.useWorldSpace = true;
                _previewLine.positionCount = 2;
                _previewLine.startWidth = 0.0025f;
                _previewLine.endWidth = 0.0025f;
                _previewLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _previewLine.receiveShadows = false;
                var shader = Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Unlit/Color")
                             ?? Shader.Find("Sprites/Default");
                _previewMaterial = new Material(shader);
                var c = new Color(_targetColor.r * 0.6f + 0.4f, _targetColor.g * 0.6f + 0.4f, _targetColor.b * 0.6f + 0.4f, 1f);
                if (_previewMaterial.HasProperty("_BaseColor")) _previewMaterial.SetColor("_BaseColor", c);
                _previewMaterial.color = c;
                _previewLine.sharedMaterial = _previewMaterial;
            }
            _previewGo.SetActive(true);
            _previewLine.SetPosition(0, entry);
            _previewLine.SetPosition(1, hover);
        }

        void HidePreview()
        {
            if (_previewGo != null) _previewGo.SetActive(false);
        }

        /// Prefer mesh-collider hits over the outer cube box. The cube's
        /// BoxCollider wraps every segment mesh, so a naive raycast always hits
        /// the cube face *in front of* the bone — leaving the screw outside
        /// the vertebra and giving 0% coverage. With RaycastAll we can pick
        /// the closest segment-mesh hit instead, falling back to the cube
        /// only when no mesh is in the line of sight.
        bool TryPickInVolume(Ray ray, Transform cubeT, out RaycastHit best)
        {
            best = default;
            var hits = Physics.RaycastAll(ray, _maxRayDistance);
            if (hits == null || hits.Length == 0) return false;
            float meshBest = float.PositiveInfinity;
            float cubeBest = float.PositiveInfinity;
            RaycastHit meshHit = default, cubeHit = default;
            bool hasMesh = false, hasCube = false;
            foreach (var h in hits)
            {
                if (h.transform == cubeT)
                {
                    if (h.distance < cubeBest) { cubeBest = h.distance; cubeHit = h; hasCube = true; }
                }
                else if (h.transform.IsChildOf(cubeT))
                {
                    if (h.distance < meshBest) { meshBest = h.distance; meshHit = h; hasMesh = true; }
                }
            }
            if (hasMesh) { best = meshHit; return true; }
            if (hasCube) { best = cubeHit; return true; }
            return false;
        }

        void CancelPending()
        {
            _state = PlacementState.Idle;
            if (_pending?.Root != null) Destroy(_pending.Root);
            _pending = null;
            HidePreview();
        }

        void EnsureRoot(PlacedScrew s, int index)
        {
            if (s.Root != null) return;
            s.Root = new GameObject($"Screw_{index}");
            if (_patientContent != null) s.Root.transform.SetParent(_patientContent, worldPositionStays: true);
        }

        // ── Visual building blocks ──────────────────────────────────────────

        GameObject SpawnMarker(Transform parent, Vector3 worldPoint, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Marker";
            if (marker.TryGetComponent<Collider>(out var col)) Destroy(col);
            marker.transform.SetParent(parent, worldPositionStays: true);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            marker.GetComponent<MeshRenderer>().sharedMaterial = new Material(shader) { color = color };
            marker.transform.position = worldPoint;
            marker.transform.localScale = Vector3.one * 0.006f;
            return marker;
        }

        void BuildCylinderFor(PlacedScrew s)
        {
            var dir = s.Tip - s.Entry;
            float len = dir.magnitude;
            if (len <= 1e-4f) return;

            s.Cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            s.Cylinder.name = "Cylinder";
            if (s.Cylinder.TryGetComponent<Collider>(out var col)) Destroy(col);
            s.Cylinder.transform.SetParent(s.Root.transform, worldPositionStays: true);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            s.CylinderMaterial = new Material(shader)
            {
                color = new Color(s.Color.r * 0.6f + 0.4f, s.Color.g * 0.6f + 0.4f, s.Color.b * 0.6f + 0.4f, 1f),
            };
            s.Cylinder.GetComponent<MeshRenderer>().sharedMaterial = s.CylinderMaterial;

            s.Cylinder.transform.position = (s.Entry + s.Tip) * 0.5f;
            s.Cylinder.transform.up = dir.normalized;
            float radius = _screwRadiusMm * 0.001f;
            s.Cylinder.transform.localScale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);
        }

        void ClearAll()
        {
            ClearAllInternal();
            ClearCanalGrade();
            RefreshCountLabel();
            RefreshStatus();
            OnScrewChanged?.Invoke(Vector3.zero, Vector3.zero, false);
            OnPlacementsChanged?.Invoke();
        }

        void ClearAllInternal()
        {
            CancelPending();
            foreach (var p in _placed)
            {
                if (p.CylinderMaterial != null) Destroy(p.CylinderMaterial);
                if (p.Root != null) Destroy(p.Root);
            }
            _placed.Clear();
            if (_coverage != null) _coverage.text = "—";
            ApplyGate(-1f);
        }

        /// Snapshot current placements as serialisable records. Workstation saves
        /// these to <patient>/screws.json on every OnPlacementsChanged.
        public IReadOnlyList<ScrewPlacement> Snapshot()
        {
            var list = new List<ScrewPlacement>(_placed.Count);
            foreach (var p in _placed)
            {
                var rec = new ScrewPlacement
                {
                    Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                    SegmentId = p.SegmentId,
                    SegmentLabel = p.TargetLabel,
                    CoveragePct = p.CoveragePct,
                    PlacedUtc = DateTime.UtcNow,
                };
                rec.SetEntry(p.Entry);
                rec.SetTip(p.Tip);
                rec.Color = p.Color;
                list.Add(rec);
            }
            return list;
        }

        /// Rehydrate placed screws from previously-saved records (after a
        /// patient is re-loaded, etc.). Coverage values come from the snapshot —
        /// they reflect the mask at save time, not the current mask.
        public void RestoreFrom(IReadOnlyList<ScrewPlacement> placements)
        {
            ClearAllInternal();
            if (placements == null) { RefreshCountLabel(); RefreshStatus(); return; }
            int i = 1;
            foreach (var p in placements)
            {
                var s = new PlacedScrew
                {
                    Entry = p.Entry,
                    Tip = p.Tip,
                    Color = p.Color,
                    TargetLabel = p.SegmentLabel,
                    SegmentId = p.SegmentId,
                    CoveragePct = p.CoveragePct,
                };
                EnsureRoot(s, i++);
                s.EntryMarker = SpawnMarker(s.Root.transform, s.Entry, new Color(0.2f, 1f, 0.4f));
                s.TipMarker   = SpawnMarker(s.Root.transform, s.Tip,   new Color(1f, 0.4f, 0.2f));
                BuildCylinderFor(s);
                _placed.Add(s);
            }
            if (_placed.Count > 0)
            {
                var last = _placed[_placed.Count - 1];
                ShowCoverage(last.CoveragePct);
                OnScrewChanged?.Invoke(last.Entry, last.Tip, true);
            }
            else
            {
                if (_coverage != null) _coverage.text = "—";
                ApplyGate(-1f);
                OnScrewChanged?.Invoke(Vector3.zero, Vector3.zero, false);
            }
            RefreshCountLabel();
            RefreshStatus();
        }

        void RefreshStatus(string overrideText = null)
        {
            if (_statusLabel == null) return;
            if (overrideText != null) { _statusLabel.text = overrideText; return; }
            _statusLabel.text = _state switch
            {
                PlacementState.AwaitingEntry => "Click an ENTRY point on the volume cube. (Right-click to cancel.)",
                PlacementState.AwaitingTip   => "Hover to preview the trajectory; click to commit the TIP. (Right-click to cancel.)",
                _ => "Click 'Place screw' then pick entry + tip in the 3D scene.",
            };
        }

        void RefreshCountLabel()
        {
            if (_countLabel == null) return;
            _countLabel.text = _placed.Count == 0 ? "No screws placed." : $"{_placed.Count} screw(s) placed.";
            RebuildScrewList();
        }

        // Per-screw row UI: colour swatch + target label / coverage % + canal
        // grade badge + delete button. Rebuilt from scratch on every change —
        // the list is short enough (a few rows) that incremental updates
        // wouldn't pay back the complexity.
        void RebuildScrewList()
        {
            if (_screwsListVE == null) return;
            _screwsListVE.Clear();
            for (int i = 0; i < _placed.Count; i++)
            {
                int captured = i;
                var p = _placed[i];

                var row = new VisualElement();
                row.AddToClassList("screw-row");

                var swatch = new VisualElement();
                swatch.AddToClassList("screw-row-swatch");
                swatch.style.backgroundColor = p.Color;
                row.Add(swatch);

                string targetText = string.IsNullOrEmpty(p.TargetLabel) ? "—" : p.TargetLabel;
                var name = new Label($"#{i + 1}  {targetText}   {p.CoveragePct:0}%");
                name.AddToClassList("screw-row-name");
                row.Add(name);

                // Mini gate badge mirroring the coverage gate logic.
                var badge = new Label(p.CoveragePct >= 80f ? "G" : p.CoveragePct >= 50f ? "Y" : "R");
                badge.AddToClassList("screw-row-grade");
                if (p.CoveragePct >= 80f) badge.AddToClassList("green");
                else if (p.CoveragePct >= 50f) badge.AddToClassList("yellow");
                else badge.AddToClassList("red");
                row.Add(badge);

                var del = new Button(() => DeleteScrewAt(captured)) { text = "✕" };
                del.AddToClassList("screw-row-delete");
                row.Add(del);

                _screwsListVE.Add(row);
            }
        }

        void DeleteScrewAt(int index)
        {
            if (index < 0 || index >= _placed.Count) return;
            var p = _placed[index];
            if (p.CylinderMaterial != null) Destroy(p.CylinderMaterial);
            if (p.Root != null) Destroy(p.Root);
            _placed.RemoveAt(index);
            RefreshCountLabel();
            // Re-show the latest remaining placement's stats, or clear if none.
            if (_placed.Count > 0)
            {
                var last = _placed[_placed.Count - 1];
                ShowCoverage(last.CoveragePct);
                ShowTrajectory(last);
                ShowCanalGrade(last);
                ShowSonarMetrics(last);
                ShowBreach(AnalyseScrew(last));
                OnScrewChanged?.Invoke(last.Entry, last.Tip, true);
            }
            else
            {
                if (_coverage != null) _coverage.text = "—";
                ApplyGate(-1f);
                if (_breachLabel != null) _breachLabel.text = "";
                if (_trajectoryLabel != null) _trajectoryLabel.text = "";
                ClearCanalGrade();
                OnScrewChanged?.Invoke(Vector3.zero, Vector3.zero, false);
            }
            OnPlacementsChanged?.Invoke();
        }

        void ShowCoverage(float pct)
        {
            if (_coverage != null) _coverage.text = $"{pct:0}%";
            ApplyGate(pct);
        }

        // ── Scoring + breach analysis ───────────────────────────────────────

        struct CoverageReport
        {
            public int Total;
            public int TargetHits;
            public int OutsideHits;
            public Dictionary<byte, int> Others;  // breached segment byte → voxel count
            public float TargetPct => Total > 0 ? 100f * TargetHits / Total : 0f;
        }

        CoverageReport AnalyseScrew(PlacedScrew screw)
        {
            var report = new CoverageReport { Others = new Dictionary<byte, int>() };
            if (_renderer == null || !_renderer.HasMask || screw.TargetByteValue == 0) return report;
            var bytes = _renderer.MaskBytes;
            if (bytes == null) return report;
            var (w, h, d) = _renderer.MaskDimensions;
            var t = _renderer.VolumeCubeTransform;
            if (t == null) return report;

            int N = Mathf.Max(8, (int)_screwLengthSamples);
            for (int i = 0; i <= N; i++)
            {
                float u = i / (float)N;
                var world = Vector3.Lerp(screw.Entry, screw.Tip, u);
                var local = t.InverseTransformPoint(world);
                if (Mathf.Abs(local.x) > 0.5f || Mathf.Abs(local.y) > 0.5f || Mathf.Abs(local.z) > 0.5f) continue;
                int x = Mathf.Clamp(Mathf.RoundToInt((local.x + 0.5f) * (w - 1)), 0, w - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt((local.y + 0.5f) * (h - 1)), 0, h - 1);
                int z = Mathf.Clamp(Mathf.RoundToInt((local.z + 0.5f) * (d - 1)), 0, d - 1);
                byte v = bytes[z * w * h + y * w + x];
                report.Total++;
                if (v == screw.TargetByteValue) report.TargetHits++;
                else if (v == 0) report.OutsideHits++;
                else
                {
                    if (!report.Others.TryGetValue(v, out var c)) report.Others[v] = 1;
                    else report.Others[v] = c + 1;
                }
            }
            return report;
        }

        void ShowBreach(CoverageReport r)
        {
            if (_breachLabel == null) return;
            if (r.Total <= 0) { _breachLabel.text = ""; return; }
            // Pick the top breach destination(s) by hit count, name them via
            // the resolver, plus call out outside-spine hits as "outside spine".
            var parts = new List<string>();
            // Sort other segments by count descending — only show top 3.
            var sorted = new List<KeyValuePair<byte, int>>(r.Others);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
            int shown = 0;
            foreach (var kv in sorted)
            {
                if (kv.Value == 0) continue;
                string name = _resolveLabel != null ? (_resolveLabel(kv.Key) ?? $"label {kv.Key}") : $"label {kv.Key}";
                float pct = 100f * kv.Value / r.Total;
                parts.Add($"{name}: {pct:0}%");
                if (++shown >= 3) break;
            }
            if (r.OutsideHits > 0)
            {
                float pct = 100f * r.OutsideHits / r.Total;
                parts.Add($"outside spine: {pct:0}%");
            }
            _breachLabel.text = parts.Count == 0
                ? "Breach: none"
                : "Breach into " + string.Join(", ", parts);
        }

        // ── Canal proximity / Gertzbein-Robbins grade ───────────────────────

        /// Identify which palette byte represents the spinal canal by name.
        /// SPIDER imports label it "Spinal canal"; users may rename. Case-insensitive
        /// substring match. Returns 0 if no canal segment is present.
        byte FindCanalByteValue()
        {
            if (_resolveLabel == null) return 0;
            for (int b = 1; b <= 31; b++)
            {
                var name = _resolveLabel((byte)b);
                if (string.IsNullOrEmpty(name)) continue;
                if (name.IndexOf("canal", StringComparison.OrdinalIgnoreCase) >= 0)
                    return (byte)b;
            }
            return 0;
        }

        /// Sample the screw line at N points and for each sample search a small
        /// voxel neighbourhood (≈ 6 mm) for canal voxels, returning the smallest
        /// Euclidean distance found in mm. Returns:
        ///   -1   = no canal segment / no spacing info
        ///   +∞   = canal exists but no canal voxels within search radius (safe)
        ///    0   = screw passes through canal
        /// otherwise = positive mm distance.
        float ComputeMinDistanceToCanalMm(PlacedScrew screw)
        {
            byte canalByte = FindCanalByteValue();
            if (canalByte == 0 || _renderer == null || !_renderer.HasMask) return -1f;
            var bytes = _renderer.MaskBytes;
            var (w, h, d) = _renderer.MaskDimensions;
            var t = _renderer.VolumeCubeTransform;
            if (bytes == null || t == null) return -1f;
            var spacing = _renderer.VoxelSpacingMm;
            if (spacing.x <= 0f || spacing.y <= 0f || spacing.z <= 0f) return -1f;

            // Search radius: 6 mm clinically covers grades A/B/C; anything past
            // that we treat as "safe". Convert to voxel neighbourhood per axis.
            const float searchMm = 6f;
            int kx = Mathf.Max(1, Mathf.CeilToInt(searchMm / spacing.x));
            int ky = Mathf.Max(1, Mathf.CeilToInt(searchMm / spacing.y));
            int kz = Mathf.Max(1, Mathf.CeilToInt(searchMm / spacing.z));

            int N = Mathf.Max(8, (int)_screwLengthSamples);
            float minDistMm = float.PositiveInfinity;
            for (int i = 0; i <= N; i++)
            {
                float u = i / (float)N;
                var world = Vector3.Lerp(screw.Entry, screw.Tip, u);
                var local = t.InverseTransformPoint(world);
                if (Mathf.Abs(local.x) > 0.5f || Mathf.Abs(local.y) > 0.5f || Mathf.Abs(local.z) > 0.5f) continue;
                int sx = Mathf.RoundToInt((local.x + 0.5f) * (w - 1));
                int sy = Mathf.RoundToInt((local.y + 0.5f) * (h - 1));
                int sz = Mathf.RoundToInt((local.z + 0.5f) * (d - 1));

                for (int dz = -kz; dz <= kz; dz++)
                {
                    int z = sz + dz; if (z < 0 || z >= d) continue;
                    for (int dy = -ky; dy <= ky; dy++)
                    {
                        int y = sy + dy; if (y < 0 || y >= h) continue;
                        int rowBase = z * w * h + y * w;
                        for (int dx = -kx; dx <= kx; dx++)
                        {
                            int x = sx + dx; if (x < 0 || x >= w) continue;
                            if (bytes[rowBase + x] != canalByte) continue;
                            float mmX = dx * spacing.x;
                            float mmY = dy * spacing.y;
                            float mmZ = dz * spacing.z;
                            float distSq = mmX * mmX + mmY * mmY + mmZ * mmZ;
                            if (distSq < minDistMm * minDistMm)
                                minDistMm = Mathf.Sqrt(distSq);
                        }
                    }
                }
            }
            return minDistMm;
        }

        void ShowCanalGrade(PlacedScrew screw)
        {
            if (_canalGrade == null) return;
            _canalGrade.RemoveFromClassList("grade-a");
            _canalGrade.RemoveFromClassList("grade-b");
            _canalGrade.RemoveFromClassList("grade-c");
            _canalGrade.RemoveFromClassList("grade-d");

            float distMm = ComputeMinDistanceToCanalMm(screw);
            if (distMm < 0f) { _canalGrade.text = ""; return; }
            if (float.IsInfinity(distMm))
            {
                _canalGrade.text = "Canal: > 6 mm · Grade A";
                _canalGrade.AddToClassList("grade-a");
                return;
            }
            // Gertzbein-Robbins (simplified for canal proximity):
            //   ≥ 4 mm     → A (none)
            //   2 – 4 mm   → B (mild)
            //   0 – 2 mm   → C (moderate)
            //   in canal   → D (severe)
            string grade, cls;
            if (distMm < 0.001f)         { grade = "D (in canal)"; cls = "grade-d"; }
            else if (distMm <= 2f)        { grade = "C";          cls = "grade-c"; }
            else if (distMm <= 4f)        { grade = "B";          cls = "grade-b"; }
            else                          { grade = "A";          cls = "grade-a"; }
            _canalGrade.text = $"Canal: {distMm:0.0} mm · Grade {grade}";
            _canalGrade.AddToClassList(cls);
        }

        void ClearCanalGrade()
        {
            if (_canalGrade == null) return;
            _canalGrade.text = "";
            _canalGrade.RemoveFromClassList("grade-a");
            _canalGrade.RemoveFromClassList("grade-b");
            _canalGrade.RemoveFromClassList("grade-c");
            _canalGrade.RemoveFromClassList("grade-d");
            if (_sonarMetrics != null) _sonarMetrics.text = "";
        }

        // ── SONAR-style trajectory metrics ──────────────────────────────────
        // We compute an "ideal" pedicle-screw trajectory from anatomy alone:
        //   Tip   = centroid of target vertebra mask
        //   Axis  = patient anterior direction (from MHA AnatomicalOrientation)
        //   Entry = tip moved 20 mm posterior along that axis
        //   Depth = 40 mm (typical pedicle-screw length range 35-55 mm)
        // Then compare the placed screw's centerline + axis to this reference
        // and report lateral / angular / depth deviation with SONAR's gate
        // semantics.

        const float IdealEntryOffsetM = 0.020f;   // 20 mm posterior
        const float IdealDepthMm = 40f;

        bool TryGetSegmentCentroidWorld(byte byteValue, out Vector3 centroidWorld)
        {
            centroidWorld = default;
            if (byteValue == 0) return false;
            if (_centroidWorldCache.TryGetValue(byteValue, out centroidWorld)) return true;
            if (_renderer == null || !_renderer.HasMask) return false;
            var bytes = _renderer.MaskBytes;
            var (w, h, d) = _renderer.MaskDimensions;
            var t = _renderer.VolumeCubeTransform;
            if (bytes == null || t == null) return false;

            double sx = 0, sy = 0, sz = 0; long count = 0;
            int wh = w * h;
            for (int z = 0; z < d; z++)
            {
                int zBase = z * wh;
                for (int y = 0; y < h; y++)
                {
                    int rowBase = zBase + y * w;
                    for (int x = 0; x < w; x++)
                    {
                        if (bytes[rowBase + x] != byteValue) continue;
                        sx += x; sy += y; sz += z; count++;
                    }
                }
            }
            if (count == 0) return false;
            float cx = (float)(sx / count);
            float cy = (float)(sy / count);
            float cz = (float)(sz / count);
            var local = new Vector3(cx / Mathf.Max(1, w - 1) - 0.5f,
                                    cy / Mathf.Max(1, h - 1) - 0.5f,
                                    cz / Mathf.Max(1, d - 1) - 0.5f);
            centroidWorld = t.TransformPoint(local);
            _centroidWorldCache[byteValue] = centroidWorld;
            return true;
        }

        void ShowSonarMetrics(PlacedScrew screw)
        {
            if (_sonarMetrics == null) return;
            if (!TryGetSegmentCentroidWorld(screw.TargetByteValue, out var centroidWorld))
            {
                _sonarMetrics.text = "";
                return;
            }
            // Need an anterior direction in world space; if we don't have it,
            // fall back to the placed screw's own axis (degenerate but at least
            // doesn't crash).
            var t = _renderer.VolumeCubeTransform;
            Vector3 apWorld = (screw.Tip - screw.Entry).normalized;
            if (t != null && _renderer.HasAnatomy)
            {
                apWorld = t.TransformDirection(_renderer.AnatomyAnteriorDir.normalized).normalized;
            }
            var idealEntry = centroidWorld - apWorld * IdealEntryOffsetM;

            // Placed-screw metrics
            var screwDir = (screw.Tip - screw.Entry);
            float screwLenMm = screwDir.magnitude * 1000f;
            if (screwDir.sqrMagnitude < 1e-9f) { _sonarMetrics.text = "Metrics unavailable: zero-length screw."; return; }
            var screwAxis = screwDir.normalized;

            // Lateral: perpendicular distance from screw midpoint to ideal axis
            var mid = (screw.Entry + screw.Tip) * 0.5f;
            var toMid = mid - idealEntry;
            float along = Vector3.Dot(toMid, apWorld);
            var projected = idealEntry + apWorld * along;
            float lateralMm = Vector3.Distance(mid, projected) * 1000f;

            // Angular: angle between screw axis and ideal anterior axis
            float angularDeg = Vector3.Angle(screwAxis, apWorld);

            // Depth: screw length vs ideal 40 mm
            float depthDeltaMm = screwLenMm - IdealDepthMm;

            // Gate each metric (green/yellow/red) with conservative defaults
            // for adult lumbar pedicle screws.
            string gLateral = GateText(lateralMm, 3f, 6f, "mm");
            string gAngular = GateText(angularDeg, 10f, 20f, "°");
            string gDepth   = GateText(Mathf.Abs(depthDeltaMm), 5f, 15f, "mm");
            string worst = WorstGate(lateralMm / 3f, angularDeg / 10f, Mathf.Abs(depthDeltaMm) / 5f);

            _sonarMetrics.text =
                $"SONAR metrics — Lateral: {lateralMm:0.0} mm [{gLateral}]   " +
                $"Angular: {angularDeg:0} ° [{gAngular}]   " +
                $"Depth Δ: {depthDeltaMm:+0.0;-0.0} mm vs {IdealDepthMm:0} mm ideal [{gDepth}]   " +
                $"Overall: {worst}";
        }

        // Threshold helpers — < good → green, < warn → yellow, else red.
        static string GateText(float value, float good, float warn, string unit)
        {
            if (value < good) return "green";
            if (value < warn) return "yellow";
            return "red";
        }

        static string WorstGate(params float[] normalised)
        {
            float worst = 0f;
            foreach (var v in normalised) if (v > worst) worst = v;
            if (worst < 1f) return "GREEN";
            if (worst < 2f) return "YELLOW";
            return "RED";
        }

        void ShowTrajectory(PlacedScrew screw)
        {
            if (_trajectoryLabel == null) return;
            var dir = screw.Tip - screw.Entry;
            float lenMm = dir.magnitude * 1000f;
            var t = _renderer != null ? _renderer.VolumeCubeTransform : null;
            if (t == null || dir.sqrMagnitude < 1e-9f)
            {
                _trajectoryLabel.text = $"Length: {lenMm:0} mm";
                return;
            }
            var localDir = t.InverseTransformDirection(dir.normalized);

            if (_renderer != null && _renderer.HasAnatomy)
            {
                float a = Vector3.Dot(localDir, _renderer.AnatomyAnteriorDir); // +anterior
                float s = Vector3.Dot(localDir, _renderer.AnatomySuperiorDir); // +cranial
                float r = Vector3.Dot(localDir, _renderer.AnatomyRightDir);    // +right
                // Medial angulation = angle in the axial plane between the
                // screw's anterior+lateral projection and the pure anterior
                // axis. Sign of `r` tells us which side, magnitude is the angle.
                float medialDeg = Mathf.Atan2(Mathf.Abs(r), Mathf.Max(0f, a)) * Mathf.Rad2Deg;
                string side = r >= 0f ? "right" : "left";
                // Sagittal angulation: cranial(+) or caudal(-) tilt off the
                // axial plane.
                float sagittalDeg = Mathf.Atan2(s, Mathf.Max(1e-4f, Mathf.Abs(a))) * Mathf.Rad2Deg;
                string sagSign = sagittalDeg >= 0f ? "cranial" : "caudal";
                _trajectoryLabel.text =
                    $"Length: {lenMm:0} mm   Medial: {medialDeg:0}° ({side})   Sagittal: {Mathf.Abs(sagittalDeg):0}° {sagSign}";
            }
            else
            {
                // Fall back to cube-local axes when AnatomicalOrientation was
                // unavailable (e.g. PNG-previewed patients).
                _trajectoryLabel.text =
                    $"Length: {lenMm:0} mm   (W:{localDir.x:+0.00;-0.00}  H:{localDir.y:+0.00;-0.00}  D:{localDir.z:+0.00;-0.00})";
            }
        }

        void ApplyGate(float pct)
        {
            if (_gate == null) return;
            _gate.RemoveFromClassList("green");
            _gate.RemoveFromClassList("yellow");
            _gate.RemoveFromClassList("red");
            if (pct < 0f) { _gate.text = "—"; return; }
            if (pct >= 80f) { _gate.text = "GREEN — within pedicle"; _gate.AddToClassList("green"); }
            else if (pct >= 50f) { _gate.text = "YELLOW — partial breach"; _gate.AddToClassList("yellow"); }
            else { _gate.text = "RED — significant breach"; _gate.AddToClassList("red"); }
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

        void HandleClose() { OnClose?.Invoke(); gameObject.SetActive(false); }
    }
}
