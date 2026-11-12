using System;
using UnityEngine;

namespace BalHanuman
{
    /// <summary>Plays named sprite clips (idle, talk, ...) on a SpriteRenderer. Used by NPCs, the basket and the portal.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ClipAnimator : MonoBehaviour
    {
        [Serializable]
        public class Clip
        {
            public string name;
            public Sprite[] frames;
            public float fps = 10f;
            public bool loop = true;
        }

        public Clip[] clips;
        public string startClip;
        SpriteRenderer sr;
        Clip cur;
        string then;
        float t;
        bool done;

        public string Current { get { return cur != null ? cur.name : ""; } }
        public bool Finished { get { return done; } }

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            t = 0f;
            if (!string.IsNullOrEmpty(startClip)) Play(startClip);
        }

        Clip Find(string n)
        {
            if (clips == null) return null;
            foreach (var c in clips) if (c.name == n) return c;
            return null;
        }

        /// <summary>Start a clip from its first frame. If it does not loop, `thenClip` plays when it ends (otherwise it holds the last frame).</summary>
        public void Play(string clip, string thenClip = null)
        {
            var c = Find(clip);
            if (c == null || c.frames == null || c.frames.Length == 0) return;
            cur = c; then = thenClip; t = 0f; done = false;
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = c.frames[0];
        }

        /// <summary>Switch to a clip only if it is not already playing (does not restart a running loop).</summary>
        public void Ensure(string clip)
        {
            if (cur == null || cur.name != clip) Play(clip);
        }

        void Update()
        {
            if (cur == null || cur.frames == null || cur.frames.Length == 0) return;
            t += Time.unscaledDeltaTime * cur.fps;      // unscaled: the sage keeps talking while a dialogue has stopped game time
            int n = cur.frames.Length;
            int i = (int)t;
            if (cur.loop) i %= n;
            else if (i >= n)
            {
                i = n - 1;
                if (!done)
                {
                    done = true;
                    if (!string.IsNullOrEmpty(then)) { var nx = then; then = null; Play(nx); return; }
                }
            }
            sr.sprite = cur.frames[Mathf.Clamp(i, 0, n - 1)];
        }
    }
}
