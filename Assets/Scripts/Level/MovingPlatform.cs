using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BalHanuman
{    /// <summary>Kinematic platform that ping-pongs between two points and carries the player standing on it.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class MovingPlatform : MonoBehaviour
    {
        public Vector2 travel = new Vector2(2f, 0f);
        public float speed = 1.2f;
        public float phaseOffset;
        Rigidbody2D rb;
        Vector2 origin;
        PlayerController2D rider;
        float riderTimer;

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
            Vector2 next = origin + travel * k;
            Vector2 delta = next - rb.position;
            rb.MovePosition(next);
            riderTimer -= Time.fixedDeltaTime;
            if (rider != null && riderTimer > 0f) rider.Carry(delta);
            else rider = null;
        }

        void OnCollisionStay2D(Collision2D col)
        {
            var p = col.collider.GetComponentInParent<PlayerController2D>();
            if (p == null) return;
            if (p.transform.position.y >= col.otherCollider.bounds.max.y - 0.35f) { rider = p; riderTimer = 0.1f; }
        }
    }
}
