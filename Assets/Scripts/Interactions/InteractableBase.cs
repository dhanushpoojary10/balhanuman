using System.Collections.Generic;
using UnityEngine;

namespace BalHanuman
{
    public interface IInteractable
    {
        string GetInteractionPrompt();
        void Interact(PlayerController2D player);
        bool CanInteract { get; }
        float InteractionRadius { get; }
        Transform transform { get; }
    }

    /// <summary>
    /// Base class: self-registers so the player only scans a short list; radius is configurable per object.
    /// An interactable can also be a mandatory stop on the path (see BlocksPath): it starts by itself when
    /// Hanuman reaches it, and he cannot go past it until it is done.
    /// </summary>
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        public static readonly List<InteractableBase> All = new List<InteractableBase>();

        [SerializeField] float interactionRadius = 2.5f;
        public float InteractionRadius => interactionRadius;
        public virtual bool CanInteract => true;

        [Header("Mandatory stop (only used when BlocksPath is true)")]
        [Tooltip("Starts by itself when Hanuman is standing within this horizontal distance.")]
        [SerializeField] protected float autoStartDistance = 2.2f;
        [Tooltip("Starts only when Hanuman is on about the same level (so passing underneath does not count).")]
        [SerializeField] protected float autoStartHeight = 2f;
        [Tooltip("While the stop is pending Hanuman cannot go further right than this, relative to the object (negative = in front of it).")]
        [SerializeField] protected float gateOffset = -1.2f;

        /// <summary>True while this activity still has to be done before Hanuman may go past it.</summary>
        protected virtual bool BlocksPath => false;
        /// <summary>Shown once if Hanuman pushes against the gate.</summary>
        protected virtual string BlockedMessage => "";
        /// <summary>True: the stop starts by itself when Hanuman arrives. False: the player must use it (hand button / E).</summary>
        protected virtual bool AutoStart => true;
        /// <summary>What to do, shown in the message slot for exactly as long as this object's hand button is showing.</summary>
        protected virtual string ArriveHint => "";
        public string Hint => ArriveHint;

        bool gateNudged;

        /// <summary>Label under the hand button: keep it to one or two short words (it shrinks to fit, but short reads best).
        /// The full "what to do" sentence belongs in ArriveHint.</summary>
        public abstract string GetInteractionPrompt();
        public abstract void Interact(PlayerController2D player);

        protected virtual void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); }

        /// <summary>Gate: Hanuman cannot run, jump or roll past a pending stop.</summary>
        void FixedUpdate()
        {
            if (S == null || S.player == null || !BlocksPath) return;
            var body = S.player.Body;
            float limit = transform.position.x + gateOffset;
            if (body.position.x <= limit) { gateNudged = false; return; }
            body.position = new Vector2(limit, body.position.y);
            if (body.linearVelocity.x > 0f) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            // (no extra nudge while this object's own hint is on screen)
            bool hintUp = S.interactor != null && ReferenceEquals(S.interactor.Current, this) && !string.IsNullOrEmpty(ArriveHint);
            if (!gateNudged && !hintUp && !string.IsNullOrEmpty(BlockedMessage)) { gateNudged = true; S.Toast(BlockedMessage, 2.5f); }
        }

        /// <summary>
        /// Pending auto stops (BlocksPath + AutoStart, e.g. the sage's first talk) begin by themselves when Hanuman arrives.
        /// (Hints for hand-button things are shown by GameSession together with the button.)
        /// </summary>
        void LateUpdate()
        {
            if (S == null || S.player == null || !CanInteract || Time.timeScale == 0f) return;
            if (!(BlocksPath && AutoStart)) return;
            var p = S.player;
            Vector2 d = p.transform.position - transform.position;
            bool near = Mathf.Abs(d.x) <= autoStartDistance && Mathf.Abs(d.y) <= autoStartHeight;

            if (p.InputLocked || !p.Grounded || p.Climbing || p.Rolling || S.overlay.DialogueOpen) return;
            if (!near) return;

            var body = p.Body;
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            p.Face(d.x < 0f);                    // look at it
            p.PlayInteract();
            Interact(p);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }

        protected static GameSession S => GameSession.Current;
    }
}
