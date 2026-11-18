using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BalHanuman.Fight;

namespace BalHanuman
{
    /// <summary>Home screen in two steps: a title screen ("tap to continue") over the key art, then a chapter select
    /// with a card per level. The art loads from Resources/Menu: title_keyart, card_level1, card_level2.
    /// Replace those files to change it. A missing file falls back to a plain dark panel.</summary>
    public class MainMenuController : MonoBehaviour
    {
        public UiSkin skin;
        public string levelScene = "Level01_MissingFruitOffering";
        public string fightScene = "Level02_PatalaYuddha";

        static readonly Color Ink = new Color(0.03f, 0.04f, 0.10f, 1f);
        static readonly Color Saffron = new Color(1f, 0.62f, 0.22f, 1f);
        static readonly Color TitleGold = new Color(1f, 0.92f, 0.74f, 1f);
        static readonly Vector2 C = new Vector2(0.5f, 0.5f);

        // coming back from a level opens straight on the chapter cards
        static bool returnToSelect;

        RectTransform root, bgDrift, logo, cardsRoot, emberLayer;
        CanvasGroup titleGroup, selectGroup, fader;
        Text tapText;
        Vector2 logoBase;
        readonly List<RectTransform> cards = new List<RectTransform>();
        readonly List<Ember> embers = new List<Ember>();
        bool onSelect, busy;
        Sprite rounded, roundedRim, gradV, gradH;

        class Ember { public RectTransform rt; public Image img; public float speed, sway, phase, alpha, x; }

        void Awake()
        {
            Time.timeScale = 1f;
            UiKit.Init(skin);
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            var cgo = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = cgo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            root = (RectTransform)cgo.transform;

            rounded = RoundedSprite(128, 36, 0f);
            roundedRim = RoundedSprite(128, 36, 3f);
            gradV = Gradient(true);
            gradH = Gradient(false);

            BuildBackground();
            BuildEmbers();
            BuildTitle();
            BuildSelect();

            // a black fader on top for the scene change
            var f = UiKit.Img("Fader", root, UiKit.Skin.white, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero, Color.black);
            f.preserveAspect = false;
            fader = f.gameObject.AddComponent<CanvasGroup>(); fader.alpha = 0f; fader.blocksRaycasts = false;

            if (returnToSelect)
            {
                returnToSelect = false;
                SetScreen(true);
                StartCoroutine(CardsIn());
            }
            else SetScreen(false);
        }

        // ================================================================ background (shared by both screens)
        void BuildBackground()
        {
            bgDrift = UiKit.Stretch("Background", root);
            bgDrift.pivot = new Vector2(0.72f, 1f);        // zoom around Hanuman's head so the slow push-in never crops his crown
            var key = LoadSprite("Menu/title_keyart");
            if (key == null) return;                      // the scene's painted backdrop shows through instead
            var img = UiKit.Img("KeyArt", bgDrift, key, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
            img.preserveAspect = false;
            // on screens narrower than the art, crop mostly from the left so Hanuman (right half) stays in view
            img.rectTransform.pivot = new Vector2(0.72f, 0.5f);
            var fit = img.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = key.rect.width / key.rect.height;
        }

        void BuildEmbers()
        {
            emberLayer = UiKit.Stretch("Embers", root);
            for (int i = 0; i < 34; i++)
            {
                float size = Random.Range(8f, 26f);
                var img = UiKit.Img("Ember", emberLayer, UiKit.Skin.softGlow, C, C, C, Vector2.zero, new Vector2(size, size), Color.clear);
                var e = new Ember
                {
                    rt = img.rectTransform, img = img,
                    speed = Random.Range(22f, 60f), sway = Random.Range(8f, 30f), phase = Random.value * 6.28f,
                    alpha = Random.Range(0.35f, 0.9f), x = Random.Range(-0.5f, 0.5f)
                };
                e.rt.anchoredPosition = new Vector2(0, Random.Range(-600f, 600f));
                embers.Add(e);
            }
        }

        // ================================================================ screen 1: title
        void BuildTitle()
        {
            var g = UiKit.Stretch("Title", root);
            titleGroup = g.gameObject.AddComponent<CanvasGroup>();

            // tap anywhere to continue
            var hit = UiKit.Img("TapArea", g, null, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero, Color.clear, true);
            hit.gameObject.AddComponent<Button>().onClick.AddListener(ShowSelect);

            // vignettes keep the logo readable on any art
            var left = UiKit.Img("LeftShade", g, gradH, Vector2.zero, new Vector2(0.66f, 1f), C, Vector2.zero, Vector2.zero, new Color(Ink.r, Ink.g, Ink.b, 0.7f));
            left.preserveAspect = false;
            var bottom = UiKit.Img("BottomShade", g, gradV, Vector2.zero, new Vector2(1f, 0.38f), C, Vector2.zero, Vector2.zero, new Color(Ink.r, Ink.g, Ink.b, 0.7f));
            bottom.preserveAspect = false;

            logo = UiKit.Rect("Logo", g, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, 70f), new Vector2(1000f, 420f));
            logoBase = logo.anchoredPosition;
            var glow = UiKit.Img("Glow", logo, UiKit.Skin.softGlow, C, C, C, new Vector2(-40f, 20f), new Vector2(1200f, 460f), new Color(Saffron.r, Saffron.g, Saffron.b, 0.22f));
            glow.preserveAspect = false;
            var L = new Vector2(0f, 0.5f);
            UiKit.Body("Kicker", logo, "T H E    L E G E N D    O F", 26, TextAnchor.MiddleLeft, L, L, new Vector2(6f, 130f), new Vector2(900f, 40f), UiKit.Gold, FontStyle.Bold);
            var title = UiKit.Title("Name", logo, "HANUMAN", 132, TextAnchor.MiddleLeft, L, L, new Vector2(0f, 35f), new Vector2(1200f, 170f), TitleGold);
            var o = title.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.85f, 0.45f, 0.1f, 0.55f); o.effectDistance = new Vector2(2f, -2f);
            UiKit.Img("Divider", logo, UiKit.Skin.divider, L, L, L, new Vector2(4f, -62f), new Vector2(560f, 28f), Color.white);
            UiKit.Body("Sub", logo, "A divine adventure", 30, TextAnchor.MiddleLeft, L, L, new Vector2(6f, -112f), new Vector2(900f, 44f), new Color(1f, 0.96f, 0.9f, 0.92f), FontStyle.Bold);

            var b = new Vector2(0.5f, 0f);
            // under the logo, in the calm left column (the art's foreground sits bottom-centre/right)
            var lb = new Vector2(0f, 0f);
            tapText = UiKit.Title("Tap", g, "TAP TO CONTINUE", 34, TextAnchor.MiddleLeft, lb, lb, new Vector2(156f, 150f), new Vector2(900f, 60f), UiKit.Cream);
        }

        // ================================================================ screen 2: chapter select
        void BuildSelect()
        {
            var g = UiKit.Stretch("Select", root);
            selectGroup = g.gameObject.AddComponent<CanvasGroup>();

            var veil = UiKit.Img("Veil", g, UiKit.Skin.white, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero, new Color(Ink.r, Ink.g, Ink.b, 0.8f));
            veil.preserveAspect = false;
            var top = new Vector2(0.5f, 1f);
            UiKit.Title("Header", g, "CHOOSE YOUR PATH", 50, TextAnchor.MiddleCenter, top, top, new Vector2(0f, -88f), new Vector2(1200f, 70f), TitleGold);
            UiKit.Img("Divider", g, UiKit.Skin.divider, top, top, top, new Vector2(0f, -132f), new Vector2(480f, 24f), Color.white);
            var tl = new Vector2(0f, 1f);
            UiKit.PillButton("Back", g, "BACK", tl, new Vector2(140f, -80f), new Vector2(200f, 72f), ShowTitle, false, 28);

            cardsRoot = UiKit.Rect("Cards", g, C, C, C, new Vector2(0f, -62f), new Vector2(1600f, 800f));

            // ---- Chapter I
            var c1 = Card("Chapter1", new Vector2(-400f, 0f), LoadSprite("Menu/card_level1"), PlayLevel1);
            CardText(c1, "C H A P T E R   I", "The Missing Fruit Offering", "Find the sacred mangoes, rescue the baby monkey and return the offering.");
            ProgressRow(c1, -248f);
            var play = UiKit.PillButton("Play", c1, "PLAY", C, new Vector2(0f, -335f), new Vector2(540f, 92f), PlayLevel1, true, 40);
            play.gameObject.AddComponent<UiPress>();

            // ---- Chapter II
            var c2 = Card("Chapter2", new Vector2(400f, 0f), LoadSprite("Menu/card_level2"), () => Fight(FightSetup.Mode.Story));
            CardText(c2, "C H A P T E R   I I", "Ahiravana's Lair", "Descend into the underworld and face the sorcerer Ahiravana.");
            var fight = UiKit.PillButton("Fight", c2, "FIGHT", C, new Vector2(0f, -262f), new Vector2(540f, 88f), () => Fight(FightSetup.Mode.Story), true, 38);
            fight.gameObject.AddComponent<UiPress>();
            UiKit.PillButton("Training", c2, "TRAINING", C, new Vector2(-139f, -350f), new Vector2(262f, 70f), () => Fight(FightSetup.Mode.Training), false, 26).gameObject.AddComponent<UiPress>();
            UiKit.PillButton("Demo", c2, "DEMO REEL", C, new Vector2(139f, -350f), new Vector2(262f, 70f), () => Fight(FightSetup.Mode.Demo), false, 26).gameObject.AddComponent<UiPress>();
        }

        RectTransform Card(string name, Vector2 pos, Sprite art, UnityAction onClick)
        {
            var card = UiKit.Rect(name, cardsRoot, C, C, C, pos, new Vector2(740f, 800f));
            card.gameObject.AddComponent<CanvasGroup>();
            card.gameObject.AddComponent<UiPress>().pressedScale = 0.985f;

            var shadow = UiKit.Img("Shadow", card, rounded, Vector2.zero, Vector2.one, C, new Vector2(0f, -16f), new Vector2(36f, 36f), new Color(0f, 0f, 0f, 0.55f));
            shadow.type = Image.Type.Sliced; shadow.preserveAspect = false;

            var mask = UiKit.Img("Mask", card, rounded, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero, new Color(0.10f, 0.08f, 0.16f, 1f), true);
            mask.type = Image.Type.Sliced; mask.preserveAspect = false;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            // the button sits on the card root: taps on the art bubble up to it, and UiPress (same object) squashes the whole card
            var btn = card.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(onClick);

            if (art != null)
            {
                var a = UiKit.Img("Art", mask.transform, art, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
                a.preserveAspect = false;
                var fit = a.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = art.rect.width / art.rect.height;
            }
            var shade = UiKit.Img("Shade", mask.transform, gradV, Vector2.zero, new Vector2(1f, 0.7f), C, Vector2.zero, Vector2.zero, new Color(Ink.r, Ink.g, Ink.b, 0.96f));
            shade.preserveAspect = false;

            var rim = UiKit.Img("Rim", card, roundedRim, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero, new Color(1f, 0.82f, 0.45f, 0.85f));
            rim.type = Image.Type.Sliced; rim.preserveAspect = false;

            cards.Add(card);
            return card;
        }

        void CardText(RectTransform card, string kicker, string title, string tagline)
        {
            UiKit.Body("Kicker", card, kicker, 22, TextAnchor.MiddleCenter, C, C, new Vector2(0f, -58f), new Vector2(680f, 34f), UiKit.Gold, FontStyle.Bold);
            UiKit.Title("Name", card, title, 48, TextAnchor.MiddleCenter, C, C, new Vector2(0f, -110f), new Vector2(690f, 66f), TitleGold, true);
            UiKit.Body("Tagline", card, tagline, 24, TextAnchor.UpperCenter, C, C, new Vector2(0f, -178f), new Vector2(620f, 70f), UiKit.Muted);
        }

        void ProgressRow(RectTransform card, float y)
        {
            int best = -1, total = 0, stars = 0;
            try { best = PlayerPrefs.GetInt("l1BestMangoes", -1); total = PlayerPrefs.GetInt("l1TotalMangoes", 0); stars = PlayerPrefs.GetInt("l1BestStars", 0); } catch { }

            var row = UiKit.Rect("Progress", card, C, C, C, new Vector2(0f, y), new Vector2(600f, 50f));
            if (best < 0)
            {
                UiKit.Body("New", row, "A new adventure awaits", 26, TextAnchor.MiddleCenter, C, C, Vector2.zero, new Vector2(600f, 44f), UiKit.Gold, FontStyle.Italic);
                return;
            }
            UiKit.Img("Mango", row, UiKit.Skin.iconMango, C, C, C, new Vector2(-200f, 0f), new Vector2(46f, 46f));
            UiKit.Body("Best", row, "Best  " + best + " / " + Mathf.Max(total, best), 28, TextAnchor.MiddleLeft, C, C, new Vector2(-50f, 0f), new Vector2(240f, 44f), UiKit.Cream, FontStyle.Bold);
            for (int i = 0; i < 3; i++)
                UiKit.Img("Star" + i, row, UiKit.Skin.iconStar, C, C, C, new Vector2(120f + i * 46f, 0f), new Vector2(40f, 40f),
                    i < stars ? UiKit.Gold : new Color(1f, 1f, 1f, 0.22f));
        }

        // ================================================================ navigation
        void SetScreen(bool select)
        {
            onSelect = select;
            titleGroup.alpha = select ? 0f : 1f; titleGroup.blocksRaycasts = !select; titleGroup.interactable = !select;
            selectGroup.alpha = select ? 1f : 0f; selectGroup.blocksRaycasts = select; selectGroup.interactable = select;
            logo.anchoredPosition = logoBase;
        }

        void ShowSelect() { if (!onSelect && !busy) StartCoroutine(Switch(true)); }
        void ShowTitle() { if (onSelect && !busy) StartCoroutine(Switch(false)); }

        IEnumerator Switch(bool toSelect)
        {
            busy = true;
            if (toSelect) Sfx.Play(Sfx.Id.Bell, 1f, 0.45f); else Sfx.Play(Sfx.Id.Click);
            titleGroup.blocksRaycasts = selectGroup.blocksRaycasts = false;
            if (toSelect) StartCoroutine(CardsIn());
            const float dur = 0.45f;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = Ease(t / dur), s = toSelect ? k : 1f - k;
                titleGroup.alpha = 1f - s;
                selectGroup.alpha = s;
                logo.anchoredPosition = logoBase + new Vector2(0f, 36f * s);
                yield return null;
            }
            SetScreen(toSelect);
            busy = false;
        }

        IEnumerator CardsIn()
        {
            var groups = new List<CanvasGroup>();
            var bases = new List<Vector2>();
            foreach (var c in cards) { var cg = c.GetComponent<CanvasGroup>(); cg.alpha = 0f; groups.Add(cg); bases.Add(c.anchoredPosition); }
            const float dur = 0.5f, stagger = 0.08f;
            for (float t = 0; t < dur + stagger * cards.Count; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    float k = Ease(Mathf.Clamp01((t - i * stagger) / dur));
                    groups[i].alpha = k;
                    cards[i].anchoredPosition = bases[i] + new Vector2(0f, -50f * (1f - k));
                }
                yield return null;
            }
            for (int i = 0; i < cards.Count; i++) { groups[i].alpha = 1f; cards[i].anchoredPosition = bases[i]; }
        }

        void PlayLevel1() { Launch(levelScene); }

        void Fight(FightSetup.Mode m) { FightSetup.Current = m; Launch(fightScene); }

        void Launch(string scene) { if (!busy) StartCoroutine(LaunchRoutine(scene)); }

        IEnumerator LaunchRoutine(string scene)
        {
            busy = true;
            Sfx.Play(Sfx.Id.Click);
            fader.blocksRaycasts = true;
            for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime) { fader.alpha = t / 0.35f; yield return null; }
            fader.alpha = 1f;
            returnToSelect = true;
            SceneManager.LoadScene(scene);
        }

        // ================================================================ per-frame motion
        void Update()
        {
            float t = Time.unscaledTime;

            // slow "Ken Burns" drift on the key art
            float s = 1.03f + 0.025f * Mathf.Sin(t * 2f * Mathf.PI / 40f);
            bgDrift.localScale = new Vector3(s, s, 1f);
            bgDrift.anchoredPosition = new Vector2(8f * Mathf.Sin(t * 2f * Mathf.PI / 53f), 0f);

            if (tapText != null)
            {
                var col = tapText.color; col.a = 0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(t * 2.6f)); tapText.color = col;
            }

            // rising embers
            float w = emberLayer.rect.width, h = emberLayer.rect.height, dt = Time.unscaledDeltaTime;
            foreach (var e in embers)
            {
                var p = e.rt.anchoredPosition;
                p.y += e.speed * dt;
                if (p.y > h * 0.5f + 30f) { p.y = -h * 0.5f - 30f; e.x = Random.Range(-0.5f, 0.5f); }
                p.x = e.x * w + Mathf.Sin(t * 0.8f + e.phase) * e.sway;
                e.rt.anchoredPosition = p;
                float life = Mathf.Clamp01((p.y + h * 0.5f) / h);                        // 0 at the bottom, 1 at the top
                float a = e.alpha * Mathf.Sin(life * Mathf.PI) * (0.75f + 0.25f * Mathf.Sin(t * 5f + e.phase));
                e.img.color = new Color(1f, 0.72f + 0.12f * Mathf.Sin(e.phase), 0.32f, a);
            }

            // keyboard / gamepad: any key continues, Escape goes back
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && !busy)
            {
                if (!onSelect && kb.anyKey.wasPressedThisFrame) ShowSelect();
                else if (onSelect && kb.escapeKey.wasPressedThisFrame) ShowTitle();
            }
        }

        // ================================================================ helpers
        static float Ease(float x) { x = Mathf.Clamp01(x); return 1f - Mathf.Pow(1f - x, 3f); }

        static Sprite LoadSprite(string path)
        {
            var s = Resources.Load<Sprite>(path);
            if (s != null) return s;
            var tex = Resources.Load<Texture2D>(path);
            return tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect) : null;
        }

        /// <summary>A 9-sliceable rounded rectangle, filled (stroke = 0) or as an outline of the given width in pixels.</summary>
        static Sprite RoundedSprite(int size, int radius, float stroke)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius), qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float sd = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;   // < 0 inside
                    float a = Mathf.Clamp01(0.5f - sd);
                    if (stroke > 0f) a *= Mathf.Clamp01(0.5f + sd + stroke);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            float b = radius + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        /// <summary>White with alpha fading out: vertical = opaque at the bottom, horizontal = opaque at the left.</summary>
        static Sprite Gradient(bool vertical)
        {
            int n = 256;
            var tex = new Texture2D(vertical ? 2 : n, vertical ? n : 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int i = 0; i < n; i++)
            {
                // solid for the first part (where text sits), then a smooth fade to clear
                float plateau = vertical ? 0.4f : 0.3f;
                float a = Mathf.Clamp01((1f - i / (float)(n - 1)) / (1f - plateau));
                a = a * a * (3f - 2f * a);
                var c = new Color(1f, 1f, 1f, a);
                if (vertical) { tex.SetPixel(0, i, c); tex.SetPixel(1, i, c); } else { tex.SetPixel(i, 0, c); tex.SetPixel(i, 1, c); }
            }
            tex.Apply();
            // FullRect: the default tight mesh trims transparent texels, which collapses a 2-px gradient to nothing
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
