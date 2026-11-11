using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BalHanuman
{
    /// <summary>Single input source: on-screen touch buttons plus keyboard for editor testing (new Input System only).</summary>
    public static class GameInput
    {
        public static bool Left, Right, Up, Down, JumpHeldTouch;
        static bool jumpDown, interactDown, rollDown;

        public static void Reset()
        {
            Left = Right = Up = Down = JumpHeldTouch = false;
            jumpDown = interactDown = rollDown = false;
        }

        public static void TouchJumpDown() { JumpHeldTouch = true; jumpDown = true; }
        public static void TouchInteractDown() { interactDown = true; }
        public static void TouchRollDown() { rollDown = true; }

        public static float Horizontal
        {
            get
            {
                float h = 0f;
                if (Left) h -= 1f;
                if (Right) h += 1f;
                var k = Keyboard.current;
                if (k != null)
                {
                    if (k.leftArrowKey.isPressed || k.aKey.isPressed) h -= 1f;
                    if (k.rightArrowKey.isPressed || k.dKey.isPressed) h += 1f;
                }
                return Mathf.Clamp(h, -1f, 1f);
            }
        }

        public static float Vertical
        {
            get
            {
                float v = 0f;
                if (Up) v += 1f;
                if (Down) v -= 1f;
                var k = Keyboard.current;
                if (k != null)
                {
                    if (k.wKey.isPressed || k.upArrowKey.isPressed) v += 1f;
                    if (k.sKey.isPressed || k.downArrowKey.isPressed) v -= 1f;
                }
                return Mathf.Clamp(v, -1f, 1f);
            }
        }

        public static bool JumpHeld
        {
            get
            {
                var k = Keyboard.current;
                return JumpHeldTouch || (k != null && k.spaceKey.isPressed);
            }
        }

        public static bool ConsumeJump(bool allowUpKeys = false)
        {
            var k = Keyboard.current;
            bool kb = k != null && (k.spaceKey.wasPressedThisFrame || (allowUpKeys && (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame)));
            bool r = jumpDown || kb;
            jumpDown = false;
            return r;
        }

        public static bool JumpHeldFor(bool allowUpKeys)
        {
            var k = Keyboard.current;
            return JumpHeldTouch || (k != null && (k.spaceKey.isPressed || (allowUpKeys && (k.wKey.isPressed || k.upArrowKey.isPressed))));
        }

        public static bool ConsumeRoll()
        {
            var k = Keyboard.current;
            bool r = rollDown || (k != null && (k.leftShiftKey.wasPressedThisFrame || k.rightShiftKey.wasPressedThisFrame || k.zKey.wasPressedThisFrame));
            rollDown = false;
            return r;
        }

        public static bool ConsumeInteract()
        {
            var k = Keyboard.current;
            bool r = interactDown || (k != null && k.eKey.wasPressedThisFrame);
            interactDown = false;
            return r;
        }

        public static bool PausePressed()
        {
            var k = Keyboard.current;
            return k != null && k.escapeKey.wasPressedThisFrame;
        }
    }
}
