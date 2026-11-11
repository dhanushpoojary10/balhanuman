using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BalHanuman
{
    /// <summary>Small helpers for building the runtime UI from the UiSkin art (navy glass, bronze hairline, gold accents).</summary>
    public static class UiKit
    {
        public static UiSkin Skin;

        public static readonly Color Cream = new Color(0.97f, 0.94f, 0.87f, 1f);
        public static readonly Color Gold = new Color(0.96f, 0.78f, 0.36f, 1f);
        public static readonly Color Muted = new Color(0.97f, 0.94f, 0.87f, 0.72f);

        public static void Init(UiSkin skin) { if (skin != null) Skin = skin; }

        public static RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            return Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        public static Image Img(string name, Transform parent, Sprite s, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, Color? color = null, bool raycast = false)
        {
            var rt = Rect(name, parent, aMin, aMax, pivot, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s; img.color = color ?? Color.white; img.raycastTarget = raycast;
            if (s != null) img.preserveAspect = true;
            return img;
        }

        /// <summary>9-sliced frame (the sprite must have borders set).</summary>
        public static Image Frame(string name, Transform parent, Sprite s, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color? color = null, bool raycast = false)
        {
            var img = Img(name, parent, s, anchor, anchor, pivot, pos, size, color, raycast);
            img.type = Image.Type.Sliced; img.preserveAspect = false;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, Font font, int size, TextAnchor align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 sz, Color color,
            FontStyle style = FontStyle.Normal, bool shadow = true, bool bestFit = false)
        {
            var rt = Rect(name, parent, anchor, anchor, pivot, pos, sz);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.text = text; t.fontSize = size; t.alignment = align; t.color = color; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true; t.raycastTarget = false;
            if (bestFit) { t.resizeTextForBestFit = true; t.resizeTextMinSize = Mathf.Max(14, size - 14); t.resizeTextMaxSize = size; t.verticalOverflow = VerticalWrapMode.Truncate; }
            if (shadow)
            {
                var sh = rt.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0.02f, 0.05f, 0.12f, 0.75f); sh.effectDistance = new Vector2(1.5f, -2f);
            }
            return t;
        }

        public static Text Title(string name, Transform parent, string text, int size, TextAnchor align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 sz, Color? color = null, bool bestFit = false)
            => Label(name, parent, text, Skin.titleFont, size, align, anchor, pivot, pos, sz, color ?? Cream, FontStyle.Bold, true, bestFit);

        public static Text Body(string name, Transform parent, string text, int size, TextAnchor align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 sz, Color? color = null, FontStyle style = FontStyle.Normal, bool bestFit = false)
            => Label(name, parent, text, Skin.bodyFont, size, align, anchor, pivot, pos, sz, color ?? Cream, style, true, bestFit);

        /// <summary>Capsule meter: frame + horizontal fill. Returns the fill image.</summary>
        public static Image Bar(string name, Transform parent, Sprite fillSprite, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var frame = Img(name, parent, Skin.barFrame, anchor, anchor, pivot, pos, size, Color.white);
            frame.preserveAspect = false;
            var fill = Img("Fill", frame.transform, fillSprite, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero, Color.white);
            fill.preserveAspect = false;
            fill.rectTransform.offsetMin = new Vector2(size.y * 0.16f, size.y * 0.16f);
            fill.rectTransform.offsetMax = new Vector2(-size.y * 0.16f, -size.y * 0.16f);
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 1f;
            return fill;
        }

        /// <summary>Round portrait badge: bronze ring with a circular mask; the portrait is top-aligned so heads fill the circle.</summary>
        public static Image Portrait(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, float size, Sprite portrait)
        {
            var ring = Img(name, parent, Skin.portraitRing, anchor, anchor, pivot, pos, new Vector2(size, size));
            var inner = size * 0.80f;
            var mask = Img("Mask", ring.transform, Skin.circle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(inner, inner), new Color(0.08f, 0.14f, 0.27f, 1f));
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var face = Img("Face", mask.transform, portrait, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, 4f), new Vector2(inner, inner), Color.white);
            face.preserveAspect = false;
            SetPortrait(face, portrait, inner);
            return face;
        }

        public static void SetPortrait(Image face, Sprite s, float width)
        {
            face.sprite = s; face.enabled = s != null;
            if (s == null) return;
            float aspect = s.rect.width / s.rect.height;
            face.rectTransform.sizeDelta = new Vector2(width, width / aspect);
        }

        /// <summary>Wide pill button (primary = copper, secondary = navy).</summary>
        public static Button PillButton(string name, Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, UnityAction onClick, bool primary, int fontSize = 36)
        {
            var img = Frame(name, parent, primary ? Skin.hudPillPrimary : Skin.hudPill, anchor, new Vector2(0.5f, 0.5f), pos, size, Color.white, true);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors; colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f); colors.pressedColor = new Color(0.78f, 0.78f, 0.78f); b.colors = colors;
            b.onClick.AddListener(onClick);
            Title("Label", img.transform, label, fontSize, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, Cream);
            return b;
        }
    }
}
