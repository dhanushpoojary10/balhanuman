using UnityEngine;

namespace BalHanuman
{
    public class SageNPC : InteractableBase
    {
        public SpriteRenderer sprite;
        public Transform marker;
        public ClipAnimator anim;
        [Tooltip("True if the artwork faces right by default (the new sage faces left)")]
        public bool artFacesRight = true;

        public DialogueLine[] introLines =
        {
            new DialogueLine("Sage", "Little Hanuman, I need your help!", "worried"),
            new DialogueLine("Hanuman", "Of course! What happened?"),
            new DialogueLine("Sage", "The basket of mangoes for the temple offering has gone missing.", "worried"),
            new DialogueLine("Hanuman", "Don't worry! I'll find it!"),
            new DialogueLine("Sage", "Follow the forest path. May your journey be blessed!", "smile"),
        };

        public DialogueLine[] hintLines =
        {
            new DialogueLine("Sage", "Follow the forest path, little one. The mangoes will show you the way."),
        };

        public DialogueLine[] finalLines =
        {
            new DialogueLine("Hanuman", "I found the basket!"),
            new DialogueLine("Sage", "Wonderful, little Hanuman! Your kindness has brought joy to the forest.", "joy"),
            new DialogueLine("Hanuman", "I'm happy I could help!"),
        };

        bool busy;
        Vector3 markerBase;

        public override bool CanInteract => !busy && S != null && S.quest.State != QuestState.Completed;

        public override string GetInteractionPrompt()
        {
            var q = S.quest.State;
            return (q == QuestState.BasketCollected || q == QuestState.ReturnToSage) ? "Give Mango Basket" : "Talk to Sage";
        }

        void Start() { if (marker != null) markerBase = marker.localPosition; }

        void Update()
        {
            if (S == null) return;
            if (sprite != null) sprite.flipX = (S.player.transform.position.x < transform.position.x) == artFacesRight;
            if (marker != null)
            {
                var q = S.quest.State;
                bool show = q == QuestState.NotStarted || q == QuestState.BasketCollected || q == QuestState.ReturnToSage;
                marker.gameObject.SetActive(show);
                marker.localPosition = markerBase + Vector3.up * Mathf.Sin(Time.time * 4f) * 0.08f;
            }
        }

        void Talk(bool on)
        {
            if (anim == null) return;
            if (on) anim.Play("talk"); else anim.Play("idle");
        }

        public override void Interact(PlayerController2D player)
        {
            if (busy) return;
            switch (S.quest.State)
            {
                case QuestState.NotStarted:
                    busy = true;
                    Talk(true);
                    S.Dialogue(introLines, () =>
                    {
                        busy = false;
                        if (anim != null) anim.Play("bless", "idle");     // a blessing as he sends Hanuman off
                        S.quest.StartQuest();
                        S.Toast("Run: arrows / A D    Jump: up button / Space\nRoll: dash button / Shift    Bend: down button / S    Interact: E", 8f);
                    });
                    break;
                case QuestState.FindBasket:
                    Talk(true);
                    S.Dialogue(hintLines, () => Talk(false));
                    break;
                case QuestState.BasketCollected:
                case QuestState.ReturnToSage:
                    busy = true;
                    Talk(true);
                    S.Dialogue(finalLines, () => { if (anim != null) anim.Play("wave"); S.BeginEnding(transform.position); });
                    break;
            }
        }
    }
}
