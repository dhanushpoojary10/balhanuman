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

        void Reset() { GetComponent<BoxCollider2D>().isTrigger = true; }

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
