using UnityEngine;

namespace BalHanuman
{
    public class ReturnPortal : InteractableBase
    {
        public GameObject visual;
        public ClipAnimator anim;
        bool active;

        public override bool CanInteract => active && S != null && S.quest.State == QuestState.BasketCollected;
        public override string GetInteractionPrompt() => "Enter Portal";

        protected override void OnEnable()
        {
            base.OnEnable();
            // the stone arch is always visible; with the animated art it simply sleeps until the basket is found
            if (visual != null && !active && anim == null) visual.SetActive(false);
        }

        public void Activate()
        {
            active = true;
            if (visual != null) visual.SetActive(true);
            if (anim != null) anim.Play("open", "active");
            Sfx.Play(Sfx.Id.Portal);
            S.Toast("A glowing portal appeared! Step in to travel back to the sage.", 5f);
        }

        void Update()
        {
            if (anim == null && visual != null && active) visual.transform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.time * 3f));
        }

        public override void Interact(PlayerController2D player)
        {
            S.TravelThroughPortal();
        }
    }
}
