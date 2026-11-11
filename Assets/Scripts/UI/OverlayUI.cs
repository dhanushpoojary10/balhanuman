using System;
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
        public int mangoes, bonus;
        public bool monkeyRescued;
        public float seconds;
    }

    /// <summary>Dialogue box, pause menu and level-complete screen.</summary>
    public class OverlayUI : MonoBehaviour
    {
        public Portrait[] portraits;

        Canvas canvas;
        GameObject dialoguePanel, pausePanel, completePanel;
        Text dlgName, dlgBody, statsText;
        Image dlgPortrait;
        DialogueLine[] lines; int idx; Action onDone;
        public bool DialogueOpen => dialoguePanel != null && dialoguePanel.activeSelf;

        void Awake()
        {
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
            Vector2 tc = new Vector2(0.5f, 1), c = new Vector2(0.5f, 0.5f);

            // dialogue (top area keeps the character visible)
            var dp = UiKit.FramedPanel("Dialogue", root, tc, tc, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(1700, 340));
            dialoguePanel = dp.gameObject;
            dp.GetComponent<Image>().raycastTarget = true;
            var btn = dp.gameObject.AddComponent<Button>(); btn.targetGraphic = dp.GetComponent<Image>(); btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Next);
            dlgPortrait = UiKit.Img("Portrait", dp, null, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(34, 0), new Vector2(270, 290), Color.white);
            dlgName = UiKit.Label("Name", dp, "", 50, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(340, -26), new Vector2(-380, 64), UiKit.Gold);
            dlgBody = UiKit.Label("Body", dp, "", 44, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero, UiKit.Cream);
            dlgBody.rectTransform.offsetMin = new Vector2(340, 70); dlgBody.rectTransform.offsetMax = new Vector2(-50, -95);
            UiKit.Label("Tap", dp, "TAP TO CONTINUE  >", 30, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 18), new Vector2(560, 44), new Color(1, 1, 1, 0.65f), false);
            dialoguePanel.SetActive(false);

            // pause
            pausePanel = new GameObject("Pause", typeof(RectTransform), typeof(Image));
            var pr = pausePanel.GetComponent<RectTransform>(); pr.SetParent(root, false);
            pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = pr.offsetMax = Vector2.zero;
            pausePanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            var pm = UiKit.FramedPanel("Menu", pr, c, c, c, Vector2.zero, new Vector2(760, 720));
            UiKit.Label("Title", pm, "PAUSED", 90, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(0, 110), UiKit.Gold);
            UiKit.TextButton("Resume", pm, "Resume", c, new Vector2(0, 90), new Vector2(560, 120), () => GameSession.Current.TogglePause());
            UiKit.TextButton("Restart", pm, "Restart Level", c, new Vector2(0, -60), new Vector2(560, 120), () => GameSession.Current.ReplayLevel());
            UiKit.TextButton("Menu", pm, "Main Menu", c, new Vector2(0, -210), new Vector2(560, 120), () => GameSession.Current.GoToMainMenu());
            pausePanel.SetActive(false);

            // level complete
            completePanel = new GameObject("Complete", typeof(RectTransform), typeof(Image));
            var cr = completePanel.GetComponent<RectTransform>(); cr.SetParent(root, false);
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = cr.offsetMax = Vector2.zero;
            completePanel.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.1f, 0.7f);
            var cm = UiKit.FramedPanel("Card", cr, c, c, c, Vector2.zero, new Vector2(1180, 840));
            UiKit.Label("Title", cm, "LEVEL 1 COMPLETE!", 100, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(0, 130), UiKit.Gold);
            UiKit.Label("Sub", cm, "The Missing Fruit Offering", 58, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(0, 80), UiKit.Cream);
            statsText = UiKit.Label("Stats", cm, "", 50, TextAnchor.UpperCenter, Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero, UiKit.Cream);
            statsText.rectTransform.offsetMin = new Vector2(60, 200); statsText.rectTransform.offsetMax = new Vector2(-60, -270);
            UiKit.TextButton("Replay", cm, "Replay Level", new Vector2(0.5f, 0), new Vector2(-290, 95), new Vector2(520, 120), () => GameSession.Current.ReplayLevel());
            UiKit.TextButton("Menu", cm, "Main Menu", new Vector2(0.5f, 0), new Vector2(290, 95), new Vector2(520, 120), () => GameSession.Current.GoToMainMenu());
            completePanel.SetActive(false);
        }

        // ---------------- dialogue ----------------
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
            var l = lines[idx];
            dlgName.text = l.speaker; dlgBody.text = l.text;
            dlgPortrait.sprite = null; dlgPortrait.enabled = false;
            if (portraits != null)
                foreach (var p in portraits)
                    if (p.speaker == l.speaker) { dlgPortrait.sprite = p.sprite; dlgPortrait.enabled = p.sprite != null; dlgPortrait.preserveAspect = true; }
            Sfx.Play(Sfx.Id.Dialogue);
        }

        void Next()
        {
            idx++;
            if (idx >= lines.Length)
            {
                dialoguePanel.SetActive(false);
                var d = onDone; onDone = null;
                d?.Invoke();
            }
            else Show();
        }

        // ---------------- pause / complete ----------------
        public void SetPause(bool on) { pausePanel.SetActive(on); }

        public void ShowComplete(CompletionStats s)
        {
            int m = Mathf.FloorToInt(s.seconds / 60f), sec = Mathf.FloorToInt(s.seconds % 60f);
            statsText.text = "Mangoes collected:  " + s.mangoes + "\n" +
                             "Baby monkey rescued:  " + (s.monkeyRescued ? "Yes!" : "Not this time") + "\n" +
                             "Bonus coins & lotus:  " + s.bonus + "\n" +
                             "Time:  " + m.ToString("00") + ":" + sec.ToString("00");
            completePanel.SetActive(true);
        }
    }
}
