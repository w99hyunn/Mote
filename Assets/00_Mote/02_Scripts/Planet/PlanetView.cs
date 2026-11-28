using System;
using DoHwa.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DoHwa.Planet
{
    public sealed class PlanetView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IPointerClickHandler, IScrollHandler
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

        private Vector2 velocity;
        private int? pointer, secondPointer;
        private Vector2 firstPosition, secondPosition;
        private bool grabbing, usedPinch;
        private float lastDrag, zoom, targetZoom;
        private float overviewRatio = .5f;
        [SerializeField, Range(1f, 2f)] private float maximumDetailScale = 1.3f;
        private Vector3 origin;

        private void Awake() => origin = transform.localPosition;

        public void Face(Vector3 direction)
        {
            if (direction.sqrMagnitude < Mathf.Epsilon) return;
            Vector3 facing = transform.TransformDirection(direction.normalized);
            transform.rotation = Quaternion.FromToRotation(facing, -viewCamera.transform.forward) * transform.rotation;
            velocity = Vector2.zero;
        }

        private void Turn(Vector2 delta)
        {
            Vector3 axis = viewCamera.transform.right * delta.x + viewCamera.transform.up * delta.y;
            float degrees = axis.magnitude;
            if (degrees > 0f)
                transform.rotation = Quaternion.AngleAxis(degrees, axis / degrees) * transform.rotation;
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
            zoom = Mathf.Lerp(zoom, targetZoom, 1f - Mathf.Exp(-8f * dt));
            float distance = Vector3.Dot(transform.position - viewCamera.transform.position, viewCamera.transform.forward);
            float halfHeight = viewCamera.orthographic ? viewCamera.orthographicSize :
                distance * Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * .5f);
            float width = halfHeight * viewCamera.aspect;
            float detailRadius = Mathf.Min(halfHeight * .88f, width * 1.63f);
            overviewRatio = Mathf.Min(halfHeight * .56f, width * .81f) / detailRadius;
            float radius = detailRadius * Mathf.Pow(overviewRatio, zoom);
            transform.localScale = Vector3.one * radius;
            transform.localPosition = origin + Vector3.down * (1f - Mathf.Clamp01(zoom)) * halfHeight * .23f;
        }

        private bool TrySurface(PointerEventData e, out Vector3 direction)
        {
            var hit = e.pointerCurrentRaycast;
            direction = transform.InverseTransformPoint(hit.worldPosition).normalized;
            return hit.gameObject == gameObject && !SystemUI.PopupCapturesInput;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || SystemUI.PopupCapturesInput) return;
            if (pointer.HasValue)
            {
                if (pointer == e.pointerId || secondPointer.HasValue) return;
                secondPointer = e.pointerId;
                secondPosition = e.position;
                usedPinch = true;
                if (grabbing) EndGrab?.Invoke(false);
                grabbing = false;
            }
            else
            {
                pointer = e.pointerId;
                firstPosition = e.position;
                usedPinch = false;
            }
            velocity = Vector2.zero;
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (pointer != e.pointerId || usedPinch) return;
            e.eligibleForClick = false;
            var start = e.pointerPressRaycast;
            grabbing = start.gameObject == gameObject && TryGrab?.Invoke(transform.InverseTransformPoint(start.worldPosition).normalized) == true;
        }

        public void OnDrag(PointerEventData e)
        {
            if (SystemUI.PopupCapturesInput || (pointer != e.pointerId && secondPointer != e.pointerId)) return;
            float previousSpan = Vector2.Distance(firstPosition, secondPosition);
            if (pointer == e.pointerId) firstPosition = e.position;
            else secondPosition = e.position;
            if (secondPointer.HasValue)
            {
                float span = Vector2.Distance(firstPosition, secondPosition);
                if (previousSpan > 1f && span > 1f) ScaleBy(span / previousSpan);
                return;
            }
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
            if (pointer != e.pointerId && secondPointer != e.pointerId) return;
            if (usedPinch) e.eligibleForClick = false;
            if (secondPointer.HasValue)
            {
                if (pointer == e.pointerId)
                {
                    pointer = secondPointer;
                    firstPosition = secondPosition;
                }
                secondPointer = null;
                return;
            }
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
            if (e.button != PointerEventData.InputButton.Left || !e.eligibleForClick || e.dragging || !TrySurface(e, out var direction)) return;
            SurfaceTapped?.Invoke(direction);
        }

        private void ScaleBy(float factor)
        {
            float logRatio = Mathf.Log(overviewRatio);
            targetZoom = Mathf.Clamp(targetZoom + Mathf.Log(factor) / logRatio,
                Mathf.Log(maximumDetailScale) / logRatio, 1f);
        }

        public void OnScroll(PointerEventData e)
        {
            if (!pointer.HasValue && !SystemUI.PopupCapturesInput)
                ScaleBy(Mathf.Exp(Mathf.Clamp(e.scrollDelta.y, -12f, 12f) * .025f));
        }

        public void ToggleDistance() => targetZoom = targetZoom < .5f ? 1f : 0f;

        private void OnDisable()
        {
            if (grabbing) EndGrab?.Invoke(false);
            secondPointer = null;
            pointer = null;
            grabbing = false;
            velocity = Vector2.zero;
        }
    }
}
