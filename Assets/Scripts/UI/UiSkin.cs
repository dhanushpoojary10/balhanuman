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
        [Tooltip("Gold border of panelLarge with a see-through middle (the rules box).")]
        public Sprite panelFrame;
        [Tooltip("White shape of panelLarge's inside (tint it for a see-through glass fill).")]
        public Sprite panelFill;

        [Header("Buttons and bars")]
        public Sprite btnNormal, btnPressed, btnPrimary, btnDisabled, portraitRing;
        public Sprite barFrame, fillCoral, fillGold, fillTeal;

        [Header("Round icon buttons (icon is part of the sprite)")]
        public Sprite btnPause, btnRoll, btnJump, btnBend, btnInteract, btnClimb, btnLeft, btnRight;
        [Tooltip("Round button with no icon (put a text glyph on it, e.g. the ? help button).")]
        public Sprite btnBlank;

        [Header("Icons")]
        public Sprite iconPause, iconRoll, iconJump, iconBend, iconInteract, iconClimb, iconMango, iconHeart;
        public Sprite iconEnergy, iconFlag, iconOm, iconLotus, iconCheck, iconStar, iconPin, iconTalk;

        [Header("Gada puzzle")]
        public Sprite gadaFull;
        [Tooltip("White shape of gadaFull (tinted for the carving and its glowing rim).")]
        public Sprite gadaSilhouette;
        [Tooltip("cap, head, collar, handle, pommel (cut from gadaFull)")]
        public Sprite[] gadaPieces;
        public Sprite stoneTexture;
        [Tooltip("Soft round glow (fades to clear at the edge).")]
        public Sprite softGlow;

        [Header("Fall dialog")]
        public Sprite[] failFrames;

        [Header("Misc")]
        public Sprite circle, white, heroPortrait;
    }
}
