using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mote.EditorTools
{
    internal enum IconLayer { Full, Background, Foreground }

    // Sample the original file, independent of the source texture's import size or readability.
    internal sealed class IconArtwork
    {
        private readonly Color32[] pixels;
        private readonly int width, height;
        private readonly float adaptiveScale;

        public IconArtwork(Texture2D source)
        {
            string path = AssetDatabase.GetAssetPath(source);
            if (!File.Exists(path) || !string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("프로젝트의 PNG 원본을 선택하세요.");
            var original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!original.LoadImage(File.ReadAllBytes(path))) throw new IOException("PNG를 읽을 수 없습니다.");
                width = original.width;
                height = original.height;
                pixels = original.GetPixels32();
            }
            finally { UnityEngine.Object.DestroyImmediate(original); }

            // Remove the nearly white paper for the adaptive foreground and retain the painted edges.
            // Store premultiplied RGB so area filtering cannot pull white fringes into the symbol.
            bool hasTransparency = Array.Exists(pixels, pixel => pixel.a == 0);
            float radiusSquared = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                float key = hasTransparency ? 1f : Mathf.Clamp01((250 - Mathf.Min(p.r, Mathf.Min(p.g, p.b))) / 15f);
                float alpha = key * p.a / 255f;
                pixels[i] = new Color32(Premultiply(p.r, key, p.a), Premultiply(p.g, key, p.a),
                    Premultiply(p.b, key, p.a), (byte)Mathf.RoundToInt(alpha * 255));
                if (alpha < .1f) continue;
                float x = i % width + .5f - width * .5f;
                float y = i / width + .5f - height * .5f;
                radiusSquared = Mathf.Max(radiusSquared, x * x + y * y);
            }
            if (radiusSquared == 0) throw new ArgumentException("원본에서 아이콘 심볼을 찾을 수 없습니다.");
            adaptiveScale = (65f / 108f) / (2f * Mathf.Sqrt(radiusSquared));
        }

        private static byte Premultiply(byte channel, float key, byte alpha) =>
            (byte)Mathf.RoundToInt(Mathf.Max(0, channel - (1f - key) * 255f) * alpha / 255f);

        internal const int MinPadding = -10;
        internal const int MaxPadding = 30;

        public Texture2D Render(int size, IconLayer layer, bool launcherPreview = false, int paddingPercent = 0)
        {
            bool foreground = layer == IconLayer.Foreground;
            var output = new Color32[size * size];
            var white = new Color32(255, 255, 255, 255);
            if (layer == IconLayer.Background) Array.Fill(output, white);
            else
            {
                float scale = foreground || launcherPreview ? adaptiveScale : 1f / Mathf.Max(width, height);
                scale *= 1f - .02f * Mathf.Clamp(paddingPercent, MinPadding, MaxPadding);
                float step = 1f / (size * scale * (launcherPreview ? 1.5f : 1f));
                float left = (width - size * step) * .5f, bottom = (height - size * step) * .5f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        output[y * size + x] = Sample(left + x * step, bottom + y * step, step, foreground);
            }
            var texture = new Texture2D(size, size, foreground ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
            texture.SetPixels32(output);
            texture.Apply();
            return texture;
        }

        // Exact area average for each output texel, including fractional pixels at its boundary.
        private Color32 Sample(float left, float bottom, float step, bool transparent)
        {
            float right = left + step, top = bottom + step;
            int xEnd = Mathf.Min(width, Mathf.CeilToInt(right)), yEnd = Mathf.Min(height, Mathf.CeilToInt(top));
            int xStart = Mathf.Max(0, Mathf.FloorToInt(left));
            float r = 0, g = 0, b = 0, a = 0;
            for (int y = Mathf.Max(0, Mathf.FloorToInt(bottom)); y < yEnd; y++)
            {
                float dy = Mathf.Min(y + 1, top) - Mathf.Max(y, bottom);
                for (int x = xStart; x < xEnd; x++)
                {
                    float weight = dy * (Mathf.Min(x + 1, right) - Mathf.Max(x, left));
                    var p = pixels[y * width + x];
                    r += p.r * weight; g += p.g * weight; b += p.b * weight; a += p.a * weight;
                }
            }
            float area = step * step;
            if (transparent && a <= 0) return default;
            float factor = transparent ? 255f / a : 1f / area;
            float paper = transparent ? 0f : 255f - a / area;
            return new Color32(Byte(r * factor + paper), Byte(g * factor + paper), Byte(b * factor + paper),
                transparent ? Byte(a / area) : (byte)255);
        }

        private static byte Byte(float value) => (byte)Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);
    }
}
