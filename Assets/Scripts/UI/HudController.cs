using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BalHanuman
{
    /// <summary>
    /// Mobile HUD: Hanuman plate (blessing + energy meters), objective card, mango pill, pause, journey tracker,
    /// labelled touch buttons, contextual interact button, toasts and screen fade. Built at runtime from the UiSkin.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        public UiSkin skin;
        public Canvas Canvas { get; private set; }

        // where each region begins (world X) and its display name
        static readonly float[] RegionStart = { 0f, 23f, 47f, 70f, 97f, 117f };
        static readonly string[] RegionName = { "THE REQUEST", "FOREST PATH", "THE STREAM", "HANGING VINES", "MOVING BRIDGE", "FORGOTTEN TEMPLE" };
        const float TempleX = 142f;
        const float SageX = 9f;

        RectTransform safe, controls, interactGroup, climbGroup;
        Rect lastSafe; Vector2 lastScreen;
        Text regionText, titleText, subText, mangoText, trackerText, toastText, interactLabel;
        Image blessingFill, energyFill, trackerFill, trackerDot, rollBg, interactBg, fade;
        RectTransform mangoPill, trackerPanel, mangoIcon;
        GameObject toastPanel;
        CanvasGroup interactCg, climbCg;
        Coroutine toastRoutine;
        int mangoes, totalMangoes = 1;
        float blessingShown, energyFlash;
        bool hasTarget;
        string interactText = "";

        void Awake()
        {
            UiKit.Init(skin);
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            var cgo = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            Canvas = cgo.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay; Canvas.sortingOrder = 50;
            var sc = cgo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;

            safe = UiKit.Stretch("SafeArea", Canvas.transform);
            Build(safe);
            ApplySafeArea();
        }

        void ApplySafeArea()
        {
            var r = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            lastSafe = r; lastScreen = new Vector2(Screen.width, Screen.height);
            safe.anchorMin = new Vector2(r.xMin / Screen.width, r.yMin / Screen.height);
            safe.anchorMax = new Vector2(r.xMax / Screen.width, r.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------ layout
        void Build(Transform root)
        {
            var S = UiKit.Skin;
            Vector2 tl = new Vector2(0, 1), tr = new Vector2(1, 1), tc = new Vector2(0.5f, 1), bl = Vector2.zero, br = new Vector2(1, 0), bc = new Vector2(0.5f, 0), c = new Vector2(0.5f, 0.5f);

            // ---------- Hanuman plate (top left)
            var plate = UiKit.Frame("HanumanPlate", root, S.hudMid, tl, tl, new Vector2(40, -30), new Vector2(520, 156));
            UiKit.Portrait("Portrait", plate.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), 118, S.heroPortrait);
            UiKit.Title("Name", plate.transform, "BAL HANUMAN", 27, TextAnchor.MiddleLeft, tl, new Vector2(0, 0.5f), new Vector2(156, -36), new Vector2(330, 34), UiKit.Cream);
            UiKit.Img("HeartIcon", plate.transform, S.iconHeart, tl, tl, tl, new Vector2(156, -64), new Vector2(30, 30), Color.white);
            blessingFill = UiKit.Bar("BlessingBar", plate.transform, S.fillCoral, tl, tl, new Vector2(196, -66), new Vector2(300, 26));
            UiKit.Img("EnergyIcon", plate.transform, S.iconEnergy, tl, tl, tl, new Vector2(156, -104), new Vector2(30, 30), Color.white);
            energyFill = UiKit.Bar("EnergyBar", plate.transform, S.fillGold, tl, tl, new Vector2(196, -106), new Vector2(300, 26));
            blessingFill.fillAmount = 0f;

            // ---------- objective card (top centre)
            var card = UiKit.Frame("ObjectiveCard", root, S.hudWide, tc, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(860, 170));
            regionText = UiKit.Body("Region", card.transform, "THE REQUEST", 21, TextAnchor.MiddleCenter, tc, c, new Vector2(0, -26), new Vector2(600, 28), UiKit.Gold, FontStyle.Bold);
            titleText = UiKit.Title("Title", card.transform, "Talk to the sage", 38, TextAnchor.MiddleCenter, tc, c, new Vector2(0, -68), new Vector2(740, 52), UiKit.Cream, true);
            subText = UiKit.Body("Subtitle", card.transform, "", 25, TextAnchor.MiddleCenter, tc, c, new Vector2(0, -108), new Vector2(740, 34), UiKit.Muted, FontStyle.Normal, true);
            UiKit.Img("Divider", card.transform, S.divider, tc, tc, tc, new Vector2(0, -138), new Vector2(380, 22), Color.white);

            // ---------- mango pill + pause (top right)
            var pause = UiKit.Img("PauseButton", root, S.btnPause, tr, tr, tr, new Vector2(-40, -34), new Vector2(78, 78), Color.white, true);
            pause.raycastPadding = new Vector4(-14f, -14f, -14f, -14f);
            var pb = pause.gameObject.AddComponent<Button>(); pb.targetGraphic = pause; pb.transition = Selectable.Transition.ColorTint;
            pb.onClick.AddListener(() => GameSession.Current?.TogglePause());
            var pill = UiKit.Frame("MangoPill", root, S.hudPill, tr, tr, new Vector2(-134, -38), new Vector2(262, 80));
            mangoPill = pill.rectTransform;
            mangoIcon = UiKit.Img("MangoIcon", pill.transform, S.iconMango, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(44, 44), Color.white).rectTransform;
            mangoText = UiKit.Body("Count", pill.transform, "0 / 0", 40, TextAnchor.MiddleCenter, c, c, new Vector2(20, 0), new Vector2(170, 60), UiKit.Cream, FontStyle.Bold);

            // ---------- journey tracker (bottom centre)
            var track = UiKit.Frame("JourneyTracker", root, S.hudMid, bc, new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(540, 120));
            trackerPanel = track.rectTransform;
            trackerText = UiKit.Body("Place", track.transform, "", 23, TextAnchor.MiddleCenter, tc, c, new Vector2(0, -36), new Vector2(480, 30), UiKit.Cream, FontStyle.Bold, true);
            var tb = UiKit.Bar("TrackBar", track.transform, S.fillTeal, bc, new Vector2(0.5f, 0), new Vector2(-18, 26), new Vector2(380, 24));
            trackerFill = tb;
            trackerDot = UiKit.Img("Dot", tb.transform.parent, S.circle, new Vector2(0, 0.5f), new Vector2(0, 0.5f), c, Vector2.zero, new Vector2(20, 20), UiKit.Cream);
            UiKit.Img("Flag", track.transform, S.iconFlag, bc, bc, new Vector2(0.5f, 0), new Vector2(228, 24), new Vector2(34, 34), Color.white);

            // ---------- touch controls (round buttons; the icon is part of each sprite)
            controls = UiKit.Stretch("Controls", root);
            MakeTouch(controls, "BtnLeft", TouchButton.Kind.Left, S.btnLeft, bl, new Vector2(150, 130), 140, null, out _);
            MakeTouch(controls, "BtnRight", TouchButton.Kind.Right, S.btnRight, bl, new Vector2(322, 130), 140, null, out _);
            MakeTouch(controls, "BtnBend", TouchButton.Kind.Down, S.btnBend, bl, new Vector2(236, 304), 112, "Bend", out _);
            MakeTouch(controls, "BtnJump", TouchButton.Kind.Jump, S.btnJump, br, new Vector2(-150, 150), 168, "Jump", out _);
            MakeTouch(controls, "BtnRoll", TouchButton.Kind.Roll, S.btnRoll, br, new Vector2(-340, 122), 130, "Roll", out rollBg);

            // ---------- interact (only when something can be used) and climb (only on vines)
            interactGroup = UiKit.Stretch("InteractGroup", root); interactCg = interactGroup.gameObject.AddComponent<CanvasGroup>();
            MakeTouch(interactGroup, "BtnInteract", TouchButton.Kind.Interact, S.btnInteract, br, new Vector2(-340, 292), 130, "", out interactBg);
            interactLabel = interactGroup.Find("BtnInteract_Label").GetComponent<Text>();
            interactCg.alpha = 0f; interactCg.blocksRaycasts = false;

            climbGroup = UiKit.Stretch("ClimbGroup", root); climbCg = climbGroup.gameObject.AddComponent<CanvasGroup>();
            MakeTouch(climbGroup, "BtnUp", TouchButton.Kind.Up, S.btnClimb, br, new Vector2(-150, 352), 124, "Climb", out _);
            climbCg.alpha = 0f; climbCg.blocksRaycasts = false;

            // ---------- toast
            var toast = UiKit.Frame("Toast", root, S.hudMid, tc, new Vector2(0.5f, 1), new Vector2(0, -218), new Vector2(980, 112));
            toastPanel = toast.gameObject;
            toastText = UiKit.Body("Text", toast.transform, "", 31, TextAnchor.MiddleCenter, c, c, Vector2.zero, new Vector2(880, 90), UiKit.Cream, FontStyle.Normal, true);
            toastPanel.SetActive(false);

            // ---------- fade
            fade = UiKit.Img("Fade", root, S.white, Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0), false);
            fade.preserveAspect = false;
            fade.transform.SetAsLastSibling();
        }

        void MakeTouch(Transform parent, string name, TouchButton.Kind kind, Sprite disc, Vector2 anchor, Vector2 pos, float size, string label, out Image bg)
        {
            var b = UiKit.Img(name, parent, disc, anchor, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size), Color.white, true);
            b.preserveAspect = true;
            b.raycastPadding = new Vector4(-22f, -22f, -22f, -22f);   // bigger tap area than the visible disc
            var t = b.gameObject.AddComponent<TouchButton>(); t.kind = kind; t.visual = b;
            // labels sit under the button and do not scale with the press animation
            var lab = UiKit.Label(name + "_Label", parent, label ?? "", UiKit.Skin.bodyFont, 24, TextAnchor.UpperCenter, anchor, new Vector2(0.5f, 1f), new Vector2(pos.x, pos.y - size / 2f - 1f), new Vector2(320, 34), UiKit.Cream, FontStyle.Bold, true);
            var ol = lab.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0.03f, 0.06f, 0.14f, 0.95f); ol.effectDistance = new Vector2(2f, -2f);
            bg = b;
        }

        // ------------------------------------------------------------------ public API
        public void SetObjective(string title, string subtitle)
        {
            if (titleText) titleText.text = (title ?? "").TrimEnd('.');
            if (subText) subText.text = subtitle ?? "";
        }

        public void SetMangoCount(int n, int total)
        {
            mangoes = n; totalMangoes = Mathf.Max(1, total);
            if (mangoText) mangoText.text = n + " / " + Mathf.Max(total, n);
            if (mangoPill) StartCoroutine(Punch(mangoPill));
        }

        /// <summary>Where the mango icon is on screen (pixels), so collected fruit can fly to it.</summary>
        public Vector2 MangoIconScreenPos
        {
            get
            {
                var rt = mangoIcon != null ? mangoIcon : mangoPill;
                return rt != null ? (Vector2)rt.position : new Vector2(Screen.width * 0.9f, Screen.height * 0.94f);
            }
        }

        /// <summary>The counter bumps when a flying mango arrives.</summary>
        public void PulseMango() { if (mangoPill) StartCoroutine(Punch(mangoPill)); }

        /// <summary>Big "Level 1 / title" card that fades in and out when the level opens.</summary>
        public void ShowTitleCard(string kicker, string title)
        {
            StartCoroutine(TitleCardRoutine(kicker, title));
        }

        IEnumerator TitleCardRoutine(string kicker, string title)
        {
            var c = new Vector2(0.5f, 0.5f);
            var root = UiKit.Stretch("TitleCard", safe);
            if (fade != null) root.SetSiblingIndex(fade.transform.GetSiblingIndex());
            var cg = root.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
            // soft dark band so the title reads over the bright backdrop
            var band = UiKit.Stretch("Band", root);
            band.anchorMin = new Vector2(0f, 0.5f); band.anchorMax = new Vector2(1f, 0.5f);
            band.offsetMin = new Vector2(0f, 170f - 125f); band.offsetMax = new Vector2(0f, 170f + 125f);
            var bandImg = band.gameObject.AddComponent<Image>(); bandImg.color = new Color(0.04f, 0.08f, 0.17f, 0.62f); bandImg.raycastTarget = false;
            var k = UiKit.Body("Kicker", root, kicker, 34, TextAnchor.MiddleCenter, c, c, new Vector2(0, 238), new Vector2(900, 50), UiKit.Gold, FontStyle.Bold);
            var t = UiKit.Title("Title", root, title, 78, TextAnchor.MiddleCenter, c, c, new Vector2(0, 170), new Vector2(1500, 110), UiKit.Cream, true);
            UiKit.Img("Line", root, UiKit.Skin.divider, c, c, c, new Vector2(0, 100), new Vector2(560, 30), Color.white);
            foreach (var g in new Graphic[] { k, t })
            {
                var o = g.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.05f, 0.08f, 0.16f, 0.85f); o.effectDistance = new Vector2(3, -3);
            }
            const float inT = 0.7f, hold = 2.0f, outT = 0.8f;
            float time = 0f;
            while (time < inT + hold + outT)
            {
                time += Time.unscaledDeltaTime;
                float a = time < inT ? time / inT : time > inT + hold ? 1f - (time - inT - hold) / outT : 1f;
                cg.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(a));
                root.localScale = Vector3.one * (1f + 0.04f * (1f - Mathf.Clamp01(time / (inT + hold + outT))));
                yield return null;
            }
            Destroy(root.gameObject);
        }

        IEnumerator Punch(RectTransform rt)
        {
            float t = 0;
            while (t < 0.25f)
            {
                t += Time.unscaledDeltaTime;
                rt.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(t / 0.25f * Mathf.PI));
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        public void SetPrompt(string prompt)
        {
            hasTarget = !string.IsNullOrEmpty(prompt);
            if (hasTarget) interactText = prompt;
            if (interactLabel) interactLabel.text = interactText;
            if (interactCg) interactCg.blocksRaycasts = hasTarget;
        }

        public void ShowClimbButtons(bool show)
        {
            if (climbCg == null) return;
            climbCg.blocksRaycasts = show;
            climbShown = show;
        }
        bool climbShown;

        public void ShowToast(string text, float seconds = 3f)
        {
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(text, seconds));
        }

        IEnumerator ToastRoutine(string text, float seconds)
        {
            toastText.text = text;
            toastPanel.SetActive(true);
            if (!toastPanel.TryGetComponent(out CanvasGroup cg)) cg = toastPanel.AddComponent<CanvasGroup>();
            var rt = (RectTransform)toastPanel.transform;
            float t = 0;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(Mathf.Min(t, seconds - t) * 4f);
                cg.alpha = a;
                rt.anchoredPosition = new Vector2(0, -218 + (1f - a) * 16f);
                yield return null;
            }
            toastPanel.SetActive(false);
        }

        public IEnumerator FadeTo(float alpha, float duration)
        {
            float start = fade.color.a, t = 0;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                fade.color = new Color(0, 0, 0, Mathf.Lerp(start, alpha, t / duration));
                yield return null;
            }
            fade.color = new Color(0, 0, 0, alpha);
        }

        public void SetControlsVisible(bool v)
        {
            if (controls) controls.gameObject.SetActive(v);
            if (!v) { SetPrompt(null); ShowClimbButtons(false); }
        }

        // ------------------------------------------------------------------ per-frame
        void Update()
        {
            if (Screen.safeArea != lastSafe || lastScreen.x != Screen.width || lastScreen.y != Screen.height) ApplySafeArea();

            // contextual buttons fade in and out
            float dt = Time.unscaledDeltaTime * 10f;
            if (interactCg) interactCg.alpha = Mathf.MoveTowards(interactCg.alpha, hasTarget ? 1f : 0f, dt);
            if (climbCg) climbCg.alpha = Mathf.MoveTowards(climbCg.alpha, climbShown ? 1f : 0f, dt);
            if (hasTarget && interactBg != null) interactBg.transform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.unscaledTime * 6f));

            var gs = GameSession.Current;
            if (gs == null || gs.player == null) return;
            var p = gs.player;

            // meters: energy is the roll stamina, blessing fills as mangoes are gathered
            if (energyFill)
            {
                energyFill.fillAmount = Mathf.MoveTowards(energyFill.fillAmount, p.Energy01, Time.unscaledDeltaTime * 2.5f);
                energyFlash = Mathf.MoveTowards(energyFlash, 0f, Time.unscaledDeltaTime * 3f);
                energyFill.color = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.4f), energyFlash);
            }
            if (blessingFill)
            {
                blessingShown = Mathf.MoveTowards(blessingShown, Mathf.Clamp01(mangoes / (float)totalMangoes), Time.unscaledDeltaTime * 1.2f);
                blessingFill.fillAmount = blessingShown;
            }
            if (rollBg)
            {
                bool can = p.Energy01 >= p.rollCost;
                rollBg.color = can ? Color.white : new Color(0.6f, 0.62f, 0.7f, 0.9f);
            }

            // journey tracker
            float x = p.transform.position.x;
            int r = 0;
            for (int i = 0; i < RegionStart.Length; i++) if (x >= RegionStart[i]) r = i;
            if (regionText) regionText.text = RegionName[r];
            var q = gs.quest.State;
            string tail = "";
            if (q == QuestState.NotStarted || q == QuestState.FindBasket) tail = "  ·  " + Mathf.Max(0, Mathf.RoundToInt(TempleX - x)) + " m to the temple";
            else if (q == QuestState.BasketCollected || q == QuestState.ReturnToSage) tail = "  ·  " + Mathf.Max(0, Mathf.RoundToInt(Mathf.Abs(x - SageX))) + " m to the sage";
            if (trackerText) trackerText.text = RegionName[r] + tail;
            float prog = (q == QuestState.BasketCollected || q == QuestState.ReturnToSage) ? 1f - Mathf.Clamp01(Mathf.Abs(x - SageX) / TempleX) : Mathf.Clamp01(x / TempleX);
            if (trackerFill)
            {
                trackerFill.fillAmount = prog;
                var bar = (RectTransform)trackerFill.transform.parent;
                float w = bar.rect.width - bar.rect.height * 0.32f;
                trackerDot.rectTransform.anchoredPosition = new Vector2(bar.rect.height * 0.16f + w * prog, 0);
            }
        }

        /// <summary>Called by the player when a roll is refused for lack of energy.</summary>
        public void FlashEnergy() { energyFlash = 1f; }
    }
}
