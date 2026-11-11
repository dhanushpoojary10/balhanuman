using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BalHanuman
{
    /// <summary>Builds and drives the mobile HUD: objective, mango counter, pause, touch controls, interact prompt, toasts and fade.</summary>
    public class HudController : MonoBehaviour
    {
        public UiKit.Sprites sprites;
        public Sprite mangoIcon, arrowSprite, pauseIcon, jumpIcon, interactIcon, rollIcon;
        public Canvas Canvas { get; private set; }

        Text objectiveText, mangoText, promptText, toastText;
        GameObject promptPanel, toastPanel, climbUp;
        Image interactBg, interactIconImg, fade;
        RectTransform mangoPanel;
        Coroutine toastRoutine;
        bool hasTarget;

        void Awake()
        {
            UiKit.Init(sprites);
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            var cgo = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            Canvas = cgo.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay; Canvas.sortingOrder = 50;
            var sc = cgo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            Build(Canvas.transform);
        }

        void Build(Transform root)
        {
            Vector2 tl = new Vector2(0, 1), tr = new Vector2(1, 1), tc = new Vector2(0.5f, 1), bl = Vector2.zero, br = new Vector2(1, 0), c = new Vector2(0.5f, 0.5f);

            // objective (top centre)
            var obj = UiKit.FramedPanel("Objective", root, tc, tc, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(980, 110));
            UiKit.Label("Title", obj, "OBJECTIVE", 26, TextAnchor.UpperCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(0, 34), UiKit.Gold, false);
            objectiveText = UiKit.Label("Text", obj, "", 40, TextAnchor.LowerCenter, Vector2.zero, Vector2.one, c, new Vector2(0, 8), new Vector2(-30, -42), UiKit.Cream, false);

            // mango counter (top left)
            mangoPanel = UiKit.FramedPanel("Mangoes", root, tl, tl, new Vector2(0, 1), new Vector2(30, -24), new Vector2(250, 100));
            UiKit.Img("Icon", mangoPanel, mangoIcon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(70, 70), Color.white);
            mangoText = UiKit.Label("Count", mangoPanel, "x 0", 56, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero, UiKit.Cream, false);
            mangoText.rectTransform.offsetMin = new Vector2(110, 0); mangoText.rectTransform.offsetMax = new Vector2(-10, 0);

            // pause (top right)
            var pbg = UiKit.Img("PauseButton", root, UiKit.Circle, tr, tr, new Vector2(1, 1), new Vector2(-30, -24), new Vector2(100, 100), UiKit.Navy, true);
            UiKit.Img("Ring", pbg.transform, UiKit.Ring, Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero, UiKit.Gold);
            UiKit.Img("Icon", pbg.transform, pauseIcon, c, c, c, Vector2.zero, new Vector2(60, 60), UiKit.Cream);
            var pb = pbg.gameObject.AddComponent<Button>(); pb.targetGraphic = pbg;
            pb.onClick.AddListener(() => GameSession.Current?.TogglePause());

            // movement + action buttons
            Touch("BtnLeft", root, TouchButton.Kind.Left, arrowSprite, bl, new Vector2(190, 180), 220, 180f);
            Touch("BtnRight", root, TouchButton.Kind.Right, arrowSprite, bl, new Vector2(460, 180), 220, 0f);
            Touch("BtnJump", root, TouchButton.Kind.Jump, jumpIcon, br, new Vector2(-190, 200), 250, 90f);
            var ib = Touch("BtnInteract", root, TouchButton.Kind.Interact, interactIcon, br, new Vector2(-480, 170), 210, 0f);
            interactBg = ib.GetComponent<Image>(); interactIconImg = ib.transform.Find("Icon").GetComponent<Image>();
            climbUp = Touch("BtnUp", root, TouchButton.Kind.Up, arrowSprite, br, new Vector2(-190, 470), 190, 90f).gameObject;
            Touch("BtnRoll", root, TouchButton.Kind.Roll, rollIcon, br, new Vector2(-760, 150), 190, 0f);
            Touch("BtnBend", root, TouchButton.Kind.Down, arrowSprite, bl, new Vector2(325, 410), 160, -90f);
            climbUp.SetActive(false);

            // interact prompt label
            var pr = UiKit.FramedPanel("Prompt", root, br, br, new Vector2(0.5f, 0), new Vector2(-480, 300), new Vector2(560, 80));
            promptPanel = pr.gameObject;
            promptText = UiKit.Label("Text", pr, "", 38, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero, UiKit.Gold, false);
            promptPanel.SetActive(false);

            // toast
            var t = UiKit.FramedPanel("Toast", root, tc, tc, new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(1100, 100));
            toastPanel = t.gameObject;
            toastText = UiKit.Label("Text", t, "", 40, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, c, Vector2.zero, new Vector2(-30, 0), UiKit.Cream, false);
            toastPanel.SetActive(false);

            // fade
            fade = UiKit.Img("Fade", root, UiKit.White, Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0), false);
            fade.preserveAspect = false;
            fade.transform.SetAsLastSibling();
        }

        Image Touch(string name, Transform root, TouchButton.Kind kind, Sprite icon, Vector2 anchor, Vector2 pos, float size, float iconRot)
        {
            var bg = UiKit.Img(name, root, UiKit.Circle, anchor, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size), new Color(0.05f, 0.08f, 0.19f, 0.62f), true);
            UiKit.Img("Ring", bg.transform, UiKit.Ring, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, UiKit.Gold);
            var ic = UiKit.Img("Icon", bg.transform, icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.55f, size * 0.55f), UiKit.Cream);
            ic.rectTransform.localEulerAngles = new Vector3(0, 0, iconRot);
            bg.gameObject.AddComponent<TouchButton>().kind = kind;
            return bg;
        }

        // ---------------- public API ----------------
        public void SetObjective(string s) { if (objectiveText) objectiveText.text = s; }

        public void SetMangoCount(int n)
        {
            if (mangoText) mangoText.text = "x " + n;
            if (mangoPanel) StartCoroutine(Punch(mangoPanel));
        }

        IEnumerator Punch(RectTransform rt)
        {
            float t = 0;
            while (t < 0.25f)
            {
                t += Time.unscaledDeltaTime;
                rt.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(t / 0.25f * Mathf.PI));
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        public void SetPrompt(string prompt)
        {
            hasTarget = !string.IsNullOrEmpty(prompt);
            promptPanel.SetActive(hasTarget);
            if (hasTarget) promptText.text = "Interact – " + prompt;
            interactBg.color = hasTarget ? new Color(0.45f, 0.32f, 0.05f, 0.85f) : new Color(0.05f, 0.08f, 0.19f, 0.4f);
            interactIconImg.color = hasTarget ? UiKit.Gold : new Color(1, 1, 1, 0.35f);
        }

        void Update()
        {
            if (hasTarget && interactBg != null)
                interactBg.transform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(Time.unscaledTime * 6f));
        }

        public void ShowClimbButtons(bool show)
        {
            if (climbUp != null && climbUp.activeSelf != show) climbUp.SetActive(show);
        }

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
            float t = 0;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(Mathf.Min(t, seconds - t) * 4f);
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
            foreach (Transform ch in Canvas.transform)
                if (ch.name.StartsWith("Btn") && ch.name != "BtnUp") ch.gameObject.SetActive(v);
        }
    }
}
