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

    public static class FxGlow
    {
        static Sprite glow;

        /// <summary>Soft white radial blob, 1 world unit across; tint it with the SpriteRenderer colour.</summary>
        public static Sprite Sprite
        {
            get
            {
                if (glow == null)
                {
                    const int N = 64;
                    var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                    var px = new Color32[N * N];
                    for (int y = 0; y < N; y++)
                        for (int x = 0; x < N; x++)
                        {
                            float d = Mathf.Sqrt((x - N / 2f + 0.5f) * (x - N / 2f + 0.5f) + (y - N / 2f + 0.5f) * (y - N / 2f + 0.5f)) / (N / 2f);
                            float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                            px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                        }
                    t.SetPixels32(px); t.Apply(false, true);
                    glow = UnityEngine.Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
                }
                return glow;
            }
        }

        /// <summary>An expanding, fading ring of light.</summary>
        public static void Ring(Vector3 pos, float fromSize, float toSize, float life, Color c, int order = 20)
        {
            var go = new GameObject("fxRing"); go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = Sprite; sr.color = c; sr.sortingOrder = order;
            var r = go.AddComponent<FxRing>(); r.from = fromSize; r.to = toSize; r.life = life; r.c0 = c;
        }

        /// <summary>A soft glow that gently pulses; returns the object so the caller can parent or destroy it.</summary>
        public static GameObject Pulse(Transform parent, Vector3 worldPos, float size, Color c, float minAlpha, float maxAlpha, float speed, int order)
        {
            var go = new GameObject("fxGlow"); go.transform.SetParent(parent, true); go.transform.position = worldPos;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = Sprite; sr.color = c; sr.sortingOrder = order;
            float ls = parent != null ? parent.lossyScale.x : 1f;
            var p = go.AddComponent<FxGlowPulse>(); p.size = size / Mathf.Max(0.0001f, ls); p.color = c; p.minAlpha = minAlpha; p.maxAlpha = maxAlpha; p.speed = speed; p.phase = Random.value * 6.28f;
            go.transform.localScale = Vector3.one * p.size;
            return go;
        }
    }

    /// <summary>A flat ripple ring on the water: stays level (no spinning), widens and fades.</summary>
    public class FxRipple : MonoBehaviour
    {
        float width, life, age; Color c0; SpriteRenderer sr; Vector3 baseScale;

        public static void Spawn(Sprite sprite, Vector3 pos, float worldWidth, float life, Color tint, int order)
        {
            if (sprite == null) return;
            var go = new GameObject("fxRipple"); go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sortingOrder = order; sr.color = tint;
            var r = go.AddComponent<FxRipple>(); r.sr = sr; r.width = worldWidth; r.life = life; r.c0 = tint;
            float k = worldWidth / Mathf.Max(0.01f, sprite.bounds.size.x);
            r.baseScale = new Vector3(k, k, 1f);
            go.transform.localScale = r.baseScale * 0.4f;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= life) { Destroy(gameObject); return; }
            float k = age / life;
            transform.localScale = baseScale * Mathf.Lerp(0.4f, 1f, 1f - (1f - k) * (1f - k));
            var c = c0; c.a = c0.a * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI); sr.color = c;
        }
    }

    public class FxRing : MonoBehaviour
    {
        public float from, to, life; public Color c0;
        float age; SpriteRenderer sr;
        void Start() { sr = GetComponent<SpriteRenderer>(); transform.localScale = Vector3.one * from; }
        void Update()
        {
            age += Time.deltaTime;
            if (age >= life) { Destroy(gameObject); return; }
            float k = age / life;
            transform.localScale = Vector3.one * Mathf.Lerp(from, to, 1f - (1f - k) * (1f - k));
            var c = c0; c.a = c0.a * (1f - k); sr.color = c;
        }
    }

    public class FxGlowPulse : MonoBehaviour
    {
        public float size, minAlpha, maxAlpha, speed, phase; public Color color;
        SpriteRenderer sr;
        void Awake() { sr = GetComponent<SpriteRenderer>(); }
        void Update()
        {
            if (sr == null) return;
            float k = (Mathf.Sin(Time.time * speed + phase) + 1f) * 0.5f;
            var c = color; c.a = Mathf.Lerp(minAlpha, maxAlpha, k); sr.color = c;
            transform.localScale = Vector3.one * size * (0.92f + 0.16f * k);
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
