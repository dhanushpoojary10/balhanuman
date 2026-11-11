using UnityEngine;

namespace BalHanuman
{
    /// <summary>Looping devotional background music (Assets/Resources/Audio/music_devotional.wav).
    /// Starts automatically with the first scene and keeps playing across scene loads.</summary>
    public class Music : MonoBehaviour
    {
        public static float Volume = 0.38f;
        static Music inst;
        AudioSource src;
        float duckLevel = 1f, duckUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (inst != null) return;
            var clip = Resources.Load<AudioClip>("Audio/music_devotional");
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
            src.volume = Mathf.MoveTowards(src.volume, want, Time.unscaledDeltaTime * (target < 1f ? 0.5f : 0.12f));
        }
    }
}
