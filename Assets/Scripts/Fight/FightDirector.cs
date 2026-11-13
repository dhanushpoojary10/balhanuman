using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BalHanuman.Fight
{
    /// <summary>Which way the fight scene starts (set by the main menu / pause menu before loading the scene).</summary>
    public static class FightSetup
    {
        public enum Mode { Story, Training, Demo }
        public static Mode Current = Mode.Story;
    }

    /// <summary>The move list shown in the training room and walked through by the demo reel.</summary>
    public static class MoveList
    {
        public class Entry
        {
            public string name, input, move, special; public int power = -1; public string section;
        }

        static Entry E(string section, string name, string input, string move = null, string special = null, int power = -1)
            => new Entry { section = section, name = name, input = input, move = move, special = special, power = power };

        public static readonly Entry[] Entries =
        {
            E("GADA", "Gada Combo", "GADA x3   (J J J)", special: "combo3"),
            E("GADA", "Vajra Thrust", "-> + GADA", move: "thrust"),
            E("GADA", "Whirlwind Gada", "<- + GADA", move: "whirl"),
            E("GADA", "Sky Launcher", "UP + GADA", move: "launcher"),
            E("GADA", "Ground Sweep", "DOWN + GADA", move: "sweep"),
            E("GADA", "Mountain-Breaker", "HOLD GADA, then release", special: "charge"),
            E("GADA", "Air Combo + Meteor Spike", "UP+GADA, then GADA x3 in the air", special: "aircombo"),
            E("GADA", "Diving Slam", "In the air: DOWN + GADA", special: "dive"),
            E("TAIL", "Kick Combo", "TAIL x2   (K K)", special: "kicks"),
            E("TAIL", "Tail Trip", "DOWN + TAIL", move: "tailtrip"),
            E("TAIL", "Tail Grab & Throw", "-> + TAIL", move: "tailgrab"),
            E("TAIL", "Lift & Body Slam", "<- + TAIL", move: "bodyslam"),
            E("TAIL", "Flying Kick", "In the air: TAIL", special: "flykick"),
            E("DEFENCE", "Parry", "Tap <- just before a hit lands", special: "parry"),
            E("DEFENCE", "Perfect Dodge", "DODGE (L) just before a hit", special: "dodge"),
            E("DEFENCE", "Reflect Dark Orb", "Strike an orb with the gada", special: "reflect"),
            E("COMBO", "Vanara Fury", "GADA, GADA, TAIL, UP+GADA", special: "vanara"),
            E("BOON", "Pawan Vega - Wind Speed", "Boon 1 (VAYU)", special: "vayu"),
            E("BOON", "Flight + Meteor Dive", "Boon 2 (FLY), then DOWN+GADA", special: "flight"),
            E("BOON", "Agni Puccha - Fire Tail", "Boon 3 (FIRE), then TAIL", special: "fire"),
            E("BOON", "Anima - Shrink", "Boon 4 (TINY), then GADA to rise", special: "shrink"),
            E("BOON", "Vajra Kaya - Thunder Body", "Boon 5 (VAJRA)", special: "vajra"),
            E("BOON", "Mahima - Giant Form", "Boon 6 (GIANT), then DOWN+GADA", special: "growth"),
            E("ULTIMATE", "PANCHMUKHI", "ULTIMATE (U) with 3 Bhakti bars", special: "panch"),
        };
    }

    /// <summary>
    /// Runs Level 2 "Patala Yuddha": builds the arena, fighters, camera and HUD, owns hit effects (hit-stop, slow-mo, shake),
    /// the round flow (R1 duel, R2 sorcery, R3 titans, Panchmukhi finale), the training room and the demo reel.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class FightDirector : MonoBehaviour
    {
        public static FightDirector I { get; private set; }

        public UiSkin skin;
        public float arenaHalf = 11f;

        [System.NonSerialized] public HanumanFighter hanuman;
        [System.NonSerialized] public AhiravanaFighter boss;
        readonly List<AhiravanaFighter> clones = new List<AhiravanaFighter>();
        readonly List<Projectile> projectiles = new List<Projectile>();
        FightCamera cam; FightHud hud; FightArena arena; FightMusic music;
        FightSetup.Mode mode;

        bool live, paused, finale, ultBusy, victory;
        int round, roundsWon, roundResult;
        float slowUntil;

        // combo + stats
        int comboHits, maxCombo, parries, perfectDodges, lastRank;
        float comboLast, totalDamage, fightStart;
        readonly List<string> chain = new List<string>();
        readonly HashSet<string> namedThisCombo = new HashSet<string>();
        MoveDef lastDodgedMove;
        float bossIdleSince;

        public bool ControlsLive => live && !paused;
        public int CloneCount => clones.Count;
        public FightSetup.Mode CurrentMode => mode;

        // =========================================================================================== setup
        void Awake()
        {
            I = this;
            Time.timeScale = 1f;
            UiKit.Init(skin);
            FightInput.Reset();
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            mode = FightSetup.Current;
            Music.Duck(0f, 99999f);
            music = FightMusic.Create();

            arena = FightArena.Build(FindAnyObjectByType<Light2D>());

            hanuman = new GameObject("Hanuman").AddComponent<HanumanFighter>();
            hanuman.Init(this);
            boss = new GameObject("Ahiravana").AddComponent<AhiravanaFighter>();
            boss.Init(this, false);
            hanuman.opponent = boss; boss.opponent = hanuman;
            hanuman.transform.position = new Vector3(-3f, 0, 0); boss.transform.position = new Vector3(3f, 0, 0); boss.facing = -1;

            var c = Camera.main;
            cam = c.GetComponent<FightCamera>();
            if (cam == null) cam = c.gameObject.AddComponent<FightCamera>();
            cam.a = hanuman; cam.b = boss; cam.arenaHalf = arenaHalf + 3.5f;
            c.backgroundColor = new Color(0.05f, 0.03f, 0.09f);

            hud = FightHud.Build(this);
        }

        void Start()
        {
            cam.Snap();
            switch (mode)
            {
                case FightSetup.Mode.Training: StartCoroutine(Training()); break;
                case FightSetup.Mode.Demo: StartCoroutine(DemoReel()); break;
                default: StartCoroutine(Story()); break;
            }
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            Time.timeScale = 1f;
        }

        // =========================================================================================== frame
        void Update()
        {
            FightInput.Poll();
            var k = Keyboard.current;
            if (k != null && k.escapeKey.wasPressedThisFrame && !victory) TogglePause();

            if (!paused && slowUntil > 0f && Time.unscaledTime >= slowUntil) { Time.timeScale = 1f; slowUntil = 0f; }

            // the combo ends once the boss recovers
            if (comboHits > 0)
            {
                bool hurt = boss.InHitReaction;
                if ((!hurt && Time.time - comboLast > 0.45f) || Time.time - comboLast > 1.5f) EndCombo();
            }

            if (mode == FightSetup.Mode.Story && live && round == 3 && !finale && boss.hp <= 1.01f) StartCoroutine(Finale());

            if (mode != FightSetup.Mode.Story)
            {
                // keep the training dummy alive
                if (boss.InHitReaction || boss.Attacking) bossIdleSince = Time.time;
                if (Time.time - bossIdleSince > 1.6f && boss.hp < boss.maxHp && boss.Alive) boss.hp = Mathf.MoveTowards(boss.hp, boss.maxHp, Time.deltaTime * 600f);
                if (hanuman.hp < hanuman.maxHp * 0.5f && !hanuman.InHitReaction) hanuman.hp = Mathf.MoveTowards(hanuman.hp, hanuman.maxHp, Time.deltaTime * 400f);
            }
            clones.RemoveAll(x => x == null);
        }

        void LateUpdate()
        {
            Separate(hanuman, boss);
            foreach (var cl in clones) if (cl != null) Separate(hanuman, cl);
        }

        void Separate(Fighter a, Fighter b)
        {
            if (a == null || b == null || !b.gameObject.activeInHierarchy) return;
            if (a.state == FState.Grabbed || b.state == FState.Grabbed || a.holding == b || b.holding == a) return;
            if (a.state == FState.Dead || b.state == FState.Dead || a.state == FState.Knockdown || b.state == FState.Knockdown) return;
            var pa = a.transform.position; var pb = b.transform.position;
            float ha = a.Height * 0.8f, hb = b.Height * 0.8f;
            if (pa.y > pb.y + hb || pb.y > pa.y + ha) return;          // one is clean above the other (cross-ups)
            float min = (a.pushWidth * a.scale + b.pushWidth * b.scale) * 0.5f;
            float dx = pb.x - pa.x;
            if (Mathf.Abs(dx) >= min) return;
            float s = Mathf.Abs(dx) < 0.01f ? -a.facing : Mathf.Sign(dx);
            float push = (min - Mathf.Abs(dx)) * 0.5f;
            float lim = arenaHalf;
            pa.x = Mathf.Clamp(pa.x - s * push, -lim, lim); pb.x = Mathf.Clamp(pb.x + s * push, -lim, lim);
            // against a wall: the other one takes the whole push
            if (Mathf.Abs(pa.x) >= lim - 0.01f) pb.x = Mathf.Clamp(pa.x + s * min, -lim, lim);
            if (Mathf.Abs(pb.x) >= lim - 0.01f) pa.x = Mathf.Clamp(pb.x - s * min, -lim, lim);
            a.transform.position = pa; b.transform.position = pb;
        }

        // =========================================================================================== queries
        public IEnumerable<Fighter> TargetsOf(Fighter f)
        {
            if (f is HanumanFighter)
            {
                if (boss != null) yield return boss;
                for (int i = 0; i < clones.Count; i++) if (clones[i] != null) yield return clones[i];
            }
            else if (hanuman != null) yield return hanuman;
        }

        public IEnumerable<Fighter> ProjectileTargets(bool playerSide)
        {
            if (playerSide) { if (boss != null) yield return boss; foreach (var c in clones) if (c != null) yield return c; }
            else if (hanuman != null) yield return hanuman;
        }

        public bool ProjectileNear(Fighter f, float dist)
        {
            foreach (var p in projectiles)
                if (p != null && !p.playerSide && Mathf.Abs(p.transform.position.x - f.transform.position.x) < dist) return true;
            return false;
        }

        public void CheckProjectiles(Fighter attacker, Vector3 point, float r)
        {
            if (!(attacker is HanumanFighter)) return;
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var p = projectiles[i];
                if (p == null || p.playerSide || p.kind != Projectile.Kind.Orb) continue;
                if (Vector2.Distance(p.transform.position, point) > r + p.radius) continue;
                p.Reflect(attacker);
                hud.Banner("REFLECT!", "", 0.7f, new Color(1f, 0.85f, 0.35f));
                SlowMo(0.4f, 0.3f);
                hanuman.AddMeter(0.25f);
            }
        }

        public void Remove(Projectile p) { projectiles.Remove(p); }

        // =========================================================================================== hit feedback
        /// <summary>Camera shake; the big ones (slams, stomps, throws) also buzz the phone.</summary>
        public void Shake(float amp, float dur)
        {
            cam.Shake(amp, dur);
            if (amp >= 0.26f) Haptics.Slam(Mathf.Clamp(amp * 2.5f, 0.6f, 1.4f));
            else if (amp >= 0.14f) Haptics.Medium();
        }
        public void Kick(Vector2 dir, float amount) { cam.Kick(dir, amount); }
        public void Rumble(float amp, float dur) { cam.Rumble(amp, dur); }

        public void SlowMo(float scale, float realSeconds)
        {
            if (paused) return;
            Time.timeScale = scale;
            slowUntil = Mathf.Max(slowUntil, Time.unscaledTime + realSeconds);
        }

        static readonly string[][] Signature =
        {
            new[] { "VANARA FURY", "m1", "m2", "k1", "launcher" },
            new[] { "SKY BREAKER", "air1", "air2", "air3" },
            new[] { "EARTH SHAKER", "m1", "m2", "m3" },
            new[] { "PAWAN STRIKE", "vayudash", "m1", "m2" },
            new[] { "LANKA DAHAN", "firewhip", "tailthrow" },
            new[] { "LANKA DAHAN", "firewhip", "k1" },
            new[] { "TITAN'S WRATH", "stomp", "m1" },
        };

        void Chain(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (chain.Count == 0 || chain[chain.Count - 1] != id) chain.Add(id);
            foreach (var sig in Signature)
            {
                int n = sig.Length - 1;
                if (chain.Count < n || namedThisCombo.Contains(sig[0])) continue;
                bool ok = true;
                for (int i = 0; i < n; i++) if (chain[chain.Count - n + i] != sig[i + 1]) { ok = false; break; }
                if (!ok) continue;
                namedThisCombo.Add(sig[0]);
                hud.ComboName(sig[0] + "!");
                FightSfx.Play("rank", 0.8f);
                hanuman.AddMeter(0.3f);
                hud.Flash(new Color(1f, 0.8f, 0.3f), 0.25f);
            }
        }

        static string Rank(int hits) => hits >= 15 ? "JAI HANUMAN!" : hits >= 10 ? "MIGHTY!" : hits >= 6 ? "GREAT!" : hits >= 3 ? "GOOD" : "";

        void CountHit(string moveId, float dmg)
        {
            comboHits++; comboLast = Time.time; totalDamage += dmg;
            maxCombo = Mathf.Max(maxCombo, comboHits);
            int rank = comboHits >= 15 ? 4 : comboHits >= 10 ? 3 : comboHits >= 6 ? 2 : comboHits >= 3 ? 1 : 0;
            if (rank > lastRank) { lastRank = rank; FightSfx.Play("rank", 0.9f + rank * 0.1f, 0.7f); }
            hud.Combo(comboHits, Rank(comboHits));
            Chain(moveId);
        }

        void EndCombo() { comboHits = 0; lastRank = 0; chain.Clear(); namedThisCombo.Clear(); }

        public void OnHit(Fighter attacker, Fighter victim, HitWindow w, Vector3 point, float dmg)
        {
            float stop = w.hitstop * (w.heavy ? 1.2f : 1f) * (attacker.scale > 1.5f ? 1.3f : 1f);
            attacker.hitstop = Mathf.Max(attacker.hitstop, stop); victim.hitstop = Mathf.Max(victim.hitstop, stop);
            float power = Mathf.Clamp01(dmg / 90f);                 // 0 = a jab, 1 = a crushing blow
            bool big = w.heavy || attacker.scale > 1.5f;
            bool onHanuman = victim is HanumanFighter;
            FightFx.Hit(point, w.fx, big, attacker.facing, power);
            FightSfx.Play(w.heavy ? "hit_heavy" : "hit", attacker.scale > 1.5f ? 0.75f : 1f);
            cam.Shake(w.shake * Mathf.Max(1f, attacker.scale * 0.8f) * (onHanuman ? 1.35f : 1f), w.heavy ? 0.25f : 0.14f);
            cam.Punch(w.heavy ? 0.32f : 0.1f, w.heavy ? 0.22f : 0.12f);
            cam.Kick(new Vector2(attacker.facing, 0.15f), 0.12f + 0.4f * power + (big ? 0.15f : 0f));
            victim.Squash(0.12f + 0.2f * power + (big ? 0.08f : 0f), 0.22f + 0.1f * power);
            if (onHanuman) Haptics.Hurt(power); else Haptics.Hit(power, big);
            if (attacker is HanumanFighter)
            {
                hanuman.AddMeter((attacker.Attacking ? attacker.move.meterGain : 0.05f) * (w.heavy ? 1.6f : 1f));
                CountHit(attacker.Attacking ? attacker.move.id : null, dmg);
                if (w.heavy) hud.Flash(Color.white, 0.18f, 2.5f);
            }
            else if (victim is HanumanFighter)
            {
                hanuman.AddMeter(0.04f);
                EndCombo();
                if (Random.value < 0.35f && attacker is AhiravanaFighter ah && !ah.isClone) { ah.SetExpr(Expr.Laugh, 0.8f); if (Random.value < 0.4f) FightSfx.Play("laugh", 1.1f, 0.5f); }
            }
        }

        public void OnBlocked(Fighter attacker, Fighter victim, HitWindow w, Vector3 point)
        {
            FightFx.Block(point, attacker.facing);
            FightSfx.Play("block");
            victim.Squash(0.07f, 0.16f);
            Haptics.Block();
            attacker.hitstop = victim.hitstop = 0.04f;
            cam.Shake(0.03f, 0.1f);
        }

        public void OnParry(Fighter defender, Fighter attacker, Vector3 point)
        {
            FightFx.Parry(point);
            FightSfx.Play("clang");
            SlowMo(0.3f, 0.6f);
            hud.Banner("PARRY!", attacker != null ? "Counter now!" : "", 0.8f, new Color(0.6f, 0.95f, 1f));
            hud.Flash(new Color(0.6f, 0.95f, 1f), 0.35f);
            cam.Punch(0.45f, 0.3f);
            cam.Kick(new Vector2(-defender.facing, 0.1f), 0.5f);
            Haptics.Parry();
            if (defender is HanumanFighter) { parries++; defender.SetExpr(Expr.Smirk, 0.8f); }
        }

        public void OnDodge(Fighter dodger, Fighter attacker)
        {
            if (!(dodger is HanumanFighter) || attacker == null || attacker.move == lastDodgedMove && attacker.Attacking) return;
            lastDodgedMove = attacker.move;
            perfectDodges++;
            hanuman.AddMeter(0.3f);
            SlowMo(0.35f, 0.5f);
            hud.Banner("PERFECT DODGE", "", 0.8f, new Color(0.6f, 0.95f, 1f));
            hud.Flash(new Color(0.5f, 0.85f, 1f), 0.2f);
            dodger.SetExpr(Expr.Smirk, 0.8f);
            FightSfx.Play("magic", 1.6f, 0.6f);
        }

        public void OnThrow(Fighter attacker, Fighter victim, float dmg)
        {
            FightFx.Hit(victim.Center, attacker is HanumanFighter ? FxKind.Heavy : FxKind.Dark, true, attacker.facing);
            FightSfx.Play("hit_heavy");
            cam.Shake(0.15f, 0.25f); cam.Punch(0.35f, 0.25f); cam.Kick(new Vector2(attacker.facing, -0.3f), 0.6f);
            victim.Squash(0.3f, 0.35f);
            if (victim is HanumanFighter) Haptics.Hurt(1f); else Haptics.Slam(0.9f);
            if (attacker is HanumanFighter) { CountHit(attacker.Attacking ? attacker.move.id : null, dmg); hanuman.AddMeter(0.15f); }
            if (victim.hp <= 0f && !victim.immortal) OnKO(victim, attacker);
        }

        public void OnKO(Fighter victim, Fighter by)
        {
            if (victim is AhiravanaFighter a && a.isClone) return;
            SlowMo(0.22f, 1.3f);
            hud.Flash(Color.white, 0.6f, 1.2f);
            hud.Banner("K.O.", "", 1.5f, new Color(1f, 0.3f, 0.15f));
            FightSfx.Play("ko");
            cam.Punch(0.6f, 0.5f); cam.Shake(0.3f, 0.6f);
            Haptics.Heavy();
            if (mode == FightSetup.Mode.Story)
            {
                if (victim == boss && finale) StartCoroutine(Victory());
                else roundResult = victim == boss ? 1 : 2;
            }
            else StartCoroutine(Revive(victim));
        }

        IEnumerator Revive(Fighter f)
        {
            yield return new WaitForSecondsRealtime(2.2f);
            if (f == null) yield break;
            f.ResetForRound(f.transform.position.x, f.facing);
            FightFx.Sparkle(f.Center, 12, 1.2f, new Color(1f, 0.85f, 0.5f), true);
        }

        public void OnPower(HanumanFighter h, PowerId p)
        {
            string big = "", small = "";
            switch (p)
            {
                case PowerId.Vayu: big = "PAWAN VEGA"; small = "Boon of Vayu - the speed of the wind"; break;
                case PowerId.Flight: big = "FLIGHT"; small = "The son of the Wind takes to the sky"; break;
                case PowerId.FireTail: big = "AGNI PUCCHA"; small = "The burning tail that set Lanka ablaze"; break;
                case PowerId.Shrink: big = "ANIMA"; small = "Small as an atom - attacks pass overhead"; break;
                case PowerId.Vajra: big = "VAJRA KAYA"; small = "Indra's boon - a body like the thunderbolt"; break;
                case PowerId.Growth: big = "MAHIMA"; small = "Vast as a mountain"; break;
            }
            hud.Banner(big, small, 1.3f, new Color(1f, 0.8f, 0.3f));
            hud.Flash(new Color(1f, 0.85f, 0.4f), 0.3f);
            cam.Punch(0.4f, 0.4f);
            SlowMo(0.5f, 0.3f);
        }

        // =========================================================================================== world actions used by moves
        public void GroundPound(Fighter f, float x, float radius, float dmg, bool knockdown)
        {
            var w = new HitWindow { damage = dmg, knock = new Vector2(3f, 6f), knockdown = knockdown, level = HitLevel.Low, stun = 0.45f, fx = FxKind.Heavy, heavy = true, hitstop = 0.07f, shake = 0.1f };
            foreach (var t in new List<Fighter>(TargetsOf(f)))
            {
                if (t == null || !t.grounded || !t.gameObject.activeInHierarchy) continue;
                if (Mathf.Abs(t.transform.position.x - x) > radius + t.hurtSize.x * t.scale * 0.5f) continue;
                t.ReceiveHit(f, w, new Vector3(t.transform.position.x, 0.4f, 0), t.transform.position.x >= x ? 1 : -1);
            }
        }

        public void SpawnOrb(Fighter f, Vector3 pos, Vector2 vel, float dmg, float r)
        {
            projectiles.Add(Projectile.Make(Projectile.Kind.Orb, f, pos, vel, dmg, r));
            FightSfx.Play("orb_cast");
            FightFx.Smoke(pos, 3, new Color(0.6f, 0.25f, 0.9f, 0.6f), 0.6f);
        }

        public void SpawnOrbAimed(Fighter f, Vector3 pos, float speed, float dmg)
        {
            var to = (Vector2)(hanuman.Center - pos);
            SpawnOrb(f, pos, to.normalized * speed, dmg, 0.36f);
        }

        public void SpawnGroundWave(Fighter f, float x)
        {
            int dir = hanuman.transform.position.x >= x ? 1 : -1;
            projectiles.Add(Projectile.Make(Projectile.Kind.GroundWave, f, new Vector3(x, 0.45f, 0), new Vector2(dir * 7.5f, 0), 60f, 0.45f));
            hud.Hint("A shockwave! JUMP or DODGE over it", 2f);
        }

        public void TeleportBehind(Fighter f)
        {
            var o = f.opponent; if (o == null) return;
            FightFx.Smoke(f.Center, 8, new Color(0.5f, 0.2f, 0.8f, 0.8f), 1f);
            FightSfx.Play("teleport");
            float x = o.transform.position.x - o.facing * 1.8f;
            if (Mathf.Abs(x) > arenaHalf - 1f) x = o.transform.position.x + o.facing * 1.8f;
            f.transform.position = new Vector3(x, 0, 0);
            f.FaceOpponent();
            FightFx.Smoke(f.Center, 8, new Color(0.5f, 0.2f, 0.8f, 0.8f), 1f);
            f.puppet.Flash(new Color(0.8f, 0.5f, 1f, 0.9f), 0.25f);
        }

        public void SpawnClones(AhiravanaFighter real)
        {
            for (int i = 0; i < 2; i++)
            {
                var c = new GameObject("AhiravanaIllusion").AddComponent<AhiravanaFighter>();
                c.Init(this, true);
                c.opponent = hanuman; c.SetRound(2);
                float x = Mathf.Clamp(real.transform.position.x + (i == 0 ? 2.6f : -2.6f), -arenaHalf + 1f, arenaHalf - 1f);
                c.transform.position = new Vector3(x, 0, 0); c.FaceOpponent();
                FightFx.Smoke(c.Center, 8, new Color(0.6f, 0.3f, 0.9f, 0.7f), 1f);
                clones.Add(c);
            }
            FightSfx.Play("laugh", 0.95f); FightSfx.Play("teleport", 0.8f);
            hud.Banner("MAYA ILLUSION", "Which one is real? Burn them with the FIRE TAIL (boon 3)", 2f, new Color(0.8f, 0.55f, 1f));
        }

        public void PopClone(AhiravanaFighter c, Vector3 point)
        {
            if (c == null || !clones.Contains(c)) return;
            clones.Remove(c);
            FightFx.Smoke(c.Center, 10, new Color(0.65f, 0.35f, 0.95f, 0.75f), 1.1f);
            FightFx.Hit(point, FxKind.Dark, false, 0f);
            FightSfx.Play("poof");
            CountHit("clone", 0f);
            Destroy(c.gameObject);
        }

        public void BurnClones()
        {
            if (clones.Count == 0) return;
            foreach (var c in new List<AhiravanaFighter>(clones))
            {
                if (c == null) continue;
                FightFx.Flames(c.Center, 14, 1.4f);
                PopClone(c, c.Center);
            }
            hud.Banner("ILLUSIONS BURNED!", "Agni reveals the true demon", 1.4f, new Color(1f, 0.6f, 0.2f));
        }

        void ClearField()
        {
            foreach (var c in new List<AhiravanaFighter>(clones)) if (c != null) Destroy(c.gameObject);
            clones.Clear();
            foreach (var p in new List<Projectile>(projectiles)) if (p != null) Destroy(p.gameObject);
            projectiles.Clear();
        }

        // =========================================================================================== ultimate
        public bool TryUltimate(HanumanFighter h)
        {
            bool ready = h.unlocked[(int)PowerId.Panchmukhi] && (h.infiniteMeter || h.bhakti >= 2.999f);
            if (!ready || ultBusy || !ControlsLive || !(h.CanAct || h.InCancelWindow || h.state == FState.Attack)) { FightSfx.Play("tick", 0.5f, 0.6f); return false; }
            StartCoroutine(Panchmukhi());
            return true;
        }

        IEnumerator Panchmukhi()
        {
            ultBusy = true; live = false;
            var h = hanuman;
            h.ActivatePanchmukhiCost();
            h.EndAllPowers();
            ClearField();
            Time.timeScale = 1f; slowUntil = 0f;
            h.Cinematic("roar", 0.2f);
            boss.Cinematic(finale ? "kneel" : "stunned", 0.2f);
            hud.ShowControls(false); hud.Dark(0.25f); arena.Mood(4); music.Fade(0.12f);
            cam.Focus(h.Center + Vector3.up * 0.9f, 3.2f, 1f);
            FightSfx.Play("ultimate"); FightSfx.Play("roar", 0.75f);
            Haptics.Ultimate(); cam.Rumble(0.05f, 2.2f);
            yield return new WaitForSecondsRealtime(0.6f);

            h.puppet.ShowPanchmukhi(true);
            FightFx.Pillar(h.transform.position, 10f, 3f, new Color(1f, 0.8f, 0.35f), 1.5f);
            hud.Flash(new Color(1f, 0.85f, 0.45f), 0.85f, 1.2f);
            hud.Banner("PANCHMUKHI", "Hanuman  -  Narasimha  -  Garuda  -  Varaha  -  Hayagriva", 2.4f, new Color(1f, 0.82f, 0.3f), true);
            cam.Shake(0.18f, 1.4f);
            Haptics.Heavy();
            yield return new WaitForSecondsRealtime(1.9f);

            for (int i = 0; i < FightArena.LampX.Length; i++)
            {
                cam.Focus(arena.LampFlamePos(i), 3.4f, 0.8f);
                yield return new WaitForSecondsRealtime(0.26f);
                FightFx.Streaks(arena.LampFlamePos(i), -h.facing, 6, new Color(1f, 0.9f, 0.6f));
                arena.Extinguish(i);
                FightSfx.Play("lamp_out", 1f + i * 0.08f);
                hud.Flash(new Color(1f, 0.9f, 0.6f), 0.25f, 4f);
                boss.SetExpr(Expr.Desperate, 1f);
                yield return new WaitForSecondsRealtime(0.18f);
            }
            if (finale)
            {
                boss.immortal = false;
                hud.Banner("THE FIVE LAMPS ARE OUT!", "Ahiravana is mortal again", 1.5f, new Color(1f, 0.85f, 0.4f), true);
                FightSfx.Play("laugh", 0.6f, 0.4f);
            }
            cam.ClearFocus(); hud.Dark(0.3f);
            yield return new WaitForSecondsRealtime(finale ? 1.2f : 0.5f);

            // the final strike
            int side = boss.transform.position.x >= h.transform.position.x ? 1 : -1;
            float hx = Mathf.Clamp(boss.transform.position.x - side * 3.4f, -arenaHalf + 0.5f, arenaHalf - 0.5f);
            if (Mathf.Abs(boss.transform.position.x - hx) < 2.5f) { side = -side; hx = Mathf.Clamp(boss.transform.position.x - side * 3.4f, -arenaHalf + 0.5f, arenaHalf - 0.5f); }
            h.transform.position = new Vector3(hx, 0, 0); h.facing = side;
            boss.EndCinematic(); boss.Stun(3f);
            h.EndCinematic();
            h.StartMove(h.GetMove("finalblow"));
            float t = 0f;
            while (t < 1.6f && boss.state == FState.Stunned) { t += Time.unscaledDeltaTime; yield return null; }
            if (boss.state != FState.Stunned) SlowMo(0.25f, 1.1f);
            yield return new WaitForSecondsRealtime(1.3f);
            h.puppet.ShowPanchmukhi(false);
            hud.Dark(0f); music.Fade(0.42f);
            if (!victory)
            {
                arena.Mood(Mathf.Clamp(round - 1, 0, 2));
                arena.RelightAll();
                if (mode != FightSetup.Mode.Demo) hud.ShowControls(true);
                live = true;
            }
            ultBusy = false;
        }

        // =========================================================================================== story
        void Unlock(params PowerId[] ps) { foreach (var p in ps) hanuman.unlocked[(int)p] = true; }

        IEnumerator Story()
        {
            hud.SetRoundVisible(true);
            fightStart = Time.time;
            live = false;
            boss.Cinematic("laugh"); hanuman.Cinematic("idle");
            hud.ShowControls(false);
            hud.Banner("HANUMAN  vs  AHIRAVANA", "Patala - the underworld temple of Mahamaya", 2.8f);
            music.SetIntensity(0);
            FightSfx.Play("laugh", 0.9f, 0.8f);
            yield return new WaitForSecondsRealtime(1.2f);
            hud.Hint("\"A monkey... in my kingdom? How amusing.\"", 2f);
            yield return new WaitForSecondsRealtime(1.8f);
            round = 1;
            while (round <= 3 && !victory)
            {
                yield return RoundIntro(round);
                roundResult = 0;
                live = true;
                while (roundResult == 0 && !victory) yield return null;
                if (victory) yield break;
                live = false;
                if (roundResult == 1)
                {
                    roundsWon++;
                    yield return new WaitForSecondsRealtime(1.6f);
                    hanuman.Cinematic("victory");
                    hud.Round(round, roundsWon);
                    hud.Banner("HANUMAN WINS", "Round " + round, 1.8f);
                    yield return new WaitForSecondsRealtime(2.2f);
                    round++;
                }
                else
                {
                    yield return new WaitForSecondsRealtime(1.6f);
                    boss.Cinematic("laugh"); FightSfx.Play("laugh");
                    hud.Banner("AHIRAVANA WINS THE ROUND", "Rise again, Hanuman!", 2.2f, new Color(0.8f, 0.5f, 1f));
                    yield return new WaitForSecondsRealtime(2.6f);
                }
            }
        }

        IEnumerator RoundIntro(int r)
        {
            ClearField();
            hanuman.EndAllPowers();
            hanuman.ResetForRound(-3f, 1); boss.ResetForRound(3f, -1);
            boss.SetRound(r); boss.immortal = r == 3;
            hanuman.bhakti = r == 1 ? 0f : Mathf.Max(hanuman.bhakti, r == 3 ? 2f : 1f);
            arena.Mood(r - 1); arena.RelightAll(); music.SetIntensity(r - 1);
            hud.Round(r == 3 ? 4 : r, roundsWon);
            hud.ShowControls(false);
            boss.Cinematic("idle"); hanuman.Cinematic("idle");
            cam.Snap();
            Unlock(PowerId.Vayu);
            if (r >= 2) Unlock(PowerId.Flight, PowerId.FireTail, PowerId.Shrink);
            if (r >= 3) Unlock(PowerId.Vajra, PowerId.Growth);
            if (r == 1) hud.Hint("Move: A/D or stick   GADA: J   TAIL: K   DODGE: L   Jump: W   Hold back to block", 5f);
            if (r == 2)
            {
                hud.Banner("BOONS AWAKENED", "FLIGHT  -  FIRE TAIL  -  ANIMA (shrink)   ·   boons 2, 3, 4", 2.4f, new Color(1f, 0.8f, 0.35f));
                boss.Cinematic("laugh"); FightSfx.Play("laugh");
                yield return new WaitForSecondsRealtime(2.4f);
                hud.Banner("SORCERY", "Ahiravana calls on the dark arts of Patala", 1.8f, new Color(0.8f, 0.55f, 1f));
                yield return new WaitForSecondsRealtime(1.8f);
            }
            if (r == 3)
            {
                boss.Cinematic("transform", 0.2f);
                cam.Focus(boss.Center + Vector3.up, 4f, 0.7f);
                FightSfx.Play("roar", 0.7f); FightSfx.Play("grow", 0.8f);
                FightFx.Pillar(boss.transform.position, 10f, 3f, new Color(0.7f, 0.3f, 1f), 1.6f);
                hud.Flash(new Color(0.7f, 0.35f, 1f), 0.5f, 1.5f);
                boss.scaleTarget = 2.1f;
                cam.Shake(0.25f, 1.6f);
                hud.Banner("TRUE FORM", "Witness the true form of Patala's king!", 2.2f, new Color(0.85f, 0.5f, 1f));
                yield return new WaitForSecondsRealtime(2.2f);
                cam.ClearFocus();
                hud.BossName("AHIRAVANA - TITAN");
                hud.Banner("VAJRA & MAHIMA", "Boons 5 and 6: become thunder-hard, then grow to match him", 2.4f, new Color(1f, 0.8f, 0.35f));
                yield return new WaitForSecondsRealtime(2.4f);
            }
            FightSfx.Play("bell");
            hud.Banner(r == 3 ? "FINAL ROUND" : "ROUND " + r, "", 1.1f);
            yield return new WaitForSecondsRealtime(1.1f);
            hud.Banner("FIGHT!", "", 0.8f, new Color(1f, 0.6f, 0.15f));
            FightSfx.Play("ko", 1.6f, 0.5f);
            boss.EndCinematic(); hanuman.EndCinematic();
            hud.ShowControls(true);
        }

        IEnumerator Finale()
        {
            finale = true; live = false;
            ClearField();
            yield return new WaitForSecondsRealtime(0.6f);
            hanuman.EndAllPowers();
            boss.scaleTarget = 1.3f;
            boss.Cinematic("kneel", 0.25f); hanuman.Cinematic("idle", 0.25f);
            hud.ShowControls(false);
            cam.Focus(new Vector2(0f, 2.4f), 5.6f, 0.75f);
            arena.FlareLamps(1.8f);
            FightSfx.Play("magic", 0.6f); FightSfx.Play("laugh", 0.8f, 0.7f);
            hud.Banner("HE CANNOT DIE!", "The five sacred lamps of Patala keep Ahiravana alive", 2.8f, new Color(0.85f, 0.55f, 1f));
            yield return new WaitForSecondsRealtime(2.8f);
            cam.ClearFocus();
            Unlock(PowerId.Panchmukhi);
            hanuman.bhakti = 3f;
            hud.Banner("UNLEASH PANCHMUKHI!", "Press PANCHMUKHI (U) to blow out all five lamps at once", 3f, new Color(1f, 0.82f, 0.3f));
            hud.Hint("PANCHMUKHI  -  U", 10f);
            boss.EndCinematic(); hanuman.EndCinematic();
            hud.ShowControls(true);
            live = true;
        }

        IEnumerator Victory()
        {
            victory = true; live = false;
            yield return new WaitForSecondsRealtime(1.8f);
            Time.timeScale = 1f;
            hanuman.Cinematic("victory", 0.3f);
            cam.Focus(hanuman.Center + Vector3.up * 0.3f, 3.4f, 0.8f);
            arena.Mood(3); music.Fade(0.2f);
            hud.ShowControls(false);
            FightSfx.Play("bell");
            hud.Banner("VICTORY!", "Jai Shri Ram!", 2.6f, new Color(1f, 0.82f, 0.3f));
            yield return new WaitForSecondsRealtime(2.8f);
            float secs = Time.time - fightStart;
            string grade = maxCombo >= 12 && parries >= 2 ? "S" : maxCombo >= 8 ? "A" : maxCombo >= 5 ? "B" : "C";
            hud.ShowVictory(
                "Time   " + Mathf.FloorToInt(secs / 60f) + ":" + Mathf.FloorToInt(secs % 60f).ToString("00") +
                "\nBest combo   " + maxCombo + " hits" +
                "\nDamage dealt   " + Mathf.RoundToInt(totalDamage) +
                "\nParries   " + parries + "      Perfect dodges   " + perfectDodges +
                "\n\n<size=56><color=#FFD060>RANK  " + grade + "</color></size>");
        }

        // =========================================================================================== training + demo
        IEnumerator Training()
        {
            hud.SetRoundVisible(false);
            foreach (PowerId p in System.Enum.GetValues(typeof(PowerId))) hanuman.unlocked[(int)p] = true;
            hanuman.infiniteMeter = true;
            boss.dummy = AhiravanaFighter.Dummy.Idle; boss.SetRound(2);
            hanuman.ResetForRound(-2f, 1); boss.ResetForRound(1.5f, -1);
            arena.Mood(0); music.SetIntensity(0);
            hud.BuildTraining(hanuman, boss);
            hud.Banner("TRAINING ROOM", "Tap any move in the list - or try it yourself", 2f);
            hud.Hint("Unlimited Bhakti. Toggle the dummy below the move list.", 5f);
            live = true;
            yield break;
        }

        public void TrainingPerform(MoveList.Entry e)
        {
            if (ultBusy) return;
            if (entryRoutine != null) StopCoroutine(entryRoutine);
            hanuman.demoBlock = false;
            entryRoutine = StartCoroutine(RunEntry(e, false));
        }

        Coroutine entryRoutine;

        void Setup(float gap)
        {
            hanuman.EndAllPowers();
            if (hanuman.state == FState.Cinematic) hanuman.EndCinematic();
            float bx = Mathf.Clamp(boss.transform.position.x, -arenaHalf + gap + 1f, arenaHalf - 1f);
            if (mode == FightSetup.Mode.Demo) bx = 2f;
            boss.ResetForRound(bx, -1);
            hanuman.ResetForRound(bx - gap, 1);
            hanuman.bhakti = 3f;
        }

        IEnumerator Wait(float s) { yield return new WaitForSeconds(s); }

        IEnumerator RunEntry(MoveList.Entry e, bool demo)
        {
            var h = hanuman;
            hud.Caption(e.name, e.input);
            if (e.move != null)
            {
                Setup(e.move == "tailgrab" || e.move == "bodyslam" ? 1.5f : 2.1f);
                yield return Wait(0.25f);
                h.Perform(e.move);
                yield return Wait(1.4f);
                yield break;
            }
            switch (e.special)
            {
                case "combo3":
                    Setup(1.9f); yield return Wait(0.25f);
                    h.Perform("m1"); yield return Wait(0.2f); h.Perform("m2"); yield return Wait(0.22f); h.Perform("m3"); yield return Wait(1.3f); break;
                case "kicks":
                    Setup(1.6f); yield return Wait(0.25f);
                    h.Perform("k1"); yield return Wait(0.24f); h.Perform("k2"); yield return Wait(1.0f); break;
                case "charge":
                    Setup(2.1f); yield return Wait(0.25f);
                    h.Cinematic("charge", 0.1f); FightSfx.Play("power", 1.4f, 0.5f);
                    for (float t = 0; t < 1f; t += 0.05f) { FightFx.Sparkle(h.WeaponPoint, 2, 1.2f, new Color(1f, 0.85f, 0.4f), true); yield return Wait(0.05f); }
                    h.EndCinematic(); h.Perform("charge2"); yield return Wait(1.5f); break;
                case "aircombo":
                    Setup(1.7f); yield return Wait(0.25f);
                    h.Perform("launcher"); yield return Wait(0.42f); h.Perform("air1"); yield return Wait(0.24f); h.Perform("air2"); yield return Wait(0.26f); h.Perform("air3"); yield return Wait(1.6f); break;
                case "vanara":
                    Setup(1.8f); yield return Wait(0.25f);
                    h.Perform("m1"); yield return Wait(0.2f); h.Perform("m2"); yield return Wait(0.2f); h.Perform("k1"); yield return Wait(0.22f);
                    h.Perform("launcher"); yield return Wait(0.42f); h.Perform("air1"); yield return Wait(0.24f); h.Perform("air2"); yield return Wait(0.26f); h.Perform("air3"); yield return Wait(1.6f); break;
                case "dive":
                    Setup(3.2f); yield return Wait(0.25f);
                    h.ForceJump(3f); yield return Wait(0.38f); h.Perform("dive"); yield return Wait(1.6f); break;
                case "flykick":
                    Setup(3.6f); yield return Wait(0.25f);
                    h.ForceJump(2f); yield return Wait(0.3f); h.Perform("flykick"); yield return Wait(1.4f); break;
                case "parry":
                    Setup(2.0f); yield return Wait(0.35f);
                    boss.Perform("jab"); yield return Wait(0.05f);
                    h.demoBlock = true; yield return Wait(0.6f); h.demoBlock = false;
                    h.Perform("m1"); yield return Wait(0.2f); h.Perform("m2"); yield return Wait(0.22f); h.Perform("m3"); yield return Wait(1.3f); break;
                case "dodge":
                    Setup(2.2f); yield return Wait(0.35f);
                    boss.Perform("overhead"); yield return Wait(0.36f);
                    h.StartDodge(-h.facing); yield return Wait(0.5f);
                    h.Perform("thrust"); yield return Wait(1.2f); break;
                case "reflect":
                    Setup(6.5f); yield return Wait(0.3f);
                    boss.Perform("orb");
                    for (float t = 0; t < 2f && !ProjectileNear(h, 2.3f); t += Time.deltaTime) yield return null;
                    h.Perform("m1"); yield return Wait(1.6f); break;
                case "vayu":
                    Setup(4.5f); yield return Wait(0.3f);
                    h.UsePower(PowerId.Vayu); yield return Wait(0.62f);
                    h.Perform("m1"); yield return Wait(0.16f); h.Perform("m2"); yield return Wait(0.17f); h.Perform("m3"); yield return Wait(1.4f); break;
                case "flight":
                    Setup(2.6f); yield return Wait(0.3f);
                    h.UsePower(PowerId.Flight); yield return Wait(0.5f);
                    h.transform.position = new Vector3(boss.transform.position.x - 1.7f, 1.4f, 0); h.vel = Vector2.zero;
                    h.Perform("air1"); yield return Wait(0.26f); h.Perform("air2"); yield return Wait(0.28f); h.Perform("air3"); yield return Wait(0.7f);
                    h.transform.position = new Vector3(boss.transform.position.x - 3.2f, 4.2f, 0);
                    h.Perform("meteor"); yield return Wait(1.8f); break;
                case "fire":
                    Setup(2.3f); yield return Wait(0.3f);
                    h.UsePower(PowerId.FireTail); yield return Wait(0.8f);
                    h.Perform("firewhip"); yield return Wait(0.6f); h.Perform("k1"); yield return Wait(1.3f); break;
                case "shrink":
                    Setup(1.7f); yield return Wait(0.3f);
                    h.UsePower(PowerId.Shrink); yield return Wait(0.4f);
                    boss.Perform("jab"); yield return Wait(0.7f);
                    h.Perform("rise"); yield return Wait(1.6f); break;
                case "vajra":
                    Setup(1.9f); yield return Wait(0.3f);
                    h.UsePower(PowerId.Vajra); yield return Wait(0.7f);
                    boss.Perform("combo"); yield return Wait(0.25f);
                    h.Perform("m1"); yield return Wait(0.2f); h.Perform("m2"); yield return Wait(0.22f); h.Perform("m3"); yield return Wait(1.4f); break;
                case "growth":
                    Setup(3.4f); yield return Wait(0.3f);
                    h.UsePower(PowerId.Growth); yield return Wait(1.3f);
                    h.Perform("stomp"); yield return Wait(1.0f); h.Perform("m3"); yield return Wait(1.6f); break;
                case "panch":
                    Setup(3f); yield return Wait(0.3f);
                    TryUltimate(h);
                    while (ultBusy) yield return null;
                    yield return Wait(1.2f); break;
            }
            if (!demo) hud.Caption("", "");
        }

        IEnumerator DemoReel()
        {
            hud.SetRoundVisible(false);
            foreach (PowerId p in System.Enum.GetValues(typeof(PowerId))) hanuman.unlocked[(int)p] = true;
            hanuman.infiniteMeter = true;
            boss.dummy = AhiravanaFighter.Dummy.Idle; boss.immortal = true; boss.SetRound(2);
            hud.ShowControls(false);
            arena.Mood(0); music.SetIntensity(0);
            hanuman.ResetForRound(-2f, 1); boss.ResetForRound(2f, -1);
            cam.Snap();
            hud.Banner("HANUMAN", "Every move, combo and boon", 2.2f);
            yield return new WaitForSecondsRealtime(2.2f);
            live = true;
            int section = 0; string last = null;
            foreach (var e in MoveList.Entries)
            {
                if (e.section != last)
                {
                    last = e.section; section++;
                    arena.Mood(e.section == "BOON" ? 1 : e.section == "ULTIMATE" ? 3 : 0);
                    music.SetIntensity(e.section == "BOON" ? 1 : e.section == "ULTIMATE" ? 2 : 0);
                }
                yield return RunEntry(e, true);
            }
            hud.Caption("", "");
            hanuman.Cinematic("victory");
            hud.Banner("JAI HANUMAN", "Level 2 - Patala Yuddha", 3f, new Color(1f, 0.82f, 0.3f));
            yield return new WaitForSecondsRealtime(3.5f);
            hud.ShowPause(true); paused = true; Time.timeScale = 0f;
        }

        // =========================================================================================== menus
        public void TogglePause()
        {
            if (victory) return;
            paused = !paused;
            Time.timeScale = paused ? 0f : 1f;
            if (!paused) slowUntil = 0f;
            hud.ShowPause(paused);
        }

        public void Restart(FightSetup.Mode m)
        {
            FightSetup.Current = m;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void ToMenu()
        {
            Time.timeScale = 1f;
            Music.Duck(1f, 0f);
            SceneManager.LoadScene("MainMenu");
        }
    }
}
