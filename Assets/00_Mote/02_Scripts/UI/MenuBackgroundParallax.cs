using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DoHwa.UI
{
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public sealed class MenuBackgroundParallax : MonoBehaviour
    {
        [SerializeField] private float distance = 12f;
        [SerializeField] private Animator peachAnimator;

        private static readonly Vector2 ArtworkSize = new(1672f, 941f);
        private static readonly int PointerLean = Animator.StringToHash("PointerLean");
        private static readonly int PeachTap = Animator.StringToHash("PeachTap");

        [SerializeField] private Image artwork;

        [SerializeField] private RectTransform layers;
        [SerializeField] private RectTransform stars;
        [SerializeField] private RectTransform islandMotion;
        [SerializeField] private RawImage window;
        [SerializeField] private RawImage[] starImages;
        private RectTransform rect;
        private Camera canvasCamera;
        private Vector2 origin;
        private Vector3 originalScale;
        private Vector2 pointerOffset;
        private float tap;
        private float windowResponse;

        private void Awake()
        {
            rect = artwork.rectTransform;
            canvasCamera = artwork.canvas.worldCamera;
            origin = rect.anchoredPosition;
            originalScale = rect.localScale;
        }

        private void OnEnable()
        {
            artwork.enabled = false;
            layers.gameObject.SetActive(true);
            islandMotion.gameObject.SetActive(true);
            rect.localScale = originalScale * 1.045f;
            UpdateMotion(Vector2.zero, Vector2.zero, false, false, 0f);
        }

        private void OnDisable()
        {
            layers.gameObject.SetActive(false);
            artwork.enabled = true;
            rect.anchoredPosition = origin;
            rect.localScale = originalScale;
        }

        private void LateUpdate()
        {
            Vector2 target = Vector2.zero;
            Vector2 artworkPosition = Vector2.zero;
            bool pressed = false;
            bool hasPointer = Application.isFocused && ReadPointer(out target, out pressed);
            if (hasPointer)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(layers, target, canvasCamera, out Vector2 local))
                {
                    Rect bounds = layers.rect;
                    artworkPosition = new Vector2(
                        (local.x - bounds.xMin) / Mathf.Max(1f, bounds.width) * ArtworkSize.x,
                        (bounds.yMax - local.y) / Mathf.Max(1f, bounds.height) * ArtworkSize.y);
                }
                target = new Vector2(
                    Mathf.Clamp(target.x / Screen.width * 2f - 1f, -1f, 1f),
                    Mathf.Clamp(target.y / Screen.height * 2f - 1f, -1f, 1f));
            }

            UpdateMotion(target, artworkPosition, hasPointer, pressed, Time.unscaledDeltaTime);
        }

        private void UpdateMotion(Vector2 target, Vector2 pointer, bool hasPointer, bool pressed, float deltaTime)
        {
            float smoothing = 1f - Mathf.Exp(-5f * deltaTime);
            pointerOffset = Vector2.Lerp(pointerOffset, target, smoothing);
            float scale = rect.rect.height / ArtworkSize.y;
            float time = Time.unscaledTime;
            rect.anchoredPosition = origin + pointerOffset * (distance * 0.55f * scale);
            stars.anchoredPosition = pointerOffset * (distance * 0.32f * scale);
            islandMotion.anchoredPosition = (pointerOffset * (distance * 1.45f) +
                new Vector2(1.7f * Mathf.Sin(time * 0.64f), 5f * Mathf.Sin(time * 0.92f))) * scale;

            Vector2 islandPointer = pointer - new Vector2(islandMotion.anchoredPosition.x, -islandMotion.anchoredPosition.y) /
                Mathf.Max(0.001f, scale);
            Vector2 treeCenter = new(976, 223);
            float treeProximity = hasPointer ? Proximity(islandPointer, treeCenter, 320f) : 0f;
            float houseProximity = hasPointer ? Proximity(islandPointer, new Vector2(625, 423), 235f) : 0f;
            float pointerLean = Mathf.Clamp((islandPointer.x - treeCenter.x) / 160f, -1f, 1f) * treeProximity;
            peachAnimator.SetFloat(PointerLean, pointerLean, 0.15f, deltaTime);
            if (pressed && hasPointer && Proximity(islandPointer, new Vector2(825, 398), 425f) > 0f)
            {
                tap = 1f;
                peachAnimator.SetTrigger(PeachTap);
            }

            tap *= Mathf.Exp(-2.3f * deltaTime);

            windowResponse = Mathf.Lerp(windowResponse, houseProximity, smoothing);
            float flicker = 0.28f + 0.09f * Mathf.Sin(time * 2.8f) + 0.05f * Mathf.Sin(time * 7.1f + 0.8f);
            window.color = new Color(1f, 1f, 1f, Mathf.Clamp01(flicker + windowResponse * 0.38f + tap * 0.2f));

            Vector2 starPointer = pointer - new Vector2(stars.anchoredPosition.x, -stars.anchoredPosition.y) /
                Mathf.Max(0.001f, scale);
            for (int index = 0; index < starImages.Length; index++)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(time * (0.96f + index % 7 * 0.17f) + index * 2.39996f);
                float twinkle = wave * wave * wave * wave;
                RawImage sparkle = starImages[index];
                var starRect = sparkle.rectTransform;
                Vector2 center = (starRect.anchorMin + starRect.anchorMax) * 0.5f;
                Vector2 position = new(center.x * ArtworkSize.x, (1f - center.y) * ArtworkSize.y);
                float proximity = hasPointer ? Proximity(starPointer, position, 145f) : 0f;
                sparkle.color = new Color(1f, 1f, 1f, Mathf.Clamp01(twinkle * 0.88f + proximity * 0.32f));
                sparkle.rectTransform.localScale = Vector3.one * (0.7f + twinkle * 0.45f + proximity * 0.16f);
            }
        }

        private static bool ReadPointer(out Vector2 position, out bool pressed)
        {
            if (Touchscreen.current is { } touchscreen && touchscreen.primaryTouch.press.isPressed)
            {
                position = touchscreen.primaryTouch.position.ReadValue();
                pressed = touchscreen.primaryTouch.press.wasPressedThisFrame;
                return true;
            }

            if (Mouse.current is { } mouse)
            {
                position = mouse.position.ReadValue();
                pressed = mouse.leftButton.wasPressedThisFrame;
                return true;
            }

            position = Vector2.zero;
            pressed = false;
            return false;
        }

        private static float Proximity(Vector2 pointer, Vector2 focalPoint, float radius)
        {
            return Mathf.Clamp01(1f - Vector2.Distance(pointer, focalPoint) / radius);
        }
    }
}
