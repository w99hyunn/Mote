using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoHwa.UI
{
    public sealed class MenuSkySwipe : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RawImage sky;
        [SerializeField] private RectTransform starLayer;
        [SerializeField, Range(.01f, .1f)] private float travel = .035f;

        private float dragRatio;
        private int? pointer;
        private Vector2 start, startOffset, target, offset, starOrigin;
        private Rect initialUv;

        private void Awake()
        {
            initialUv = sky.uvRect;
            starOrigin = starLayer.anchoredPosition;
        }

        public void OnPointerDown(PointerEventData e) => BeginDrag(e, 1f);

        public void BeginDrag(PointerEventData e, float ratio)
        {
            if (pointer.HasValue || e.button != PointerEventData.InputButton.Left || SystemUI.PopupCapturesInput) return;
            pointer = e.pointerId;
            dragRatio = ratio;
            start = e.position;
            startOffset = offset;
            target = offset;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointer || SystemUI.PopupCapturesInput) return;
            float range = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) * .45f);
            Vector2 drag = Vector2.ClampMagnitude((e.position - start) / range, 1f);
            target = Vector2.ClampMagnitude(startOffset + drag * dragRatio, 1f);
        }

        public void OnPointerUp(PointerEventData e) => EndDrag(e.pointerId);

        public void EndDrag(int pointerId)
        {
            if (pointerId != pointer) return;
            pointer = null;
            target = Vector2.zero;
        }

        private void LateUpdate()
        {
            offset = Vector2.Lerp(offset, target, 1f - Mathf.Exp(-(pointer.HasValue ? 14f : 5f) * Time.unscaledDeltaTime));
            Vector2 margin = initialUv.size * travel;
            Rect uv = initialUv;
            uv.size -= 2f * margin;
            uv.center = initialUv.center - Vector2.Scale(offset, margin);
            sky.uvRect = uv;
            starLayer.anchoredPosition = starOrigin + Vector2.Scale(offset, sky.rectTransform.rect.size) * (travel * 1.4f);
        }

        private void OnDisable()
        {
            pointer = null;
            target = offset = Vector2.zero;
            if (sky) sky.uvRect = initialUv;
            if (starLayer) starLayer.anchoredPosition = starOrigin;
        }
    }
}
