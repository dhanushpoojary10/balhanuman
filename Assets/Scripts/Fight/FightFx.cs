using UnityEngine;

namespace BalHanuman.Fight
{
    /// <summary>One self-animating effect sprite: scales, fades, moves, spins, then destroys itself.</summary>
    public class FxAnim : MonoBehaviour
    {
        public float life = 0.3f, age, delay;
        public Vector2 s0 = Vector2.one, s1 = Vector2.one;
        public Color c0 = Color.white, c1 = new Color(1, 1, 1, 0);
        public Vector2 vel; public float gravity, drag, spin;
        public bool easeOut = true, alignToVelocity, unscaled;
        public Transform follow; public Vector3 followOffset;
        SpriteRenderer sr;

        void Awake() { sr = GetComponent<SpriteRenderer>(); }

        void Start() { Apply(0f); if (delay > 0f && sr != null) sr.enabled = false; }

        void Apply(float k)
        {
            float e = easeOut ? 1f - (1f - k) * (1f - k) : k;
            transform.localScale = new Vector3(Mathf.Lerp(s0.x, s1.x, e), Mathf.Lerp(s0.y, s1.y, e), 1f);
            if (sr != null) sr.color = Color.Lerp(c0, c1, k);
        }

        void Update()
        {
            float dt = unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            if (delay > 0f) { delay -= dt; if (delay <= 0f && sr != null) sr.enabled = true; return; }
            age += dt;
            if (age >= life) { Destroy(gameObject); return; }
            vel.y -= gravity * dt;
            vel *= Mathf.Max(0f, 1f - drag * dt);
            if (follow != null) transform.position = follow.position + followOffset;
            transform.position += (Vector3)(vel * dt);
            if (alignToVelocity && vel.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg);
            else if (spin != 0f) transform.Rotate(0, 0, spin * dt);
            Apply(age / life);
        }
    }

    /// <summary>Hit sparks, shockwaves, auras, pillars and smoke built from the procedural sprites.</summary>
    public static class FightFx
    {
        public const int Order = 120;

        public static FxAnim Spawn(Sprite s, Vector3 pos, float life, Vector2 s0, Vector2 s1, Color c0, Color c1, int order = Order, float rot = 0f)
        {
            var go = new GameObject("fx");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, 0, rot);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.sortingOrder = order; sr.color = c0;
            var a = go.AddComponent<FxAnim>();
            a.life = life; a.s0 = s0; a.s1 = s1; a.c0 = c0; a.c1 = c1;
            return a;
        }

        static Color A(Color c, float a) { c.a = a; return c; }

        public static Color KindColor(FxKind k)
        {
            switch (k)
            {
                case FxKind.Heavy: return new Color(1f, 0.72f, 0.25f);
                case FxKind.Fire: return new Color(1f, 0.45f, 0.1f);
                case FxKind.Dark: return new Color(0.75f, 0.35f, 1f);
                case FxKind.Gold: return new Color(1f, 0.88f, 0.4f);
                default: return new Color(1f, 0.95f, 0.75f);
            }
        }

        public static void Sparks(Vector3 pos, int n, float speed, Color c, float len = 0.6f, float life = 0.28f, float dirX = 0f)
        {
            for (int i = 0; i < n; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                var v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed * Random.Range(0.5f, 1.1f);
                v.x += dirX * speed * 0.5f;
                float l = len * Random.Range(0.6f, 1.2f);
                var a = Spawn(FightArt.Spark, pos, life * Random.Range(0.7f, 1.2f), new Vector2(l, l * 0.35f), new Vector2(l * 0.3f, l * 0.12f), c, A(c, 0f), Order + 2);
                a.vel = v; a.drag = 4f; a.alignToVelocity = true; a.gravity = 4f;
            }
        }

        /// <summary>
        /// The main impact: white flash, star pop, slash bars along the blow, radial rays, ring, sparks and debris.
        /// Heavy = bigger and longer; power (0..1, from damage) scales it again so a jab and a crusher do not look alike.
        /// </summary>
        public static void Hit(Vector3 pos, FxKind kind, bool heavy, float dirX, float power = 0.5f)
        {
            var c = KindColor(kind);
            float pk = 0.85f + power * 0.9f;                  // 0.85 .. 1.75
            float k = (heavy ? 1.5f : 1f) * pk;
            float dir = dirX >= 0f ? 1f : -1f;
            float ang = dir > 0f ? 0f : 180f;

            // one-frame white flash plus the colour bloom
            Spawn(FightArt.Soft, pos, 0.07f, Vector2.one * 0.9f * k, Vector2.one * 1.4f * k, A(Color.white, 0.8f), A(Color.white, 0f), Order + 6);
            Spawn(FightArt.Soft, pos, 0.22f * k, Vector2.one * 0.9f * k, Vector2.one * 2.0f * k, A(c, 0.75f), A(c, 0f), Order + 1);
            // star pop and ring
            Spawn(FightArt.Star, pos, 0.17f * k, Vector2.one * 0.5f * k, Vector2.one * 2.0f * k, Color.white, A(c, 0f), Order + 4, Random.Range(0f, 45f));
            Spawn(FightArt.Ring, pos, 0.26f, Vector2.one * 0.3f, Vector2.one * 2.3f * k, A(c, 0.9f), A(c, 0f), Order + 3);
            // slash bars stretched along the blow (a cross for heavy hits)
            Slash(pos, ang + Random.Range(-10f, 10f), 1.7f * k, c, Order + 5);
            if (heavy) { Slash(pos, ang + 38f, 1.2f * k, c, Order + 5); Slash(pos, ang - 38f, 1.2f * k, c, Order + 5); }
            // rays burst outward in every direction, longer ones forward
            Rays(pos, heavy ? 10 : 6, 0.85f * Mathf.Sqrt(k), 9f * Mathf.Sqrt(k), c, dir);
            Sparks(pos, Mathf.RoundToInt((heavy ? 14 : 7) * Mathf.Lerp(0.8f, 1.5f, power)), heavy ? 11f : 8f, c, heavy ? 0.9f : 0.6f, 0.3f, dirX);
            if (heavy || power > 0.6f) Rocks(pos + Vector3.down * 0.2f, Mathf.RoundToInt(3 + power * 5f), 0.6f + power);
            if (kind == FxKind.Fire) Flames(pos, heavy ? 10 : 5, 1f);
            if (kind == FxKind.Dark) Smoke(pos, 4, new Color(0.45f, 0.2f, 0.7f, 0.7f), 1f);
        }

        /// <summary>A thin bright bar that streaks through the contact point and snaps shut.</summary>
        public static void Slash(Vector3 pos, float angleDeg, float length, Color c, int order)
        {
            Spawn(FightArt.Spark, pos, 0.13f, new Vector2(length * 0.55f, 0.28f), new Vector2(length * 1.25f, 0.02f), Color.white, A(c, 0f), order, angleDeg);
            Spawn(FightArt.Spark, pos, 0.17f, new Vector2(length * 0.45f, 0.5f), new Vector2(length, 0.04f), A(c, 0.7f), A(c, 0f), order - 1, angleDeg);
        }

        /// <summary>Evenly spread long thin rays flying outward from the impact (forward-biased).</summary>
        public static void Rays(Vector3 pos, int n, float len, float speed, Color c, float dir)
        {
            float off = Random.Range(0f, 360f);
            for (int i = 0; i < n; i++)
            {
                float a = (off + i * 360f / n) * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float fwd = 0.7f + 0.5f * Mathf.Max(0f, d.x * dir);
                var r = Spawn(FightArt.Spark, pos + (Vector3)d * 0.25f, 0.16f, new Vector2(len * fwd, len * 0.07f), new Vector2(len * 0.15f, len * 0.015f), Color.white, A(c, 0f), Order + 2);
                r.vel = d * speed * fwd; r.alignToVelocity = true; r.drag = 6f;
            }
        }

        public static void Block(Vector3 pos, float dirX)
        {
            var c = new Color(0.55f, 0.85f, 1f);
            Spawn(FightArt.Soft, pos, 0.18f, Vector2.one * 0.8f, Vector2.one * 1.8f, A(c, 0.8f), A(c, 0f), Order + 1);
            Spawn(FightArt.Ring, pos, 0.2f, Vector2.one * 0.4f, Vector2.one * 1.5f, A(c, 0.8f), A(c, 0f), Order + 2);
            Sparks(pos, 5, 7f, c, 0.5f, 0.22f, dirX);
        }

        public static void Parry(Vector3 pos)
        {
            var c = new Color(0.6f, 0.95f, 1f);
            Spawn(FightArt.Star, pos, 0.35f, Vector2.one * 1.2f, Vector2.one * 4.5f, Color.white, A(c, 0f), Order + 5, 0f);
            Spawn(FightArt.Star, pos, 0.35f, Vector2.one * 1f, Vector2.one * 3.5f, Color.white, A(c, 0f), Order + 5, 45f);
            Spawn(FightArt.Ring, pos, 0.4f, Vector2.one * 0.5f, Vector2.one * 4f, A(c, 1f), A(c, 0f), Order + 4);
            Sparks(pos, 16, 12f, c, 1f, 0.35f);
        }

        public static void Dust(Vector3 pos, int n, float spread, float dirX = 0f, float size = 0.7f)
        {
            var c = new Color(0.62f, 0.55f, 0.6f, 0.55f);
            for (int i = 0; i < n; i++)
            {
                var a = Spawn(FightArt.Soft, pos + new Vector3(Random.Range(-0.2f, 0.2f), 0.1f, 0), Random.Range(0.35f, 0.6f),
                    Vector2.one * size * 0.5f, Vector2.one * size * Random.Range(1.2f, 1.8f), c, A(c, 0f), 60);
                a.vel = new Vector2(Random.Range(-spread, spread) + dirX, Random.Range(0.3f, 1.4f)); a.drag = 3f;
            }
        }

        /// <summary>Flat ring racing along the floor plus dust and rocks.</summary>
        public static void Shockwave(Vector3 groundPos, float size, Color c)
        {
            Spawn(FightArt.Ring, groundPos + Vector3.up * 0.05f, 0.45f, new Vector2(0.5f, 0.12f), new Vector2(size * 2f, size * 0.45f), A(c, 1f), A(c, 0f), 61);
            Spawn(FightArt.Soft, groundPos + Vector3.up * 0.3f, 0.35f, new Vector2(1f, 0.4f), new Vector2(size * 1.5f, size * 0.5f), A(c, 0.8f), A(c, 0f), 62);
            Dust(groundPos, 10, size * 1.2f, 0f, 0.9f);
            Rocks(groundPos, 7, size);
        }

        public static void Rocks(Vector3 pos, int n, float power)
        {
            var c = new Color(0.22f, 0.2f, 0.26f, 1f);
            for (int i = 0; i < n; i++)
            {
                float s = Random.Range(0.08f, 0.22f) * Mathf.Clamp(power * 0.5f, 0.8f, 2f);
                var a = Spawn(FightArt.Square, pos + Vector3.up * 0.1f, Random.Range(0.6f, 0.9f), Vector2.one * s, Vector2.one * s, c, A(c, 0.6f), 63, Random.Range(0f, 90f));
                a.vel = new Vector2(Random.Range(-4f, 4f) * power * 0.4f, Random.Range(4f, 9f)); a.gravity = 28f; a.spin = Random.Range(-500f, 500f); a.easeOut = false;
            }
        }

        public static void Flames(Vector3 pos, int n, float size, Transform follow = null)
        {
            for (int i = 0; i < n; i++)
            {
                var c = Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.3f, 0.05f), Random.value);
                var a = Spawn(FightArt.Soft, pos + (Vector3)Random.insideUnitCircle * 0.15f * size, Random.Range(0.25f, 0.45f),
                    Vector2.one * 0.45f * size, Vector2.one * 0.1f * size, A(c, 0.95f), A(new Color(0.6f, 0.1f, 0.05f), 0f), Order);
                a.vel = new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(1.2f, 2.6f)) * size;
            }
        }

        public static void Smoke(Vector3 pos, int n, Color c, float size)
        {
            for (int i = 0; i < n; i++)
            {
                var a = Spawn(FightArt.Soft, pos + (Vector3)Random.insideUnitCircle * 0.3f * size, Random.Range(0.5f, 0.8f),
                    Vector2.one * 0.6f * size, Vector2.one * 1.8f * size, c, A(c, 0f), Order - 1);
                a.vel = Random.insideUnitCircle * 1.2f * size + Vector2.up * 0.6f; a.drag = 2f;
            }
        }

        /// <summary>Tall column of light (growth, transformations).</summary>
        public static void Pillar(Vector3 groundPos, float height, float width, Color c, float life = 0.9f)
        {
            var a = Spawn(FightArt.Soft, groundPos + Vector3.up * height * 0.5f, life, new Vector2(width * 0.3f, height), new Vector2(width * 1.4f, height * 1.3f), A(c, 0.95f), A(c, 0f), Order);
            a.easeOut = true;
            Spawn(FightArt.Soft, groundPos + Vector3.up * height * 0.5f, life * 0.8f, new Vector2(width * 0.12f, height), new Vector2(width * 0.4f, height * 1.2f), Color.white, A(Color.white, 0f), Order + 1);
            Spawn(FightArt.Ring, groundPos + Vector3.up * 0.1f, life * 0.6f, new Vector2(0.5f, 0.15f), new Vector2(width * 3f, width * 0.6f), A(c, 1f), A(c, 0f), 61);
            for (int i = 0; i < 18; i++)
            {
                var p = Spawn(FightArt.Soft, groundPos + new Vector3(Random.Range(-width, width), Random.Range(0f, height * 0.5f)), Random.Range(0.6f, 1.1f),
                    Vector2.one * 0.18f, Vector2.one * 0.05f, Color.white, A(c, 0f), Order + 2);
                p.vel = new Vector2(0, Random.Range(3f, 8f));
            }
        }

        public static void Sparkle(Vector3 pos, int n, float radius, Color c, bool inward)
        {
            for (int i = 0; i < n; i++)
            {
                var off = (Vector3)Random.insideUnitCircle.normalized * radius;
                var a = Spawn(FightArt.Star, inward ? pos + off : pos, 0.35f, Vector2.one * 0.35f, Vector2.one * 0.05f, Color.white, A(c, 0f), Order + 3, Random.Range(0, 90f));
                a.vel = (inward ? -off : off) / 0.35f;
            }
        }

        /// <summary>Speed streaks behind a dashing body.</summary>
        public static void Streaks(Vector3 pos, float dirX, int n, Color c)
        {
            for (int i = 0; i < n; i++)
            {
                var p = pos + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-1f, 1f));
                var a = Spawn(FightArt.Spark, p, 0.22f, new Vector2(1.8f, 0.12f), new Vector2(0.3f, 0.05f), A(c, 0.8f), A(c, 0f), Order - 2);
                a.vel = new Vector2(-dirX * 8f, 0);
            }
        }
    }
}
