using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BalHanuman.Fight
{
    /// <summary>
    /// Fight controls: virtual joystick + buttons on touch, keyboard in the editor.
    /// Keyboard: A/D or arrows move, W/Up/Space jump, S/Down crouch, J mace, K kick/tail, L or Shift dodge, U ultimate, 1-6 powers.
    /// Poll() once per frame (the director does), then read Held/Down/Up.
    /// </summary>
    public static class FightInput
    {
        public enum Btn { Mace, Kick, Dodge, Jump, Ultimate, P1, P2, P3, P4, P5, P6, Count }

        static readonly bool[] touchHeld = new bool[(int)Btn.Count];
        static readonly bool[] kbPrev = new bool[(int)Btn.Count];
        static readonly bool[] down = new bool[(int)Btn.Count];
        static readonly bool[] up = new bool[(int)Btn.Count];
        static readonly bool[] pendingDown = new bool[(int)Btn.Count];
        static readonly bool[] pendingUp = new bool[(int)Btn.Count];
        static readonly float[] pressTime = new float[(int)Btn.Count];
        public static Vector2 Stick;            // from the virtual joystick
        static int polledFrame = -1;

        public static void Reset()
        {
            for (int i = 0; i < (int)Btn.Count; i++) touchHeld[i] = kbPrev[i] = down[i] = up[i] = pendingDown[i] = pendingUp[i] = false;
            Stick = Vector2.zero;
        }

        public static void TouchDown(Btn b) { touchHeld[(int)b] = true; pendingDown[(int)b] = true; }
        public static void TouchUp(Btn b) { touchHeld[(int)b] = false; pendingUp[(int)b] = true; }

        static bool Key(Btn b)
        {
            var k = Keyboard.current; if (k == null) return false;
            switch (b)
            {
                case Btn.Mace: return k.jKey.isPressed;
                case Btn.Kick: return k.kKey.isPressed;
                case Btn.Dodge: return k.lKey.isPressed || k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
                case Btn.Jump: return k.spaceKey.isPressed;
                case Btn.Ultimate: return k.uKey.isPressed || k.iKey.isPressed;
                case Btn.P1: return k.digit1Key.isPressed;
                case Btn.P2: return k.digit2Key.isPressed;
                case Btn.P3: return k.digit3Key.isPressed;
                case Btn.P4: return k.digit4Key.isPressed;
                case Btn.P5: return k.digit5Key.isPressed;
                case Btn.P6: return k.digit6Key.isPressed;
            }
            return false;
        }

        public static void Poll()
        {
            if (polledFrame == Time.frameCount) return;
            polledFrame = Time.frameCount;
            for (int i = 0; i < (int)Btn.Count; i++)
            {
                bool kb = Key((Btn)i);
                down[i] = pendingDown[i] || (kb && !kbPrev[i]);
                up[i] = pendingUp[i] || (!kb && kbPrev[i] && !touchHeld[i]);
                if (down[i]) pressTime[i] = Time.unscaledTime;
                kbPrev[i] = kb; pendingDown[i] = pendingUp[i] = false;
            }
        }

        public static bool Held(Btn b) => touchHeld[(int)b] || kbPrev[(int)b];
        public static bool Down(Btn b) => down[(int)b];
        public static bool Up(Btn b) => up[(int)b];
        public static float HeldFor(Btn b) => Held(b) || Up(b) ? Time.unscaledTime - pressTime[(int)b] : 0f;

        public static float X
        {
            get
            {
                float h = Stick.x; var k = Keyboard.current;
                if (k != null) { if (k.aKey.isPressed || k.leftArrowKey.isPressed) h -= 1f; if (k.dKey.isPressed || k.rightArrowKey.isPressed) h += 1f; }
                return Mathf.Clamp(h, -1f, 1f);
            }
        }

        public static float Y
        {
            get
            {
                float v = Stick.y; var k = Keyboard.current;
                if (k != null) { if (k.wKey.isPressed || k.upArrowKey.isPressed) v += 1f; if (k.sKey.isPressed || k.downArrowKey.isPressed) v -= 1f; }
                return Mathf.Clamp(v, -1f, 1f);
            }
        }
    }

    /// <summary>Hold-to-press touch button for the fight HUD.</summary>
    public class FightButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public FightInput.Btn btn;
        public Image visual;
        public System.Action onPress;     // optional extra (e.g. training menu buttons)
        bool isDown;

        public void OnPointerDown(PointerEventData e)
        {
            isDown = true; FightInput.TouchDown(btn); onPress?.Invoke();
            transform.localScale = Vector3.one * 0.9f;
            if (visual != null) visual.color = new Color(0.8f, 0.8f, 0.85f, visual.color.a);
        }
        public void OnPointerUp(PointerEventData e) { Release(); }
        public void OnPointerExit(PointerEventData e) { if (isDown) Release(); }
        void OnDisable() { if (isDown) Release(); }

        void Release()
        {
            isDown = false; FightInput.TouchUp(btn);
            transform.localScale = Vector3.one;
            if (visual != null) visual.color = new Color(1f, 1f, 1f, visual.color.a);
        }
    }

    /// <summary>Floating virtual joystick: touch anywhere in its zone, drag to steer.</summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform baseRect, knob;
        public float radius = 110f;
        Vector2 home;

        void Start() { if (baseRect != null) home = baseRect.anchoredPosition; }

        public void OnPointerDown(PointerEventData e)
        {
            if (baseRect == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)baseRect.parent, e.position, e.pressEventCamera, out var lp);
            baseRect.anchoredPosition = lp;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRect, e.position, e.pressEventCamera, out var lp);
            var v = Vector2.ClampMagnitude(lp, radius);
            knob.anchoredPosition = v;
            var s = v / radius;
            // small dead zone; snap to 8 directions feels crisper for fighting inputs
            FightInput.Stick = s.magnitude < 0.25f ? Vector2.zero : new Vector2(Mathf.Abs(s.x) > 0.38f ? Mathf.Sign(s.x) : 0f, Mathf.Abs(s.y) > 0.5f ? Mathf.Sign(s.y) : 0f);
        }

        public void OnPointerUp(PointerEventData e)
        {
            knob.anchoredPosition = Vector2.zero; FightInput.Stick = Vector2.zero;
            if (baseRect != null) baseRect.anchoredPosition = home;
        }
    }
}
