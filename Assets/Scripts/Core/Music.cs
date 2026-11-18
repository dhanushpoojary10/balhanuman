using UnityEngine;

namespace BalHanuman
{
    /// <summary>Looping background music, chosen per scene:
    /// Level 1 plays the composed bhajan (Resources/Audio/music_devotional.wav, made by Tools/AudioGen/generate_audio.py),
    /// the menu plays "Beneath the Temple Gates" (Resources/Audio/music_temple.mp3), and the fight (Level02) silences it for its own music.
    /// Starts automatically with the first scene and keeps playing across scene loads.</summary>
    public class Music : MonoBehaviour
    {
        public static float Volume = 0.7f;
        static Music inst;
        AudioSource src;
        float duckLevel = 1f, duckUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (inst != null) return;
            var clip = Resources.Load<AudioClip>(TrackFor(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
            if (clip == null) return;
            var go = new GameObject("Music");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<Music>();
            inst.src = go.AddComponent<AudioSource>();
            inst.src.clip = clip;
            inst.src.loop = true;
            inst.src.playOnAwake = false;
            inst.src.ignoreListenerPause = true;
            inst.src.volume = 0f;
            inst.src.Play();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += inst.OnScene;
        }

        static string TrackFor(string scene) => scene.StartsWith("Level01") ? "Audio/music_devotional" : "Audio/music_temple";

        /// <summary>Each scene gets its own track back: un-ducked, and swapped/restarted if anything else displaced it
        /// (the fight silences it and plays its own music).</summary>
        void OnScene(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name.StartsWith("Level02")) return;
            duckLevel = 1f; duckUntil = 0f;
            var clip = Resources.Load<AudioClip>(TrackFor(scene.name));
            if (clip != null && (src.clip != clip || !src.isPlaying)) { src.volume = 0f; src.clip = clip; src.Play(); }   // fades back in from Update
        }

        /// <summary>Lower the music to <paramref name="level"/> (0-1) for <paramref name="seconds"/> so a fanfare can be heard.</summary>
        public static void Duck(float level, float seconds)
        {
            if (inst == null) return;
            inst.duckLevel = level;
            inst.duckUntil = Time.unscaledTime + seconds;
        }

        void Update()
        {
            float target = Time.unscaledTime < duckUntil ? duckLevel : 1f;
            float k = Time.timeScale == 0f ? 0.5f : 1f;                       // a bit quieter while paused
            float want = Volume * target * k;
            // fade in at start, ease toward target otherwise
            src.volume = Mathf.MoveTowards(src.volume, want, Time.unscaledDeltaTime * (target < 1f ? 0.5f : 0.25f));
        }
    }
}
