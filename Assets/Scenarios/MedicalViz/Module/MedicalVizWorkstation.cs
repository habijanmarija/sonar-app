using System.Collections.Generic;
using Host.App;
using Host.Patients;
using Host.Tools.AnnotationWidget;
using Host.Tools.DicomVolumeControl;
using Host.Tools.DicomWidget;
using Host.Tools.Notifications;
using Host.Tools.OpacityControl;
using Host.Tools.OrthoViewer;
using Host.Tools.PanelToolbar;
using Host.Tools.PatientBriefing;
using Host.Tools.ScrewPlanner;
using Host.Tools.PatientSelector;
using Host.Tools.Segmentation;
using Host.Tools.ViewControl;
using Host.Visualization;
using UnityEngine;

namespace MedicalViz.Module
{
    /// Scene-level orchestrator for MedicalViz_Workstation. Two states:
    /// PickPatient (selector visible) and ViewPatient (briefing + opacity + view + annotations).
    /// Spawns mesh GameObjects under PatientContent and tracks them by organ id so
    /// OpacityControl can update materials live without reaching into the scene.
    [AddComponentMenu("SONAR Host/MedicalViz Workstation")]
    public sealed class MedicalVizWorkstation : MonoBehaviour
    {
        [SerializeField] PatientSelectorController _selector;
        [SerializeField] PatientBriefingController _briefing;
        [SerializeField] OpacityControlController _opacity;
        [SerializeField] ViewControlController _view;
        [SerializeField] AnnotationWidgetController _annotations;
        [SerializeField] AnnotationPlacer _placer;
        [SerializeField] DicomWidgetController _dicom;
        [SerializeField] DicomVolumeControlController _volumeControl;
        [SerializeField] VolumeRenderer _volume;
        [SerializeField] SegmentationController _segmentation;
        [SerializeField] BrushPlacer _brushPlacer;
        [SerializeField] OrthoViewerController _ortho;
        [SerializeField] ScrewPlannerController _screwPlanner;
        [SerializeField] PanelToolbarController _toolbar;
        [SerializeField] Transform _patientContentRoot;

        SegmentationDoc _activeSegmentation;

        GameObject _renderedPatientRoot;
        readonly Dictionary<string, MeshRenderer> _renderersByOrganId = new();
        readonly Dictionary<string, AnnotationMarker> _markersById = new();
        AnnotationCollection _activeAnnotations;

        void OnEnable()
        {
            if (_selector != null) _selector.OnPatientPicked += HandlePatientPicked;
            if (_selector != null) _selector.OnClose += HandleToolClose;
            if (_briefing != null)
            {
                _briefing.OnClose += HandleToolClose;
                _briefing.OnBackToPatients += HandleBackToPatients;
            }
            if (_opacity != null) _opacity.OnOpacityChanged = HandleOpacityChanged;
            if (_opacity != null) _opacity.OnClose += HandleToolClose;
            if (_view != null) _view.OnClose += HandleToolClose;
            if (_annotations != null)
            {
                _annotations.OnAddRequested += HandleAddAnnotationRequested;
                _annotations.OnDeleteRequested += HandleDeleteAnnotationRequested;
                _annotations.OnClose += HandleToolClose;
            }
            if (_placer != null) _placer.OnPlacementHit += HandlePlacementHit;
            if (_dicom != null) _dicom.OnClose += HandleToolClose;
            if (_volumeControl != null)
            {
                _volumeControl.OnClose += HandleToolClose;
                _volumeControl.OnSeriesPicked += HandleSeriesPicked;
                _volumeControl.OnShowSegmentationRequested += SetSegmentMeshesVisible;
            }
            if (_segmentation != null)
            {
                _segmentation.OnClose += HandleToolClose;
                _segmentation.OnDocChanged += HandleSegmentationDocChanged;
                _segmentation.OnActiveSegmentChanged += HandleActiveSegmentChanged;
            }
            if (_ortho != null)
            {
                _ortho.OnClose += HandleToolClose;
                _ortho.OnPaintRequest += HandleOrthoPaintRequest;
            }
            if (_screwPlanner != null)
            {
                _screwPlanner.OnClose += HandleToolClose;
                _screwPlanner.OnScrewChanged += HandleScrewChanged;
                _screwPlanner.OnPlacementsChanged += HandleScrewPlacementsChanged;
            }

            PatientEventSystem.PatientUnloaded += HandlePatientUnloaded;

            if (_volume != null) _volume.OnMaskChanged += HandleMaskChanged;
        }

        void HandleMaskChanged() => _maskDirty = true;

        void Start()
        {
            // Deferred to Start so every tool controller's OnEnable has already run
            // and cached its UI element refs before Switch() disables their GameObjects.
            // If we did this in OnEnable, Unity's unspecified per-GameObject OnEnable
            // order could let Switch() disable a panel before its OnEnable fires —
            // then its UI fields stay null and the next Bind() / RebuildList() NREs.
            ConfigureToolbar();
            Switch(State.PickPatient);
        }

        void ConfigureToolbar()
        {
            if (_toolbar == null) return;
            var entries = new System.Collections.Generic.List<PanelToolbarController.Entry>();
            if (_briefing != null)     entries.Add(new() { Label = "Briefing",     Target = _briefing.gameObject });
            if (_opacity != null)      entries.Add(new() { Label = "Opacity",      Target = _opacity.gameObject });
            if (_annotations != null)  entries.Add(new() { Label = "Annotations",  Target = _annotations.gameObject });
            if (_dicom != null)        entries.Add(new() { Label = "Slice viewer", Target = _dicom.gameObject });
            if (_ortho != null)        entries.Add(new() { Label = "Ortho views",  Target = _ortho.gameObject });
            if (_volumeControl != null)entries.Add(new() { Label = "Volume",       Target = _volumeControl.gameObject });
            if (_segmentation != null) entries.Add(new() { Label = "Segmentation", Target = _segmentation.gameObject });
            if (_screwPlanner != null) entries.Add(new() { Label = "Screw planner",Target = _screwPlanner.gameObject });
            if (_view != null)         entries.Add(new() { Label = "Views",        Target = _view.gameObject });
            _toolbar.Configure(entries);
        }

        void OnDisable()
        {
            if (_selector != null) _selector.OnPatientPicked -= HandlePatientPicked;
            if (_selector != null) _selector.OnClose -= HandleToolClose;
            if (_briefing != null)
            {
                _briefing.OnClose -= HandleToolClose;
                _briefing.OnBackToPatients -= HandleBackToPatients;
            }
            if (_opacity != null) _opacity.OnOpacityChanged = null;
            if (_opacity != null) _opacity.OnClose -= HandleToolClose;
            if (_view != null) _view.OnClose -= HandleToolClose;
            if (_annotations != null)
            {
                _annotations.OnAddRequested -= HandleAddAnnotationRequested;
                _annotations.OnDeleteRequested -= HandleDeleteAnnotationRequested;
                _annotations.OnClose -= HandleToolClose;
            }
            if (_placer != null) _placer.OnPlacementHit -= HandlePlacementHit;
            if (_dicom != null) _dicom.OnClose -= HandleToolClose;
            if (_volumeControl != null)
            {
                _volumeControl.OnClose -= HandleToolClose;
                _volumeControl.OnSeriesPicked -= HandleSeriesPicked;
                _volumeControl.OnShowSegmentationRequested -= SetSegmentMeshesVisible;
            }
            if (_segmentation != null)
            {
                _segmentation.OnClose -= HandleToolClose;
                _segmentation.OnDocChanged -= HandleSegmentationDocChanged;
                _segmentation.OnActiveSegmentChanged -= HandleActiveSegmentChanged;
            }
            if (_ortho != null)
            {
                _ortho.OnClose -= HandleToolClose;
                _ortho.OnPaintRequest -= HandleOrthoPaintRequest;
            }
            if (_screwPlanner != null)
            {
                _screwPlanner.OnClose -= HandleToolClose;
                _screwPlanner.OnScrewChanged -= HandleScrewChanged;
                _screwPlanner.OnPlacementsChanged -= HandleScrewPlacementsChanged;
            }
            PatientEventSystem.PatientUnloaded -= HandlePatientUnloaded;
            if (_volume != null) _volume.OnMaskChanged -= HandleMaskChanged;

            FlushMaskIfDirty();
            ClearPatientContent();
        }

        // ── Patient lifecycle ────────────────────────────────────────────────

        void HandlePatientPicked(Patient p)
        {
            RenderPatientMeshes(p);
            // Switch first so tool panels' OnEnable hooks run and initialise their UXML
            // element references before we try to push data into them.
            Switch(State.ViewPatient);
            LoadAnnotationsFor(p);
            LoadVolumeFor(p);
            LoadSegmentationFor(p);
        }

        void LoadSegmentationFor(Patient p)
        {
            if (_segmentation == null || _volume == null) return;
            _activeSegmentation = SegmentationStore.Load(p);
            _knownSegmentIds.Clear();
            if (_activeSegmentation.Segments != null)
                foreach (var s in _activeSegmentation.Segments) _knownSegmentIds.Add(s.Id);
            _segmentation.Bind(_volume, _activeSegmentation);
            if (_brushPlacer != null)
            {
                _brushPlacer.Bind(_volume);
                _segmentation.BindBrush(_brushPlacer);
            }
            UpdatePalette();
            RebuildCombinedFromDisk();
            UpdateBrushActiveValue();
            // LoadVolumeFor (called just before this) set _activeBrushSegmentId
            // from a then-null _activeSegmentation, so the screw planner's
            // target byte was 0. Re-push now that segments are loaded.
            _activeBrushSegmentId = _activeSegmentation?.ActiveSegmentId;
            RefreshScrewPlannerTarget();
            // Restore any screws this patient already has on disk. Suppress the
            // save-on-change callback during restore so we don't re-write the
            // file we just read.
            if (_screwPlanner != null)
            {
                _screwPlanner.OnPlacementsChanged -= HandleScrewPlacementsChanged;
                var doc = ScrewPlacementStore.Load(p);
                _screwPlanner.RestoreFrom(doc.Screws);
                _screwPlanner.OnPlacementsChanged += HandleScrewPlacementsChanged;
            }
        }

        void HandleSegmentationDocChanged(SegmentationDoc doc)
        {
            _activeSegmentation = doc;
            if (PatientRegistry.Active != null)
                SegmentationStore.Save(PatientRegistry.Active, doc);
            // Detect deleted segments and remove their mask files from disk.
            var current = new HashSet<string>();
            if (doc?.Segments != null)
                foreach (var s in doc.Segments) current.Add(s.Id);
            bool anyDeleted = false;
            foreach (var prevId in _knownSegmentIds)
            {
                if (current.Contains(prevId)) continue;
                anyDeleted = true;
                if (_activeVolumePatient != null && !string.IsNullOrEmpty(_activeVolumeSeriesId))
                    BrushMaskStore.Delete(_activeVolumePatient, _activeVolumeSeriesId, prevId);
            }
            _knownSegmentIds.Clear();
            foreach (var id in current) _knownSegmentIds.Add(id);

            UpdatePalette();
            // A deletion (or re-order) changes the byte-index mapping — rebuild
            // the combined mask (which also rebuilds all meshes). For renames /
            // recolours we just refresh visibility + colours of existing meshes.
            if (anyDeleted) RebuildCombinedFromDisk();
            else RefreshSegmentMeshVisibility();
            UpdateBrushActiveValue();
        }

        /// User switched which segment is "active" — in multi-segment rendering
        /// the combined mask stays put (so every segment remains visible at once);
        /// we just retarget the brush at the newly-active byte value.
        void HandleActiveSegmentChanged(string oldId, string newId)
        {
            FlushMaskIfDirty();
            _activeBrushSegmentId = newId;
            UpdateBrushActiveValue();
            RefreshScrewPlannerTarget();
            _segmentation?.SyncAfterMaskSwap();
        }

        // ── Multi-segment palette + combined-mask plumbing ───────────────────

        /// Byte value to write into the mask voxel when painting `segmentId`.
        /// Index 0 is reserved for "empty"; we cap at 31 (palette length).
        byte GetSegmentByteValue(string segmentId)
        {
            if (string.IsNullOrEmpty(segmentId) || _activeSegmentation?.Segments == null) return 0;
            for (int i = 0; i < _activeSegmentation.Segments.Count && i < 31; i++)
                if (_activeSegmentation.Segments[i].Id == segmentId)
                    return (byte)(i + 1);
            return 0;
        }

        void UpdateBrushActiveValue()
        {
            if (_brushPlacer == null) return;
            var v = GetSegmentByteValue(_activeBrushSegmentId);
            // If the active segment isn't in the palette range, fall back to 255
            // so the brush still works (renderer treats 255 as legacy _MaskColor).
            _brushPlacer.ActiveValue = v == 0 ? (byte)255 : v;
        }

        void UpdatePalette()
        {
            if (_volume == null || _activeSegmentation?.Segments == null)
            {
                var empty = new Color[32];
                _volume?.SetSegmentPalette(empty);
                _ortho?.SetPalette(empty);
                return;
            }
            var palette = new Color[32];
            float global = Mathf.Clamp01(_activeSegmentation.GlobalOpacity);
            for (int i = 0; i < _activeSegmentation.Segments.Count && i < 31; i++)
            {
                var seg = _activeSegmentation.Segments[i];
                var c = seg.Color;
                // Combined alpha = global × per-segment × enabled-flag. Lets the
                // user dim the entire overlay with one slider while still
                // tweaking individual segments. Disabled segments collapse to 0.
                c.a = seg.Enabled ? global * Mathf.Clamp01(seg.Opacity) : 0f;
                palette[i + 1] = c;
            }
            _volume.SetSegmentPalette(palette);
            _ortho?.SetPalette(palette);
            // Push the same alpha into each segment mesh's material so the
            // 3D view fades with the sliders, not just the palette overlay.
            for (int i = 0; i < _activeSegmentation.Segments.Count && i < 31; i++)
            {
                var seg = _activeSegmentation.Segments[i];
                if (!_segmentMeshes.TryGetValue(seg.Id, out var holder) || holder.Material == null) continue;
                var col = seg.Color;
                col.a = seg.Enabled ? global * Mathf.Clamp01(seg.Opacity) : 0f;
                holder.Material.color = col;
                if (holder.Material.HasProperty("_BaseColor")) holder.Material.SetColor("_BaseColor", col);
            }
        }

        // ── Segment-mesh extraction (marching cubes) ─────────────────────────

        /// Rebuild every segment's surface mesh from the current combined mask.
        /// Each mesh is a child of the VolumeRenderer's cube transform so it
        /// inherits the cube's pose+scale.
        void RebuildAllSegmentMeshes()
        {
            ClearAllSegmentMeshes();
            if (_volume == null || !_volume.HasMask || _activeSegmentation?.Segments == null) return;
            var cubeTransform = _volume.VolumeCubeTransform;
            if (cubeTransform == null) return;
            var (w, h, d) = _volume.MaskDimensions;
            var combined = _volume.GetMaskSnapshot();
            if (combined == null) return;
            for (int i = 0; i < _activeSegmentation.Segments.Count && i < 31; i++)
                BuildOneSegmentMesh(_activeSegmentation.Segments[i], (byte)(i + 1), combined, w, h, d, cubeTransform);
        }

        /// Rebuild just one segment's mesh — used after a paint stroke so we
        /// don't pay the marching-cubes cost on every segment when only one
        /// changed.
        void RebuildSegmentMesh(string segmentId)
        {
            if (string.IsNullOrEmpty(segmentId)) return;
            if (_volume == null || !_volume.HasMask || _activeSegmentation?.Segments == null) return;
            var cubeTransform = _volume.VolumeCubeTransform;
            if (cubeTransform == null) return;
            byte idx = GetSegmentByteValue(segmentId);
            if (idx == 0) return;
            var seg = _activeSegmentation.Segments[idx - 1];
            var (w, h, d) = _volume.MaskDimensions;
            var combined = _volume.GetMaskSnapshot();
            if (combined == null) return;
            DestroySegmentMesh(segmentId);
            BuildOneSegmentMesh(seg, idx, combined, w, h, d, cubeTransform);
        }

        void BuildOneSegmentMesh(Segment seg, byte idx, byte[] combined, int w, int h, int d, Transform cubeTransform)
        {
            var mesh = MarchingCubes.Build(combined, w, h, d, idx);
            if (mesh == null || mesh.vertexCount == 0) { if (mesh != null) Destroy(mesh); return; }
            var go = new GameObject($"Segment_{seg.Id}");
            go.transform.SetParent(cubeTransform, worldPositionStays: false);
            go.SetActive(_segmentMeshesVisible && seg.Enabled);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            // Two-sided lit shader: shades both sides of each triangle, flips
            // the normal on back faces so marching-cubes meshes never look
            // dim or washed regardless of which side faces the camera.
            // Falls back to URP/Unlit if the project doesn't include the
            // custom shader (e.g. shader stripping in a build).
            var shader = Shader.Find("Host/Visualization/SegmentMeshLit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", seg.Color);
            mat.color = seg.Color;
            mr.sharedMaterial = mat;
            // Hittable MeshCollider so the screw planner and the brush can land
            // clicks directly on the rendered vertebra/disc/canal surface
            // instead of the outer cube box. Marching-cubes meshes are small
            // (a few thousand triangles per segment), so MeshCollider is cheap.
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            _segmentMeshes[seg.Id] = new SegmentMeshHolder { Go = go, Mesh = mesh, Material = mat };
        }

        void DestroySegmentMesh(string segmentId)
        {
            if (!_segmentMeshes.TryGetValue(segmentId, out var h)) return;
            if (h.Mesh != null) Destroy(h.Mesh);
            if (h.Material != null) Destroy(h.Material);
            if (h.Go != null) Destroy(h.Go);
            _segmentMeshes.Remove(segmentId);
        }

        void ClearAllSegmentMeshes()
        {
            foreach (var id in new List<string>(_segmentMeshes.Keys)) DestroySegmentMesh(id);
        }

        void SetSegmentMeshesVisible(bool show)
        {
            _segmentMeshesVisible = show;
            RefreshSegmentMeshVisibility();
        }

        /// Apply per-segment Enabled state + global visibility to every mesh
        /// GameObject (called after rebuilds + after the doc changes).
        void RefreshSegmentMeshVisibility()
        {
            if (_activeSegmentation?.Segments == null) return;
            float global = Mathf.Clamp01(_activeSegmentation.GlobalOpacity);
            foreach (var seg in _activeSegmentation.Segments)
            {
                if (!_segmentMeshes.TryGetValue(seg.Id, out var h) || h.Go == null) continue;
                h.Go.SetActive(_segmentMeshesVisible && seg.Enabled);
                if (h.Material != null)
                {
                    var col = seg.Color;
                    col.a = seg.Enabled ? global * Mathf.Clamp01(seg.Opacity) : 0f;
                    h.Material.color = col;
                    if (h.Material.HasProperty("_BaseColor")) h.Material.SetColor("_BaseColor", col);
                }
            }
        }

        /// Aggregate every segment's persisted brush mask into one combined
        /// volume where each voxel byte is the segment's palette index. All
        /// segments render simultaneously; the brush still only writes the
        /// active segment's index, so painting into one doesn't disturb others
        /// unless it overlaps spatially.
        void RebuildCombinedFromDisk()
        {
            if (_volume == null || !_volume.HasMask) return;
            if (_activeVolumePatient == null || string.IsNullOrEmpty(_activeVolumeSeriesId)) return;
            if (_activeSegmentation?.Segments == null || _activeSegmentation.Segments.Count == 0)
            {
                _volume.ClearMask();
                return;
            }
            var (w, h, d) = _volume.MaskDimensions;
            long total = (long)w * h * d;
            if (total <= 0 || total > int.MaxValue) return;
            var combined = new byte[total];
            for (int i = 0; i < _activeSegmentation.Segments.Count && i < 31; i++)
            {
                var seg = _activeSegmentation.Segments[i];
                if (!BrushMaskStore.TryLoad(_activeVolumePatient, _activeVolumeSeriesId, seg.Id, out int sw, out int sh, out int sd, out var bytes)) continue;
                if (sw != w || sh != h || sd != d || bytes == null) continue;
                byte idx = (byte)(i + 1);
                for (int v = 0; v < bytes.Length; v++)
                    if (bytes[v] > 0) combined[v] = idx;
            }
            _volume.TryRestoreMask(w, h, d, combined);
            _maskDirty = false;
            RebuildAllSegmentMeshes();
        }

        Patient _activeVolumePatient;
        string _activeVolumeSeriesId;
        string _activeBrushSegmentId;
        bool _maskDirty;
        float _maskLastSaveTime = -10f;
        const float MaskSaveInterval = 0.5f;
        // Segment ids the workstation has seen in the current doc — used to detect
        // deletions in HandleSegmentationDocChanged and clean up orphaned mask files.
        readonly HashSet<string> _knownSegmentIds = new();

        // ── 3D segment meshes (marching cubes) ───────────────────────────────
        sealed class SegmentMeshHolder { public GameObject Go; public Mesh Mesh; public Material Material; }
        readonly Dictionary<string, SegmentMeshHolder> _segmentMeshes = new();
        bool _segmentMeshesVisible = true;

        void LoadVolumeFor(Patient p)
        {
            FlushMaskIfDirty();
            _activeVolumePatient = p;
            if (_volume == null) return;
            // Collect all loadable image series so the Volume panel can offer a
            // dropdown for the user to switch between them.
            var ids = new List<string>();
            ImageSeries thickest = null;
            if (p?.Series != null)
            {
                foreach (var s in p.Series.Values)
                {
                    if (s is ImageSeries imgSeries && imgSeries.SliceCount > 0)
                    {
                        ids.Add(imgSeries.Id);
                        // Default pick: thickest series — most likely the useful volume.
                        if (thickest == null || imgSeries.SliceCount > thickest.SliceCount)
                            thickest = imgSeries;
                    }
                }
            }
            if (thickest == null)
            {
                _activeVolumeSeriesId = null;
                _volume.Unload();
                _volumeControl?.Unbind();
                _volumeControl?.SetSeriesChoices(System.Array.Empty<string>(), null);
                return;
            }
            ActivateSeries(thickest);
            _volumeControl?.SetSeriesChoices(ids, thickest.Id);
        }

        void HandleSeriesPicked(string seriesId)
        {
            FlushMaskIfDirty();
            if (_activeVolumePatient == null || _volume == null) return;
            if (!_activeVolumePatient.Series.TryGetValue(seriesId, out var s)) return;
            if (s is not ImageSeries imgSeries) return;
            ActivateSeries(imgSeries);
        }

        void ActivateSeries(ImageSeries series)
        {
            if (!_volume.LoadFromImageSeries(series)) return;
            _activeVolumeSeriesId = series.Id;
            _volumeControl?.Bind(_volume);
            _ortho?.Bind(_volume);
            _ortho?.BindBrush(_brushPlacer);
            _screwPlanner?.Bind(_volume, _patientContentRoot);
            _activeBrushSegmentId = _activeSegmentation?.ActiveSegmentId;
            UpdatePalette();
            RebuildCombinedFromDisk();
            UpdateBrushActiveValue();
            RefreshScrewPlannerTarget();
            _maskDirty = false;
        }

        /// Persist whenever the placed-screw list changes (placement added or
        /// Clear all). Active patient is the one whose folder we save into.
        void HandleScrewPlacementsChanged()
        {
            if (_screwPlanner == null) return;
            var active = PatientRegistry.Active;
            if (active != null)
            {
                var doc = new ScrewPlacementDoc { PatientId = active.Meta?.PatientId };
                doc.Screws = new List<ScrewPlacement>(_screwPlanner.Snapshot());
                ScrewPlacementStore.Save(active, doc);
            }
            // Mirror the latest screw set onto the ortho panes too — keeps the
            // 2D projection in sync with the 3D scene after every commit / clear.
            PushAllScrewsToOrtho();
        }

        /// Bridge ScrewPlanner → OrthoViewer: project every placed screw onto
        /// the 2D panes (not just the latest), each in its own segment colour.
        /// Called on every placement, clear, restore, and patient swap.
        void PushAllScrewsToOrtho()
        {
            if (_ortho == null) return;
            if (_volume == null || !_volume.HasMask || _screwPlanner == null)
            {
                _ortho.ClearScrews();
                return;
            }
            var t = _volume.VolumeCubeTransform;
            if (t == null) { _ortho.ClearScrews(); return; }
            var (w, h, d) = _volume.MaskDimensions;
            var snaps = _screwPlanner.Snapshot();
            var projections = new List<OrthoViewerController.ScrewProjection>(snaps.Count);
            foreach (var s in snaps)
            {
                projections.Add(new OrthoViewerController.ScrewProjection
                {
                    EntryVoxel = WorldToVoxel(t, s.Entry, w, h, d),
                    TipVoxel   = WorldToVoxel(t, s.Tip,   w, h, d),
                    Color      = s.Color,
                });
            }
            _ortho.SetScrews(projections);
        }

        // Existing event still fires; we just rebuild the full ortho overlay
        // each time rather than tracking one screw. Cheap (a few voxels of math
        // + VisualElement create/destroy per placement).
        void HandleScrewChanged(Vector3 entryWorld, Vector3 tipWorld, bool valid) => PushAllScrewsToOrtho();

        static Vector3Int WorldToVoxel(Transform cube, Vector3 world, int w, int h, int d)
        {
            var local = cube.InverseTransformPoint(world);
            int x = Mathf.Clamp(Mathf.RoundToInt((local.x + 0.5f) * (w - 1)), 0, w - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt((local.y + 0.5f) * (h - 1)), 0, h - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt((local.z + 0.5f) * (d - 1)), 0, d - 1);
            return new Vector3Int(x, y, z);
        }

        void RefreshScrewPlannerTarget()
        {
            if (_screwPlanner == null) return;
            // Always rebuild the byte→label resolver, so breach reports name the
            // adjacent SPIDER structures even when no segment is active.
            _screwPlanner.SetLabelResolver(BuildSegmentLabelResolver());
            if (_activeSegmentation?.Segments == null || string.IsNullOrEmpty(_activeBrushSegmentId))
            {
                _screwPlanner.SetTarget(null, null, 0, Color.gray);
                return;
            }
            for (int i = 0; i < _activeSegmentation.Segments.Count && i < 31; i++)
            {
                var s = _activeSegmentation.Segments[i];
                if (s.Id != _activeBrushSegmentId) continue;
                _screwPlanner.SetTarget(s.Id, s.Label, (byte)(i + 1), s.Color);
                return;
            }
            _screwPlanner.SetTarget(null, null, 0, Color.gray);
        }

        System.Func<byte, string> BuildSegmentLabelResolver()
        {
            var doc = _activeSegmentation;
            return b =>
            {
                if (b == 0 || doc?.Segments == null) return null;
                int idx = b - 1;
                if (idx < 0 || idx >= doc.Segments.Count) return null;
                return doc.Segments[idx].Label;
            };
        }

        /// Drag-paint on a 2D ortho slice. axis matches VolumeRenderer.PaintVoxelDisk:
        /// 0 = sagittal (X fixed), 1 = coronal (Y fixed), 2 = axial (Z fixed).
        void HandleOrthoPaintRequest(Vector3Int v, int axis)
        {
            if (_volume == null || _brushPlacer == null) return;
            byte value = _brushPlacer.Erasing ? (byte)0 : _brushPlacer.ActiveValue;
            _volume.PaintVoxelDisk(v.x, v.y, v.z, _brushPlacer.Radius, value, axis);
        }

        /// Save the segment whose byte value is `segmentId`'s palette index, by
        /// extracting voxels whose value matches that index from the combined
        /// mask and writing them as a 255/0 binary mask file. Only the active
        /// segment is saved here — other segments stay untouched (their files
        /// are stale-tolerant: a paint that overwrites another segment's voxels
        /// leaves the other segment's .bin slightly out of sync until the user
        /// activates it and paints again, which is acceptable for clean labels
        /// like SPIDER's where overlaps are rare).
        void FlushMaskForSegment(string segmentId)
        {
            if (string.IsNullOrEmpty(segmentId)) return;
            if (_activeVolumePatient == null || _activeVolumeSeriesId == null || _volume == null || !_volume.HasMask) return;
            byte idx = GetSegmentByteValue(segmentId);
            if (idx == 0) return;
            var (w, h, d) = _volume.MaskDimensions;
            var combined = _volume.GetMaskSnapshot();
            if (combined == null) return;
            var segBytes = new byte[combined.Length];
            for (int i = 0; i < combined.Length; i++)
                if (combined[i] == idx) segBytes[i] = 255;
            BrushMaskStore.Save(_activeVolumePatient, _activeVolumeSeriesId, segmentId, w, h, d, segBytes);
        }

        void LateUpdate()
        {
            if (!_maskDirty) return;
            if (Time.unscaledTime - _maskLastSaveTime < MaskSaveInterval) return;
            FlushMaskIfDirty();
        }

        void FlushMaskIfDirty()
        {
            if (!_maskDirty) return;
            if (string.IsNullOrEmpty(_activeBrushSegmentId)) { _maskDirty = false; return; }
            FlushMaskForSegment(_activeBrushSegmentId);
            RebuildSegmentMesh(_activeBrushSegmentId);
            _maskLastSaveTime = Time.unscaledTime;
            _maskDirty = false;
        }

        /// "← Patients" button on the briefing panel. Distinct from briefing's Close,
        /// which now just hides the widget like every other tool's Close button.
        void HandleBackToPatients()
        {
            PersistAnnotations();
            PatientRegistry.UnloadActive();
            Switch(State.PickPatient);
        }

        void HandlePatientUnloaded(Patient _)
        {
            FlushMaskIfDirty();
            ClearPatientContent();
        }

        void HandleToolClose() { /* per-panel close handled by the controller itself */ }

        // ── Opacity slider → live material update ────────────────────────────

        void HandleOpacityChanged(string organId, float opacity01)
        {
            if (string.IsNullOrEmpty(organId)) return;
            if (!_renderersByOrganId.TryGetValue(organId, out var renderer)) return;
            if (renderer == null) return;
            var mat = renderer.sharedMaterial;
            if (mat == null) return;
            var c = mat.color;
            c.a = Mathf.Clamp01(opacity01);
            mat.color = c;
        }

        // ── Annotations ──────────────────────────────────────────────────────

        void LoadAnnotationsFor(Patient p)
        {
            _activeAnnotations = AnnotationStore.Load(p);
            _markersById.Clear();
            if (_activeAnnotations?.Annotations != null && _renderedPatientRoot != null)
            {
                foreach (var a in _activeAnnotations.Annotations)
                {
                    var marker = AnnotationMarker.Create(a, _renderedPatientRoot.transform);
                    _markersById[a.Id] = marker;
                }
            }
            _annotations?.RebuildList(_activeAnnotations?.Annotations);
        }

        void HandleAddAnnotationRequested(string label)
        {
            if (_placer == null)
            {
                NotificationSystem.Show("No AnnotationPlacer wired on MedicalVizWorkstation.", NotificationSeverity.Error);
                return;
            }
            _pendingLabel = label;
            _placer.Begin();
        }

        string _pendingLabel;

        void HandlePlacementHit(Vector3 worldPoint)
        {
            if (_activeAnnotations == null || _renderedPatientRoot == null) return;
            var local = _renderedPatientRoot.transform.InverseTransformPoint(worldPoint);
            var ann = Annotation.New(local, _pendingLabel ?? "marker");
            _activeAnnotations.Annotations.Add(ann);
            var marker = AnnotationMarker.Create(ann, _renderedPatientRoot.transform);
            _markersById[ann.Id] = marker;
            _annotations?.RebuildList(_activeAnnotations.Annotations);
            PersistAnnotations();
            NotificationSystem.Show($"Added '{ann.Label}'", NotificationSeverity.Info, 1.5f);
            _pendingLabel = null;
        }

        void HandleDeleteAnnotationRequested(string id)
        {
            if (_activeAnnotations == null) return;
            _activeAnnotations.Annotations.RemoveAll(a => a.Id == id);
            if (_markersById.TryGetValue(id, out var marker) && marker != null) Destroy(marker.gameObject);
            _markersById.Remove(id);
            _annotations?.RebuildList(_activeAnnotations.Annotations);
            PersistAnnotations();
        }

        void PersistAnnotations()
        {
            if (PatientRegistry.Active == null || _activeAnnotations == null) return;
            if (!AnnotationStore.Save(PatientRegistry.Active, _activeAnnotations))
                NotificationSystem.Show("Annotation save failed — see Console.", NotificationSeverity.Error);
        }

        // ── State switching ──────────────────────────────────────────────────

        void Switch(State state)
        {
            bool viewing = state == State.ViewPatient;
            if (_selector != null) _selector.gameObject.SetActive(state == State.PickPatient);
            // ViewPatient default: only the toolbar and the slice viewer are visible;
            // every other tool panel is summoned on demand via a toolbar button.
            if (_toolbar != null) _toolbar.gameObject.SetActive(viewing);
            if (_dicom != null) _dicom.gameObject.SetActive(viewing);
            if (_briefing != null) _briefing.gameObject.SetActive(false);
            if (_opacity != null) _opacity.gameObject.SetActive(false);
            if (_view != null) _view.gameObject.SetActive(false);
            if (_annotations != null) _annotations.gameObject.SetActive(false);
            if (_volumeControl != null) _volumeControl.gameObject.SetActive(false);
            if (_segmentation != null) _segmentation.gameObject.SetActive(false);
            if (_ortho != null) _ortho.gameObject.SetActive(false);
            if (_screwPlanner != null) _screwPlanner.gameObject.SetActive(false);
        }

        // ── Mesh spawning ────────────────────────────────────────────────────

        void RenderPatientMeshes(Patient p)
        {
            ClearPatientContent();
            if (p == null)
            {
                Debug.LogWarning("[MedicalViz] RenderPatientMeshes called with null patient.");
                return;
            }
            if (_patientContentRoot == null)
            {
                Debug.LogError("[MedicalViz] _patientContentRoot is not assigned on MedicalVizWorkstation — cannot spawn meshes. Re-run the Editor menu or wire the field manually.");
                return;
            }

            _renderedPatientRoot = new GameObject($"Patient_{p.Meta.PatientId}");
            _renderedPatientRoot.transform.SetParent(_patientContentRoot, worldPositionStays: false);
            Debug.Log($"[MedicalViz] spawning mesh GameObjects under {_patientContentRoot.name} → Patient_{p.Meta.PatientId} ({p.Meshes.Count} mesh(es)).");

            int spawned = 0;
            foreach (var organ in p.Meshes.Values)
            {
                if (organ.UnityMesh == null)
                {
                    Debug.LogWarning($"[MedicalViz] organ '{organ.Id}' has null UnityMesh — skipped.");
                    continue;
                }
                var go = new GameObject(organ.Name);
                go.transform.SetParent(_renderedPatientRoot.transform, worldPositionStays: false);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = organ.UnityMesh;
                var mr = go.AddComponent<MeshRenderer>();
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Standard");
                var mat = new Material(shader);
                var c = organ.Color;
                c.a = organ.Opacity;
                mat.color = c;
                mr.sharedMaterial = mat;

                // Collider so AnnotationPlacer's raycast can hit this organ.
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = organ.UnityMesh;

                _renderersByOrganId[organ.Id] = mr;
                spawned++;
            }
            Debug.Log($"[MedicalViz] spawned {spawned} mesh GameObject(s) for patient '{p.Meta.PatientId}'.");
        }

        void ClearPatientContent()
        {
            if (_renderedPatientRoot != null) Destroy(_renderedPatientRoot);
            _renderedPatientRoot = null;
            _renderersByOrganId.Clear();
            _markersById.Clear();
            _activeAnnotations = null;
            ClearAllSegmentMeshes();
            if (_volume != null) _volume.Unload();
            _volumeControl?.Unbind();
            _volumeControl?.SetSeriesChoices(System.Array.Empty<string>(), null);
            _segmentation?.Unbind();
            _activeSegmentation = null;
            _activeVolumePatient = null;
        }

        enum State { PickPatient, ViewPatient }
    }
}
