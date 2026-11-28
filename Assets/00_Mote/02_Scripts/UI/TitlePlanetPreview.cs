using System;
using DoHwa.Planet;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DoHwa.UI
{
    public sealed class TitlePlanetPreview : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IScrollHandler, ICanvasRaycastFilter
    {
        [SerializeField] private GameObject stage;
        [SerializeField] private PlanetView surface;
        [SerializeField] private Camera portraitCamera;
        [SerializeField] private Transform ornaments;
        [SerializeField] private PlanetDecorationCatalog catalog;
        [SerializeField, Tooltip("초당 자전 각도")] private float spinSpeed = 7f;
        [SerializeField, Tooltip("미리보기 너비만큼 드래그했을 때의 회전 각도")] private float dragDegrees = 150f;

        [SerializeField] private MenuSkySwipe background;
        [SerializeField, Range(0f, 1f)] private float backgroundDragRatio = .35f;

        [SerializeField, Range(.5f, 1f)] private float minimumZoom = .72f;
        [SerializeField, Range(1f, 2f)] private float maximumZoom = 1.55f;

        private int? pointer, secondPointer;
        private Vector2 primaryPosition, secondaryPosition;
        private bool resumeDrag;
        private Vector3 defaultScale;
        private float zoom = 1f, targetZoom = 1f;

        private void Awake() => defaultScale = transform.localScale;
        private int generation;
        private float lastDrag;
        private Vector2 velocity;
        private Vector2 spinDirection = Vector2.up;

        private async void OnEnable()
        {
            int request = ++generation;
            stage.SetActive(true);
            string owner = PlanetApi.Player;
            if (string.IsNullOrEmpty(owner)) return;
            try
            {
                PlanetData data = await PlanetApi.Read(owner);
                if (!this || request != generation || PlanetApi.Player != owner || data.owner != owner) return;
                Physics.SyncTransforms();
                foreach (PlanetItem item in data.items)
                    catalog.Draw(item, surface, ornaments);
            }
            catch (Exception error)
            {
                if (this && request == generation)
                    Debug.LogWarning($"행성 미리보기를 불러오지 못했습니다. {error.Message}", this);
            }
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            zoom = Mathf.Lerp(zoom, targetZoom, 1f - Mathf.Exp(-18f * dt));
            transform.localScale = defaultScale * zoom;
            if (pointer.HasValue) return;
            var cruise = spinDirection * Mathf.Abs(spinSpeed);
            float decay = Mathf.Exp(-5f * dt);
            Turn(cruise * dt + (velocity - cruise) * ((1f - decay) / 5f));
            velocity = cruise + (velocity - cruise) * decay;
        }

        private void Turn(Vector2 degrees)
        {
            Vector3 axis = portraitCamera.transform.right * degrees.x + portraitCamera.transform.up * degrees.y;
            float angle = axis.magnitude;
            if (angle > 0f)
                surface.transform.rotation = Quaternion.AngleAxis(angle, axis / angle) * surface.transform.rotation;
        }

        public bool IsRaycastLocationValid(Vector2 position, Camera camera)
        {
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, position, camera, out var point)) return false;
            return Vector2.Distance(point, rect.rect.center) <= rect.rect.width * .43f;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || SystemUI.PopupCapturesInput) return;
            if (pointer.HasValue)
            {
                if (pointer == e.pointerId || secondPointer.HasValue) return;
                secondPointer = e.pointerId;
                secondaryPosition = e.position;
                velocity = Vector2.zero;
                background.EndDrag(pointer.Value);
                return;
            }
            primaryPosition = e.position;
            pointer = e.pointerId;
            background.BeginDrag(e, backgroundDragRatio);
            velocity = Vector2.zero;
        }

        public void OnDrag(PointerEventData e)
        {
            if (SystemUI.PopupCapturesInput || (pointer != e.pointerId && secondPointer != e.pointerId)) return;
            float oldSpan = Vector2.Distance(primaryPosition, secondaryPosition);
            if (pointer == e.pointerId) primaryPosition = e.position;
            else secondaryPosition = e.position;
            if (secondPointer.HasValue)
            {
                float span = Vector2.Distance(primaryPosition, secondaryPosition);
                if (oldSpan > 1f && span > 1f) ApplyZoom(span / oldSpan);
                return;
            }
            if (resumeDrag)
            {
                background.BeginDrag(e, backgroundDragRatio);
                resumeDrag = false;
            }
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var next) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position - e.delta, e.pressEventCamera, out var previous)) return;
            Vector2 distance = next - previous;
            Vector2 step = new Vector2(distance.y, -distance.x) * (dragDegrees / Mathf.Max(rect.rect.width, 1f));
            if (step.sqrMagnitude < 0.000001f) return;
            spinDirection = step.normalized;
            Turn(step);
            background.OnDrag(e);
            lastDrag = Time.unscaledTime;
            velocity = Vector2.ClampMagnitude(step / Mathf.Max(.008f, Time.unscaledDeltaTime), 180f);
        }

        private void ApplyZoom(float factor) =>
            targetZoom = Mathf.Clamp(targetZoom * factor, minimumZoom, maximumZoom);

        public void OnScroll(PointerEventData e)
        {
            if (!pointer.HasValue && !SystemUI.PopupCapturesInput)
                ApplyZoom(Mathf.Exp(Mathf.Clamp(e.scrollDelta.y, -12f, 12f) * .025f));
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (secondPointer.HasValue)
            {
                if (e.pointerId != pointer && e.pointerId != secondPointer) return;
                if (e.pointerId == pointer)
                {
                    pointer = secondPointer;
                    primaryPosition = secondaryPosition;
                }
                secondPointer = null;
                resumeDrag = true;
                return;
            }
            if (pointer != e.pointerId) return;
            if (Time.unscaledTime - lastDrag > .08f) velocity = Vector2.zero;
            resumeDrag = false;
            background.OnPointerUp(e);
            pointer = null;
        }

        private void OnDisable()
        {
            ++generation;
            secondPointer = null;
            resumeDrag = false;
            if (pointer.HasValue && background) background.EndDrag(pointer.Value);
            pointer = null;
            velocity = Vector2.zero;
            if (stage) stage.SetActive(false);
            if (ornaments)
                foreach (Transform child in ornaments) Destroy(child.gameObject);
        }
    }
}
