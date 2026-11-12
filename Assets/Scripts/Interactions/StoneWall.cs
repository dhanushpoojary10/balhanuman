using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BalHanuman
{
    /// <summary>
    /// A wall of stones that only Hanuman's gada can break. A required stop the player works through himself:
    ///  - without the gada: the message says the wall needs his gada, and the hand button "Find Gada" opens the puzzle;
    ///  - with the gada: the hand button says "Break", and tapping it swings the gada and shatters the wall.
    /// </summary>
    public class StoneWall : InteractableBase
    {
        [Tooltip("The wall itself (a solid stack of boulders).")]
        public Sprite wallSprite;
        public float wallHeight = 4.2f;
        [Tooltip("Rocks that fly apart when it breaks.")]
        public Sprite[] debris;
        [Tooltip("Fallback look when no wall sprite is set: temple blocks stacked up.")]
        public Sprite blockA, blockB;
        public Sprite dustSprite, sparkleSprite;
        [Tooltip("Hanuman's strike frames, played while he swings the gada.")]
        public Sprite[] strikeFrames;
        public int sortingOrder = 6;

        readonly List<SpriteRenderer> stones = new List<SpriteRenderer>();
        bool broken, busy;

        public bool Broken => broken;
        public override bool CanInteract => !broken && !busy;
        public override string GetInteractionPrompt() => S != null && S.HasGada ? "Break" : "Find Gada";

        // required stop; the player does both steps with the hand button, and the message says what to do
        protected override bool BlocksPath => !broken;
        protected override bool AutoStart => false;
        protected override string ArriveHint => S != null && S.HasGada
            ? "Tap the hand button (or press E) to break the wall!"
            : "To break this stone wall, Hanuman needs his gada! Tap the hand button to find it.";
        protected override string BlockedMessage => "";

        void Awake()
        {
            // the wall is ~2.4 wide: Hanuman stops just in front of it, where the hand button shows
            gateOffset = -1.7f;
            autoStartDistance = 2.4f;
            Build();
        }

        // ---------------------------------------------------------------- stones
        void Build()
        {
            if (wallSprite != null)
            {
                // one solid boulder wall, bottom on the ground, a touch wider than the art for a sturdier look.
                // It is laid out from 2 x 3 pieces of its own picture, so the real wall is what flies apart.
                var s = wallSprite;
                var r = s.textureRect;
                const int cols = 2, rows = 3;
                float ppu = s.pixelsPerUnit;
                float scale = wallHeight / (r.height / ppu);
                Vector2 size = new Vector2(r.width / ppu * scale * 1.1f, wallHeight);
                for (int row = 0; row < rows; row++)
                    for (int col = 0; col < cols; col++)
                    {
                        var sub = new Rect(r.x + r.width * col / cols, r.y + r.height * row / rows, r.width / cols, r.height / rows);
                        var piece = Sprite.Create(s.texture, sub, new Vector2(0.5f, 0.5f), ppu);
                        var go = new GameObject("WallPiece" + (row * cols + col));
                        go.transform.SetParent(transform, false);
                        var sr = go.AddComponent<SpriteRenderer>();
                        sr.sprite = piece; sr.sortingOrder = sortingOrder;
                        go.transform.localScale = new Vector3(scale * 1.1f, scale, 1f);
                        go.transform.localPosition = new Vector3(-size.x / 2f + size.x * (col + 0.5f) / cols, size.y * (row + 0.5f) / rows, 0f);
                        stones.Add(sr);
                    }
                return;
            }
            // fallback: a rough stack of temple blocks, wider at the bottom
            Add(blockA, 0f, 0f, 2.5f, false);
            Add(blockB, 0.08f, 1.25f, 2.2f, false);
            Add(blockA, -0.05f, 2.25f, 2.3f, true);
            Add(blockB, 0.1f, 3.3f, 1.6f, true);
        }

        void Add(Sprite s, float x, float bottom, float width, bool flip)
        {
            if (s == null) return;
            var go = new GameObject("Stone" + stones.Count);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.flipX = flip; sr.sortingOrder = sortingOrder + stones.Count % 2;
            float scale = width / s.bounds.size.x;
            go.transform.localScale = Vector3.one * scale;
            go.transform.localPosition = new Vector3(x - s.bounds.center.x * scale, bottom - s.bounds.min.y * scale, 0f);
            stones.Add(sr);
        }

        // ---------------------------------------------------------------- use
        public override void Interact(PlayerController2D player)
        {
            if (broken || busy) return;
            if (!S.HasGada) S.OpenGadaPuzzle();       // "Find Gada": the Complete the Gada puzzle
            else StartCoroutine(Break(player));
        }

        IEnumerator Break(PlayerController2D p)
        {
            busy = true;
            p.InputLocked = true;
            p.Face(transform.position.x > p.transform.position.x);
            var body = p.Body; body.linearVelocity = new Vector2(0f, body.linearVelocity.y);

            if (S.Gada != null)
            {
                // wind-up -> swing -> impact (the wall shatters right then) -> recovery -> victory pose
                yield return S.Gada.Strike(() =>
                {
                    Shatter(p);
                    broken = true;
                    S.Toast("The wall crumbles! The path to the temple is open.", 3.5f);
                },
                // victory pose: a little shower of gold sparkles around him
                () => Fx.Burst(sparkleSprite, p.transform.position + Vector3.up * 1.7f, 12, 2.6f, 0.9f, 0.5f, 0.3f, new Color(1f, 0.92f, 0.55f), 2f, 30));
            }
            else
            {
                StartCoroutine(PlayStrike(p));
                yield return new WaitForSeconds(0.16f);
                Shatter(p);
                broken = true;
                yield return new WaitForSeconds(0.55f);
                S.Toast("The wall crumbles! The path to the temple is open.", 3f);
            }
            busy = false;
            p.InputLocked = false;
        }

        IEnumerator PlayStrike(PlayerController2D p)
        {
            if (strikeFrames == null || strikeFrames.Length == 0 || p.spriteRenderer == null) yield break;
            var anim = p.animator; bool had = anim != null && anim.enabled;
            if (anim != null) anim.enabled = false;
            var keep = p.spriteRenderer.sprite;
            foreach (var f in strikeFrames) { p.spriteRenderer.sprite = f; yield return new WaitForSeconds(1f / 16f); }
            p.spriteRenderer.sprite = keep;
            if (anim != null) anim.enabled = had;
        }

        void Shatter(PlayerController2D p)
        {
            Vector3 mid = transform.position + Vector3.up * 1.6f;
            SideScrollCamera.Shake(0.22f, 0.4f);
            Haptics.Heavy();
            Sfx.Play(Sfx.Id.Land, 1f, 0.55f);
            Sfx.Play(Sfx.Id.Lever, 0.8f, 0.6f);
            Fx.Burst(dustSprite, transform.position + Vector3.up * 0.6f, 22, 3.5f, 1.1f, -0.4f, 0.9f, new Color(0.9f, 0.82f, 0.7f, 0.85f), 0.6f, sortingOrder + 3, 1.5f);
            Fx.Burst(sparkleSprite, mid, 14, 4f, 0.8f, 1f, 0.35f, new Color(1f, 0.9f, 0.5f), 1.5f, sortingOrder + 4);
            FxGlow.Ring(mid, 0.5f, 4f, 0.45f, new Color(1f, 0.9f, 0.6f, 0.7f), sortingOrder + 2);
            float dir = transform.position.x >= p.transform.position.x ? 1f : -1f;
            foreach (var s in stones) StartCoroutine(Fly(s, dir));      // the wall's own pieces fly apart
            // a few extra small rocks for a fuller burst
            if (stones.Count > 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    // small chips of the same wall (or the debris sprites if set and there is no wall picture)
                    var d = wallSprite != null || debris == null || debris.Length == 0 ? stones[Random.Range(0, stones.Count)].sprite : debris[i % debris.Length];
                    if (d == null) continue;
                    var go = new GameObject("Rock" + i);
                    go.transform.SetParent(transform, false);
                    var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = d; sr.sortingOrder = sortingOrder;
                    float w = Random.Range(0.35f, 0.6f);
                    go.transform.localScale = Vector3.one * (w / d.bounds.size.x);
                    go.transform.localPosition = new Vector3(Random.Range(-0.9f, 0.9f), 0.5f + (i / 4f) * (wallHeight - 1f), 0f);
                    go.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-30f, 30f));
                    StartCoroutine(Fly(sr, dir));
                }
            }
        }

        IEnumerator Fly(SpriteRenderer s, float dir)
        {
            var t = s.transform;
            Vector2 v = new Vector2(dir * Random.Range(2.5f, 6f), Random.Range(3.5f, 7.5f));
            float spin = Random.Range(-260f, 260f), life = 1.3f, s0 = t.localScale.x;
            s.sortingOrder += 4;
            for (float a = 0; a < life; a += Time.deltaTime)
            {
                v.y -= 16f * Time.deltaTime;
                t.position += (Vector3)(v * Time.deltaTime);
                t.Rotate(0f, 0f, spin * Time.deltaTime);
                float k = a / life;
                t.localScale = Vector3.one * s0 * Mathf.Lerp(1f, 0.55f, k);
                var c = s.color; c.a = 1f - Mathf.Clamp01((k - 0.55f) / 0.45f); s.color = c;
                yield return null;
            }
            s.gameObject.SetActive(false);
        }
    }
}
