using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BalHanuman
{    /// <summary>Attach to a UI Image to make it a multi-touch hold/press button.</summary>
    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public enum Kind { Left, Right, Up, Down, Jump, Interact, Roll }
        public Kind kind;
        public UnityEngine.UI.Image visual;
        public Sprite normalSprite, pressedSprite;
        bool down;

        public void OnPointerDown(PointerEventData e)
        {
            down = true;
            switch (kind)
            {
                case Kind.Left: GameInput.Left = true; break;
                case Kind.Right: GameInput.Right = true; break;
                case Kind.Up: GameInput.Up = true; break;
                case Kind.Down: GameInput.Down = true; break;
                case Kind.Jump: GameInput.TouchJumpDown(); break;
                case Kind.Interact: GameInput.TouchInteractDown(); break;
                case Kind.Roll: GameInput.TouchRollDown(); break;
            }
            transform.localScale = Vector3.one * 0.93f;
            if (visual != null) { if (pressedSprite != null) visual.sprite = pressedSprite; else visual.color = new Color(0.78f, 0.78f, 0.82f, 1f); }
        }

        public void OnPointerUp(PointerEventData e) { Release(); }
        public void OnPointerExit(PointerEventData e) { if (down) Release(); }
        void OnDisable() { if (down) Release(); }

        void Release()
        {
            down = false;
            switch (kind)
            {
                case Kind.Left: GameInput.Left = false; break;
                case Kind.Right: GameInput.Right = false; break;
                case Kind.Up: GameInput.Up = false; break;
                case Kind.Down: GameInput.Down = false; break;
                case Kind.Jump: GameInput.JumpHeldTouch = false; break;
            }
            transform.localScale = Vector3.one;
            if (visual != null) { if (normalSprite != null) visual.sprite = normalSprite; else visual.color = Color.white; }
        }
    }
}
