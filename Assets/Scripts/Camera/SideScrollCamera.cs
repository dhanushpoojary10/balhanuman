using UnityEngine;

namespace BalHanuman
{
    /// <summary>Smooth side-scrolling follow camera with look-ahead, fall lead, shake and a pan-to-point helper (Cinemachine is not installed).</summary>
    [RequireComponent(typeof(Camera))]
    public class SideScrollCamera : MonoBehaviour
    {
        public static SideScrollCamera Current { get; private set; }

        public Transform target;
        public Vector2 minBounds = new Vector2(8f, 3f);
        public Vector2 maxBounds = new Vector2(142f, 12f);
        [Tooltip("Keep the view inside [worldLeft, worldRight] for any screen aspect ratio (wide phones show more width)")]
        public bool fitToWorld;
        public float worldLeft = -6.5f, worldRight = 152.5f;
        public float yOffset = 1.8f;
        public float lookAhead = 1.8f;
        public float smoothX = 5f;
        public float smoothY = 3.5f;
        float look, fallLead;
        Rigidbody2D targetBody;
        Vector3 basePos; bool hasPos;

        float shakeAmp, shakeT, shakeDur;
        Vector2 focusPoint; float focusStart = -99f, focusEnd = -99f, focusWeight = 0.6f;

        void Awake() { Current = this; }
        void OnDestroy() { if (Current == this) Current = null; }

        void Start()
        {
            if (target != null) targetBody = target.GetComponent<Rigidbody2D>();
            Snap();
        }

        public void Snap()
        {
            if (target == null) return;
            basePos = Clamp(new Vector3(target.position.x, target.position.y + yOffset, -10f));
            hasPos = true; fallLead = 0f; look = 0f; shakeT = 0f;
            transform.position = basePos;
        }

        /// <summary>Camera shake; amplitude in world units (0.05 is a soft thud, 0.2 is strong).</summary>
        public static void Shake(float amplitude, float duration = 0.25f)
        {
            if (Current == null) return;
            if (Current.shakeT > 0f && Current.shakeAmp * (Current.shakeT / Current.shakeDur) > amplitude) return;
            Current.shakeAmp = amplitude; Current.shakeT = Current.shakeDur = Mathf.Max(0.05f, duration);
        }

        /// <summary>Gently pull the view towards a point for a few seconds, then ease back to the player.</summary>
        public static void Focus(Vector2 point, float seconds, float weight = 0.6f)
        {
            if (Current == null) return;
            Current.focusPoint = point; Current.focusStart = Time.time; Current.focusEnd = Time.time + seconds; Current.focusWeight = weight;
        }

        Vector3 Clamp(Vector3 p)
        {
            float lo = minBounds.x, hi = maxBounds.x;
            if (fitToWorld)
            {
                float halfW = GetComponent<Camera>().orthographicSize * GetComponent<Camera>().aspect;
                lo = worldLeft + halfW; hi = worldRight - halfW;
                if (lo > hi) lo = hi = (worldLeft + worldRight) * 0.5f;
            }
            p.x = Mathf.Clamp(p.x, lo, hi);
            p.y = Mathf.Clamp(p.y, minBounds.y, maxBounds.y);
            p.z = -10f;
            return p;
        }

        void LateUpdate()
        {
            if (target == null) return;
            if (!hasPos) { basePos = transform.position; hasPos = true; }
            float vx = targetBody != null ? targetBody.linearVelocity.x : 0f;
            float vy = targetBody != null ? targetBody.linearVelocity.y : 0f;
            look = Mathf.Lerp(look, Mathf.Sign(vx) * lookAhead * Mathf.Clamp01(Mathf.Abs(vx) / 4f), 1f - Mathf.Exp(-2f * Time.deltaTime));
            // while falling fast, show more of what is below so the landing is visible
            float leadGoal = vy < -7f ? Mathf.Clamp((vy + 7f) * 0.1f, -1.5f, 0f) : 0f;
            fallLead = Mathf.Lerp(fallLead, leadGoal, 1f - Exp(leadGoal < fallLead ? 3f : 2f));

            float gx = target.position.x + look, gy = target.position.y + yOffset + fallLead;
            if (Time.time < focusEnd)
            {
                float w = focusWeight * Mathf.Clamp01(Mathf.Min((Time.time - focusStart) * 2.5f, (focusEnd - Time.time) * 2.5f));
                gx = Mathf.Lerp(gx, focusPoint.x, w);
                gy = Mathf.Lerp(gy, focusPoint.y + 1.5f, w * 0.4f);
            }
            var goal = Clamp(new Vector3(gx, gy, -10f));
            var p = basePos;
            p.x = Mathf.Lerp(p.x, goal.x, 1f - Exp(smoothX));
            p.y = Mathf.Lerp(p.y, goal.y, 1f - Exp(smoothY));
            basePos = Clamp(p);

            Vector3 shake = Vector3.zero;
            if (shakeT > 0f)
            {
                shakeT -= Time.deltaTime;
                float k = Mathf.Clamp01(shakeT / shakeDur);
                shake = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * shakeAmp * k;
            }
            transform.position = basePos + shake;
        }

        static float Exp(float rate) { return Mathf.Exp(-rate * Time.deltaTime); }
    }
}
