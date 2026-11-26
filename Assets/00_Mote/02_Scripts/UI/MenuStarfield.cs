using UnityEngine;
using UnityEngine.UI;

namespace DoHwa.UI
{
    public sealed class MenuStarfield : MonoBehaviour
    {
        [SerializeField] private RawImage[] stars;

        private void Update()
        {
            float time = Time.unscaledTime;
            for (int i = 0; i < stars.Length; i++)
            {
                float wave = .5f + .5f * Mathf.Sin(time * (.96f + i % 7 * .17f) + i * 2.39996f);
                float glow = wave * wave * wave * wave;
                RawImage star = stars[i];
                star.canvasRenderer.SetAlpha(.12f + .88f * glow);
                star.rectTransform.localScale = Vector3.one * (.7f + .45f * glow);
            }
        }
    }
}
