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
        // painted flipbook: the sprite steps through `frames` (once across the life, or looping at `fps`); tailFade holds the colour, then fades out over the last 30%
        public Sprite[] frames; public bool loopFrames, reverseFrames, tailFade, fadeIn; public float fps = 14f;
        SpriteRenderer sr;

        bool started;

        void Awake() { sr = GetComponent<SpriteRenderer>(); }

        /// <summary>Back to a clean state so a pooled object can be reused.</summary>
        public void ResetFx()
        {
            life = 0.3f; age = 0f; delay = 0f; s0 = s1 = Vector2.one; c0 = Color.white; c1 = new Color(1, 1, 1, 0);
            vel = Vector2.zero; gravity = drag = spin = 0f; easeOut = true; alignToVelocity = unscaled = false; follow = null; followOffset = Vector3.zero;
            frames = null; loopFrames = reverseFrames = tailFade = fadeIn = false; fps = 14f;
            started = false;
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = true;
        }

        void Apply(float k)
        {
            float e = easeOut ? 1f - (1f - k) * (1f - k) : k;
            transform.localScale = new Vector3(Mathf.Lerp(s0.x, s1.x, e), Mathf.Lerp(s0.y, s1.y, e), 1f);
            if (sr == null) return;
            var col = Color.Lerp(c0, c1, tailFade ? Mathf.Clamp01((k - 0.7f) / 0.3f) : k);
            if (fadeIn) col.a *= Mathf.Clamp01(k / 0.15f);
            sr.color = col;
            if (frames != null && frames.Length > 1)
            {
                int n = frames.Length;
                int idx = loopFrames ? (int)(age * fps) % n : Mathf.Min(n - 1, (int)(k * n));
                if (reverseFrames) idx = n - 1 - idx;
                if (sr.sprite != frames[idx]) sr.sprite = frames[idx];
            }
        }

        void Update()
        {
            float dt = unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            if (!started) { started = true; Apply(0f); if (delay > 0f && sr != null) sr.enabled = false; }
            if (delay > 0f) { delay -= dt; if (delay <= 0f && sr != null) sr.enabled = true; return; }
            age += dt;
            if (age >= life) { FightFx.Recycle(this); return; }
            vel.y -= gravity * dt;
            vel *= Mathf.Max(0f, 1f - drag * dt);
            if (follow != null) transform.position = follow.position + followOffset;
            transform.position += (Vector3)(vel * dt);
            if (alignToVelocity && vel.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg);
            else if (spin != 0f) transform.Rotate(0, 0, spin * dt);
            Apply(age / life);
        }
    }

    /// <summary>The painted VFX sheets in Resources/Level02/Vfx (cut by Tools/VfxSheets/make_vfx.py): hit spark, impact, slash arc, fire, bolt,
    /// crackle, wind, orb burst, aura ring, vanish smoke, dust, crack, rocks, sun, mandala. Missing sheets are simply skipped (the procedural effects stay).</summary>
    public static class VfxSheets
    {
        [System.Serializable] public class Meta { public string name; public int n; public int cell; public string pivot; }
        [System.Serializable] public class MetaList { public Meta[] sheets; }

        public class Sheet { public Sprite[] frames; public int cell; public bool bottom; }

        static System.Collections.Generic.Dictionary<string, Sheet> sheets;

        static void Load()
        {
            if (sheets != null) return;
            sheets = new System.Collections.Generic.Dictionary<string, Sheet>();
            var ta = Resources.Load<TextAsset>("Level02/Vfx/vfx");
            if (ta == null) return;
            var list = JsonUtility.FromJson<MetaList>(ta.text);
            if (list == null || list.sheets == null) return;
            foreach (var m in list.sheets)
            {
                var fr = new Sprite[m.n]; bool ok = true;
                for (int i = 0; i < m.n; i++) { fr[i] = Resources.Load<Sprite>("Level02/Vfx/" + m.name + "_" + i.ToString("00")); if (fr[i] == null) ok = false; }
                if (ok) sheets[m.name] = new Sheet { frames = fr, cell = Mathf.Max(1, m.cell), bottom = m.pivot == "b" };
                else Debug.LogWarning("VFX sheet '" + m.name + "' is missing frames");
            }
        }

        public static Sheet Get(string name) { Load(); return sheets.TryGetValue(name, out var s) ? s : null; }
        public static bool Has(string name) => Get(name) != null;
    }

    /// <summary>Hit sparks, shockwaves, auras, pillars and smoke: painted flipbooks (VfxSheets) layered over procedural sprites.</summary>
    public static class FightFx
    {
        public const int Order = 120;
        public const int Behind = 3;           // in front of the floor and shadows, behind the fighters (dust, smoke)
        const int MaxLive = 120;

        // effects are pooled: a hit used to create dozens of fresh objects, which stutters phones
        static readonly System.Collections.Generic.Stack<FxAnim> pool = new System.Collections.Generic.Stack<FxAnim>();
        public static int Live;

        public static void Recycle(FxAnim a)
        {
            if (a == null) return;
            Live = Mathf.Max(0, Live - 1);
            a.gameObject.SetActive(false);
            if (pool.Count < 400) pool.Push(a); else Object.Destroy(a.gameObject);
        }

        public static FxAnim Spawn(Sprite s, Vector3 pos, float life, Vector2 s0, Vector2 s1, Color c0, Color c1, int order = Order, float rot = 0f)
        {
            FxAnim a = null;
            while (pool.Count > 0 && a == null) a = pool.Pop();      // objects from an unloaded scene come back null: skip them
            SpriteRenderer sr;
            if (a == null)
            {
                var go = new GameObject("fx");
                sr = go.AddComponent<SpriteRenderer>();
                a = go.AddComponent<FxAnim>();
            }
            else { a.gameObject.SetActive(true); sr = a.GetComponent<SpriteRenderer>(); }
            a.ResetFx();
            Live++;
            a.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, 0, rot));
            sr.sprite = s; sr.sortingOrder = order; sr.color = c0;
            a.life = life; a.s0 = s0; a.s1 = s1; a.c0 = c0; a.c1 = c1;
            return a;
        }

        static Color A(Color c, float a) { c.a = a; return c; }

        // ------------------------------------------------------------------ painted flipbooks
        /// <summary>
        /// Plays a painted sheet. size = world width of one source cell (the effect nearly fills it); pos = its centre, or its ground/base point for
        /// base-anchored sheets (dust, fire, bolt). life stretches the animation (looping sheets repeat at fps). aspectY squashes it flat (ground rings).
        /// Returns null when the sheet is not there.
        /// </summary>
        public static FxAnim Flip(string sheet, Vector3 pos, float size, float life, Color tint, int order = Order + 3, float rot = 0f, bool flipX = false,
            bool loop = false, float fps = 14f, float aspectY = 1f, float grow = 1f, Transform follow = null, bool reverse = false)
        {
            var sh = VfxSheets.Get(sheet);
            if (sh == null || Live > MaxLive) return null;       // long combos: never pile flipbooks up
            float sc = size * 100f / sh.cell;
            if (sh.bottom) pos.y += sh.frames[0].bounds.size.y * sc * aspectY * 0.5f;
            var s0 = new Vector2(sc * (flipX ? -1f : 1f), sc * aspectY);
            var a = Spawn(sh.frames[reverse ? sh.frames.Length - 1 : 0], pos, life, s0, s0 * grow, tint, A(tint, 0f), order, rot);
            a.frames = sh.frames; a.loopFrames = loop; a.fps = fps; a.reverseFrames = reverse; a.tailFade = true;
            if (follow != null) { a.follow = follow; a.followOffset = pos - follow.position; }
            return a;
        }

        /// <summary>One still frame of a sheet (ground cracks, the sun disc) that holds, then fades.</summary>
        public static FxAnim Still(string sheet, int index, Vector3 pos, float size, float life, Color tint, int order, bool flipX = false, float aspectY = 1f, float grow = 1f)
        {
            var sh = VfxSheets.Get(sheet);
            if (sh == null) return null;
            float sc = size * 100f / sh.cell;
            var s0 = new Vector2(sc * (flipX ? -1f : 1f), sc * aspectY);
            var a = Spawn(sh.frames[Mathf.Clamp(index, 0, sh.frames.Length - 1)], pos, life, s0, s0 * grow, tint, A(tint, 0f), order);
            a.tailFade = true; a.easeOut = true;
            return a;
        }

        /// <summary>The crescent that trails a weapon swing (white sheet, tinted per fighter).</summary>
        public static void SwingArc(Fighter f, MoveDef m, Color tint)
        {
            if (!VfxSheets.Has("slash") || f == null || m == null) return;
            float sc = f.scale, dir = f.facing >= 0 ? 1f : -1f;
            string id = m.id ?? "";
            float rot = -70f, y = 1.5f, size = 1.8f * sc;
            switch (id)
            {
                case "m3": case "charge1": case "charge2": case "dive": case "meteor": case "air3": case "finalblow": case "overhead": case "gslam": break;
                case "whirl": rot = 0f; y = 1.2f; size = 2.0f * sc; break;
                default: return;            // quick combo swings: no arc, the hit spark is enough
            }
            var pos = f.transform.position + new Vector3(dir * 0.8f * sc, y * sc, 0f);
            Flip("slash", pos, size, 0.2f, tint, Order + 1, dir > 0 ? rot : -rot, dir < 0, false, 14f, 0.75f);
        }

        /// <summary>Face-on golden ring that blooms outward (power-ups, bhakti).</summary>
        public static void Aura(Vector3 pos, float size, Color tint, float life = 0.5f)
        {
            Flip("aura", pos, size, life, tint, Order + 2);
        }

        /// <summary>A slowly turning sun disc that fades in behind a big moment (Panchmukhi).</summary>
        public static FxAnim Sun(Vector3 pos, float size, float life, Color tint, int order = -30)
        {
            var a = Still("sun", 0, pos, size, life, tint, order, false, 1f, 1.1f);
            if (a != null) { a.spin = 8f; a.easeOut = false; a.fadeIn = true; }
            return a;
        }

        /// <summary>Magic smoke puff (Ahiravana's shadow step, illusions). reverse plays it as an implosion: the reappearing side.</summary>
        public static void Poof(Vector3 pos, float size, bool reverse = false)
        {
            if (Flip("vanish", pos, size, 0.55f, Color.white, Behind, Random.Range(0f, 360f), false, false, 14f, 1f, 1f, null, reverse) == null)
                Smoke(pos, 8, new Color(0.5f, 0.2f, 0.8f, 0.8f), 1f);
        }

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

            // the painted burst is the hit: spark for light hits, starburst for heavy, fire burst / dark orb for elemental ones
            string sheet = kind == FxKind.Dark ? "orb" : kind == FxKind.Fire ? "fireburst" : heavy ? "impact" : "spark";
            float lo, hi;
            if (kind == FxKind.Dark) { lo = 1.4f; hi = 1.8f; }
            else if (kind == FxKind.Fire) { lo = 1.2f; hi = 1.6f; }
            else if (heavy) { lo = 1.6f; hi = 2.0f; }
            else { lo = 0.9f; hi = 1.2f; }
            float sz = Mathf.Lerp(lo, hi, power) * Random.Range(0.92f, 1.08f);
            bool painted = Flip(sheet, pos, sz, heavy ? 0.35f : 0.28f, Color.white, Order + 4, Random.Range(0f, 360f)) != null;
            // a short white pop sells the contact frame
            Spawn(FightArt.Soft, pos, 0.06f, Vector2.one * 0.5f * k, Vector2.one * 0.8f * k, A(Color.white, 0.7f), A(Color.white, 0f), Order + 6);
            if (painted)
            {
                if (heavy && Live < 90) Sparks(pos, Mathf.RoundToInt(Mathf.Lerp(4f, 6f, power)), 10f, c, 0.7f, 0.25f, dirX);
                return;
            }
            // fallback without the sheets: the procedural burst
            Spawn(FightArt.Soft, pos, 0.22f * k, Vector2.one * 0.9f * k, Vector2.one * 2.0f * k, A(c, 0.75f), A(c, 0f), Order + 1);
            Spawn(FightArt.Star, pos, 0.17f * k, Vector2.one * 0.5f * k, Vector2.one * 2.0f * k, Color.white, A(c, 0f), Order + 4, Random.Range(0f, 45f));
            Spawn(FightArt.Ring, pos, 0.26f, Vector2.one * 0.3f, Vector2.one * 2.3f * k, A(c, 0.9f), A(c, 0f), Order + 3);
            Slash(pos, ang + Random.Range(-10f, 10f), 1.7f * k, c, Order + 5);
            if (heavy) { Slash(pos, ang + 38f, 1.2f * k, c, Order + 5); Slash(pos, ang - 38f, 1.2f * k, c, Order + 5); }
            float th = Live > 110 ? 0.5f : 1f;
            Rays(pos, Mathf.Max(2, Mathf.RoundToInt((heavy ? 8 : 4) * th)), 0.85f * Mathf.Sqrt(k), 9f * Mathf.Sqrt(k), c, dir);
            Sparks(pos, Mathf.Max(2, Mathf.RoundToInt((heavy ? 10 : 4) * Mathf.Lerp(0.8f, 1.4f, power) * th)), heavy ? 11f : 8f, c, heavy ? 0.9f : 0.6f, 0.3f, dirX);
            if (kind == FxKind.Fire) Flames(pos, heavy ? 10 : 5, 1f);
            if (kind == FxKind.Dark) Smoke(pos, 4, new Color(0.45f, 0.2f, 0.7f, 0.7f), 1f);
        }

        /// <summary>A thin bright bar that streaks through the contact point and snaps shut.</summary>
        public static void Slash(Vector3 pos, float angleDeg, float length, Color c, int order)
        {
            Spawn(FightArt.Spark, pos, 0.14f, new Vector2(length * 0.5f, 0.34f), new Vector2(length * 1.2f, 0.03f), Color.Lerp(Color.white, c, 0.25f), A(c, 0f), order, angleDeg);
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
            // one small painted cloud on the floor, behind the fighters (only for real impacts: n >= 4); a couple of soft puffs otherwise
            if (VfxSheets.Has("dust"))
            {
                if (n >= 4) Flip("dust", new Vector3(pos.x, pos.y + 0.03f, pos.z), size * 1.8f * Random.Range(0.92f, 1.08f), Random.Range(0.4f, 0.5f),
                    new Color(1f, 0.97f, 0.95f, 0.75f), Behind, 0f, dirX != 0f ? dirX < 0f : Random.value < 0.5f);
                n = Mathf.Min(n, 2);
            }
            for (int i = 0; i < n; i++)
            {
                var a = Spawn(FightArt.Soft, pos + new Vector3(Random.Range(-0.2f, 0.2f), 0.1f, 0), Random.Range(0.35f, 0.6f),
                    Vector2.one * size * 0.5f, Vector2.one * size * Random.Range(1.2f, 1.8f), c, A(c, 0f), Behind);
                a.vel = new Vector2(Random.Range(-spread, spread) + dirX, Random.Range(0.3f, 1.4f)); a.drag = 3f;
            }
        }

        /// <summary>Flat ring racing along the floor plus dust and rocks.</summary>
        public static void Shockwave(Vector3 groundPos, float size, Color c)
        {
            Spawn(FightArt.Ring, groundPos + Vector3.up * 0.05f, 0.45f, new Vector2(0.5f, 0.12f), new Vector2(size * 2f, size * 0.45f), A(c, 1f), A(c, 0f), 61);
            Spawn(FightArt.Soft, groundPos + Vector3.up * 0.3f, 0.35f, new Vector2(1f, 0.4f), new Vector2(size * 1.5f, size * 0.5f), A(c, 0.8f), A(c, 0f), 62);
            // glowing cracks left in the floor by the big slams, then they cool off
            // only the big slams crack the floor; the crack cools off in about a second
            if (size >= 2.2f) Still("crack", Random.Range(0, 4), groundPos + Vector3.up * 0.04f, Mathf.Clamp(size * 1.1f, 2.5f, 3.5f) * Mathf.Max(1f, size / 3.2f), 1.1f, Color.white, Behind - 1, Random.value < 0.5f);
            Dust(groundPos, 6, size * 1.2f, 0f, 0.9f);
            Rocks(groundPos, size >= 2.2f ? 4 : 3, size);
        }

        public static void Rocks(Vector3 pos, int n, float power)
        {
            var c = new Color(0.22f, 0.2f, 0.26f, 1f);
            var rocks = VfxSheets.Get("rock");
            for (int i = 0; i < n; i++)
            {
                float s = Random.Range(0.08f, 0.22f) * Mathf.Clamp(power * 0.5f, 0.8f, 2f);
                FxAnim a;
                if (rocks != null)
                {
                    // painted lava-rock chunks, 0.2-0.4 units wide
                    var spr = rocks.frames[Random.Range(0, rocks.frames.Length)];
                    float k = Random.Range(0.2f, 0.4f) * Mathf.Clamp(power * 0.4f, 0.8f, 1.5f) / Mathf.Max(0.1f, spr.bounds.size.x);
                    var tint = new Color(1f, 1f, 1f, 1f);
                    a = Spawn(spr, pos + Vector3.up * 0.1f, Random.Range(0.6f, 0.9f), Vector2.one * k, Vector2.one * k, tint, A(tint, 0.5f), 63, Random.Range(0f, 360f));
                }
                else a = Spawn(FightArt.Square, pos + Vector3.up * 0.1f, Random.Range(0.6f, 0.9f), Vector2.one * s, Vector2.one * s, c, A(c, 0.6f), 63, Random.Range(0f, 90f));
                a.vel = new Vector2(Random.Range(-4f, 4f) * power * 0.4f, Random.Range(4f, 9f)); a.gravity = 28f; a.spin = Random.Range(-500f, 500f); a.easeOut = false;
            }
        }

        public static void Flames(Vector3 pos, int n, float size, Transform follow = null)
        {
            // painted fire tongues rise from the spot (a few per call); the soft puffs below add glow and embers
            if (VfxSheets.Has("fire"))
            {
                int tongues = n >= 6 ? 2 : 1;
                for (int i = 0; i < tongues; i++)
                {
                    var f = Flip("fire", pos + new Vector3(Random.Range(-0.15f, 0.15f) * size, Random.Range(-0.1f, 0.1f) * size, 0f), 1.0f * size * Random.Range(0.85f, 1.1f), Random.Range(0.35f, 0.45f),
                        Color.white, 18, 0f, Random.value < 0.5f, true, 16f);
                    if (f != null) f.vel = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.8f, 1.6f)) * size;
                }
                n = Mathf.Min(n, 2);
            }
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
