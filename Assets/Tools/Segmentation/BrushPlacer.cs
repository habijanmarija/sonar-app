using System;
using Host.Visualization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Host.Tools.Segmentation
{
    /// Brush-mode input controller. When `Active`, raycasts camera→mouse on each
    /// LMB-down (and during drag) and asks the bound VolumeRenderer to paint a
    /// sphere of voxels into its mask at the hit point.
    [AddComponentMenu("SONAR Host/Tools/Brush Placer")]
    public sealed class BrushPlacer : MonoBehaviour
    {
        public event Action<Vector3> OnPaintStroke;

        public bool Active { get; private set; }
        public bool Erasing { get; set; }
        /// Byte value written into the mask when painting — the segment index
        /// (1..31) in the shader's _SegmentPalette. 255 is legacy single-segment
        /// mode (renderer falls back to _MaskColor).
        public byte ActiveValue { get; set; } = 255;
        /// Current brush radius in voxels, exposed so the OrthoViewer can paint
        /// 2D disks of the same size when the user drags on a slice pane.
        public int Radius => _radiusVoxels;

        [SerializeField] VolumeRenderer _renderer;
        [SerializeField] int _radiusVoxels = 4;
        [SerializeField] float _maxRayDistance = 10f;
        // Tube-paint depth: extend each stroke into the cube along the camera ray
        // so the paint sits as 3D voxels, not a flat 2D patch on the front face.
        // Steps × stepSize ≈ how deep the tube reaches in world metres.
        [SerializeField] int _depthSteps = 30;
        [SerializeField] float _depthStepMetres = 0.004f;

        public void Bind(VolumeRenderer renderer) => _renderer = renderer;
        public void SetRadius(int voxels) => _radiusVoxels = Mathf.Max(1, voxels);
        public void SetErasing(bool erasing) => Erasing = erasing;
        public void Begin() { Active = true; Debug.Log("[BrushPlacer] Begin — Active=true"); }
        public void End()   { Active = false; Debug.Log("[BrushPlacer] End — Active=false"); }

        // Log only when the LMB transitions from up→down, so we don't spam the
        // console while idle. One line per click tells us which gate the input
        // is failing at: no renderer, no volume, no camera, no raycast hit, or
        // wrong-collider hit.
        bool _wasPressed;

        void Update()
        {
            if (!Active) return;
            var mouse = Mouse.current;
            bool pressed = mouse != null && mouse.leftButton.isPressed;
            bool edge = pressed && !_wasPressed;
            _wasPressed = pressed;

            if (_renderer == null)            { if (edge) Debug.Log("[BrushPlacer] click ignored: _renderer is null"); return; }
            if (!_renderer.HasVolume)         { if (edge) Debug.Log("[BrushPlacer] click ignored: _renderer.HasVolume = false"); return; }
            if (Camera.main == null)          { if (edge) Debug.Log("[BrushPlacer] click ignored: Camera.main is null"); return; }
            if (!pressed) return;

            var screenPos = mouse.position.ReadValue();
            var ray = Camera.main.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, _maxRayDistance))
            {
                if (edge) Debug.Log($"[BrushPlacer] click at {screenPos} → raycast missed (maxDist {_maxRayDistance:0.00})");
                return;
            }
            var t = _renderer.VolumeCubeTransform;
            // Accept hits on the cube OR any of its segment-mesh children — the
            // brush is happy with any world point inside the volume box.
            if (t == null || (hit.collider.transform != t && !hit.collider.transform.IsChildOf(t)))
            {
                if (edge) Debug.Log($"[BrushPlacer] click hit '{hit.collider.name}' (not the volume cube — expected '{(t != null ? t.name : "null")}')");
                return;
            }

            // Walk into the cube along the camera ray, painting a sphere at each step,
            // until we exit the cube's local AABB. Result is a tube of voxels — visible
            // from any orbit angle, so the paint reads as 3D rather than a flat patch.
            var dir = ray.direction.normalized;
            byte value = Erasing ? (byte)0 : ActiveValue;
            bool anyPainted = false;
            int painted = 0;
            for (int s = 0; s < _depthSteps; s++)
            {
                var point = hit.point + dir * (s * _depthStepMetres);
                var local = t.InverseTransformPoint(point);
                if (Mathf.Abs(local.x) > 0.5f || Mathf.Abs(local.y) > 0.5f || Mathf.Abs(local.z) > 0.5f) break;
                if (_renderer.PaintAt(point, _radiusVoxels, value)) { anyPainted = true; painted++; }
            }
            if (anyPainted)
            {
                if (edge) Debug.Log($"[BrushPlacer] painted at {hit.point} → {painted} step(s) deep (erase={Erasing}, radius={_radiusVoxels})");
                OnPaintStroke?.Invoke(hit.point);
            }
        }
    }
}
