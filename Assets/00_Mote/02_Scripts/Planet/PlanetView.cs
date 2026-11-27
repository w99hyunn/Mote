using System;
using DoHwa.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DoHwa.Planet
{
    public sealed class PlanetView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IPointerClickHandler
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private MeshCollider terrain;
        public Vector3 SurfacePosition(Vector3 direction)
        {
            Vector3 outside = transform.TransformPoint(direction * 1.2f);
            var ray = new Ray(outside, transform.position - outside);
            if (terrain.Raycast(ray, out var hit, Vector3.Distance(outside, transform.position)))
                return transform.InverseTransformPoint(hit.point);
            return direction;
        }
        public event Action<Vector3> SurfaceTapped;
        public event Func<Vector3, bool> TryGrab;
        public event Action<Vector3> MoveGrab;
        public event Action<bool> EndGrab;

        private Vector2 angles, velocity;
        private int? pointer;
        private bool grabbing, overview;
        private float lastDrag, zoom;
        private Vector3 origin;

        private void Awake() => origin = transform.localPosition;

        public void Face(Vector3 direction)
        {
            angles.y = Mathf.Atan2(direction.x, -direction.z) * Mathf.Rad2Deg;
            angles.x = -Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            velocity = Vector2.zero;
            ApplyRotation();
        }

        private void ApplyRotation() => transform.localRotation =
            Quaternion.AngleAxis(angles.x, Vector3.right) * Quaternion.AngleAxis(angles.y, Vector3.up);

        private void Turn(Vector2 delta)
        {
            angles.x = Mathf.Clamp(angles.x + delta.x, -89f, 89f);
            angles.y = Mathf.Repeat(angles.y + delta.y, 360f);
            ApplyRotation();
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (SystemUI.PopupCapturesInput) velocity = Vector2.zero;
            if (!pointer.HasValue && velocity.sqrMagnitude > .01f)
            {
                float decay = Mathf.Exp(-6f * dt);
                Turn(velocity * ((1f - decay) / 6f));
                velocity *= decay;
            }
            zoom = Mathf.Lerp(zoom, overview ? 1f : 0f, 1f - Mathf.Exp(-8f * dt));
            float distance = Vector3.Dot(transform.position - viewCamera.transform.position, viewCamera.transform.forward);
            float halfHeight = viewCamera.orthographic ? viewCamera.orthographicSize :
                distance * Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * .5f);
            float width = halfHeight * viewCamera.aspect;
            float radius = Mathf.Lerp(Mathf.Min(halfHeight * .88f, width * 1.63f), Mathf.Min(halfHeight * .56f, width * .81f), zoom);
            transform.localScale = Vector3.one * radius;
            transform.localPosition = origin + Vector3.down * (1f - zoom) * halfHeight * .23f;
        }

        private bool TrySurface(PointerEventData e, out Vector3 direction)
        {
            var hit = e.pointerCurrentRaycast;
            direction = transform.InverseTransformPoint(hit.worldPosition).normalized;
            return hit.gameObject == gameObject && !SystemUI.PopupCapturesInput;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (pointer.HasValue || e.button != PointerEventData.InputButton.Left || SystemUI.PopupCapturesInput) return;
            pointer = e.pointerId;
            velocity = Vector2.zero;
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (pointer != e.pointerId) return;
            e.eligibleForClick = false;
            var start = e.pointerPressRaycast;
            grabbing = start.gameObject == gameObject && TryGrab?.Invoke(transform.InverseTransformPoint(start.worldPosition).normalized) == true;
        }

        public void OnDrag(PointerEventData e)
        {
            if (pointer != e.pointerId || SystemUI.PopupCapturesInput) return;
            if (grabbing)
            {
                if (TrySurface(e, out var direction)) MoveGrab?.Invoke(direction);
                return;
            }
            float step = Mathf.Lerp(88f, 160f, zoom) / Mathf.Max(1, Mathf.Min(Screen.width, Screen.height));
            var delta = new Vector2(e.delta.y, -e.delta.x) * step;
            Turn(delta);
            lastDrag = Time.unscaledTime;
            velocity = Vector2.ClampMagnitude(delta / Mathf.Max(.008f, Time.unscaledDeltaTime), 160f);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (pointer != e.pointerId) return;
            if (grabbing)
            {
                bool valid = TrySurface(e, out var direction);
                if (valid) MoveGrab?.Invoke(direction);
                EndGrab?.Invoke(valid);
            }
            if (Time.unscaledTime - lastDrag > .08f) velocity = Vector2.zero;
            grabbing = false;
            pointer = null;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || e.dragging || !TrySurface(e, out var direction)) return;
            SurfaceTapped?.Invoke(direction);
        }

        public void ToggleDistance() => overview = !overview;

        private void OnDisable()
        {
            if (grabbing) EndGrab?.Invoke(false);
            pointer = null;
            grabbing = false;
            velocity = Vector2.zero;
        }
    }
}
