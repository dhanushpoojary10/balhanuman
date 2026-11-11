using UnityEngine;

namespace BalHanuman
{
    /// <summary>Trigger zone that allows the player to climb vertically. Place on a vine with a BoxCollider2D trigger.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class ClimbableVine : MonoBehaviour
    {
        [Tooltip("+1 hops right, -1 hops left when reaching the top")]
        public float exitDirection = 1f;
        [Tooltip("World Y of the platform the vine leads to; reaching it hops the player off")]
        public float topOffset = 0.2f;

        public float TopY { get { var c = GetComponent<BoxCollider2D>(); return c.bounds.max.y - topOffset; } }

        [Tooltip("Optional: the hanging pieces of the vine, with the pivot at the top. It sways gently and swings when grabbed.")]
        public Transform swayRoot;
        float swayPhase, nudgeAmp, nudgeStart = -99f;

        void Reset() { GetComponent<BoxCollider2D>().isTrigger = true; }

        void Awake() { swayPhase = Random.value * 6.28f; }

        /// <summary>Give the vine a little swing (called when the player grabs it).</summary>
        public void Nudge(float direction) { nudgeAmp = 5f * Mathf.Sign(direction); nudgeStart = Time.time; }

        void Update()
        {
            if (swayRoot == null) return;
            float t = Time.time - nudgeStart;
            float swing = nudgeAmp * Mathf.Exp(-2.2f * t) * Mathf.Cos(t * 6.5f);
            float idle = Mathf.Sin(Time.time * 1.1f + swayPhase) * 1.4f;
            swayRoot.localRotation = Quaternion.Euler(0f, 0f, idle + swing);
        }

        static bool hintShown;

        void OnTriggerEnter2D(Collider2D other)
        {
            var p = other.GetComponentInParent<PlayerController2D>();
            if (p == null) return;
            p.EnterVine(this);
            if (!hintShown && GameSession.Current != null)
            {
                hintShown = true;
                GameSession.Current.Toast("Hold W / Up arrow (or the up button) to climb the vine!", 4f);
            }
        }

        void OnDestroy() { hintShown = false; }

        void OnTriggerExit2D(Collider2D other)
        {
            var p = other.GetComponentInParent<PlayerController2D>();
            if (p != null) p.ExitVine(this);
        }
    }
}
