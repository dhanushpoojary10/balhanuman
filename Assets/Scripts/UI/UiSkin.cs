using UnityEngine;

namespace BalHanuman
{
    /// <summary>All sprites and fonts used by the runtime UI. One asset shared by the HUD, overlays and main menu.</summary>
    [CreateAssetMenu(menuName = "Bal Hanuman/UI Skin")]
    public class UiSkin : ScriptableObject
    {
        [Header("Fonts")]
        public Font titleFont;      // Cinzel
        public Font bodyFont;       // Nunito Sans

        [Header("Frames")]
        public Sprite panelLarge, hudWide, hudMid, hudPill, hudPillPrimary, divider;

        [Header("Buttons and bars")]
        public Sprite btnNormal, btnPressed, btnPrimary, btnDisabled, portraitRing;
        public Sprite barFrame, fillCoral, fillGold, fillTeal;

        [Header("Round icon buttons (icon is part of the sprite)")]
        public Sprite btnPause, btnRoll, btnJump, btnBend, btnInteract, btnClimb, btnLeft, btnRight;

        [Header("Icons")]
        public Sprite iconPause, iconRoll, iconJump, iconBend, iconInteract, iconClimb, iconMango, iconHeart;
        public Sprite iconEnergy, iconFlag, iconOm, iconLotus, iconCheck, iconStar, iconPin, iconTalk;

        [Header("Fall dialog")]
        public Sprite[] failFrames;

        [Header("Misc")]
        public Sprite circle, white, heroPortrait;
    }
}
