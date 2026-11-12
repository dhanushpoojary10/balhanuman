using System.Collections;
using UnityEngine;

namespace BalHanuman
{
    public class BabyMonkey : InteractableBase
    {
        public Transform landingPoint;
        public Sprite sparkle;
        public int bonusMangoes = 3;
        public DialogueLine[] lines =
        {
            new DialogueLine("Baby Monkey", "I can't get down!", "scared"),
            new DialogueLine("Hanuman", "Don't worry, little friend!"),
        };

        public ClipAnimator clips;
        public SpriteRenderer sprite;
        AmbientAnimal anim;
        bool busy, rescued;

        public override bool CanInteract => !busy && !rescued;
        public override string GetInteractionPrompt() => "Help";

        // Mandatory stop: Hanuman helps the baby monkey before crossing the cliff.
        protected override bool BlocksPath => !rescued;
        protected override string BlockedMessage => "The baby monkey needs your help!";

        void Awake() { anim = GetComponent<AmbientAnimal>(); }

        public override void Interact(PlayerController2D player)
        {
            busy = true;
            S.Dialogue(lines, () => StartCoroutine(Rescue()));
        }

        IEnumerator Rescue()
        {
            if (anim != null) anim.enabled = false;
            Vector3 a = transform.position;
            Vector3 b = landingPoint != null ? landingPoint.position : a + Vector3.down * 4f;
            if (clips != null) clips.Play("rescued");                          // looks up, cheers, leaps
            if (sprite != null) sprite.flipX = b.x < a.x;
            float dur = 1.5f, t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                Vector3 p = Vector3.Lerp(a, b, k);
                p.y += Mathf.Sin(k * Mathf.PI) * 1.6f;
                transform.position = p;
                yield return null;
            }
            transform.position = b;
            rescued = true; busy = false;
            S.MonkeyRescued = true;
            Sfx.Play(Sfx.Id.Rescue);
            Fx.Burst(sparkle, b + Vector3.up, 24, 3.5f, 1.4f, 1f, 0.35f, new Color(1f, 0.9f, 0.4f), 2f, 25);
            S.AddMangoes(bonusMangoes);
            S.Toast("Baby Monkey Rescued!  +" + bonusMangoes + " mangoes", 3.5f);
            if (clips != null) clips.Play("happy");                            // claps and bounces
            if (anim != null) { anim.enabled = true; anim.hopHeight = 0.7f; anim.Celebrate(); }
        }
    }
}
