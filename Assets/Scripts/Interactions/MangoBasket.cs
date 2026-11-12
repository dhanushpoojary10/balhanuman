using UnityEngine;

namespace BalHanuman
{
    public class MangoBasket : InteractableBase
    {
        public ReturnPortal portal;
        public Sprite sparkle;

        public override bool CanInteract => S != null && (S.quest.State == QuestState.FindBasket || S.quest.State == QuestState.NotStarted);
        public override string GetInteractionPrompt() => "Pick Up";

        // Free to walk around; whenever the Pick Up button is showing, the message says what to do.
        protected override string ArriveHint => "Tap the hand button (or press E) to pick up the basket!";

        public override void Interact(PlayerController2D player)
        {
            if (S.quest.State == QuestState.NotStarted) S.quest.StartQuest(); // player ran ahead of the sage: still counts
            if (!S.quest.CollectBasket()) return;
            Sfx.Play(Sfx.Id.PickupBig);
            Fx.Burst(sparkle, transform.position + Vector3.up * 0.5f, 30, 4f, 1.4f, 1f, 0.4f, new Color(1f, 0.85f, 0.3f), 2f, 25);
            S.Toast("I found it! The sage will be so happy!", 3.5f);
            if (portal != null) portal.Activate();
            gameObject.SetActive(false);
        }
    }
}
