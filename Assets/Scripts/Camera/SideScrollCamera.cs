using UnityEngine;

namespace BalHanuman
{
    /// <summary>Smooth side-scrolling follow camera with look-ahead and level bounds (Cinemachine is not installed).</summary>
    [RequireComponent(typeof(Camera))]
    public class SideScrollCamera : MonoBehaviour
    {
        public Transform target;
        public Vector2 minBounds = new Vector2(8f, 3f);
        public Vector2 maxBounds = new Vector2(142f, 12f);
        public float yOffset = 1.8f;
        public float lookAhead = 1.8f;
        public float smoothX = 5f;
        public float smoothY = 3.5f;
        float look;
        Rigidbody2D targetBody;

        void Start()
        {
            if (target != null) targetBody = target.GetComponent<Rigidbody2D>();
            Snap();
        }

        public void Snap()
        {
            if (target == null) return;
            transform.position = Clamp(new Vector3(target.position.x, target.position.y + yOffset, -10f));
        }

        Vector3 Clamp(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, minBounds.x, maxBounds.x);
            p.y = Mathf.Clamp(p.y, minBounds.y, maxBounds.y);
            p.z = -10f;
            return p;
        }

        void LateUpdate()
        {
            if (target == null) return;
            float vx = targetBody != null ? targetBody.linearVelocity.x : 0f;
            look = Mathf.Lerp(look, Mathf.Sign(vx) * lookAhead * Mathf.Clamp01(Mathf.Abs(vx) / 4f), 1f - Mathf.Exp(-2f * Time.deltaTime));
            var goal = Clamp(new Vector3(target.position.x + look, target.position.y + yOffset, -10f));
            var p = transform.position;
            p.x = Mathf.Lerp(p.x, goal.x, 1f - Mathf.Exp(-smoothX * Time.deltaTime));
            p.y = Mathf.Lerp(p.y, goal.y, 1f - Mathf.Exp(-smoothY * Time.deltaTime));
            transform.position = Clamp(p);
        }
    }
}
