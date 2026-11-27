using UnityEngine;

namespace DoHwa.UI
{
    public sealed class TopSafeArea : MonoBehaviour
    {
        [SerializeField] private RectTransform[] content;
        private Vector2[] originalAnchors;
        private Canvas rootCanvas;
        private float appliedTop = -1f;

        private void Awake()
        {
            rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
            originalAnchors = new Vector2[content.Length];
            for (int i = 0; i < content.Length; i++)
                originalAnchors[i] = new Vector2(content[i].anchorMin.y, content[i].anchorMax.y);
        }

        private void OnEnable() => Apply();
        private void LateUpdate() => Apply();

        private void Apply()
        {
            Rect viewport = rootCanvas.pixelRect;
            if (viewport.height <= 0f) return;
            float top = Mathf.Clamp01((Screen.safeArea.yMax - viewport.yMin) / viewport.height);
            if (Mathf.Approximately(top, appliedTop)) return;
            appliedTop = top;
            for (int i = 0; i < content.Length; i++)
            {
                RectTransform item = content[i];
                item.anchorMin = new Vector2(item.anchorMin.x, originalAnchors[i].x * top);
                item.anchorMax = new Vector2(item.anchorMax.x, originalAnchors[i].y * top);
            }
        }
    }
}
