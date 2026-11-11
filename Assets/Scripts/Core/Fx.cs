using UnityEngine;

namespace BalHanuman
{
    /// <summary>Tiny sprite-based particle helper (sparkles, dust, splashes, leaves, celebration).</summary>
    public static class Fx
    {
        public static void Burst(Sprite sprite, Vector3 pos, int count, float speed, float life, float gravity = 0f,
            float size = 0.4f, Color? tint = null, float upBias = 0f, int order = 20, float drift = 0f)
        {
            if (sprite == null) return;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("fx");
                go.transform.position = pos;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite; sr.sortingOrder = order;
                sr.color = tint ?? Color.white;
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float sp = speed * Random.Range(0.4f, 1f);
                var p = go.AddComponent<FxParticle>();
                p.velocity = new Vector2(Mathf.Cos(ang) * sp + drift, Mathf.Sin(ang) * sp + upBias);
                p.gravity = gravity; p.life = life * Random.Range(0.7f, 1.2f);
                float s = size * Random.Range(0.7f, 1.2f) / Mathf.Max(0.01f, sprite.bounds.size.y);
                p.baseScale = s; p.spin = Random.Range(-180f, 180f);
            }
        }
    }

    public class FxParticle : MonoBehaviour
    {
        public Vector2 velocity; public float gravity, life, baseScale, spin;
        float age; SpriteRenderer sr; Color c0;

        void Start() { sr = GetComponent<SpriteRenderer>(); c0 = sr.color; transform.localScale = Vector3.one * baseScale; }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= life) { Destroy(gameObject); return; }
            velocity.y -= gravity * Time.deltaTime;
            transform.position += (Vector3)(velocity * Time.deltaTime);
            transform.Rotate(0, 0, spin * Time.deltaTime);
            float k = age / life;
            var c = c0; c.a = c0.a * (1f - k * k);
            sr.color = c;
            transform.localScale = Vector3.one * baseScale * (1f + k * 0.3f);
        }
    }
}
