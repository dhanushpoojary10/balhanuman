using System;
using UnityEngine;

namespace BalHanuman
{
    /// <summary>Picks the single closest interactable in range and triggers it on the interact button.</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public PlayerController2D player;
        public event Action<IInteractable> TargetChanged;
        IInteractable current;
        public IInteractable Current => current;

        void Awake() { if (player == null) player = GetComponent<PlayerController2D>(); }

        /// <summary>Re-picks the target next frame (use when the same object's button label or hint has changed).</summary>
        public void Refresh()
        {
            current = null;
            TargetChanged?.Invoke(null);
        }

        void Update()
        {
            if (Time.timeScale == 0f) return;
            IInteractable best = null;
            if (!player.InputLocked && !player.Climbing && !player.Rolling)
            {
                float bestD = float.MaxValue;
                Vector2 p = transform.position;
                foreach (var i in InteractableBase.All)
                {
                    if (i == null || !i.CanInteract) continue;
                    float d = Vector2.Distance(p + Vector2.up * 0.8f, i.transform.position);
                    if (d <= i.InteractionRadius && d < bestD) { best = i; bestD = d; }
                }
            }
            if (best != current)
            {
                current = best;
                TargetChanged?.Invoke(current);
            }
            if (current != null && GameInput.ConsumeInteract())
            {
                player.PlayInteract();
                var t = current;
                Sfx.Play(Sfx.Id.Click);
                t.Interact(player);
            }
            else GameInput.ConsumeInteract();
        }
    }
}
