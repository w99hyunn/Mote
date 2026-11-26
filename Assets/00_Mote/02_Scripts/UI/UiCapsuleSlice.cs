using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoHwa.UI
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Image))]
    public sealed class UiCapsuleSlice : UIBehaviour
    {
        private Image surface;

        protected override void OnEnable()
        {
            base.OnEnable();
            TryGetComponent<Image>(out surface);
            FitBorders();
        }

        protected override void OnRectTransformDimensionsChange() => FitBorders();

        protected override void OnCanvasHierarchyChanged() => FitBorders();

        private void FitBorders()
        {
            // Rect and canvas callbacks can arrive before OnEnable or while detaching.
            if (!surface || !surface.canvas || !surface.sprite)
                return;

            Rect rect = surface.rectTransform.rect;
            float diameter = Mathf.Min(rect.width, rect.height);
            if (diameter <= 0f)
                return;

            Sprite sprite = surface.sprite;
            float multiplier = sprite.rect.height * surface.canvas.referencePixelsPerUnit
                               / (diameter * sprite.pixelsPerUnit);
            if (!Mathf.Approximately(surface.pixelsPerUnitMultiplier, multiplier))
                surface.pixelsPerUnitMultiplier = multiplier;
        }
    }
}
