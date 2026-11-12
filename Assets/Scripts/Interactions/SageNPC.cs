using UnityEngine;

namespace BalHanuman
{
    public class SageNPC : InteractableBase
    {
        public SpriteRenderer sprite;
        public Transform marker;

        public DialogueLine[] introLines =
        {
            new DialogueLine("Sage", "Little Hanuman, I need your help!"),
            new DialogueLine("Hanuman", "Of course! What happened?"),
            new DialogueLine("Sage", "The basket of mangoes for the temple offering has gone missing."),
            new DialogueLine("Hanuman", "Don't worry! I'll find it!"),
            new DialogueLine("Sage", "Follow the forest path. May your journey be blessed!"),
        };

        public DialogueLine[] hintLines =
        {
            new DialogueLine("Sage", "Follow the forest path, little one. The mangoes will show you the way."),
        };

        public DialogueLine[] finalLines =
        {
            new DialogueLine("Hanuman", "I found the basket!"),
            new DialogueLine("Sage", "Wonderful, little Hanuman! Your kindness has brought joy to the forest."),
            new DialogueLine("Hanuman", "I'm happy I could help!"),
        };

        bool busy;
        Vector3 markerBase;

        public override bool CanInteract => !busy && S != null && S.quest.State != QuestState.Completed;

        public override string GetInteractionPrompt()
        {
            var q = S.quest.State;
            return (q == QuestState.BasketCollected || q == QuestState.ReturnToSage) ? "Give Basket" : "Talk";
        }

        void Start() { if (marker != null) markerBase = marker.localPosition; }

        void Update()
        {
            if (S == null) return;
            if (sprite != null) sprite.flipX = S.player.transform.position.x < transform.position.x;
            if (marker != null)
            {
                var q = S.quest.State;
                bool show = q == QuestState.NotStarted || q == QuestState.BasketCollected || q == QuestState.ReturnToSage;
                marker.gameObject.SetActive(show);
                marker.localPosition = markerBase + Vector3.up * Mathf.Sin(Time.time * 4f) * 0.08f;
            }
        }

        // Mandatory stop: the first talk starts by itself and Hanuman cannot pass the sage before it.
        protected override bool BlocksPath => S != null && S.quest.State == QuestState.NotStarted;
        protected override string BlockedMessage => "The sage wants to talk to you first!";
        // Coming back with the basket: the hand-over is done by the player, so say how.
        protected override string ArriveHint =>
            S != null && (S.quest.State == QuestState.BasketCollected || S.quest.State == QuestState.ReturnToSage)
                ? "Tap the hand button (or press E) to give the basket to the sage." : "";

        void StartIntro()
        {
            busy = true;
            S.Dialogue(introLines, () =>
            {
                busy = false;
                S.quest.StartQuest();
            });
        }

        public override void Interact(PlayerController2D player)
        {
            if (busy) return;
            switch (S.quest.State)
            {
                case QuestState.NotStarted:
                    StartIntro();
                    break;
                case QuestState.FindBasket:
                    S.Dialogue(hintLines);
                    break;
                case QuestState.BasketCollected:
                case QuestState.ReturnToSage:
                    busy = true;
                    S.Dialogue(finalLines, () => S.BeginEnding(transform.position));
                    break;
            }
        }
    }
}
