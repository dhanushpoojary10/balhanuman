using UnityEngine;

namespace BalHanuman
{
    /// <summary>Kinematic platform that ping-pongs between two points and carries the player standing on it. Optionally bobs on water and dips when stood on.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class MovingPlatform : MonoBehaviour
    {
        public Vector2 travel = new Vector2(2f, 0f);
        public float speed = 1.2f;
        public float phaseOffset;
        [Header("Floating (logs)")]
        [Tooltip("Gentle up and down bobbing, in world units")]
        public float bobAmount;
        [Tooltip("How far the platform sinks while the player stands on it")]
        public float dipOnRide;
        public Sprite ripple;
        Rigidbody2D rb;
        Vector2 origin;
        PlayerController2D rider;
        float riderTimer, dip;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        void Start() { origin = rb.position; }

        void FixedUpdate()
        {
            float len = Mathf.Max(0.01f, travel.magnitude);
            float k = Mathf.PingPong((Time.time + phaseOffset) * speed / len, 1f);
            k = Mathf.SmoothStep(0f, 1f, k);
            bool ridden = rider != null && riderTimer > 0f;
            dip = Mathf.MoveTowards(dip, ridden ? dipOnRide : 0f, 0.5f * Time.fixedDeltaTime);
            Vector2 next = origin + travel * k;
            next.y += (bobAmount > 0f ? Mathf.Sin((Time.time + phaseOffset) * 1.7f) * bobAmount : 0f) - dip;
            Vector2 delta = next - rb.position;
            rb.MovePosition(next);
            riderTimer -= Time.fixedDeltaTime;
            if (ridden) rider.Carry(delta);
            else rider = null;
        }

        void OnCollisionStay2D(Collision2D col)
        {
            var p = col.collider.GetComponentInParent<PlayerController2D>();
            if (p == null) return;
            if (p.transform.position.y >= col.otherCollider.bounds.max.y - 0.35f)
            {
                if (rider == null && ripple != null)
                {
                    // first step onto the log: little rings and a splash on the water
                    Vector3 at = new Vector3(p.transform.position.x, col.otherCollider.bounds.min.y + 0.1f, 0f);
                    FxRipple.Spawn(ripple, new Vector3(at.x, at.y - 0.15f, 0f), 2.8f, 0.9f, new Color(1f, 1f, 1f, 0.8f), 8);
                }
                rider = p; riderTimer = 0.1f;
            }
        }
    }
}
