using UnityEngine;

namespace BalHanuman
{
    public class Lever : InteractableBase
    {
        public SpriteRenderer sprite;
        public BridgeController bridge;
        bool used;

        public override bool CanInteract => !used;
        public override string GetInteractionPrompt() => "Lower Bridge";

        public override void Interact(PlayerController2D player)
        {
            if (used) return;
            used = true;
            if (sprite != null) sprite.flipX = true; // lever swings to the other side
            Sfx.Play(Sfx.Id.Lever);
            Haptics.Medium();
            SideScrollCamera.Shake(0.05f, 0.2f);
            // show the player what they changed: the view drifts towards the bridge while it lowers
            SideScrollCamera.Focus(bridge.FocusPoint, bridge.duration + 1.2f, 0.7f);
            S.Toast("The ancient bridge creaks down...", 2.5f);
            bridge.Lower(() => S.Toast("The bridge is ready. Cross carefully!", 3f));
        }
    }
}
