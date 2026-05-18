using System;
using Host.Patients;
using UnityEngine;

namespace Host.Visualization
{
    /// Spawns a unit cube with the volume-rendering shader attached and uploads an
    /// ImageSeries' slice stack into a Texture3D bound to the material. Exposes
    /// threshold / density / tint properties for the DicomVolumeControl UI to drive.
    [AddComponentMenu("SONAR Host/Visualization/Volume Renderer")]
    public sealed class VolumeRenderer : MonoBehaviour
    {
        const string ShaderName = "Host/Visualization/Volume";

        [SerializeField] Vector3 _volumeSizeMetres = new(0.3f, 0.3f, 0.3f);

        Material _material;
        Texture3D _volumeTexture;
        Texture3D _maskTexture;
        byte[] _maskBytes;
        byte[] _volumeBytes;          // retained so 2D slicers (OrthoViewer) can sample any axis
        int _maskW, _maskH, _maskD;
        GameObject _cube;
        BoxCollider _cubeCollider;
        GameObject _outlineRoot;
        ImageSeries _series;

        public bool HasVolume => _volumeTexture != null;
        public ImageSeries Series => _series;

        /// Fires whenever PaintAt or ClearMask mutated the mask. Workstation listens
        /// to this to schedule a debounced save through BrushMaskStore.
        public event System.Action OnMaskChanged;

        /// World-space transform of the volume cube — brush raycasts hit this collider
        /// and the hit point converts back into mask voxel coords via the cube's transform.
        public Transform VolumeCubeTransform => _cube != null ? _cube.transform : null;

        void Awake()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[VolumeRenderer] shader not found: {ShaderName}");
                return;
            }
            _material = new Material(shader);
            // The volumetric mask path is permanently off — segmentation is now
            // rendered as solid marching-cubes meshes from the workstation. The
            // mask Texture3D is still updated by the brush so the path could be
            // re-enabled later if we want both modes side-by-side.
            _material.SetFloat("_ShowSegmentation", 0f);

            _cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cube.name = "VolumeCube";
            _cube.transform.SetParent(transform, worldPositionStays: false);
            _cube.transform.localScale = _volumeSizeMetres;
            // Keep the BoxCollider so brush raycasts can hit the volume cube.
            _cubeCollider = _cube.GetComponent<BoxCollider>();
            _cube.GetComponent<MeshRenderer>().sharedMaterial = _material;
            BuildWireframeOutline();
            _cube.SetActive(false); // no volume loaded yet
        }

        // 12 LineRenderers tracing the cube's edges so the user has a visible target
        // for the brush even when the volume content renders fully transparent.
        void BuildWireframeOutline()
        {
            _outlineRoot = new GameObject("Outline");
            _outlineRoot.transform.SetParent(_cube.transform, worldPositionStays: false);

            var lineShader = Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Unlit/Color")
                             ?? Shader.Find("Sprites/Default");
            var lineMat = new Material(lineShader);
            var outlineColor = new Color(0.45f, 0.85f, 1.0f, 1.0f);
            if (lineMat.HasProperty("_BaseColor")) lineMat.SetColor("_BaseColor", outlineColor);
            lineMat.color = outlineColor;

            var c = new Vector3[]
            {
                new(-0.5f, -0.5f, -0.5f),
                new( 0.5f, -0.5f, -0.5f),
                new( 0.5f,  0.5f, -0.5f),
                new(-0.5f,  0.5f, -0.5f),
                new(-0.5f, -0.5f,  0.5f),
                new( 0.5f, -0.5f,  0.5f),
                new( 0.5f,  0.5f,  0.5f),
                new(-0.5f,  0.5f,  0.5f),
            };
            int[] edges = { 0,1, 1,2, 2,3, 3,0,  4,5, 5,6, 6,7, 7,4,  0,4, 1,5, 2,6, 3,7 };
            for (int i = 0; i < edges.Length; i += 2)
            {
                var edge = new GameObject($"Edge_{i/2}");
                edge.transform.SetParent(_outlineRoot.transform, worldPositionStays: false);
                var lr = edge.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.positionCount = 2;
                lr.SetPosition(0, c[edges[i]]);
                lr.SetPosition(1, c[edges[i + 1]]);
                // Width is in world units, not cube-local, so a small value keeps the
                // outline crisp regardless of _volumeSizeMetres.
                lr.startWidth = 0.0025f;
                lr.endWidth = 0.0025f;
                lr.sharedMaterial = lineMat;
                lr.numCapVertices = 0;
                lr.numCornerVertices = 0;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
            }
        }

        /// Load an ImageSeries' slice stack into a Texture3D. Assumes slices are
        /// grayscale-equivalent (we read the R channel). All slices must share the
        /// same width × height; mismatched series get rejected.
        public bool LoadFromImageSeries(ImageSeries series)
        {
            if (series == null || series.SliceCount == 0)
            {
                Debug.LogWarning("[VolumeRenderer] LoadFromImageSeries: empty series.");
                return false;
            }
            var first = series.Slice(0);
            if (first == null) return false;
            int w = first.width, h = first.height, d = series.SliceCount;

            for (int z = 0; z < d; z++)
            {
                var s = series.Slice(z);
                if (s == null || s.width != w || s.height != h)
                {
                    Debug.LogError($"[VolumeRenderer] slice {z} has mismatched dimensions {s?.width}×{s?.height} vs {w}×{h}.");
                    return false;
                }
            }

            DisposeVolumeTexture();
            _volumeTexture = new Texture3D(w, h, d, TextureFormat.R8, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var bytes = new byte[w * h * d];
            for (int z = 0; z < d; z++)
            {
                var pixels = series.Slice(z).GetPixels32();
                int baseIdx = z * w * h;
                for (int i = 0; i < pixels.Length; i++)
                    bytes[baseIdx + i] = pixels[i].r;
            }
            _volumeTexture.SetPixelData(bytes, 0);
            // makeNoLongerReadable: true would drop the GPU-side CPU copy, but
            // we keep our own byte[] anyway so 2D slice viewers can sample any
            // axis without re-reading the texture.
            _volumeTexture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            _volumeBytes = bytes;

            _material.SetTexture("_Volume", _volumeTexture);

            // Allocate the mask Texture3D at the same dimensions; starts empty.
            DisposeMaskTexture();
            _maskW = w; _maskH = h; _maskD = d;
            _maskBytes = new byte[w * h * d];
            _maskTexture = new Texture3D(w, h, d, TextureFormat.R8, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            _maskTexture.SetPixelData(_maskBytes, 0);
            _maskTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            _material.SetTexture("_Mask", _maskTexture);

            _series = series;
            AnatomyAnteriorDir = series.AnatomyAnteriorDir;
            AnatomySuperiorDir = series.AnatomySuperiorDir;
            AnatomyRightDir    = series.AnatomyRightDir;
            VoxelSpacingMm     = series.VoxelSpacingMm;
            ApplyCubeProportions(w, h, d, series.VoxelSpacingMm);
            _cube.SetActive(true);
            Debug.Log($"[VolumeRenderer] uploaded volume {w}×{h}×{d} for series '{series.Id}' ({series.Modality}), cube scale {_cube.transform.localScale}.");
            return true;
        }

        /// Scale the cube to match the volume's actual physical proportions so
        /// thin-slice acquisitions don't appear stretched into a square block.
        /// When `spacing` is zero (unknown — PNG previews) we fall back to the
        /// SerializeField cube size.
        void ApplyCubeProportions(int w, int h, int d, Vector3 spacingMm)
        {
            if (_cube == null) return;
            if (spacingMm.x <= 0f || spacingMm.y <= 0f || spacingMm.z <= 0f)
            {
                _cube.transform.localScale = _volumeSizeMetres;
                return;
            }
            float xMm = w * spacingMm.x;
            float yMm = h * spacingMm.y;
            float zMm = d * spacingMm.z;
            float maxMm = Mathf.Max(xMm, Mathf.Max(yMm, zMm));
            if (maxMm <= 0f) { _cube.transform.localScale = _volumeSizeMetres; return; }
            float maxEdge = Mathf.Max(_volumeSizeMetres.x, Mathf.Max(_volumeSizeMetres.y, _volumeSizeMetres.z));
            _cube.transform.localScale = new Vector3(xMm, yMm, zMm) / maxMm * maxEdge;
        }

        // ── Brush mask ───────────────────────────────────────────────────────

        /// Paint a sphere of voxels at `worldPoint` in the mask. `radiusVoxels` is
        /// in mask-texture-space (typical 2–10 for a visible mark). `value` is the
        /// byte written into each voxel — 255 for paint, 0 for erase. Returns true
        /// if any voxels were touched.
        public bool PaintAt(Vector3 worldPoint, int radiusVoxels, byte value = 255)
        {
            if (_maskTexture == null || _cube == null) return false;
            var localPoint = _cube.transform.InverseTransformPoint(worldPoint);
            // Local cube is unit-size centred at origin; map [-0.5,0.5] → [0,1] then to voxels.
            float u = localPoint.x + 0.5f;
            float v = localPoint.y + 0.5f;
            float w = localPoint.z + 0.5f;
            int cx = Mathf.RoundToInt(u * (_maskW - 1));
            int cy = Mathf.RoundToInt(v * (_maskH - 1));
            int cz = Mathf.RoundToInt(w * (_maskD - 1));

            int r = Mathf.Max(1, radiusVoxels);
            int r2 = r * r;
            bool touched = false;
            for (int z = Mathf.Max(0, cz - r); z <= Mathf.Min(_maskD - 1, cz + r); z++)
            for (int y = Mathf.Max(0, cy - r); y <= Mathf.Min(_maskH - 1, cy + r); y++)
            for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(_maskW - 1, cx + r); x++)
            {
                int dx = x - cx, dy = y - cy, dz = z - cz;
                if (dx*dx + dy*dy + dz*dz <= r2)
                {
                    int idx = z * _maskW * _maskH + y * _maskW + x;
                    if (_maskBytes[idx] != value)
                    {
                        _maskBytes[idx] = value;
                        touched = true;
                    }
                }
            }
            if (touched)
            {
                _maskTexture.SetPixelData(_maskBytes, 0);
                _maskTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                OnMaskChanged?.Invoke();
            }
            return touched;
        }

        public void ClearMask()
        {
            if (_maskBytes == null || _maskTexture == null) return;
            System.Array.Clear(_maskBytes, 0, _maskBytes.Length);
            _maskTexture.SetPixelData(_maskBytes, 0);
            _maskTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            OnMaskChanged?.Invoke();
        }

        /// Paint a 2D disk of voxels in one of the three orthogonal planes,
        /// holding one axis fixed. `axis` is 0 (X-fixed, YZ plane), 1 (Y-fixed,
        /// XZ plane), or 2 (Z-fixed, XY plane — the typical "axial" cut).
        /// Used by the OrthoViewer when the user drag-paints on a 2D slice.
        public bool PaintVoxelDisk(int cx, int cy, int cz, int radiusVoxels, byte value, int axis)
        {
            if (_maskBytes == null || _maskTexture == null) return false;
            int r = Mathf.Max(1, radiusVoxels);
            int r2 = r * r;
            bool touched = false;
            switch (axis)
            {
                case 0:
                    if (cx < 0 || cx >= _maskW) return false;
                    for (int dz = -r; dz <= r; dz++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (dy * dy + dz * dz > r2) continue;
                        int y = cy + dy, z = cz + dz;
                        if (y < 0 || y >= _maskH || z < 0 || z >= _maskD) continue;
                        int idx = z * _maskW * _maskH + y * _maskW + cx;
                        if (_maskBytes[idx] != value) { _maskBytes[idx] = value; touched = true; }
                    }
                    break;
                case 1:
                    if (cy < 0 || cy >= _maskH) return false;
                    for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (dx * dx + dz * dz > r2) continue;
                        int x = cx + dx, z = cz + dz;
                        if (x < 0 || x >= _maskW || z < 0 || z >= _maskD) continue;
                        int idx = z * _maskW * _maskH + cy * _maskW + x;
                        if (_maskBytes[idx] != value) { _maskBytes[idx] = value; touched = true; }
                    }
                    break;
                case 2:
                default:
                    if (cz < 0 || cz >= _maskD) return false;
                    for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (dx * dx + dy * dy > r2) continue;
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || x >= _maskW || y < 0 || y >= _maskH) continue;
                        int idx = cz * _maskW * _maskH + y * _maskW + x;
                        if (_maskBytes[idx] != value) { _maskBytes[idx] = value; touched = true; }
                    }
                    break;
            }
            if (touched)
            {
                _maskTexture.SetPixelData(_maskBytes, 0);
                _maskTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                OnMaskChanged?.Invoke();
            }
            return touched;
        }

        public bool HasMask => _maskTexture != null && _maskBytes != null;
        public (int W, int H, int D) MaskDimensions => (_maskW, _maskH, _maskD);

        /// Anatomical unit vectors in cube-local space (set from the active
        /// ImageSeries — populated for MHA-loaded volumes, zero for PNG previews).
        /// Lets downstream tools report trajectory angles in clinical terms
        /// (medial / cranial / etc.) instead of generic axes.
        public Vector3 AnatomyAnteriorDir { get; private set; }
        public Vector3 AnatomySuperiorDir { get; private set; }
        public Vector3 AnatomyRightDir    { get; private set; }

        /// Physical voxel spacing in mm of the loaded series — needed by tools
        /// that report distances clinically (e.g. screw-to-canal in mm rather
        /// than voxels). Zero on any axis = unknown (PNG previews).
        public Vector3 VoxelSpacingMm { get; private set; }
        public bool HasAnatomy => AnatomyAnteriorDir.sqrMagnitude > 0.5f
                                  && AnatomySuperiorDir.sqrMagnitude > 0.5f
                                  && AnatomyRightDir.sqrMagnitude > 0.5f;

        /// Direct (non-copying) access to the volume's voxel bytes — used by the
        /// OrthoViewer to extract orthogonal 2D slices. Caller must not mutate
        /// the array; it's owned by the VolumeRenderer and freed on Unload.
        public byte[] VolumeBytes => _volumeBytes;
        /// Same for the mask. The C# brush mutates this through PaintAt, so any
        /// reader should re-read it whenever OnMaskChanged fires.
        public byte[] MaskBytes => _maskBytes;

        /// Copy of the mask's voxel bytes — owned by the caller. Used by BrushMaskStore
        /// to persist segmentation paint to disk between sessions.
        public byte[] GetMaskSnapshot()
        {
            if (_maskBytes == null) return null;
            var copy = new byte[_maskBytes.Length];
            System.Buffer.BlockCopy(_maskBytes, 0, copy, 0, _maskBytes.Length);
            return copy;
        }

        /// Restore mask from saved bytes. Dimensions must match the currently loaded
        /// volume — caller should check before invoking. Returns false on size mismatch.
        public bool TryRestoreMask(int w, int h, int d, byte[] bytes)
        {
            if (_maskTexture == null || _maskBytes == null) return false;
            if (w != _maskW || h != _maskH || d != _maskD) return false;
            if (bytes == null || bytes.Length != _maskBytes.Length) return false;
            System.Buffer.BlockCopy(bytes, 0, _maskBytes, 0, bytes.Length);
            _maskTexture.SetPixelData(_maskBytes, 0);
            _maskTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return true;
        }

        public void SetMaskColor(Color c) => _material?.SetColor("_MaskColor", c);

        /// Upload the per-segment colour palette to the shader. The byte value
        /// stored in each mask voxel selects an entry from this array — index 0
        /// is "empty", indices 1..31 are user-defined segment colours. Pass an
        /// array up to length 32; missing entries are zero-filled, which the
        /// shader treats as "fall back to _MaskColor".
        public void SetSegmentPalette(Color[] palette)
        {
            if (_material == null) return;
            var v = new Vector4[32];
            if (palette != null)
            {
                for (int i = 0; i < palette.Length && i < 32; i++)
                    v[i] = new Vector4(palette[i].r, palette[i].g, palette[i].b, palette[i].a);
            }
            _material.SetVectorArray("_SegmentPalette", v);
        }

        /// Per-layer visibility — both default to on. The workstation exposes
        /// these as two toggles on the Volume panel so the user can render
        /// volume-only, segmentation-only, or both at once.
        public void SetShowVolume(bool show) => _material?.SetFloat("_ShowVolume", show ? 1f : 0f);
        public void SetShowSegmentation(bool show) => _material?.SetFloat("_ShowSegmentation", show ? 1f : 0f);

        public void Unload()
        {
            _cube?.SetActive(false);
            DisposeVolumeTexture();
            _series = null;
        }

        // ── UI-bound parameter setters ───────────────────────────────────────

        public void SetThresholdLow(float low) => _material?.SetFloat("_ThresholdLow", Mathf.Clamp01(low));
        public void SetThresholdHigh(float high) => _material?.SetFloat("_ThresholdHigh", Mathf.Clamp01(high));
        public void SetDensityScale(float scale) => _material?.SetFloat("_DensityScale", Mathf.Max(0f, scale));
        public void SetTint(Color tint) => _material?.SetColor("_Tint", tint);
        public void SetStepCount(int steps) => _material?.SetInt("_StepCount", Mathf.Clamp(steps, 16, 512));

        public void SetSegmentEnabled(bool enabled) => _material?.SetFloat("_SegmentEnabled", enabled ? 1f : 0f);
        public void SetSegmentRange(float low, float high) {
            _material?.SetFloat("_SegmentLow", Mathf.Clamp01(low));
            _material?.SetFloat("_SegmentHigh", Mathf.Clamp01(high));
        }
        public void SetSegmentColor(Color c) => _material?.SetColor("_SegmentColor", c);
        public void SetSegmentBoost(float boost) => _material?.SetFloat("_SegmentBoost", Mathf.Max(0f, boost));

        public float ThresholdLow => _material != null ? _material.GetFloat("_ThresholdLow") : 0f;
        public float ThresholdHigh => _material != null ? _material.GetFloat("_ThresholdHigh") : 1f;
        public float DensityScale => _material != null ? _material.GetFloat("_DensityScale") : 1f;
        public Color Tint => _material != null ? _material.GetColor("_Tint") : Color.white;
        public bool SegmentEnabled => _material != null && _material.GetFloat("_SegmentEnabled") > 0.5f;
        public float SegmentLow => _material != null ? _material.GetFloat("_SegmentLow") : 0.4f;
        public float SegmentHigh => _material != null ? _material.GetFloat("_SegmentHigh") : 0.6f;
        public Color SegmentColor => _material != null ? _material.GetColor("_SegmentColor") : new Color(1f, 0.4f, 0.3f);

        void DisposeVolumeTexture()
        {
            if (_volumeTexture != null) Destroy(_volumeTexture);
            _volumeTexture = null;
            _volumeBytes = null;
        }

        void DisposeMaskTexture()
        {
            if (_maskTexture != null) Destroy(_maskTexture);
            _maskTexture = null;
            _maskBytes = null;
        }

        void OnDestroy()
        {
            DisposeVolumeTexture();
            DisposeMaskTexture();
            if (_material != null) Destroy(_material);
        }
    }
}
