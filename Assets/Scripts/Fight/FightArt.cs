using System.Collections.Generic;
using UnityEngine;

namespace BalHanuman.Fight
{
    /// <summary>Procedural placeholder sprites (capsules, circles, sparks, rings...). Replaced by real art later; nothing here needs an asset.</summary>
    public static class FightArt
    {
        static Sprite circle, square, ring, spark, star, tri, gradient, soft;
        static readonly Dictionary<int, Sprite> capsules = new Dictionary<int, Sprite>();

        static Texture2D Tex(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        static Sprite Make(int w, int h, System.Func<float, float, float> alpha, Vector2 pivot, float ppu, Vector4 border = default)
        {
            var t = Tex(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float a = Mathf.Clamp01(alpha((x + 0.5f) / w, (y + 0.5f) / h));
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px); t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, w, h), pivot, ppu, 0, SpriteMeshType.FullRect, border);
        }

        /// <summary>Hard-edged white disc, 1 unit across.</summary>
        public static Sprite Circle => circle ??= Make(128, 128, (u, v) =>
        {
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            return (1f - d) * 64f;
        }, new Vector2(0.5f, 0.5f), 128);

        public static Sprite Square => square ??= Make(8, 8, (u, v) => 1f, new Vector2(0.5f, 0.5f), 8);

        /// <summary>Soft glow disc (bright centre, clear edge), 1 unit across.</summary>
        public static Sprite Soft => soft ??= Make(64, 64, (u, v) =>
        {
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            float a = Mathf.Clamp01(1f - d); return a * a;
        }, new Vector2(0.5f, 0.5f), 64);

        public static Sprite Ring => ring ??= Make(128, 128, (u, v) =>
        {
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            float k = 1f - Mathf.Abs(d - 0.86f) / 0.12f;
            return Mathf.Clamp01(k) * Mathf.Clamp01(k);
        }, new Vector2(0.5f, 0.5f), 128);

        /// <summary>Thin bright streak pointing along +x (1 unit long).</summary>
        public static Sprite Spark => spark ??= Make(64, 16, (u, v) =>
        {
            float w = 1f - Mathf.Abs(v - 0.5f) * 2f;
            float l = Mathf.Sin(u * Mathf.PI);
            return Mathf.Clamp01(w * 1.6f - (1f - l)) ;
        }, new Vector2(0.5f, 0.5f), 64);

        /// <summary>Four-pointed impact star.</summary>
        public static Sprite Star => star ??= Make(128, 128, (u, v) =>
        {
            float x = Mathf.Abs(u - 0.5f) * 2f, y = Mathf.Abs(v - 0.5f) * 2f;
            float a = 1f - (Mathf.Pow(x, 0.5f) + Mathf.Pow(y, 0.5f)) * 0.75f;
            float core = 1f - Mathf.Sqrt(x * x + y * y) * 2.2f;
            return Mathf.Max(a * 3f, core * 2f);
        }, new Vector2(0.5f, 0.5f), 128);

        /// <summary>Triangle pointing up, base at the bottom, pivot bottom-centre (spikes, horns, beaks).</summary>
        public static Sprite Tri => tri ??= Make(64, 64, (u, v) =>
        {
            float half = (1f - v) * 0.5f;
            return (half - Mathf.Abs(u - 0.5f)) * 64f;
        }, new Vector2(0.5f, 0f), 64);

        /// <summary>Vertical gradient: white at the bottom fading to clear at the top.</summary>
        public static Sprite Gradient => gradient ??= Make(4, 64, (u, v) => 1f - v, new Vector2(0.5f, 0f), 4);

        /// <summary>A rounded capsule that 9-slices to any length: draw with SpriteRenderer.drawMode = Sliced and set size = (width, length).
        /// Pivot at the top so it hangs down from a joint.</summary>
        public static Sprite Capsule(float width)
        {
            int key = Mathf.RoundToInt(width * 100f);
            if (capsules.TryGetValue(key, out var s) && s != null) return s;
            float ppu = 64f / Mathf.Max(0.02f, width);
            s = Make(64, 64, (u, v) =>
            {
                float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                return (1f - d) * 48f;
            }, new Vector2(0.5f, 1f), ppu, new Vector4(31, 31, 31, 31));
            capsules[key] = s;
            return s;
        }

        public static SpriteRenderer Quad(string name, Transform parent, Sprite s, Vector2 localPos, Vector2 scale, Color c, int order, float rot = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0, 0, rot);
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.color = c; sr.sortingOrder = order;
            return sr;
        }

        /// <summary>A capsule segment hanging from (0,0) of the parent, `length` long (plus round caps), `width` wide.</summary>
        public static SpriteRenderer Segment(string name, Transform parent, float length, float width, Color c, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, width * 0.5f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Capsule(width); sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(width, length + width);
            sr.color = c; sr.sortingOrder = order;
            return sr;
        }
    }
}
