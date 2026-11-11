using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BalHanuman
{
    /// <summary>Title screen: Play loads Level 1.</summary>
    public class MainMenuController : MonoBehaviour
    {
        public UiSkin skin;
        public string levelScene = "Level01_MissingFruitOffering";

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
            var S = UiKit.Skin;
            Vector2 c = new Vector2(0.5f, 0.5f);

            // soft veil so the title reads over the painted backdrop
            var veil = UiKit.Img("Veil", cgo.transform, S.white, Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero, new Color(0.04f, 0.08f, 0.18f, 0.52f));
            veil.preserveAspect = false;

            UiKit.Portrait("Hero", cgo.transform, c, c, new Vector2(-470, 0), 520, S.heroPortrait);
            UiKit.Body("Kicker", cgo.transform, "A JOURNEY TO THE FORGOTTEN TEMPLE", 28, TextAnchor.MiddleCenter, c, c, new Vector2(330, 250), new Vector2(1000, 40), UiKit.Gold, FontStyle.Bold);
            UiKit.Title("Title", cgo.transform, "BAL HANUMAN", 124, TextAnchor.MiddleCenter, c, c, new Vector2(330, 150), new Vector2(1100, 180), UiKit.Cream);
            UiKit.Img("Divider", cgo.transform, S.divider, c, c, c, new Vector2(330, 50), new Vector2(640, 30), Color.white);
            UiKit.PillButton("Play", cgo.transform, "PLAY", c, new Vector2(330, -90), new Vector2(520, 124), () => SceneManager.LoadScene(levelScene), true, 52);
            UiKit.Body("Level", cgo.transform, "Level 1  ·  The Missing Fruit Offering", 30, TextAnchor.MiddleCenter, c, c, new Vector2(330, -200), new Vector2(1000, 44), UiKit.Cream);
        }
    }
}
