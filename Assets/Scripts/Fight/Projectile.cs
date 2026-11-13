using UnityEngine;

namespace BalHanuman.Fight
{
    /// <summary>Dark orbs (blockable, parry or mace-hit to reflect) and ground shockwaves (unblockable: jump, dodge or fly over them).</summary>
    public class Projectile : MonoBehaviour
    {
        public enum Kind { Orb, GroundWave }
        public Kind kind;
        public Fighter owner;
        public bool playerSide;         // true once Hanuman owns it (reflected)
        public Vector2 vel;
        public float radius = 0.42f, life = 3.5f;
        public HitWindow hit;
        SpriteRenderer core, glow;
        float trailT, orbK;       // orbK > 0: painted orb art, scaled by this

        public static Projectile Make(Kind k, Fighter owner, Vector3 pos, Vector2 vel, float dmg, float radius)
        {
            var go = new GameObject(k == Kind.Orb ? "DarkOrb" : "GroundWave");
            go.transform.position = pos;
            var p = go.AddComponent<Projectile>();
            p.kind = k; p.owner = owner; p.vel = vel; p.radius = radius;
            p.playerSide = owner is HanumanFighter;
            var col = new Color(0.72f, 0.35f, 1f);
            p.glow = FightArt.Quad("Glow", go.transform, FightArt.Soft, Vector2.zero, Vector2.one * radius * 4f, new Color(col.r, col.g, col.b, 0.8f), FightFx.Order - 3);
            p.core = FightArt.Quad("Core", go.transform, FightArt.Circle, Vector2.zero, Vector2.one * radius * 1.4f, new Color(1f, 0.75f, 1f), FightFx.Order - 2);
            var orbArt = k == Kind.Orb ? Resources.Load<Sprite>("Level02/Ahiravana/ahi_orb") : null;
            if (orbArt != null && !p.playerSide)
            {   // Ahiravana's painted dark orb (it spins; a reflected one turns gold)
                p.core.sprite = orbArt; p.core.color = Color.white;
                p.orbK = radius * 3.0f / orbArt.bounds.size.x;
                p.core.transform.localScale = Vector3.one * p.orbK;
            }
            if (k == Kind.GroundWave)
            {
                p.glow.transform.localScale = new Vector3(radius * 3f, radius * 5f, 1f);
                p.core.sprite = FightArt.Tri; p.core.transform.localScale = new Vector3(radius * 2.4f, radius * 3.2f, 1);
                p.core.transform.localPosition = new Vector3(0, -radius, 0);
                p.life = 4f;
            }
            p.hit = new HitWindow
            {
                damage = dmg, radius = radius, knock = k == Kind.Orb ? new Vector2(3f, 3f) : new Vector2(3f, 8f), stun = 0.45f,
                level = k == Kind.Orb ? HitLevel.Mid : HitLevel.Unblockable, fx = FxKind.Dark, heavy = k == Kind.GroundWave,
                knockdown = k == Kind.GroundWave, hitstop = 0.07f, shake = 0.08f
            };
            return p;
        }

        public void Reflect(Fighter by)
        {
            playerSide = by is HanumanFighter; owner = by;
            vel = new Vector2(-vel.x * 1.35f, 0f);
            if (Mathf.Abs(vel.x) < 6f) vel.x = by.facing * 10f;
            hit.damage *= 1.5f; hit.fx = FxKind.Gold; life = 3f;
            var c = new Color(1f, 0.85f, 0.35f);
            core.color = new Color(1f, 0.97f, 0.8f); glow.color = new Color(c.r, c.g, c.b, 0.85f);
            FightFx.Hit(transform.position, FxKind.Gold, true, Mathf.Sign(vel.x));
            FightSfx.Play("clang", 1.2f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            if (life <= 0f || Mathf.Abs(transform.position.x) > 30f) { Kill(false); return; }
            transform.position += (Vector3)(vel * dt);
            trailT -= dt;
            if (trailT <= 0f)
            {
                trailT = 0.03f;
                var c = glow.color; c.a = 0.5f;
                var a = FightFx.Spawn(FightArt.Soft, transform.position, 0.3f, Vector2.one * radius * 2.4f, Vector2.one * radius * 0.6f, c, new Color(c.r, c.g, c.b, 0f), FightFx.Order - 4);
                if (kind == Kind.GroundWave) { FightFx.Dust(transform.position + Vector3.down * radius, 1, 0.5f, 0, 0.6f); if (Random.value < 0.3f) FightFx.Rocks(transform.position, 1, 0.8f); }
            }
            float pulse = 1f + Mathf.Sin(Time.time * 30f) * 0.08f;
            if (kind == Kind.Orb && orbK > 0f) { core.transform.localScale = Vector3.one * orbK * pulse; core.transform.Rotate(0, 0, 140f * dt); }
            else core.transform.localScale = kind == Kind.Orb ? Vector3.one * radius * 1.4f * pulse : core.transform.localScale;

            var dir = FightDirector.I;
            if (dir == null) return;
            foreach (var t in dir.ProjectileTargets(playerSide))
            {
                if (t == null || !t.gameObject.activeInHierarchy || !t.Overlaps(transform.position, radius)) continue;
                var res = t.ReceiveHit(owner != null ? owner : t, hit, transform.position, vel.x >= 0 ? 1 : -1, true);
                if (res.parried) { Reflect(t); return; }
                if (res.dodged) continue;
                if (res.connected || res.blocked) { Kill(true); return; }
            }
        }

        public void Kill(bool burst)
        {
            if (burst) { FightFx.Hit(transform.position, hit.fx, kind == Kind.GroundWave, 0f); FightSfx.Play("orb_blast", 1f, 0.8f); }
            FightDirector.I?.Remove(this);
            Destroy(gameObject);
        }
    }
}
