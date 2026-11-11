using UnityEngine;

namespace BalHanuman
{
    /// <summary>Soft contact shadow that stays on the ground below its owner and shrinks as the owner rises.</summary>
    public class BlobShadow : MonoBehaviour
    {
        public Transform owner;
        public LayerMask groundMask;
        public float baseWidth = 0.9f;
        public float maxAlpha = 0.32f;
        SpriteRenderer sr;
        float unit;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            unit = sr.sprite != null ? sr.sprite.bounds.size.x : 2.56f;
        }

        void LateUpdate()
        {
            if (owner == null) return;
            var hit = Physics2D.Raycast((Vector2)owner.position + Vector2.up * 0.3f, Vector2.down, 9f, groundMask);
            if (hit.collider == null) { sr.enabled = false; return; }
            sr.enabled = true;
            float h = Mathf.Max(0f, owner.position.y - hit.point.y);
            float k = Mathf.Clamp01(1f - h / 6f);
            transform.position = new Vector3(owner.position.x, hit.point.y + 0.04f, 0f);
            float w = baseWidth * (0.6f + 0.4f * k);
            transform.localScale = new Vector3(w / unit, w * 0.2f / unit, 1f);
            sr.color = new Color(0f, 0f, 0f, maxAlpha * k);
        }
    }
}
