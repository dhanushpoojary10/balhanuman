using UnityEngine;
using UnityEngine.EventSystems;

namespace BalHanuman
{
    /// <summary>Squashes a UI element slightly while it is pressed (tactile feedback for cards and big buttons).</summary>
    public class UiPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float pressedScale = 0.97f;
        float target = 1f;

        public void OnPointerDown(PointerEventData e) { target = pressedScale; }
        public void OnPointerUp(PointerEventData e) { target = 1f; }
        public void OnPointerExit(PointerEventData e) { target = 1f; }

        void Update()
        {
            float s = Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * 0.6f);
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
