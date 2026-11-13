using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BalHanuman.Fight
{
    /// <summary>Fighting-game HUD built at runtime from the shared UiSkin: health bars, Bhakti meter, boons, banners,
    /// combo counter, touch controls, pause menu, training move list, demo captions and the victory screen.</summary>
    public class FightHud : MonoBehaviour
    {
        FightDirector dir;
        RectTransform root;
        Image hpL, hpR, trailL, trailR;
        readonly Image[] meter = new Image[3];
        readonly Image[] meterGlow = new Image[3];
        readonly List<(Image bg, Image ring, Image lockImg, Text label, Text cost, PowerId p)> powers = new List<(Image, Image, Image, Text, Text, PowerId)>();
        Image ultBtn, ultGlow;
        Text banner, sub, comboNum, comboRank, comboName, hint, caption, captionSub, roundText;
        Image flash, dark, captionBar;
        readonly Image[] roundMarks = new Image[3];
        RectTransform pausePanel, victoryPanel, trainingPanel, controls;
        Text victoryStats, bossName;
        float bannerT, bannerDur, comboT, comboNameT, flashA, flashDecay, darkGoal, darkA, hintT;
        float shownL = 1f, shownR = 1f, trailVL = 1f, trailVR = 1f;
        Color flashColor = Color.white;
        static readonly Color Saffron = new Color(1f, 0.62f, 0.15f);
        static readonly Color Violet = new Color(0.72f, 0.4f, 1f);

        static Vector2 V(float x, float y) => new Vector2(x, y);

        // painted bars (Resources/Level02/UI): when present they replace the plain skin frames and fills
        static Sprite Art(string n) => Resources.Load<Sprite>("Level02/UI/" + n);
        bool paintedMeter, paintedBtns;
        Color tintL = Saffron, tintR = Violet;      // plain fills are tinted; the painted ones carry their own colour

        public static FightHud Build(FightDirector d)
        {
            var cgo = new GameObject("FightHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = V(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            var h = cgo.AddComponent<FightHud>();
            h.dir = d; h.root = (RectTransform)cgo.transform;
            h.Make();
            return h;
        }

        Text T(string name, Transform p, string s, int size, Vector2 anchor, Vector2 pos, Vector2 sz, Color c, bool title = true, TextAnchor al = TextAnchor.MiddleCenter)
        {
            var t = title ? UiKit.Title(name, p, s, size, al, anchor, anchor, pos, sz, c) : UiKit.Body(name, p, s, size, al, anchor, anchor, pos, sz, c, FontStyle.Bold);
            return t;
        }

        Image Fill(string name, Transform p, Color c, bool fromRight)
        {
            var img = UiKit.Img(name, p, UiKit.Skin.white, Vector2.zero, Vector2.one, V(0.5f, 0.5f), Vector2.zero, Vector2.zero, c);
            img.preserveAspect = false; img.type = Image.Type.Filled; img.fillMethod = Image.FillMethod.Horizontal; img.fillOrigin = fromRight ? 1 : 0;
            img.rectTransform.offsetMin = V(10, 9); img.rectTransform.offsetMax = V(-10, -9);
            return img;
        }

        void HealthBar(bool left, out Image fill, out Image trail)
        {
            var S = UiKit.Skin;
            var anchor = left ? V(0, 1) : V(1, 1);
            float sx = left ? 1f : -1f;
            var artFrame = Art(left ? "hp_frame" : "hp_frame_r"); var artFill = Art(left ? "hp_fill_hero" : "hp_fill_boss");
            if (artFrame != null && artFill != null)
            {
                const float w = 700f; float hgt = w * artFrame.rect.height / artFrame.rect.width;
                var fr = UiKit.Img(left ? "HpL" : "HpR", root, artFrame, anchor, anchor, V(left ? 0 : 1, 0.5f), V(sx * 130, -92), V(w, hgt));
                fr.preserveAspect = false;
                // the dark channel sits between the lotus and the rounded end; the painted fill goes inside it
                float x0 = left ? 0.085f : 0.015f, x1 = left ? 0.985f : 0.915f;
                Image Painted(string n, Color c)
                {
                    var im = UiKit.Img(n, fr.transform, artFill, Vector2.zero, Vector2.one, V(0.5f, 0.5f), Vector2.zero, Vector2.zero, c);
                    im.preserveAspect = false; im.type = Image.Type.Filled; im.fillMethod = Image.FillMethod.Horizontal; im.fillOrigin = left ? 0 : 1;
                    im.rectTransform.anchorMin = V(x0, 0.115f); im.rectTransform.anchorMax = V(x1, 0.725f);
                    im.rectTransform.offsetMin = im.rectTransform.offsetMax = Vector2.zero;
                    return im;
                }
                trail = Painted("Trail", new Color(1f, 0.95f, 0.85f, 0.85f));
                fill = Painted("Fill", Color.white);
                if (left) tintL = Color.white; else tintR = Color.white;
                var bossArt = left ? null : Resources.Load<Sprite>("Level02/Ahiravana/ahi_portrait");
                var ring2 = UiKit.Portrait(left ? "PortraitL" : "PortraitR", root, anchor, V(0.5f, 0.5f), V(sx * 100, -92), 160, left ? S.heroPortrait : bossArt);
                if (!left && bossArt == null)
                {
                    var mask = ring2.transform.parent;
                    mask.GetComponent<Image>().color = new Color(0.22f, 0.1f, 0.32f);
                    T("Glyph", mask, "A", 72, V(0.5f, 0.5f), V(0, -4), V(140, 140), new Color(0.95f, 0.55f, 1f));
                }
                var nm = T(left ? "NameL" : "NameR", root, left ? "HANUMAN" : "AHIRAVANA", 28, anchor, V(sx * 316, -34), V(380, 36), UiKit.Cream, true, TextAnchor.MiddleCenter);   // centred over the bar
                if (!left) bossName = nm;
                return;
            }
            var frame = UiKit.Img(left ? "HpL" : "HpR", root, S.barFrame, anchor, anchor, V(left ? 0 : 1, 0.5f), V(sx * 190, -78), V(700, 52));
            frame.preserveAspect = false;
            UiKit.Img("Back", frame.transform, S.white, Vector2.zero, Vector2.one, V(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.05f, 0.03f, 0.08f, 0.85f)).rectTransform.offsetMin = V(10, 9);
            frame.transform.GetChild(0).GetComponent<RectTransform>().offsetMax = V(-10, -9);
            trail = Fill("Trail", frame.transform, new Color(1f, 0.95f, 0.85f, 0.9f), !left);
            fill = Fill("Fill", frame.transform, left ? Saffron : Violet, !left);
            var shine = UiKit.Img("Shine", fill.transform, S.white, V(0, 0.55f), V(1, 1), V(0.5f, 1), Vector2.zero, Vector2.zero, new Color(1, 1, 1, 0.22f));
            shine.preserveAspect = false;
            var bossArt1 = left ? null : Resources.Load<Sprite>("Level02/Ahiravana/ahi_portrait");
            var ring = UiKit.Portrait(left ? "PortraitL" : "PortraitR", root, anchor, V(0.5f, 0.5f), V(sx * 100, -92), 160, left ? S.heroPortrait : bossArt1);
            if (!left && bossArt1 == null)
            {
                var mask = ring.transform.parent;
                mask.GetComponent<Image>().color = new Color(0.22f, 0.1f, 0.32f);
                T("Glyph", mask, "A", 72, V(0.5f, 0.5f), V(0, -4), V(140, 140), new Color(0.95f, 0.55f, 1f));
            }
            var name = T(left ? "NameL" : "NameR", root, left ? "HANUMAN" : "AHIRAVANA", 28, anchor, V(sx * 390, -30), V(380, 36), UiKit.Cream, true, left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            if (!left) bossName = name;
        }

        void Make()
        {
            var S = UiKit.Skin;
            flash = UiKit.Img("Flash", root, S.white, Vector2.zero, Vector2.one, V(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(1, 1, 1, 0)); flash.preserveAspect = false;
            dark = UiKit.Img("Dark", root, S.white, Vector2.zero, Vector2.one, V(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0)); dark.preserveAspect = false;

            HealthBar(true, out hpL, out trailL);
            HealthBar(false, out hpR, out trailR);

            // Bhakti meter: three segments under Hanuman's bar
            T("MeterLabel", root, "BHAKTI", 22, V(0, 1), V(200, -156), V(160, 30), new Color(1f, 0.85f, 0.5f), false, TextAnchor.MiddleLeft);
            for (int i = 0; i < 3; i++)
            {
                var mSeg = Art("bhakti_segment"); var mFill = Art("bhakti_fill");
                var fr = UiKit.Img("Meter" + i, root, mSeg != null ? mSeg : S.barFrame, V(0, 1), V(0, 1), V(0, 0.5f), V(300 + i * 170, -156), mSeg != null ? V(160, 160f * mSeg.rect.height / mSeg.rect.width) : V(150, 30)); fr.preserveAspect = false;
                meterGlow[i] = UiKit.Img("Glow", fr.transform, S.softGlow != null ? S.softGlow : S.circle, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), Vector2.zero, V(220, 90), new Color(1f, 0.7f, 0.2f, 0f));
                meter[i] = Fill("Fill", fr.transform, new Color(1f, 0.75f, 0.2f), false);
                meter[i].rectTransform.offsetMin = V(7, 6); meter[i].rectTransform.offsetMax = V(-7, -6);
                if (mSeg != null && mFill != null) { paintedMeter = true; meter[i].sprite = mFill; meter[i].rectTransform.offsetMin = V(5, 5); meter[i].rectTransform.offsetMax = V(-5, -5); }
            }

            // round info
            roundText = T("Round", root, "ROUND 1", 30, V(0.5f, 1), V(0, -40), V(400, 40), UiKit.Gold);
            for (int i = 0; i < 3; i++) roundMarks[i] = UiKit.Img("RoundMark" + i, root, S.circle, V(0.5f, 1), V(0.5f, 1), V(0.5f, 0.5f), V(-40 + i * 40, -82), V(26, 26), new Color(0.2f, 0.15f, 0.25f, 0.9f));
            var pause = UiKit.Img("Pause", root, S.btnPause != null ? S.btnPause : S.btnBlank, V(0.5f, 1), V(0.5f, 1), V(0.5f, 0.5f), V(0, -160), V(76, 76), Color.white, true);
            pause.gameObject.AddComponent<Button>().onClick.AddListener(() => dir.TogglePause());

            // banners
            banner = T("Banner", root, "", 130, V(0.5f, 0.5f), V(0, 120), V(1600, 200), UiKit.Gold);
            banner.gameObject.AddComponent<Outline>().effectColor = new Color(0.35f, 0.05f, 0.02f, 0.9f);
            sub = T("Sub", root, "", 40, V(0.5f, 0.5f), V(0, 10), V(1500, 60), UiKit.Cream, false);

            // combo counter
            comboNum = T("ComboNum", root, "", 96, V(0, 0.5f), V(190, 140), V(300, 120), new Color(1f, 0.85f, 0.4f), true, TextAnchor.MiddleLeft);
            comboNum.gameObject.AddComponent<Outline>().effectColor = new Color(0.4f, 0.08f, 0.02f, 1f);
            comboRank = T("ComboRank", root, "", 40, V(0, 0.5f), V(190, 60), V(500, 50), UiKit.Cream, true, TextAnchor.MiddleLeft);
            comboName = T("ComboName", root, "", 46, V(0, 0.5f), V(190, 5), V(700, 60), new Color(1f, 0.6f, 0.2f), true, TextAnchor.MiddleLeft);

            hint = T("Hint", root, "", 28, V(0.5f, 1), V(0, -232), V(1300, 40), UiKit.Cream, false);

            captionBar = UiKit.Img("CaptionBar", root, S.white, V(0.5f, 1), V(0.5f, 1), V(0.5f, 1), V(0, -180), V(1100, 110), new Color(0.04f, 0.02f, 0.08f, 0.7f));
            captionBar.preserveAspect = false;
            caption = T("Caption", captionBar.transform, "", 46, V(0.5f, 0.5f), V(0, 18), V(1080, 60), UiKit.Gold);
            captionSub = T("CaptionSub", captionBar.transform, "", 26, V(0.5f, 0.5f), V(0, -28), V(1080, 40), UiKit.Cream, false);
            captionBar.gameObject.SetActive(false);

            BuildControls();
            BuildPause();
            BuildVictory();
        }

        // ------------------------------------------------------------------ touch controls
        Image RoundButton(string name, Transform p, string label, Vector2 pos, float size, FightInput.Btn b, Color tint, int font = 30, string art = null)
        {
            // painted button (Resources/Level02/UI/<art>.png + <art>_pressed.png): the icon is part of the picture, so no text and no tint
            var spr = art != null ? Art(art) : null; var spr2 = art != null ? Art(art + "_pressed") : null;
            bool painted = spr != null;
            var img = UiKit.Img(name, p, painted ? spr : UiKit.Skin.btnBlank, V(1, 0), V(1, 0), V(0.5f, 0.5f), pos, V(size, size), painted ? Color.white : tint, true);
            if (painted) paintedBtns = true;
            var fb = img.gameObject.AddComponent<FightButton>(); fb.btn = b; fb.visual = img;
            if (painted) { fb.normal = spr; fb.pressed = spr2; }
            T("Label", img.transform, painted ? "" : label, font, V(0.5f, 0.5f), Vector2.zero, V(size, size), UiKit.Cream);
            return img;
        }

        void BuildControls()
        {
            var S = UiKit.Skin;
            controls = UiKit.Stretch("Controls", root);
            // joystick zone (left half, lower part)
            var zone = UiKit.Img("StickZone", controls, S.white, V(0, 0), V(0.42f, 0.55f), V(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0), true);
            zone.preserveAspect = false;
            var bs = UiKit.Img("StickBase", zone.transform, S.circle, V(0, 0), V(0, 0), V(0.5f, 0.5f), V(230, 210), V(250, 250), new Color(0.1f, 0.06f, 0.15f, 0.45f));
            UiKit.Img("Ring", bs.transform, S.portraitRing != null ? S.portraitRing : S.circle, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), Vector2.zero, V(260, 260), new Color(1f, 0.85f, 0.5f, 0.55f));
            var knob = UiKit.Img("Knob", bs.transform, S.circle, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), Vector2.zero, V(115, 115), new Color(1f, 0.8f, 0.35f, 0.85f));
            var js = zone.gameObject.AddComponent<VirtualJoystick>(); js.baseRect = bs.rectTransform; js.knob = knob.rectTransform;

            var c = controls;
            RoundButton("Mace", c, "GADA", V(-220, 190), 210, FightInput.Btn.Mace, new Color(1f, 0.85f, 0.6f), 38, "btn_gada");
            RoundButton("Kick", c, "TAIL\nKICK", V(-430, 120), 160, FightInput.Btn.Kick, Color.white, 28, "btn_tail");
            RoundButton("Jump", c, "JUMP", V(-110, 390), 135, FightInput.Btn.Jump, Color.white, 26, "btn_jump");
            RoundButton("Dodge", c, "DODGE", V(-600, 80), 130, FightInput.Btn.Dodge, new Color(0.8f, 0.9f, 1f), 24, "btn_dodge");
            ultBtn = RoundButton("Ultimate", c, "PANCH\nMUKHI", V(-400, 330), 175, FightInput.Btn.Ultimate, new Color(1f, 0.9f, 0.55f), 26, "btn_ultimate");
            ultGlow = UiKit.Img("UltGlow", ultBtn.transform, S.softGlow != null ? S.softGlow : S.circle, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), Vector2.zero, V(300, 300), new Color(1f, 0.8f, 0.3f, 0f));
            ultGlow.transform.SetAsFirstSibling();

            string[] labels = { "VAYU", "FLY", "FIRE", "TINY", "VAJRA", "GIANT" };
            string[] arts = { "icon_vayu", "icon_fly", "icon_fire", "icon_tiny", "icon_vajra", "icon_giant" };
            for (int i = 0; i < 6; i++)
            {
                var p = HanumanFighter.Boons[i];
                var bg = RoundButton("Power" + i, c, labels[i], V(-1390 + i * 118, 70), 108, FightInput.Btn.P1 + i, new Color(1f, 0.8f, 0.55f), 22, arts[i]);
                bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = V(1, 0);
                var ring = UiKit.Img("Active", bg.transform, S.circle, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), Vector2.zero, V(118, 118), new Color(0.5f, 1f, 1f, 0.6f));
                ring.type = Image.Type.Filled; ring.fillMethod = Image.FillMethod.Radial360; ring.transform.SetAsFirstSibling();
                var lk = UiKit.Img("Lock", bg.transform, S.circle, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), Vector2.zero, V(96, 96), new Color(0.05f, 0.03f, 0.08f, 0.75f));
                var cost = T("Cost", bg.transform, HanumanFighter.Cost(p) == 2 ? "••" : "•", 30, V(0.5f, 0f), V(0, -4), V(100, 30), new Color(1f, 0.75f, 0.2f));
                var label = bg.transform.Find("Label").GetComponent<Text>();
                T("Key", bg.transform, (i + 1).ToString(), 18, V(0.5f, 1f), V(0, -6), V(40, 24), UiKit.Muted, false);
                powers.Add((bg, ring, lk, label, cost, p));
            }
        }

        // ------------------------------------------------------------------ pause / victory / training panels
        RectTransform Panel(string name, Vector2 size)
        {
            var S = UiKit.Skin;
            var veil = UiKit.Img(name, root, S.white, Vector2.zero, Vector2.one, V(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.02f, 0.01f, 0.05f, 0.72f), true);
            veil.preserveAspect = false;
            var box = UiKit.Frame("Box", veil.transform, S.panelLarge, V(0.5f, 0.5f), V(0.5f, 0.5f), Vector2.zero, size, Color.white, true);
            veil.gameObject.SetActive(false);
            return box.rectTransform;
        }

        void BuildPause()
        {
            pausePanel = Panel("Pause", V(720, 760));
            T("Title", pausePanel, "PAUSED", 70, V(0.5f, 1), V(0, -90), V(600, 90), UiKit.Gold);
            float y = 150;
            UiKit.PillButton("Resume", pausePanel, "RESUME", V(0.5f, 0.5f), V(0, y), V(460, 100), () => dir.TogglePause(), true, 40);
            UiKit.PillButton("Restart", pausePanel, "RESTART FIGHT", V(0.5f, 0.5f), V(0, y - 120), V(460, 100), () => dir.Restart(FightSetup.Mode.Story), false, 34);
            UiKit.PillButton("Training", pausePanel, "TRAINING ROOM", V(0.5f, 0.5f), V(0, y - 240), V(460, 100), () => dir.Restart(FightSetup.Mode.Training), false, 34);
            UiKit.PillButton("Demo", pausePanel, "DEMO REEL", V(0.5f, 0.5f), V(0, y - 360), V(460, 100), () => dir.Restart(FightSetup.Mode.Demo), false, 34);
            UiKit.PillButton("Menu", pausePanel, "MAIN MENU", V(0.5f, 0.5f), V(0, y - 480), V(460, 100), () => dir.ToMenu(), false, 34);
        }

        void BuildVictory()
        {
            victoryPanel = Panel("Victory", V(900, 820));
            T("Title", victoryPanel, "VICTORY", 96, V(0.5f, 1), V(0, -100), V(800, 120), UiKit.Gold);
            T("Sub", victoryPanel, "Dharma prevails. Rama and Lakshmana are free.", 30, V(0.5f, 1), V(0, -180), V(820, 50), UiKit.Cream, false);
            victoryStats = T("Stats", victoryPanel, "", 32, V(0.5f, 0.5f), V(0, -40), V(760, 330), UiKit.Cream, false);
            victoryStats.lineSpacing = 1.25f;
            UiKit.PillButton("Again", victoryPanel, "FIGHT AGAIN", V(0.5f, 0), V(-200, 110), V(360, 100), () => dir.Restart(FightSetup.Mode.Story), true, 32);
            UiKit.PillButton("Menu", victoryPanel, "MAIN MENU", V(0.5f, 0), V(200, 110), V(360, 100), () => dir.ToMenu(), false, 32);
        }

        /// <summary>Training room: a scrolling move list on the left (tap to perform) plus dummy behaviour.</summary>
        public void BuildTraining(HanumanFighter h, AhiravanaFighter boss)
        {
            var S = UiKit.Skin;
            var panel = UiKit.Img("Training", root, S.white, V(0, 0.5f), V(0, 0.5f), V(0, 0.5f), V(20, 80), V(360, 540), new Color(0.04f, 0.02f, 0.08f, 0.72f), true);
            panel.preserveAspect = false;
            trainingPanel = panel.rectTransform;
            T("Title", panel.transform, "MOVE LIST", 32, V(0.5f, 1), V(0, -28), V(340, 44), UiKit.Gold);
            var view = UiKit.Img("View", panel.transform, S.white, V(0, 0), V(1, 1), V(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(1, 1, 1, 0.01f), true);
            view.preserveAspect = false; view.rectTransform.offsetMin = V(10, 80); view.rectTransform.offsetMax = V(-10, -56);
            view.gameObject.AddComponent<RectMask2D>();
            var content = UiKit.Rect("Content", view.transform, V(0, 1), V(1, 1), V(0.5f, 1), Vector2.zero, V(0, 0));
            var sr = view.gameObject.AddComponent<ScrollRect>(); sr.content = content; sr.horizontal = false; sr.viewport = view.rectTransform; sr.scrollSensitivity = 30f;
            float y = 0;
            void Row(string label, string keys, System.Action act)
            {
                var b = UiKit.Img("Row", content, S.white, V(0, 1), V(1, 1), V(0.5f, 1), V(0, -y), V(0, 56), new Color(0.2f, 0.1f, 0.25f, 0.85f), true);
                b.preserveAspect = false; b.rectTransform.offsetMin = new Vector2(4, b.rectTransform.offsetMin.y); b.rectTransform.offsetMax = new Vector2(-4, b.rectTransform.offsetMax.y);
                b.gameObject.AddComponent<Button>().onClick.AddListener(() => act());
                T("L", b.transform, label, 22, V(0, 0.5f), V(12, 9), V(320, 30), UiKit.Cream, false, TextAnchor.MiddleLeft);
                T("K", b.transform, keys, 17, V(0, 0.5f), V(12, -14), V(320, 24), new Color(1f, 0.8f, 0.45f), false, TextAnchor.MiddleLeft);
                y += 60;
            }
            foreach (var e in MoveList.Entries)
            {
                var entry = e;
                Row(entry.name, entry.input, () => dir.TrainingPerform(entry));
            }
            content.sizeDelta = V(0, y);
            // dummy behaviour toggle
            var mode = UiKit.PillButton("Dummy", panel.transform, "DUMMY: IDLE", V(0.5f, 0), V(0, 40), V(330, 60), () => { }, false, 24);
            var label2 = mode.GetComponentInChildren<Text>();
            mode.onClick.AddListener(() =>
            {
                boss.dummy = boss.dummy == AhiravanaFighter.Dummy.Idle ? AhiravanaFighter.Dummy.Block : boss.dummy == AhiravanaFighter.Dummy.Block ? AhiravanaFighter.Dummy.Fight : AhiravanaFighter.Dummy.Idle;
                label2.text = "DUMMY: " + boss.dummy.ToString().ToUpperInvariant();
            });
        }

        // ------------------------------------------------------------------ API
        public void ShowPause(bool on) { pausePanel.parent.gameObject.SetActive(on); }
        public void ShowControls(bool on) { controls.gameObject.SetActive(on); if (trainingPanel != null) trainingPanel.gameObject.SetActive(on); }

        public void ShowVictory(string stats)
        {
            victoryStats.text = stats;
            victoryPanel.parent.gameObject.SetActive(true);
            victoryPanel.localScale = Vector3.one * 0.6f;
        }

        public void Banner(string big, string small = "", float dur = 1.2f, Color? c = null, bool low = false)
        {
            banner.text = big; sub.text = small; banner.color = c ?? UiKit.Gold;
            banner.fontSize = big.Length > 16 ? 76 : big.Length > 11 ? 100 : 130;
            bannerT = bannerDur = dur;
            banner.rectTransform.anchoredPosition = V(0, low ? -250 : 120);
            sub.rectTransform.anchoredPosition = V(0, low ? -360 : 10);
        }

        public void Hint(string s, float dur = 4f) { hint.text = s; hintT = dur; }

        public void Caption(string title, string subText)
        {
            captionBar.gameObject.SetActive(!string.IsNullOrEmpty(title));
            caption.text = title; captionSub.text = subText;
        }

        public void Combo(int hits, string rank)
        {
            if (hits < 2) return;
            comboNum.text = hits + "<size=44> HITS</size>";
            comboRank.text = rank;
            comboT = 1.6f;
            comboNum.transform.localScale = Vector3.one * 1.3f;
        }

        public void ComboName(string n) { comboName.text = n; comboNameT = 2.2f; comboName.transform.localScale = Vector3.one * 1.4f; }

        public void Flash(Color c, float a, float decay = 3f) { flashColor = c; flashA = a; flashDecay = decay; }
        public void Dark(float a) { darkGoal = a; }

        public void Round(int r, int won)
        {
            roundText.text = r >= 4 ? "FINAL ROUND" : "ROUND " + r;
            for (int i = 0; i < 3; i++) roundMarks[i].color = i < won ? new Color(1f, 0.8f, 0.3f) : new Color(0.2f, 0.15f, 0.25f, 0.9f);
        }

        public void SetRoundVisible(bool on) { roundText.gameObject.SetActive(on); foreach (var m in roundMarks) m.gameObject.SetActive(on); }

        public void BossName(string n) { if (bossName != null) bossName.text = n; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var h = dir.hanuman; var b = dir.boss;
            if (h != null && b != null)
            {
                float l = Mathf.Clamp01(h.hp / h.maxHp), r = Mathf.Clamp01(b.hp / b.maxHp);
                shownL = Mathf.MoveTowards(shownL, l, dt * 3f); shownR = Mathf.MoveTowards(shownR, r, dt * 3f);
                trailVL = l > trailVL ? l : Mathf.MoveTowards(trailVL, l, dt * 0.45f);
                trailVR = r > trailVR ? r : Mathf.MoveTowards(trailVR, r, dt * 0.45f);
                hpL.fillAmount = shownL; hpR.fillAmount = shownR; trailL.fillAmount = trailVL; trailR.fillAmount = trailVR;
                hpL.color = l < 0.3f ? Color.Lerp(tintL, new Color(1f, 0.2f, 0.1f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)) : tintL;
                hpR.color = b.immortal && r < 0.02f ? Color.Lerp(tintR, new Color(1f, 0.85f, 1f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)) : tintR;

                for (int i = 0; i < 3; i++)
                {
                    float f = Mathf.Clamp01(h.bhakti - i);
                    meter[i].fillAmount = f;
                    meter[i].color = paintedMeter ? (f >= 1f ? Color.white : new Color(0.9f, 0.7f, 0.5f)) : f >= 1f ? new Color(1f, 0.85f, 0.35f) : new Color(0.85f, 0.5f, 0.15f);
                    var g = meterGlow[i].color; g.a = f >= 1f ? 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f + i) : 0f; meterGlow[i].color = g;
                }
                foreach (var p in powers)
                {
                    bool un = h.unlocked[(int)p.p];
                    bool can = h.CanUse(p.p);
                    p.lockImg.gameObject.SetActive(!un || (!can && !h.Active(p.p)));
                    p.lockImg.color = !un ? new Color(0.05f, 0.03f, 0.08f, 0.85f) : new Color(0.05f, 0.03f, 0.08f, 0.45f);
                    p.ring.fillAmount = h.Remaining01(p.p);
                    p.label.color = un ? UiKit.Cream : new Color(1, 1, 1, 0.3f);
                    p.cost.gameObject.SetActive(un);
                }
                bool ult = h.unlocked[(int)PowerId.Panchmukhi] && (h.bhakti >= 2.999f || h.infiniteMeter);
                var ug = ultGlow.color; ug.a = ult ? 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f) : 0f; ultGlow.color = ug;
                ultBtn.color = h.unlocked[(int)PowerId.Panchmukhi] ? (paintedBtns ? new Color(1f, 1f, 1f, ult ? 1f : 0.6f) : new Color(1f, 0.9f, 0.55f, ult ? 1f : 0.55f)) : new Color(1, 1, 1, 0.25f);
            }

            if (bannerT > 0f)
            {
                bannerT -= dt;
                float age = bannerDur - bannerT;
                float pop = age < 0.15f ? Mathf.Lerp(1.6f, 1f, age / 0.15f) : 1f;
                banner.transform.localScale = Vector3.one * pop;
                float a = bannerT < 0.25f ? bannerT / 0.25f : 1f;
                banner.canvasRenderer.SetAlpha(a); sub.canvasRenderer.SetAlpha(a);
                if (bannerT <= 0f) { banner.text = ""; sub.text = ""; }
            }
            if (comboT > 0f)
            {
                comboT -= dt;
                comboNum.transform.localScale = Vector3.Lerp(comboNum.transform.localScale, Vector3.one, dt * 12f);
                float a = Mathf.Clamp01(comboT / 0.3f);
                comboNum.canvasRenderer.SetAlpha(a); comboRank.canvasRenderer.SetAlpha(a);
            }
            else { comboNum.text = ""; comboRank.text = ""; }
            if (comboNameT > 0f)
            {
                comboNameT -= dt;
                comboName.transform.localScale = Vector3.Lerp(comboName.transform.localScale, Vector3.one, dt * 10f);
                comboName.canvasRenderer.SetAlpha(Mathf.Clamp01(comboNameT / 0.3f));
            }
            else comboName.text = "";
            if (hintT > 0f) { hintT -= dt; hint.canvasRenderer.SetAlpha(Mathf.Clamp01(hintT / 0.4f)); } else hint.text = "";

            flashA = Mathf.MoveTowards(flashA, 0f, dt * flashDecay);
            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashA);
            darkA = Mathf.MoveTowards(darkA, darkGoal, dt * 1.5f);
            dark.color = new Color(0.02f, 0.0f, 0.05f, darkA);
            if (victoryPanel.parent.gameObject.activeSelf) victoryPanel.localScale = Vector3.Lerp(victoryPanel.localScale, Vector3.one, dt * 8f);
        }
    }
}
