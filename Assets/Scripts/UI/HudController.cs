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
        Text kickerText, titleText, subText, mangoText, trackerText, toastText, interactLabel;
        Image blessingFill, energyFill, trackerFill, trackerDot, rollBg, interactBg, fade;
        RectTransform mangoPill, trackerPanel, mangoIcon;
        GameObject toastPanel;
        CanvasGroup interactCg, climbCg;
        Coroutine toastRoutine;
        int mangoes, totalMangoes = 1;
        float blessingShown, energyFlash;
        bool hasTarget;
        string interactText = "";

        // one message slot under the journey tracker, shared by tips (toasts) and task messages: never two at once
        const float SlotY = -205f;
        // widest a touch-button label may be: the space between two neighbouring buttons
        const float LabelWidth = 170f;
        // centre line of the top HUD row (the Hanuman plate: 30 from the top, 156 tall)
        const float TopMid = -30f - 156f / 2f;
        RectTransform questMsg;
        CanvasGroup questCg;
        bool msgWanted, msgSwapping, titleCardOn;
        float msgShown, msgHoldLeft;
        string nextKicker, nextTitle, nextSub; float nextHold;

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

            // ---------- quest message (screen centre): slides in with each new task, moves away when it is done
            var card = UiKit.Frame("QuestMessage", root, S.hudWide, tc, tc, new Vector2(0, SlotY), new Vector2(860, 170));
            questMsg = card.rectTransform;
            questCg = card.gameObject.AddComponent<CanvasGroup>(); questCg.alpha = 0f; questCg.blocksRaycasts = false; questCg.interactable = false;
            kickerText = UiKit.Body("Kicker", card.transform, "THE REQUEST", 21, TextAnchor.MiddleCenter, tc, c, new Vector2(0, -26), new Vector2(600, 28), UiKit.Gold, FontStyle.Bold);
            titleText = UiKit.Title("Title", card.transform, "", 38, TextAnchor.MiddleCenter, tc, c, new Vector2(0, -68), new Vector2(740, 52), UiKit.Cream, true);
            subText = UiKit.Body("Subtitle", card.transform, "", 25, TextAnchor.MiddleCenter, tc, c, new Vector2(0, -108), new Vector2(740, 34), UiKit.Muted, FontStyle.Normal, true);
            UiKit.Img("Divider", card.transform, S.divider, tc, tc, tc, new Vector2(0, -138), new Vector2(380, 22), Color.white);

            // ---------- top right, from the corner inwards: How to Play (?) · pause · mango pill
            // Every top element is centred on the same line as the Hanuman plate (TopMid).
            const float btn = 78f, gap = 18f;
            var help = UiKit.Img("HelpButton", root, S.btnBlank != null ? S.btnBlank : S.btnPause, tr, tr, tr, new Vector2(-40, TopMid + btn / 2f), new Vector2(btn, btn), Color.white, true);
            help.raycastPadding = new Vector4(-14f, -14f, -14f, -14f);
            var hb = help.gameObject.AddComponent<Button>(); hb.targetGraphic = help; hb.transition = Selectable.Transition.ColorTint;
            hb.onClick.AddListener(() => GameSession.Current?.OpenRules());
            UiKit.Title("Glyph", help.transform, "?", 46, TextAnchor.MiddleCenter, c, c, new Vector2(1, 1), new Vector2(btn, btn), UiKit.Cream);

            var pause = UiKit.Img("PauseButton", root, S.btnPause, tr, tr, tr, new Vector2(-40 - btn - gap, TopMid + btn / 2f), new Vector2(btn, btn), Color.white, true);
            pause.raycastPadding = new Vector4(-14f, -14f, -14f, -14f);
            var pb = pause.gameObject.AddComponent<Button>(); pb.targetGraphic = pause; pb.transition = Selectable.Transition.ColorTint;
            pb.onClick.AddListener(() => GameSession.Current?.TogglePause());
            var pill = UiKit.Frame("MangoPill", root, S.hudPill, tr, tr, new Vector2(-40 - 2f * (btn + gap), TopMid + 40f), new Vector2(262, 80));
            mangoPill = pill.rectTransform;
            mangoIcon = UiKit.Img("MangoIcon", pill.transform, S.iconMango, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(44, 44), Color.white).rectTransform;
            mangoText = UiKit.Body("Count", pill.transform, "0 / 0", 40, TextAnchor.MiddleCenter, c, c, new Vector2(20, 0), new Vector2(170, 60), UiKit.Cream, FontStyle.Bold);

            // ---------- journey tracker (top centre)
            var track = UiKit.Frame("JourneyTracker", root, S.hudMid, tc, new Vector2(0.5f, 1), new Vector2(0, TopMid + 60f), new Vector2(540, 120));
            trackerPanel = track.rectTransform;
            trackerText = UiKit.Body("Place", track.transform, "", 23, TextAnchor.MiddleCenter, tc, c, new Vector2(0, -36), new Vector2(480, 30), UiKit.Cream, FontStyle.Bold, true);
            var tb = UiKit.Bar("TrackBar", track.transform, S.fillTeal, bc, new Vector2(0.5f, 0), new Vector2(-18, 26), new Vector2(380, 24));
            trackerFill = tb;
            trackerDot = UiKit.Img("Dot", tb.transform.parent, S.circle, new Vector2(0, 0.5f), new Vector2(0, 0.5f), c, Vector2.zero, new Vector2(20, 20), UiKit.Cream);
            UiKit.Img("Flag", track.transform, S.iconFlag, bc, bc, new Vector2(0.5f, 0), new Vector2(228, 24), new Vector2(34, 34), Color.white);

            // ---------- touch controls (round buttons; the icon is part of each sprite)
            controls = UiKit.Stretch("Controls", root);
            // (button sizes from the larger layout; positions follow the one-thumb-per-job rule)
            MakeTouch(controls, "BtnLeft", TouchButton.Kind.Left, S.btnLeft, bl, new Vector2(150, 130), 140, null, out _);
            MakeTouch(controls, "BtnRight", TouchButton.Kind.Right, S.btnRight, bl, new Vector2(322, 130), 140, null, out _);
            // left thumb only moves; every action sits under the right thumb. Bend sits right above Jump
            // (its label clears Jump's top edge); on vines the Climb button appears above Bend: up over down.
            MakeTouch(controls, "BtnBend", TouchButton.Kind.Down, S.btnBend, br, new Vector2(-150, 336), 112, "Bend", out _);
            MakeTouch(controls, "BtnJump", TouchButton.Kind.Jump, S.btnJump, br, new Vector2(-150, 150), 168, "Jump", out _);
            MakeTouch(controls, "BtnRoll", TouchButton.Kind.Roll, S.btnRoll, br, new Vector2(-340, 122), 130, "Roll", out rollBg);

            // ---------- interact (only when something can be used) and climb (only on vines)
            interactGroup = UiKit.Stretch("InteractGroup", root); interactCg = interactGroup.gameObject.AddComponent<CanvasGroup>();
            // sits high enough that its label clears the Roll button below it
            MakeTouch(interactGroup, "BtnInteract", TouchButton.Kind.Interact, S.btnInteract, br, new Vector2(-340, 292), 130, "", out interactBg);
            interactLabel = interactGroup.Find("BtnInteract_Label").GetComponent<Text>();
            interactCg.alpha = 0f; interactCg.blocksRaycasts = false;

            climbGroup = UiKit.Stretch("ClimbGroup", root); climbCg = climbGroup.gameObject.AddComponent<CanvasGroup>();
            MakeTouch(climbGroup, "BtnUp", TouchButton.Kind.Up, S.btnClimb, br, new Vector2(-150, 498), 124, "Climb", out _);
            climbCg.alpha = 0f; climbCg.blocksRaycasts = false;

            // ---------- toast
            var toast = UiKit.Frame("Toast", root, S.hudMid, tc, new Vector2(0.5f, 1), new Vector2(0, SlotY), new Vector2(980, 112));
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
            // labels sit under the button and do not scale with the press animation.
            // Every label lives in a fixed box (one line, LabelWidth wide): longer text shrinks to fit
            // (down to 16) instead of spilling over the neighbouring buttons.
            var lab = UiKit.Label(name + "_Label", parent, label ?? "", UiKit.Skin.bodyFont, 24, TextAnchor.UpperCenter, anchor, new Vector2(0.5f, 1f), new Vector2(pos.x, pos.y - size / 2f - 1f), new Vector2(LabelWidth, 34), UiKit.Cream, FontStyle.Bold, true, true);
            lab.resizeTextMinSize = 16; lab.horizontalOverflow = HorizontalWrapMode.Wrap; lab.alignment = TextAnchor.MiddleCenter;
            var ol = lab.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0.03f, 0.06f, 0.14f, 0.95f); ol.effectDistance = new Vector2(2f, -2f);
            bg = b;
        }

        // ------------------------------------------------------------------ public API
        /// <summary>
        /// Show a new task as a message in the middle of the screen. The previous message moves away first.
        /// holdSeconds &lt;= 0 keeps it up until the next task; otherwise it moves away after that long on screen.
        /// A null title just moves the current message away.
        /// </summary>
        public void SetObjective(string title, string subtitle, string kicker = "NEW TASK", float holdSeconds = 5f)
        {
            nextTitle = string.IsNullOrEmpty(title) ? null : title.TrimEnd('.');
            nextSub = subtitle ?? ""; nextKicker = kicker ?? ""; nextHold = holdSeconds;
            msgSwapping = true;                  // Update hides the old one, then applies these
        }

        void UpdateQuestMessage()
        {
            if (questCg == null) return;
            float dt = Time.unscaledDeltaTime;
            if (msgSwapping && msgShown <= 0f)
            {
                msgSwapping = false;
                msgWanted = nextTitle != null;
                if (msgWanted)
                {
                    titleText.text = nextTitle; subText.text = nextSub; kickerText.text = nextKicker;
                    msgHoldLeft = nextHold;
                }
            }
            // stays out of the way of tips, dialogues, the pause/fail screens and the level title
            bool show = msgWanted && !msgSwapping && !titleCardOn && !ToastShowing && Time.timeScale > 0f;
            msgShown = Mathf.MoveTowards(msgShown, show ? 1f : 0f, dt * (show ? 2.5f : 3.5f));
            float e = Mathf.SmoothStep(0f, 1f, msgShown);
            questCg.alpha = e;
            // comes in rising from below, leaves floating upward
            questMsg.anchoredPosition = new Vector2(0f, SlotY + (1f - e) * (show ? -30f : 40f));
            questMsg.localScale = Vector3.one * (0.9f + 0.1f * e);
            if (show && msgShown >= 1f && msgHoldLeft > 0f)
            {
                msgHoldLeft -= dt;
                if (msgHoldLeft <= 0f) msgWanted = false;
            }
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
            titleCardOn = true;
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
            titleCardOn = false;
        }

        /// <summary>
        /// "How to play" box shown before the level starts: the gold frame with a see-through middle over a soft dim.
        /// The game waits (time stopped) until the player taps the button or presses Enter / Space.
        /// </summary>
        public void ShowRules(string buttonLabel, System.Action onClose)
        {
            StartCoroutine(RulesRoutine(buttonLabel, onClose));
        }

        IEnumerator RulesRoutine(string buttonLabel, System.Action onClose)
        {
            var S = UiKit.Skin;
            Vector2 c = new Vector2(0.5f, 0.5f), tc = new Vector2(0.5f, 1f), bc = new Vector2(0.5f, 0f);
            Vector2 tl = new Vector2(0f, 1f), tr = new Vector2(1f, 1f);

            // light full-screen dim (outside the safe area too); the glass inside the box does most of the work
            var root = UiKit.Stretch("RulesScreen", Canvas.transform);   // added last, so it sits over the whole HUD
            var cg = root.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0f;
            var dim = root.gameObject.AddComponent<Image>(); dim.color = new Color(0.02f, 0.04f, 0.09f, 0.55f); dim.raycastTarget = true;

            // box = dark-blue see-through glass (shaped exactly like the inside of the frame) + gold frame on top.
            // Sized to sit between the top HUD and the touch buttons.
            Vector2 boxSize = new Vector2(1300, 700);
            var box = UiKit.Rect("RulesBox", root, c, c, c, new Vector2(0, -16), boxSize);
            var glass = UiKit.Frame("Glass", box, S.panelFill != null ? S.panelFill : S.white, c, c, Vector2.zero, boxSize, new Color(0.04f, 0.10f, 0.24f, 0.95f));   // deep navy glass; the project blends in linear space, so 0.95 still lets the level show through faintly
            glass.rectTransform.anchorMin = Vector2.zero; glass.rectTransform.anchorMax = Vector2.one; glass.rectTransform.sizeDelta = Vector2.zero;
            var frame = UiKit.Frame("Frame", box, S.panelFrame != null ? S.panelFrame : S.panelLarge, c, c, Vector2.zero, boxSize);
            frame.rectTransform.anchorMin = Vector2.zero; frame.rectTransform.anchorMax = Vector2.one; frame.rectTransform.sizeDelta = Vector2.zero;
            var bt = box.transform;

            // every line gets a dark outline so it stays crisp over the glass and whatever shows through it
            Text Crisp(Text t) { var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.03f, 0.07f, 0.16f, 0.9f); o.effectDistance = new Vector2(1.6f, -1.6f); return t; }
            var navyLine = new Color(1f, 0.85f, 0.45f, 0.45f);

            Crisp(UiKit.Body("Kicker", bt, "LEVEL 1  ·  THE MISSING FRUIT OFFERING", 24, TextAnchor.MiddleCenter, tc, c, new Vector2(0, -62), new Vector2(1100, 32), UiKit.Gold, FontStyle.Bold));
            Crisp(UiKit.Title("Title", bt, "How to Play", 58, TextAnchor.MiddleCenter, tc, c, new Vector2(0, -110), new Vector2(1100, 70), UiKit.Cream));
            UiKit.Img("Divider", bt, S.divider, tc, tc, tc, new Vector2(0, -152), new Vector2(460, 24), Color.white);

            // ---- Controls: a small table, action names in gold, how-to in cream
            Crisp(UiKit.Title("ControlsHead", bt, "Controls", 34, TextAnchor.MiddleLeft, tl, new Vector2(0f, 0.5f), new Vector2(100, -206), new Vector2(460, 44), UiKit.Gold));
            var names = Crisp(UiKit.Body("ControlNames", bt, "Run\nJump\nRoll\nBend\nClimb\nInteract", 28, TextAnchor.UpperLeft, tl, tl, new Vector2(100, -240), new Vector2(140, 320), UiKit.Gold, FontStyle.Bold));
            var how = Crisp(UiKit.Body("ControlKeys", bt,
                "Arrow buttons  ·  A / D\n" +
                "Jump button  ·  Space\n" +
                "Roll button  ·  Shift\n" +
                "Bend button  ·  S\n" +
                "Climb button (vines)  ·  W\n" +
                "Hand button  ·  E",
                27, TextAnchor.UpperLeft, tl, tl, new Vector2(240, -241), new Vector2(330, 320), UiKit.Cream));
            names.lineSpacing = how.lineSpacing = 1.22f;

            // thin gold line between the two columns
            UiKit.Img("ColumnLine", bt, S.white, tl, tl, tl, new Vector2(590, -204), new Vector2(2, 330), navyLine).preserveAspect = false;

            // ---- Rules: gold bullets, short lines
            Crisp(UiKit.Title("RulesHead", bt, "Rules", 34, TextAnchor.MiddleLeft, tl, new Vector2(0f, 0.5f), new Vector2(624, -206), new Vector2(580, 44), UiKit.Gold));
            const string dot = "<color=#F5C75C>•</color>  ";
            var rules = Crisp(UiKit.Body("Rules", bt,
                dot + "Find the missing mango basket near the\n     temple and bring it back to the sage.\n" +
                dot + "Help anyone who needs you. You can only\n     go on once it is done.\n" +
                dot + "Collect mangoes to fill your blessing.\n" +
                dot + "Rolling uses energy. It refills when you rest.\n" +
                dot + "Mind the water and pits. If you fall,\n     you go back to the last flag.",
                27, TextAnchor.UpperLeft, tl, tl, new Vector2(624, -240), new Vector2(600, 340), UiKit.Cream));
            rules.lineSpacing = 1.02f;

            bool closed = false;
            UiKit.PillButton("StartButton", bt, buttonLabel, bc, new Vector2(0, 84), new Vector2(340, 86), () => closed = true, true, 36);

            // fade in, wait for the player, fade out (all in unscaled time: the game is stopped meanwhile)
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.35f) { cg.alpha = t; yield return null; }
            cg.alpha = 1f;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            while (!closed)
            {
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) closed = true;
                yield return null;
            }
            Sfx.Play(Sfx.Id.Click);
            cg.blocksRaycasts = false;
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.3f) { cg.alpha = t; yield return null; }
            Destroy(root.gameObject);
            onClose?.Invoke();
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

        /// <summary>True while a tip is using the message slot (task messages wait for it).</summary>
        public bool ToastShowing => toastPanel != null && toastPanel.activeSelf;

        /// <summary>
        /// Short tip in the message slot. A newer tip replaces it; a task message waits until it is gone.
        /// dismissWhen (optional) ends it early, e.g. as soon as Hanuman moves.
        /// </summary>
        public void ShowToast(string text, float seconds = 3f, System.Func<bool> dismissWhen = null)
        {
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(text, seconds, dismissWhen));
        }

        IEnumerator ToastRoutine(string text, float seconds, System.Func<bool> dismissWhen)
        {
            toastText.text = text;
            toastPanel.SetActive(true);
            if (!toastPanel.TryGetComponent(out CanvasGroup cg)) cg = toastPanel.AddComponent<CanvasGroup>();
            var rt = (RectTransform)toastPanel.transform;
            const float fadeOut = 0.25f;
            float t = 0;
            while (t < seconds)
            {
                // hidden and on hold behind the level title, dialogues, pause and fail screens
                // also waits for a task message to finish leaving the slot, so the two never overlap
                bool held = titleCardOn || Time.timeScale == 0f || msgShown > 0f;
                if (!held)
                {
                    t += Time.unscaledDeltaTime;
                    if (dismissWhen != null && t < seconds - fadeOut && dismissWhen()) t = seconds - fadeOut;
                }
                float a = held ? 0f : Mathf.Clamp01(Mathf.Min(t, seconds - t) * 4f);
                cg.alpha = a;
                rt.anchoredPosition = new Vector2(0, SlotY + (1f - a) * 16f);
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

        /// <summary>Hides or shows the whole HUD (plate, tracker, counters, buttons, messages) — e.g. behind a full-screen popup.</summary>
        public void SetHudVisible(bool v)
        {
            if (safe == null) return;
            if (!safe.TryGetComponent(out CanvasGroup g)) g = safe.gameObject.AddComponent<CanvasGroup>();
            g.alpha = v ? 1f : 0f; g.blocksRaycasts = v; g.interactable = v;
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
            UpdateQuestMessage();

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
