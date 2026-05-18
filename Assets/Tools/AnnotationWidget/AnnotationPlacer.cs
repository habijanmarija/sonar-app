using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Host.Tools.AnnotationWidget
{
    /// Placement-mode input controller. When `Active` is true, raycasts the camera through
    /// the mouse position; if the ray hits any collider, fires OnPlacementHit with the
    /// world-space hit point. The widget controller then constructs an Annotation and
    /// adds it to the store.
    ///
    /// Phase D scope: mouse + camera raycast for flat-mode dev. Phase 3/headset adds
    /// controller-ray input by swapping this MonoBehaviour for one that reads XR input.
    [AddComponentMenu("SONAR Host/Tools/Annotation Placer")]
    public sealed class AnnotationPlacer : MonoBehaviour
    {
        public event Action<Vector3> OnPlacementHit;

        public bool Active { get; private set; }

        [SerializeField] float _maxDistanceM = 10f;
        [SerializeField] LayerMask _hitMask = ~0;

        public void Begin() => Active = true;
        public void Cancel() => Active = false;

        void Update()
        {
            if (!Active) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (Camera.main == null) return;

            var ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out var hit, _maxDistanceM, _hitMask))
            {
                Active = false;
                OnPlacementHit?.Invoke(hit.point);
            }
            else
            {
                // No hit — let the user try again. Don't deactivate.
            }
        }
    }
}
