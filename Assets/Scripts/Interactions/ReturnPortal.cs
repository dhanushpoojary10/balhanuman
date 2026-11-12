using UnityEngine;

namespace BalHanuman
{
    public class ReturnPortal : InteractableBase
    {
        public GameObject visual;
        bool active;

        public override bool CanInteract => active && S != null && S.quest.State == QuestState.BasketCollected;
        public override string GetInteractionPrompt() => "Enter";

        // Shows what to do when Hanuman reaches the portal (not a gate: nothing lies beyond it).
        protected override bool AutoStart => false;
        protected override string ArriveHint => "Tap the hand button (or press E) to step into the portal.";

        protected override void OnEnable()
        {
            base.OnEnable();
            if (visual != null && !active) visual.SetActive(false);
        }

        public void Activate()
        {
            active = true;
            if (visual != null) visual.SetActive(true);
            Sfx.Play(Sfx.Id.Portal);
            S.Toast("A glowing portal appeared ahead! Go to it to travel back to the sage.", 5f);
        }

        void Update()
        {
            if (visual != null && active) visual.transform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.time * 3f));
        }

        public override void Interact(PlayerController2D player)
        {
            S.TravelThroughPortal();
        }
    }
}
