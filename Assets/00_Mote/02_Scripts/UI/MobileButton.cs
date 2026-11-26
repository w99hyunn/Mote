using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoHwa.UI
{
    public sealed class MobileButton : Button
    {
        // Full-screen tap targets can animate an emblem while their backdrop stays still.
        [SerializeField] private RectTransform feedbackVisual;
        private RectTransform visual;
        private Vector3 restingScale;
        private Vector2 restingPosition;
        private Vector4 restingPadding;
        private Coroutine motion;
        private int? pointer;
        private float scale = 1f;
        private bool clicked;

        public override void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !IsInteractable() || pointer.HasValue) return;
            base.OnPointerDown(e);
            pointer = e.pointerId;
            BeginMotion();
        }

        public override void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            base.OnPointerUp(e);
            pointer = null;
        }

        public override void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && IsActive() && IsInteractable())
            {
                clicked = true;
                base.OnPointerClick(e);
            }
        }

        public override void OnSubmit(BaseEventData e)
        {
            if (!IsActive() || !IsInteractable()) return;
            BeginMotion();
            clicked = true;
            base.OnSubmit(e);
        }

        private void BeginMotion()
        {
            if (motion != null)
                StopCoroutine(motion);
            else
            {
                visual = feedbackVisual ? feedbackVisual : (RectTransform)transform;
                restingScale = visual.localScale;
                restingPosition = visual.anchoredPosition;
                if (targetGraphic) restingPadding = targetGraphic.raycastPadding;
            }
            clicked = false;
            motion = StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            yield return ScaleBetween(scale, .92f, .065f);
            while (pointer.HasValue) yield return null;
            if (clicked)
                yield return ScaleBetween(.92f, 1.055f, .095f);
            yield return ScaleBetween(scale, 1f, .09f);
            motion = null;
        }

        private IEnumerator ScaleBetween(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetScale(Mathf.SmoothStep(from, to, t / seconds));
                yield return null;
            }
            SetScale(to);
        }

        private void SetScale(float factor)
        {
            scale = factor;
            visual.localScale = restingScale * factor;
            visual.anchoredPosition = restingPosition +
                Vector2.Scale(visual.rect.center, restingScale) * (1f - factor);
            if (targetGraphic && targetGraphic.rectTransform == visual)
            {
                var inset = visual.rect.size * (Mathf.Max(1f / factor, 1f) - 1f) * .5f;
                targetGraphic.raycastPadding = restingPadding - new Vector4(inset.x, inset.y, inset.x, inset.y);
            }
        }

        protected override void OnDisable()
        {
            if (motion != null)
            {
                StopCoroutine(motion);
                if (visual) SetScale(1f);
                motion = null;
            }
            clicked = false;
            pointer = null;
            base.OnDisable();
        }
    }
}
