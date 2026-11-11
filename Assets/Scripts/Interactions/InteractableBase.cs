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

    /// <summary>Base class: self-registers so the player only scans a short list; radius is configurable per object.</summary>
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        public static readonly List<InteractableBase> All = new List<InteractableBase>();

        [SerializeField] float interactionRadius = 2.5f;
        public float InteractionRadius => interactionRadius;
        public virtual bool CanInteract => true;

        public abstract string GetInteractionPrompt();
        public abstract void Interact(PlayerController2D player);

        protected virtual void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }

        protected static GameSession S => GameSession.Current;
    }
}
