using UnityEngine;

namespace BalHanuman
{
    /// <summary>Stops the device screen from dimming or locking while the game is open (e.g. while the player is
    /// watching a fight or a cutscene without touching the screen). Applies to every scene; re-asserted on resume.</summary>
    public class KeepScreenOn : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            var go = new GameObject("KeepScreenOn");
            DontDestroyOnLoad(go);
            go.AddComponent<KeepScreenOn>();
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused) Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
