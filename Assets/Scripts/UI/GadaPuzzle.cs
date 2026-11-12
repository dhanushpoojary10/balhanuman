using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BalHanuman
{
    /// <summary>
    /// "Complete the Gada" popup: the gada's shape is carved into a stone slab and five golden pieces wait in a tray.
    /// Drag a piece onto the carving (it snaps when close) or tap it to send it home. One hint lights up the next piece.
    /// Runs in unscaled time (the game is paused behind it). Built at runtime from the UiSkin, like the rest of the UI.
    /// </summary>
    public class GadaPuzzle : MonoBehaviour
    {
        public static GadaPuzzle Current { get; private set; }

        // where each piece sits inside the whole gada image, in its pixels (x from left, y from top).
        // Matches the sprites cut from ArtHQ/Gada/gada_full.png: cap, head, collar, handle, pommel.
        static readonly RectInt[] Layout =
        {
            new RectInt(144, 0, 200, 137),
            new RectInt(0, 137, 488, 357),
            new RectInt(136, 494, 218, 107),
            new RectInt(189, 601, 114, 648),
            new RectInt(159, 1249, 171, 243),
        };
        const float FullW = 488f, FullH = 1492f;
        const float CarveH = 660f;                          // height of the carved gada on screen (reference px)
        static readonly Vector2 SlabPos = new Vector2(-150f, -78f), SlabSize = new Vector2(500f, 760f);
        static readonly Vector2 TrayPos = new Vector2(470f, -78f), TraySize = new Vector2(660f, 760f);
        // tray slots (tray-local centre, box size) and which piece starts in each, so the tray looks mixed up
        static readonly Vector2[] SlotPos = { new Vector2(-195f, 175f), new Vector2(0f, 175f), new Vector2(195f, 175f), new Vector2(-110f, -165f), new Vector2(135f, -165f) };
        static readonly Vector2[] SlotBox = { new Vector2(170f, 250f), new Vector2(190f, 250f), new Vector2(170f, 250f), new Vector2(200f, 340f), new Vector2(230f, 340f) };
        static readonly int[] SlotPiece = { 4, 1, 0, 3, 2 };   // pommel, head, cap / handle, collar

        RectTransform root;
        CanvasGroup cg;
        Text title, subtitle, progress;
        Image[] gems = new Image[5];
        RectTransform tray, slab;
        Image hintBtn, hintBadge; Text hintBadgeText;
        readonly List<Piece> pieces = new List<Piece>();
        Action onDone;
        bool hintUsed, finished;
        float k;                                            // sprite px -> screen px at the carving's scale

        public int Placed { get { int n = 0; foreach (var p in pieces) if (p.placed) n++; return n; } }

        // ------------------------------------------------------------------ open
        public static GadaPuzzle Show(Transform canvas, Sprite hanuman, Action done)
        {
            var go = new GameObject("GadaPuzzle", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var gp = go.AddComponent<GadaPuzzle>();
            gp.onDone = done;
            gp.Build(hanuman);
            Current = gp;
            return gp;
        }

        void OnDestroy() { if (Current == this) Current = null; }

        void Build(Sprite hanuman)
        {
            var S = UiKit.Skin;
            Vector2 c = new Vector2(0.5f, 0.5f), tc = new Vector2(0.5f, 1f), tr = new Vector2(1f, 1f);
            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            cg = gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0f;
            var dim = gameObject.AddComponent<Image>(); dim.color = new Color(0.02f, 0.04f, 0.09f, 0.62f); dim.raycastTarget = true;
            k = CarveH / FullH;

            // banner
            var banner = UiKit.Frame("Banner", root, S.hudWide, tc, tc, new Vector2(0, -36), new Vector2(1060, 168));
            title = UiKit.Title("Title", banner.transform, "Complete the Gada!", 48, TextAnchor.MiddleCenter, c, c, new Vector2(0, 20), new Vector2(940, 64), UiKit.Cream, true);
            subtitle = UiKit.Body("Subtitle", banner.transform, "Drag the golden pieces into the shape carved in the stone.", 26, TextAnchor.MiddleCenter, c, c, new Vector2(0, -30), new Vector2(940, 36), UiKit.Muted, FontStyle.Normal, true);

            // Hanuman watching from the left
            if (hanuman != null)
            {
                var h = UiKit.Img("Hanuman", root, hanuman, c, c, new Vector2(0.5f, 0f), new Vector2(-700, -420), new Vector2(250, 380), Color.white);
                h.preserveAspect = true;
            }

            // stone slab with the gada carved in: gold rim (a slightly larger copy) under the dark cut-in silhouette
            var slabImg = UiKit.Img("Slab", root, S.stoneTexture != null ? S.stoneTexture : S.white, c, c, c, SlabPos, SlabSize, S.stoneTexture != null ? new Color(0.86f, 0.8f, 0.76f) : new Color(0.42f, 0.31f, 0.26f));
            slabImg.preserveAspect = false;
            // one tile tall (no repeat seams): the rock texture is scaled up to cover the slab
            if (S.stoneTexture != null) { slabImg.type = Image.Type.Tiled; slabImg.pixelsPerUnitMultiplier = S.stoneTexture.rect.height / SlabSize.y; }
            slab = slabImg.rectTransform;
            var edge = slabImg.gameObject.AddComponent<Outline>(); edge.effectColor = new Color(0.16f, 0.09f, 0.05f, 1f); edge.effectDistance = new Vector2(6f, -6f);
            var shade = UiKit.Img("Shade", slab, S.white, Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero, new Color(0.12f, 0.06f, 0.03f, 0.3f)); shade.preserveAspect = false;
            // the shape cut into the stone: a glowing gold rim (slightly larger copy) under the dark cut-in
            var shape = S.gadaSilhouette != null ? S.gadaSilhouette : S.gadaFull;
            Vector2 carveSize = new Vector2(FullW * k, CarveH);
            var carve = UiKit.Img("Carving", slab, shape, c, c, c, Vector2.zero, carveSize, new Color(0.17f, 0.09f, 0.05f, 1f)); carve.preserveAspect = false;
            var rimGlow = carve.gameObject.AddComponent<Outline>(); rimGlow.effectColor = new Color(1f, 0.85f, 0.45f, 0.35f); rimGlow.effectDistance = new Vector2(8f, -8f);
            var rim = carve.gameObject.AddComponent<Outline>(); rim.effectColor = new Color(1f, 0.82f, 0.38f, 1f); rim.effectDistance = new Vector2(4f, -4f);

            // progress under the slab
            var prog = UiKit.Rect("Progress", root, c, c, c, SlabPos + new Vector2(0, -SlabSize.y / 2f - 44f), new Vector2(420, 40));
            for (int i = 0; i < 5; i++)
            {
                gems[i] = UiKit.Img("Gem" + i, prog, S.white, c, c, c, new Vector2(-150 + i * 34, 0), new Vector2(18, 18), new Color(1f, 1f, 1f, 0.15f));
                gems[i].preserveAspect = false; gems[i].rectTransform.localEulerAngles = new Vector3(0, 0, 45);
                var o = gems[i].gameObject.AddComponent<Outline>(); o.effectColor = UiKit.Gold; o.effectDistance = new Vector2(1.5f, -1.5f);
            }
            progress = UiKit.Body("Count", prog, "0 of 5", 26, TextAnchor.MiddleLeft, c, new Vector2(0f, 0.5f), new Vector2(26, 0), new Vector2(200, 36), UiKit.Cream, FontStyle.Bold);

            // tray: navy glass + gold frame, like the How to Play box
            tray = UiKit.Rect("Tray", root, c, c, c, TrayPos, TraySize);
            var glass = UiKit.Frame("Glass", tray, S.panelFill != null ? S.panelFill : S.white, c, c, Vector2.zero, TraySize, new Color(0.04f, 0.10f, 0.24f, 0.95f));
            glass.rectTransform.anchorMin = Vector2.zero; glass.rectTransform.anchorMax = Vector2.one; glass.rectTransform.sizeDelta = Vector2.zero;
            var frame = UiKit.Frame("Frame", tray, S.panelFrame != null ? S.panelFrame : S.panelLarge, c, c, Vector2.zero, TraySize);
            frame.rectTransform.anchorMin = Vector2.zero; frame.rectTransform.anchorMax = Vector2.one; frame.rectTransform.sizeDelta = Vector2.zero;

            // hint button (one use)
            hintBtn = UiKit.Img("HintButton", root, S.btnBlank != null ? S.btnBlank : S.btnPause, tr, tr, tr, new Vector2(-70, -60), new Vector2(104, 104), Color.white, true);
            UiKit.Img("Icon", hintBtn.transform, S.iconStar, c, c, c, Vector2.zero, new Vector2(56, 56), Color.white);
            hintBadge = UiKit.Img("Badge", hintBtn.transform, S.circle, tr, tr, c, new Vector2(-8, -8), new Vector2(40, 40), new Color(0.85f, 0.25f, 0.18f));
            hintBadgeText = UiKit.Body("N", hintBadge.transform, "1", 24, TextAnchor.MiddleCenter, c, c, new Vector2(0, 1), new Vector2(40, 40), Color.white, FontStyle.Bold);
            var hintLabel = UiKit.Body("Label", hintBtn.transform, "Hint", 24, TextAnchor.UpperCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -2), new Vector2(160, 32), UiKit.Cream, FontStyle.Bold);
            var hb = hintBtn.gameObject.AddComponent<Button>(); hb.targetGraphic = hintBtn; hb.onClick.AddListener(UseHint);

            // the five pieces start in the tray
            for (int s = 0; s < 5; s++)
            {
                int i = SlotPiece[s];
                var sprite = S.gadaPieces != null && i < S.gadaPieces.Length ? S.gadaPieces[i] : null;
                var r = Layout[i];
                var img = UiKit.Img("Piece" + i, root, sprite, c, c, c, Vector2.zero, new Vector2(r.width * k, r.height * k), Color.white, true);
                img.preserveAspect = false;
                var piece = img.gameObject.AddComponent<Piece>();
                piece.owner = this; piece.index = i; piece.img = img;
                piece.target = SlabPos + new Vector2((r.x + r.width / 2f - FullW / 2f) * k, (FullH / 2f - (r.y + r.height / 2f)) * k);
                piece.home = TrayPos + SlotPos[s];
                // in the tray each piece is shown as big as its slot allows (bigger than on the carving)
                float fit = Mathf.Min(SlotBox[s].x / (r.width * k), SlotBox[s].y / (r.height * k));
                piece.homeScale = Mathf.Clamp(fit, 0.6f, 1.6f);
                piece.rt.anchoredPosition = piece.home; piece.rt.localScale = Vector3.one * piece.homeScale;
                var shadow = img.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, 0.45f); shadow.effectDistance = new Vector2(4, -6);
                pieces.Add(piece);
            }

            StartCoroutine(FadeIn());
            RefreshProgress();
        }

        IEnumerator FadeIn()
        {
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.3f) { cg.alpha = t; yield return null; }
            cg.alpha = 1f;
        }

        // ------------------------------------------------------------------ pieces
        // A piece goes in only by dragging it to its own spot. Anywhere else: "Oops!" and it glides back to the tray.
        const float SnapDist = 110f;
        static readonly string[] Praise = { "Perfect!", "Great!", "Well done!", "Yes!", "Super!" };
        static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        static float EaseOutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); }

        internal void OnDropped(Piece p)
        {
            if (finished || p.placed || p.busy) return;
            HideDropGlow();
            if (Vector2.Distance(p.rt.anchoredPosition, p.target) < SnapDist) { StartCoroutine(PlaceRoutine(p)); return; }
            // barely moved (a nudge): slide back quietly; otherwise it was the wrong place
            bool nudge = Vector2.Distance(p.rt.anchoredPosition, p.home) < 60f;
            StartCoroutine(ReturnHome(p, !nudge));
        }

        internal void OnTapped(Piece p)
        {
            if (finished || p.placed || p.busy) return;
            StartCoroutine(Wiggle(p));
            FloatText("Drag me to the gada!", p.home + new Vector2(0f, 150f), UiKit.Cream, 30);
        }

        IEnumerator PlaceRoutine(Piece p)
        {
            p.placed = true; p.busy = true; p.dragging = false; p.img.raycastTarget = false;
            foreach (var s in p.GetComponents<Shadow>()) s.enabled = false;
            // glide into the carving (slightly big), then settle with a little bounce
            Vector2 from = p.rt.anchoredPosition; float s0 = p.rt.localScale.x; float r0 = Mathf.DeltaAngle(0f, p.rt.localEulerAngles.z);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.16f)
            {
                float e = EaseOutCubic(t);
                p.rt.anchoredPosition = Vector2.LerpUnclamped(from, p.target, e);
                p.rt.localScale = Vector3.one * Mathf.Lerp(s0, 1.14f, e);
                p.rt.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(r0, 0f, e));
                yield return null;
            }
            p.rt.anchoredPosition = p.target; p.rt.localEulerAngles = Vector3.zero;
            p.rt.SetSiblingIndex(slab.GetSiblingIndex() + 1);            // sits in the carving, under pieces still in hand
            Sfx.Play(Sfx.Id.Pickup, 0.9f, 1.05f + Placed * 0.07f);
            Haptics.Light();
            Flash(p);
            Sparkles(p.target, 14);
            FloatText(Praise[(Placed - 1 + Praise.Length) % Praise.Length], p.target + new Vector2(170f, 30f), UiKit.Gold, 40);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.28f)
            {
                p.rt.localScale = Vector3.one * Mathf.LerpUnclamped(1.14f, 1f, EaseOutBack(t));
                yield return null;
            }
            p.rt.localScale = Vector3.one;
            p.busy = false;
            ClearHintGlow();
            RefreshProgress();
            if (Placed >= pieces.Count && !finished) StartCoroutine(Complete());
        }

        IEnumerator ReturnHome(Piece p, bool oops)
        {
            p.busy = true; p.dragging = false; p.img.raycastTarget = false;
            if (oops)
            {
                Sfx.Play(Sfx.Id.Click, 0.9f, 0.55f);
                Haptics.Tick();
                FloatText("Oops! Not here.", p.rt.anchoredPosition + new Vector2(0f, 110f), new Color(1f, 0.62f, 0.52f), 38);
                // a quick red-tinted shake where it was dropped
                Vector2 at = p.rt.anchoredPosition;
                for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.38f)
                {
                    p.rt.anchoredPosition = at + new Vector2(Mathf.Sin(t * 42f) * 16f * (1f - t), 0f);
                    p.img.color = Color.Lerp(Color.white, new Color(1f, 0.5f, 0.45f), Mathf.Sin(t * Mathf.PI));
                    yield return null;
                }
                p.img.color = Color.white;
            }
            // glide back to its tray slot with a soft overshoot
            Vector2 from = p.rt.anchoredPosition; float s0 = p.rt.localScale.x; float r0 = Mathf.DeltaAngle(0f, p.rt.localEulerAngles.z);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.42f)
            {
                float e = EaseOutBack(t);
                p.rt.anchoredPosition = Vector2.LerpUnclamped(from, p.home, e);
                p.rt.localScale = Vector3.one * Mathf.LerpUnclamped(s0, p.homeScale, EaseOutCubic(t));
                p.rt.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(r0, 0f, EaseOutCubic(t)));
                yield return null;
            }
            p.rt.anchoredPosition = p.home; p.rt.localScale = Vector3.one * p.homeScale; p.rt.localEulerAngles = Vector3.zero;
            p.busy = false; p.img.raycastTarget = true;
        }

        IEnumerator Wiggle(Piece p)
        {
            p.busy = true;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.4f)
            {
                p.rt.localEulerAngles = new Vector3(0, 0, Mathf.Sin(t * 30f) * 9f * (1f - t));
                yield return null;
            }
            p.rt.localEulerAngles = Vector3.zero;
            p.busy = false;
        }

        // ------------------------------------------------------------------ little effects (all unscaled time)
        /// <summary>A gold copy of the piece that blooms out and fades: the "it fits!" flash.</summary>
        void Flash(Piece p)
        {
            var g = UiKit.Img("Flash", root, p.img.sprite, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), p.target, p.rt.sizeDelta, new Color(1f, 0.9f, 0.5f, 0.9f));
            g.preserveAspect = false;
            g.rectTransform.SetSiblingIndex(p.rt.GetSiblingIndex() + 1);
            StartCoroutine(FadeGrow(g, 1f, 1.35f, 0.45f));
        }

        IEnumerator FadeGrow(Image g, float s0, float s1, float dur)
        {
            Color c = g.color;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / dur)
            {
                g.rectTransform.localScale = Vector3.one * Mathf.Lerp(s0, s1, EaseOutCubic(t));
                g.color = new Color(c.r, c.g, c.b, c.a * (1f - t));
                yield return null;
            }
            Destroy(g.gameObject);
        }

        void Sparkles(Vector2 at, int count)
        {
            var S = UiKit.Skin;
            for (int i = 0; i < count; i++)
            {
                bool star = i % 3 == 0 && S.iconStar != null;
                float size = star ? UnityEngine.Random.Range(26f, 38f) : UnityEngine.Random.Range(9f, 17f);
                var img = UiKit.Img("Spark", root, star ? S.iconStar : S.circle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, new Vector2(size, size), star ? Color.white : new Color(1f, 0.88f, 0.45f));
                float ang = (i / (float)count) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.3f, 0.3f);
                StartCoroutine(Spark(img, at, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * UnityEngine.Random.Range(110f, 220f)));
            }
        }

        IEnumerator Spark(Image img, Vector2 from, Vector2 travel)
        {
            var rt = img.rectTransform; Color c = img.color; float spin = UnityEngine.Random.Range(-360f, 360f);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.65f)
            {
                rt.anchoredPosition = from + travel * EaseOutCubic(t) + new Vector2(0f, -40f * t * t);
                rt.localScale = Vector3.one * (1f - 0.6f * t);
                rt.localEulerAngles = new Vector3(0, 0, spin * t);
                img.color = new Color(c.r, c.g, c.b, 1f - t * t);
                yield return null;
            }
            Destroy(img.gameObject);
        }

        /// <summary>A word that pops in, floats up and fades ("Perfect!", "Oops! Not here.").</summary>
        void FloatText(string text, Vector2 at, Color color, int size)
        {
            var c = new Vector2(0.5f, 0.5f);
            var t = UiKit.Title("Float", root, text, size, TextAnchor.MiddleCenter, c, c, at, new Vector2(520, 70), color);
            var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.04f, 0.07f, 0.16f, 0.95f); o.effectDistance = new Vector2(2.5f, -2.5f);
            StartCoroutine(FloatRoutine(t, at));
        }

        IEnumerator FloatRoutine(Text t, Vector2 at)
        {
            var rt = t.rectTransform; Color c = t.color;
            for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / 1.1f)
            {
                float pop = k < 0.2f ? Mathf.LerpUnclamped(0.5f, 1f, EaseOutBack(k / 0.2f)) : 1f;
                rt.localScale = Vector3.one * pop;
                rt.anchoredPosition = at + new Vector2(0f, 46f * EaseOutCubic(k));
                t.color = new Color(c.r, c.g, c.b, k < 0.65f ? 1f : 1f - (k - 0.65f) / 0.35f);
                yield return null;
            }
            Destroy(t.gameObject);
        }

        // "drop here": the piece's own spot glows while it is carried close enough to snap
        Image dropGlow; Piece dropGlowFor;

        void ShowDropGlow(Piece p, float strength)
        {
            if (dropGlowFor != p) { HideDropGlow(); dropGlowFor = p; }
            if (dropGlow == null)
            {
                dropGlow = UiKit.Img("DropGlow", root, p.img.sprite, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), p.target, p.rt.sizeDelta, new Color(1f, 0.85f, 0.4f, 0f));
                dropGlow.preserveAspect = false;
                dropGlow.rectTransform.SetSiblingIndex(slab.GetSiblingIndex() + 1);
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f);
            dropGlow.color = new Color(1f, 0.85f, 0.4f, strength * (0.3f + 0.35f * pulse));
        }

        void HideDropGlow()
        {
            if (dropGlow != null) Destroy(dropGlow.gameObject);
            dropGlow = null; dropGlowFor = null;
        }

        void RefreshProgress()
        {
            int n = Placed;
            progress.text = n + " of 5";
            for (int i = 0; i < gems.Length; i++) gems[i].color = i < n ? UiKit.Gold : new Color(1f, 1f, 1f, 0.15f);
        }

        // ------------------------------------------------------------------ hint
        Image hintGlow; Piece hinted;

        void UseHint()
        {
            if (hintUsed || finished) return;
            Piece next = null;
            foreach (var p in pieces) if (!p.placed) { next = p; break; }
            if (next == null) return;
            hintUsed = true;
            hintBadgeText.text = "0"; hintBadge.color = new Color(0.45f, 0.47f, 0.55f);
            hintBtn.color = new Color(0.65f, 0.67f, 0.75f);
            Sfx.Play(Sfx.Id.Click);
            hinted = next;
            // a glowing gold copy of the piece where it belongs
            hintGlow = UiKit.Img("HintGlow", root, next.img.sprite, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), next.target, next.img.rectTransform.sizeDelta, new Color(1f, 0.85f, 0.4f, 0.5f));
            hintGlow.preserveAspect = false;
            hintGlow.rectTransform.SetSiblingIndex(slab.GetSiblingIndex() + 1);
        }

        void ClearHintGlow()
        {
            if (hinted != null && hinted.placed) { if (hintGlow != null) Destroy(hintGlow.gameObject); hintGlow = null; hinted = null; }
        }

        void Update()
        {
            float now = Time.unscaledTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 5f);
            if (hintGlow != null) hintGlow.color = new Color(1f, 0.85f, 0.4f, 0.25f + 0.45f * pulse);

            Piece carried = null;
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (p.dragging) { carried = p; continue; }
                if (p.placed || p.busy) continue;
                // pieces waiting in the tray bob gently, each a little out of step; the hinted one also pulses
                p.rt.anchoredPosition = p.home + new Vector2(0f, Mathf.Sin(now * 2.2f + i * 1.3f) * 5f);
                float s = p.homeScale * (p == hinted ? 1f + 0.07f * pulse : 1f);
                p.rt.localScale = Vector3.one * s;
            }

            // "drop here" glow while a piece is carried near its spot
            if (carried != null)
            {
                float d = Vector2.Distance(carried.rt.anchoredPosition, carried.target);
                if (d < SnapDist * 1.6f) ShowDropGlow(carried, Mathf.Clamp01(1.6f - d / SnapDist));
                else if (dropGlow != null) HideDropGlow();
            }
            else if (dropGlow != null) HideDropGlow();
        }

        // ------------------------------------------------------------------ done
        IEnumerator Complete()
        {
            finished = true;
            hintBtn.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(0.25f);
            Sfx.Play(Sfx.Id.Bell);
            Haptics.Medium();
            var S = UiKit.Skin;
            var c = new Vector2(0.5f, 0.5f);
            var glow = UiKit.Img("Glow", root, S.softGlow != null ? S.softGlow : S.circle, c, c, c, SlabPos, new Vector2(900, 1000), new Color(1f, 0.82f, 0.4f, 0f));
            glow.preserveAspect = false;
            glow.rectTransform.SetSiblingIndex(slab.GetSiblingIndex());
            title.text = "The Gada is Ready!";
            subtitle.text = "Hanuman's mighty gada is whole again.";
            for (int i = 0; i < tray.childCount; i++) tray.GetChild(i).gameObject.SetActive(i < 2);    // keep glass + frame
            UiKit.Body("Done", tray, "All 5 pieces in place!", 30, TextAnchor.MiddleCenter, c, c, new Vector2(0, 90), new Vector2(560, 44), UiKit.Gold, FontStyle.Bold);
            var take = UiKit.PillButton("TakeGada", tray, "Take the Gada", c, new Vector2(0, -40), new Vector2(420, 110), Close, true, 40);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.6f)
            {
                glow.color = new Color(1f, 0.82f, 0.4f, 0.45f * Mathf.Sin(Mathf.Min(1f, t * 1.2f) * Mathf.PI * 0.5f));
                slab.localScale = Vector3.one * (1f + 0.035f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            slab.localScale = Vector3.one;
            take.transform.localScale = Vector3.one;
        }

        bool closing;

        /// <summary>Takes the gada and closes the popup.</summary>
        public void Close()
        {
            if (closing || !finished) return;
            closing = true;
            Sfx.Play(Sfx.Id.Click);
            StartCoroutine(CloseRoutine());
        }

        IEnumerator CloseRoutine()
        {
            cg.blocksRaycasts = false;
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.25f) { cg.alpha = t; yield return null; }
            var done = onDone; onDone = null;
            Destroy(gameObject);
            done?.Invoke();
        }

        /// <summary>Test hook: place every piece at once (the playtest bot uses this).</summary>
        public void SolveForTest() { foreach (var p in pieces) if (!p.placed) StartCoroutine(PlaceRoutine(p)); }

        // ------------------------------------------------------------------ one draggable piece
        internal class Piece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
        {
            public GadaPuzzle owner;
            public int index;
            public Image img;
            public Vector2 home, target;
            public float homeScale = 1f;
            public bool placed, dragging, busy;
            Vector2 grab, desired, lastPos;
            float tilt;
            public RectTransform rt => (RectTransform)transform;

            public void OnBeginDrag(PointerEventData e)
            {
                if (placed || busy || owner.finished) return;
                dragging = true;
                rt.SetAsLastSibling();
                RectTransformUtility.ScreenPointToLocalPointInRectangle(owner.root, e.position, e.pressEventCamera, out var lp);
                // keep the same point of the piece under the finger as it shrinks to carving size
                grab = (rt.anchoredPosition - lp) / Mathf.Max(0.01f, homeScale);
                desired = lastPos = rt.anchoredPosition;
                foreach (var s in GetComponents<Shadow>()) s.effectDistance = new Vector2(10f, -16f);   // lifted off the tray
                Sfx.Play(Sfx.Id.Click, 0.5f, 1.3f);
            }

            public void OnDrag(PointerEventData e)
            {
                if (!dragging) return;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(owner.root, e.position, e.pressEventCamera, out var lp);
                desired = lp + grab;
            }

            public void OnEndDrag(PointerEventData e)
            {
                if (!dragging) return;
                rt.anchoredPosition = desired;               // drop exactly where the finger let go
                foreach (var s in GetComponents<Shadow>()) s.effectDistance = new Vector2(4f, -6f);
                dragging = false;
                owner.OnDropped(this);
            }

            public void OnPointerClick(PointerEventData e)
            {
                if (e.dragging) return;
                owner.OnTapped(this);                       // a tap alone does not place it: it says "drag me"
            }

            void Update()
            {
                if (!dragging) return;
                float dt = Time.unscaledDeltaTime;
                // follow the finger smoothly, grow to carving size (plus a little lift), and lean into the movement
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, desired, 1f - Mathf.Exp(-28f * dt));
                rt.localScale = Vector3.one * Mathf.Lerp(rt.localScale.x, 1.08f, 1f - Mathf.Exp(-14f * dt));
                float vx = dt > 0f ? (rt.anchoredPosition.x - lastPos.x) / dt : 0f;
                tilt = Mathf.Lerp(tilt, Mathf.Clamp(-vx * 0.012f, -14f, 14f), 1f - Mathf.Exp(-10f * dt));
                rt.localEulerAngles = new Vector3(0f, 0f, tilt);
                lastPos = rt.anchoredPosition;
            }
        }
    }
}
