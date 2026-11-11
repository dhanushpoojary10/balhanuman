using UnityEngine;

namespace BalHanuman
{
    /// <summary>Loops a short sprite sequence (used for NPCs; the player uses an Animator Controller).</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteAnimator : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 3f;
        public bool pingPong = true;
        SpriteRenderer sr;
        float t;

        void Awake() { sr = GetComponent<SpriteRenderer>(); t = Random.value * 5f; }

        void Update()
        {
            if (frames == null || frames.Length == 0) return;
            t += Time.deltaTime * fps;
            int n = frames.Length;
            int i = pingPong && n > 1 ? (int)Mathf.PingPong(t, n - 1 + 0.999f) : (int)t % n;
            sr.sprite = frames[Mathf.Clamp(i, 0, n - 1)];
        }
    }
}
