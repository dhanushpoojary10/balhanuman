using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BalHanuman
{
    /// <summary>Shared helpers + dark navy / gold theme used by every runtime-built UI.</summary>
    public static class UiKit
    {
        public static readonly Color Navy = new Color(0.05f, 0.08f, 0.19f, 0.93f);
        public static readonly Color NavyLight = new Color(0.12f, 0.17f, 0.34f, 0.95f);
        public static readonly Color Gold = new Color(0.99f, 0.8f, 0.3f, 1f);
        public static readonly Color Cream = new Color(1f, 0.97f, 0.88f, 1f);

        public static Font Font;
        public static Sprite Circle, Ring, Panel, White;

        [System.Serializable]
        public class Sprites { public Sprite circle, ring, panel, white; }

        public static void Init(Sprites s)
        {
            if (Font == null) Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (s == null) return;
            Circle = s.circle; Ring = s.ring; Panel = s.panel; White = s.white;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static Image Img(string name, Transform parent, Sprite s, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, Color c, bool raycast = false)
        {
            var rt = Rect(name, parent, aMin, aMax, pivot, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s; img.color = c; img.raycastTarget = raycast;
            if (s != null) img.preserveAspect = true;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, TextAnchor anchor, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 sz, Color c, bool outline = true)
        {
            var rt = Rect(name, parent, aMin, aMax, pivot, pos, sz);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.alignment = anchor; t.color = c;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (outline) { var ol = rt.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0, 0, 0, 0.8f); ol.effectDistance = new Vector2(2, -2); }
            return t;
        }

        /// <summary>Navy rounded panel with a gold border.</summary>
        public static RectTransform FramedPanel(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, Color? fill = null)
        {
            var outer = Img(name, parent, Panel, aMin, aMax, pivot, pos, size, Gold);
            outer.type = Image.Type.Sliced; outer.preserveAspect = false;
            var inner = Img("Fill", outer.transform, Panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, fill ?? Navy);
            inner.type = Image.Type.Sliced; inner.preserveAspect = false;
            inner.rectTransform.offsetMin = new Vector2(5, 5); inner.rectTransform.offsetMax = new Vector2(-5, -5);
            return outer.rectTransform;
        }

        public static Button TextButton(string name, Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, UnityAction onClick, int fontSize = 52)
        {
            var rt = FramedPanel(name, parent, anchor, anchor, new Vector2(0.5f, 0.5f), pos, size, NavyLight);
            rt.GetComponent<Image>().raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = rt.GetComponent<Image>();
            b.onClick.AddListener(onClick);
            Label("Label", rt, label, fontSize, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Gold);
            return b;
        }
    }
}
