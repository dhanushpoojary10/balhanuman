using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BalHanuman
{
    /// <summary>Simple title screen: Play loads Level 1.</summary>
    public class MainMenuController : MonoBehaviour
    {
        public UiKit.Sprites sprites;
        public Sprite hero, mango;
        public string levelScene = "Level01_MissingFruitOffering";

        void Awake()
        {
            Time.timeScale = 1f;
            UiKit.Init(sprites);
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            var cgo = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = cgo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            Vector2 c = new Vector2(0.5f, 0.5f);

            var veil = UiKit.Img("Veil", canvas.transform, UiKit.White, Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero, new Color(0.04f, 0.07f, 0.18f, 0.55f));
            veil.preserveAspect = false;
            if (hero != null) UiKit.Img("Hero", canvas.transform, hero, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), c, new Vector2(-520, -20), new Vector2(560, 760), Color.white);
            UiKit.Label("Title", canvas.transform, "BAL HANUMAN", 150, TextAnchor.MiddleCenter, c, c, c, new Vector2(280, 260), new Vector2(1100, 200), UiKit.Gold);
            UiKit.Label("Sub", canvas.transform, "The Journey to the Forgotten Temple", 54, TextAnchor.MiddleCenter, c, c, c, new Vector2(280, 130), new Vector2(1100, 90), UiKit.Cream);
            UiKit.TextButton("Play", canvas.transform, "Play", c, new Vector2(280, -80), new Vector2(520, 140), () => SceneManager.LoadScene(levelScene), 70);
            UiKit.Label("Level", canvas.transform, "Level 1 - The Missing Fruit Offering", 40, TextAnchor.MiddleCenter, c, c, c, new Vector2(280, -220), new Vector2(1100, 70), UiKit.Gold);
        }
    }
}
