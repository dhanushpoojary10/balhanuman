using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BalHanuman
{
    [Serializable]
    public struct DialogueLine
    {
        public string speaker;
        [TextArea] public string text;
        public DialogueLine(string s, string t) { speaker = s; text = t; }
    }

    [Serializable] public class Portrait { public string speaker; public Sprite sprite; }

    public struct CompletionStats
    {
        public int mangoes, totalMangoes, bonus;
        public bool monkeyRescued;
        public float seconds;
    }

    /// <summary>Dialogue box (portrait, name plate, typewriter text), pause menu and level-complete screen.</summary>
    public class OverlayUI : MonoBehaviour
    {
        public UiSkin skin;
        public Portrait[] portraits;

        Canvas canvas;
        GameObject dialoguePanel, pausePanel, completePanel;
        Text dlgName, dlgBody, tapHint;
        Image dlgFace;
        RectTransform tapIcon;
        float faceWidth;
        DialogueLine[] lines; int idx; Action onDone;
        Coroutine typing; bool typingDone = true; string fullText = "";
        Text statValueMango, statValueMonkey, statValueBonus, statValueTime;
        GameObject failPanel; Image failImage; float failStart; Action failCheckpoint, failReplay;
        public bool FailOpen => failPanel != null && failPanel.activeSelf;

        public bool DialogueOpen => dialoguePanel != null && dialoguePanel.activeSelf;

        void Awake()
        {
            UiKit.Init(skin);
            var cgo = new GameObject("OverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 80;
            var sc = cgo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            Build(canvas.transform);
        }

        void Build(Transform root)
        {
            var S = UiKit.Skin;
            Vector2 tc = new Vector2(0.5f, 1), c = new Vector2(0.5f, 0.5f), tl = new Vector2(0, 1), br = new Vector2(1, 0);

            // ---------------------------------------------------------------- dialogue
            var dp = UiKit.Frame("Dialogue", root, S.panelLarge, tc, tc, new Vector2(0, -200), new Vector2(1560, 310), Color.white, true);
            dialoguePanel = dp.gameObject;
            var btn = dp.gameObject.AddComponent<Button>(); btn.targetGraphic = dp; btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Next);
            faceWidth = 156f / 0.80f;
            dlgFace = UiKit.Portrait("Portrait", dp.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(44, 0), 200, null);
            var plate = UiKit.Frame("NamePlate", dp.transform, S.hudPill, tl, tl, new Vector2(290, -34), new Vector2(330, 66));
            dlgName = UiKit.Title("Name", plate.transform, "", 32, TextAnchor.MiddleCenter, c, c, Vector2.zero, new Vector2(300, 60), UiKit.Gold);
            dlgBody = UiKit.Body("Body", dp.transform, "", 40, TextAnchor.UpperLeft, tl, tl, new Vector2(290, -122), new Vector2(1180, 150), UiKit.Cream);
            tapHint = UiKit.Body("Tap", dp.transform, "Tap to continue", 24, TextAnchor.MiddleRight, br, br, new Vector2(-90, 34), new Vector2(300, 34), UiKit.Muted);
            var ti = UiKit.Img("TapIcon", dp.transform, S.iconBend, br, br, br, new Vector2(-48, 24), new Vector2(34, 34), Color.white);
            tapIcon = ti.rectTransform;
            dialoguePanel.SetActive(false);

            // ---------------------------------------------------------------- pause
            pausePanel = new GameObject("Pause", typeof(RectTransform), typeof(Image));
            var pr = pausePanel.GetComponent<RectTransform>(); pr.SetParent(root, false);
            pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = pr.offsetMax = Vector2.zero;
            pausePanel.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.1f, 0.62f);
            var pm = UiKit.Frame("Menu", pr, S.panelLarge, c, c, Vector2.zero, new Vector2(760, 760));
            UiKit.Title("Title", pm.transform, "PAUSED", 66, TextAnchor.MiddleCenter, tc, tc, new Vector2(0, -50), new Vector2(660, 90), UiKit.Cream);
            UiKit.Img("Divider", pm.transform, S.divider, tc, tc, tc, new Vector2(0, -150), new Vector2(460, 26), Color.white);
            UiKit.PillButton("Resume", pm.transform, "RESUME", c, new Vector2(0, 70), new Vector2(520, 112), () => GameSession.Current.TogglePause(), true);
            UiKit.PillButton("Restart", pm.transform, "RESTART LEVEL", c, new Vector2(0, -75), new Vector2(520, 112), () => GameSession.Current.ReplayLevel(), false);
            UiKit.PillButton("Menu", pm.transform, "MAIN MENU", c, new Vector2(0, -220), new Vector2(520, 112), () => GameSession.Current.GoToMainMenu(), false);
            pausePanel.SetActive(false);

            // ---------------------------------------------------------------- fall dialog (Hanuman is down)
            failPanel = new GameObject("Fail", typeof(RectTransform), typeof(Image));
            var fr = failPanel.GetComponent<RectTransform>(); fr.SetParent(root, false);
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
            failPanel.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.1f, 0.66f);
            var fm = UiKit.Frame("Card", fr, S.panelLarge, c, c, Vector2.zero, new Vector2(1000, 720));
            failImage = UiKit.Img("Hanuman", fm.transform, S.failFrames != null && S.failFrames.Length > 0 ? S.failFrames[0] : null, tc, tc, tc, new Vector2(0, -56), new Vector2(320, 230), Color.white);
            UiKit.Title("Title", fm.transform, "HANUMAN IS DOWN!", 62, TextAnchor.MiddleCenter, tc, c, new Vector2(0, -330), new Vector2(900, 80), UiKit.Gold);
            UiKit.Img("Divider", fm.transform, S.divider, tc, tc, tc, new Vector2(0, -386), new Vector2(520, 26), Color.white);
            UiKit.Body("Text", fm.transform, "Don't worry, little hero. Everyone stumbles sometimes.\nTry again from your last checkpoint?", 36, TextAnchor.MiddleCenter, tc, c, new Vector2(0, -470), new Vector2(880, 110), UiKit.Cream);
            var bc2 = new Vector2(0.5f, 0);
            UiKit.PillButton("FailCheckpoint", fm.transform, "GO TO CHECKPOINT", bc2, new Vector2(-235, 84), new Vector2(440, 108), () => failCheckpoint?.Invoke(), true, 30);
            UiKit.PillButton("FailReplay", fm.transform, "REPLAY LEVEL", bc2, new Vector2(235, 84), new Vector2(440, 108), () => failReplay?.Invoke(), false, 30);
            failPanel.SetActive(false);

            // ---------------------------------------------------------------- level complete
            completePanel = new GameObject("Complete", typeof(RectTransform), typeof(Image));
            var cr = completePanel.GetComponent<RectTransform>(); cr.SetParent(root, false);
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = cr.offsetMax = Vector2.zero;
            completePanel.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.1f, 0.7f);
            var cm = UiKit.Frame("Card", cr, S.panelLarge, c, c, Vector2.zero, new Vector2(1180, 860));
            UiKit.Body("Label", cm.transform, "LEVEL 1", 26, TextAnchor.MiddleCenter, tc, tc, new Vector2(0, -56), new Vector2(600, 34), UiKit.Gold, FontStyle.Bold);
            UiKit.Title("Title", cm.transform, "COMPLETE!", 92, TextAnchor.MiddleCenter, tc, tc, new Vector2(0, -112), new Vector2(1000, 110), UiKit.Gold);
            UiKit.Title("Sub", cm.transform, "The Missing Fruit Offering", 42, TextAnchor.MiddleCenter, tc, tc, new Vector2(0, -232), new Vector2(1000, 56), UiKit.Cream);
            UiKit.Img("Divider", cm.transform, S.divider, tc, tc, tc, new Vector2(0, -306), new Vector2(560, 28), Color.white);
            statValueMango = StatRow(cm.transform, 0, S.iconMango, "Mangoes collected");
            statValueMonkey = StatRow(cm.transform, 1, S.iconHeart, "Baby monkey rescued");
            statValueBonus = StatRow(cm.transform, 2, S.iconOm, "Bonus coins and lotus");
            statValueTime = StatRow(cm.transform, 3, S.iconFlag, "Time");
            UiKit.PillButton("Replay", cm.transform, "REPLAY LEVEL", new Vector2(0.5f, 0), new Vector2(-285, 84), new Vector2(500, 112), () => GameSession.Current.ReplayLevel(), true);
            UiKit.PillButton("Menu", cm.transform, "MAIN MENU", new Vector2(0.5f, 0), new Vector2(285, 84), new Vector2(500, 112), () => GameSession.Current.GoToMainMenu(), false);
            completePanel.SetActive(false);
        }

        Text StatRow(Transform parent, int row, Sprite icon, string label)
        {
            Vector2 tc = new Vector2(0.5f, 1), c = new Vector2(0.5f, 0.5f);
            float y = -392 - row * 84;
            UiKit.Img("Icon" + row, parent, icon, tc, tc, c, new Vector2(-380, y), new Vector2(52, 52), Color.white);
            UiKit.Body("Label" + row, parent, label, 36, TextAnchor.MiddleLeft, tc, c, new Vector2(-20, y), new Vector2(560, 56), UiKit.Cream);
            return UiKit.Body("Value" + row, parent, "", 40, TextAnchor.MiddleRight, tc, c, new Vector2(320, y), new Vector2(300, 56), UiKit.Gold, FontStyle.Bold);
        }

        // ---------------------------------------------------------------- dialogue
        public void ShowDialogue(DialogueLine[] l, Action done)
        {
            if (l == null || l.Length == 0) { done?.Invoke(); return; }
            lines = l; idx = 0; onDone = done;
            GameInput.Reset();
            dialoguePanel.SetActive(true);
            Show();
        }

        void Show()
        {
            var line = lines[idx];
            dlgName.text = line.speaker.ToUpperInvariant();
            Sprite face = null;
            if (portraits != null)
                foreach (var p in portraits) if (p.speaker == line.speaker) face = p.sprite;
            UiKit.SetPortrait(dlgFace, face, 200 * 0.80f);
            fullText = line.text;
            if (typing != null) StopCoroutine(typing);
            typing = StartCoroutine(Type());
            Sfx.Play(Sfx.Id.Dialogue);
        }

        IEnumerator Type()
        {
            typingDone = false;
            tapHint.text = "";
            tapIcon.gameObject.SetActive(false);
            float shown = 0f;
            int n = fullText.Length;
            while (shown < n)
            {
                shown += Time.unscaledDeltaTime * 70f;
                int k = Mathf.Min(n, (int)shown);
                // the unrevealed part stays in the layout but is invisible, so lines never reflow while typing
                dlgBody.text = fullText.Substring(0, k) + "<color=#00000000>" + fullText.Substring(k) + "</color>";
                yield return null;
            }
            Finish();
        }

        void Finish()
        {
            dlgBody.text = fullText;
            typingDone = true;
            tapHint.text = idx >= lines.Length - 1 ? "Tap to close" : "Tap to continue";
            tapIcon.gameObject.SetActive(true);
        }

        void Update()
        {
            if (failPanel != null && failPanel.activeSelf && skin != null && skin.failFrames != null && skin.failFrames.Length > 0)
            {
                int f = Mathf.Min(skin.failFrames.Length - 1, (int)((Time.unscaledTime - failStart) * 6f));
                failImage.sprite = skin.failFrames[f];
            }
            if (tapIcon != null && tapIcon.gameObject.activeInHierarchy)
                tapIcon.anchoredPosition = new Vector2(-48, 24 + Mathf.Sin(Time.unscaledTime * 6f) * 4f);
        }

        void Next()
        {
            if (!typingDone) { if (typing != null) StopCoroutine(typing); Finish(); return; }
            idx++;
            if (idx >= lines.Length)
            {
                dialoguePanel.SetActive(false);
                var d = onDone; onDone = null;
                d?.Invoke();
            }
            else Show();
        }

        // ---------------------------------------------------------------- fall dialog
        public void ShowFail(Action onCheckpoint, Action onReplay)
        {
            failCheckpoint = onCheckpoint; failReplay = onReplay;
            failStart = Time.unscaledTime;
            if (skin != null && skin.failFrames != null && skin.failFrames.Length > 0) failImage.sprite = skin.failFrames[0];
            failPanel.SetActive(true);
            Sfx.Play(Sfx.Id.Rescue, 0.6f, 0.7f);
        }

        public void HideFail() { failPanel.SetActive(false); }

        // ---------------------------------------------------------------- pause / complete
        public void SetPause(bool on) { pausePanel.SetActive(on); }

        public void ShowComplete(CompletionStats s)
        {
            int m = Mathf.FloorToInt(s.seconds / 60f), sec = Mathf.FloorToInt(s.seconds % 60f);
            statValueMango.text = s.mangoes + " / " + Mathf.Max(s.totalMangoes, s.mangoes);
            statValueMonkey.text = s.monkeyRescued ? "Yes!" : "Not this time";
            statValueBonus.text = s.bonus.ToString();
            statValueTime.text = m.ToString("00") + ":" + sec.ToString("00");
            completePanel.SetActive(true);
        }
    }
}
