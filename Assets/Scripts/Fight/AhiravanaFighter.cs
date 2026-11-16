using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BalHanuman.Fight
{
    /// <summary>
    /// Ahiravana, sorcerer king of Patala. Distance-based AI with reaction blocks, telegraphed heavies (parry cues),
    /// and a per-round kit: R1 trident duel, R2 sorcery (orbs, teleports, clones, hovering barrage), R3 titan form.
    /// Clones use the same class with isClone set: one hit pops them, fire burns them all.
    /// </summary>
    public class AhiravanaFighter : Fighter
    {
        public enum Dummy { Fight, Idle, Block }
        public Dummy dummy = Dummy.Fight;
        public int round = 1;
        public float aggression = 1f, blockChance = 0.3f;

        Dictionary<string, MoveDef> moves;
        float thinkT, blockHoldT, walkT;
        bool crouchBlock;
        MoveDef lastSeenOppMove;
        readonly Dictionary<string, float> cooldown = new Dictionary<string, float>();
        float walkDir;
        int oppBlocks;

        public void Init(FightDirector d, bool clone)
        {
            director = d; isClone = clone;
            displayName = "AHIRAVANA";
            maxHp = hp = clone ? 1f : 1000f;
            walkSpeed = 2.4f; jumpVel = 12f; mood = Expr.Smirk;
            puppet = Puppet.Build(transform, MoveLib.AhiravanaDims(), MoveLib.AhiravanaStyle(), clone ? 8 : 10);
            // the old cut-out rig art is gone: every move is drawn from the whole-body pose pictures (AhiravanaPoses)
            moves = MoveLib.Ahiravana(clips);
            SetupPoses();
            ApplyDifficulty();
            hurtSize = new Vector2(0.95f, 2.85f);
            if (clone) { puppet.SetTint(new Color(0.8f, 0.6f, 1f, 0.7f)); damageDealtMul = 0.5f; aggression = 1.4f; }
        }

        // ---- whole-body pose pictures (clips with pictures use them; the rest still use the rig)
        AhiravanaPoses.Data poseData;
        SpriteRenderer poseSr;
        readonly Dictionary<PuppetClip, string> clipNames = new Dictionary<PuppetClip, string>();
        readonly Dictionary<string, AhiravanaPoses.Pose> poseInfo = new Dictionary<string, AhiravanaPoses.Pose>();
        string shownPose;

        void SetupPoses()
        {
            poseData = AhiravanaPoses.Get();
            if (poseData == null || poseData.poses == null) return;
            foreach (var p in poseData.poses) poseInfo[p.name] = p;
            foreach (var kv in clips) if (kv.Value != null) clipNames[kv.Value] = kv.Key;
            foreach (var kv in moves) if (kv.Value != null && kv.Value.clip != null) clipNames[kv.Value.clip] = kv.Key;
            // the throw that follows a grab is a move of its own (not in the move list): give it its pictures too, or the rig shows
            foreach (var kv in moves) if (kv.Value != null && kv.Value.throwMove != null && kv.Value.throwMove.clip != null) clipNames[kv.Value.throwMove.clip] = kv.Value.throwMove.id;
            var go = new GameObject("PoseSprite"); go.transform.SetParent(puppet.transform, false);
            poseSr = go.AddComponent<SpriteRenderer>(); poseSr.sortingOrder = 5; poseSr.enabled = false;
            puppet.poseSprite = poseSr;
        }

        SpriteRenderer poseSr2;      // the next frame, faded in over the current one on blended clips
        string shownPose2;

        void Place(SpriteRenderer sr, string name, ref string shown)
        {
            if (name == shown || !poseInfo.TryGetValue(name, out var pi)) return;
            var sp = AhiravanaPoses.Sprite(name);
            if (sp == null) return;
            shown = name;
            float k = pi.k > 0f ? pi.k : poseData.k;
            sr.sprite = sp;
            // the pictures face left, the rig's forward is +x (the fighter is mirrored when facing left): flip once so they agree
            sr.transform.localScale = new Vector3(-k * sp.pixelsPerUnit, k * sp.pixelsPerUnit, 1f);
            sr.transform.localPosition = new Vector3((pi.px - pi.w * 0.5f) * k, (pi.py - pi.h * 0.5f) * k, 0f);
        }

        void LateUpdate()
        {
            if (poseSr == null || puppet == null) return;
            string key = null; (float t, string name)[] frames = null;
            var clip = puppet.Clip;
            if (clip != null && clipNames.TryGetValue(clip, out key)) AhiravanaPoses.Frames.TryGetValue(key, out frames);
            bool use = frames != null;
            puppet.SetRigVisible(!use);
            poseSr.enabled = use;
            if (poseSr2 == null) { var g2 = new GameObject("PoseSprite2"); g2.transform.SetParent(puppet.transform, false); poseSr2 = g2.AddComponent<SpriteRenderer>(); poseSr2.sortingOrder = 6; }
            if (!use) { poseSr2.enabled = false; return; }
            float len = Mathf.Max(0.01f, clip.Length), t = puppet.ClipTime;
            float u = clip.loop ? Mathf.Repeat(t, len) / len : Mathf.Clamp01(t / len);
            int cur = 0;
            for (int i = 0; i < frames.Length; i++) if (frames[i].t <= u + 1e-4f) cur = i;
            if (key == "launched" && frames.Length >= 4) cur = LaunchedPicture();       // follows the flight, never loops or restarts
            Place(poseSr, frames[cur].name, ref shownPose);
            var tint = puppet.Tinted(Color.white);
            poseSr.color = tint;
            // slow clips: dissolve into the next frame over the second half of each step
            float blend = 0f; int nxt = -1;
            if (AhiravanaPoses.Blends(key) && frames.Length > 1)
            {
                nxt = cur + 1 < frames.Length ? cur + 1 : (clip.loop ? 0 : -1);
                if (nxt >= 0)
                {
                    float u0 = frames[cur].t, u1 = cur + 1 < frames.Length ? frames[cur + 1].t : 1f;
                    blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 1f, Mathf.InverseLerp(u0, u1, u)));
                }
            }
            if (nxt >= 0 && blend > 0.01f)
            {
                Place(poseSr2, frames[nxt].name, ref shownPose2);
                poseSr2.enabled = true; tint.a *= blend; poseSr2.color = tint;
            }
            else poseSr2.enabled = false;
        }

        /// <summary>Which of the four flight pictures to show: thrown back, still rising, flat at the top, head-first on the way down.</summary>
        int launchPic = -1; float launchPicT;
        int LaunchedPicture()
        {
            float vy = vel.y, y = transform.position.y;
            int want = vy > 6f ? 0 : vy > 1.5f ? 1 : (vy > -3f || y < 0.6f) ? 2 : 3;
            // hold each picture a moment so rapid re-hits do not make him flicker between two poses
            if (launchPic < 0 || Time.time - launchPicT > 0.14f || want == 0 && launchPic != 0) { if (want != launchPic) launchPicT = Time.time; launchPic = want; }
            return launchPic;
        }

        string handPose;
        protected override void Update()
        {
            base.Update();
            // the barrage turns gravity off for its duration; if anything interrupts it (a hit, a parry, a phase roar) the flag must not stay on
            if (flying && !(Attacking && move.id == "barrage")) flying = false;
            if (!isClone && dummy == Dummy.Fight && director != null && director.PhasesActive && !phaseBusy && Alive && director.ControlsLive)
            {
                float f = hp / maxHp;
                int np = f < 0.3f ? 2 : f < 0.6f ? 1 : 0;
                if (np > phase) StartCoroutine(PhaseShift(np));
            }
            if (puppet == null || !puppet.Skinned) return;
            // hands follow the move: casting moves glow, everything else grips the trident (near) with a clawed far hand
            string id = move != null ? move.id : null;
            bool cast = id == "orb" || id == "barrage" || id == "summon" || id == "vanish";
            string key = cast ? "cast" : "idle";
            if (key == handPose) return;
            handPose = key;
            puppet.SetVariant("hand_near", cast ? "cast" : "fist");
            puppet.SetVariant("hand_far", cast ? "cast" : "claw");
        }

        public override MoveDef GetMove(string id) => moves != null && moves.TryGetValue(id, out var m) ? m : null;

        protected override Expr CurrentMood()
        {
            if (isClone) return Expr.Laugh;
            float k = hp / maxHp;
            return k > 0.6f ? Expr.Smirk : k > 0.3f ? Expr.Furious : Expr.Desperate;
        }

        protected override void Start()
        {
            base.Start();
            if (isClone) puppet.SetTint(new Color(0.8f, 0.6f, 1f, 0.7f));
        }

        public void SetRound(int r)
        {
            round = r; phase = 0; phaseBusy = false; patQueue.Clear(); obs.Reset();
            aggression = r == 1 ? 0.9f : r == 2 ? 1.1f : 1.25f;
            blockChance = r == 1 ? 0.3f : r == 2 ? 0.4f : 0.35f;
            baseAggression = aggression; baseBlock = blockChance;
            cooldown.Clear();
        }

        bool Ready(string id) => !cooldown.TryGetValue(id, out var t) || Time.time >= t;
        void Cool(string id, float s) { cooldown[id] = Time.time + s; }

        public bool Perform(string id) { var m = GetMove(id); return m != null && StartMove(m); }

        protected override void Think(float dt)
        {
            inX = 0; inBlock = false; inCrouch = false;
            if (opponent == null || !opponent.Alive) return;
            if (dummy == Dummy.Idle) return;
            obs.Tick(opponent, dt, opponent.transform.position.x >= transform.position.x ? 1 : -1);
            float dx = opponent.transform.position.x - transform.position.x;
            float dist = Mathf.Abs(dx); int toward = dx >= 0 ? 1 : -1;
            float reach = 2.4f * scale;

            // reaction blocks. The training dummy in Block mode reacts instantly. In a real fight he only blocks what he can see coming:
            // he notices a new attack, needs a reaction time, and then only blocks if that swing is still winding up. Quick jabs beat his reflexes.
            if (opponent.move != lastSeenOppMove)
            {
                lastSeenOppMove = opponent.move; oppSeenT = Time.time; reactRoll = Random.value;
                reactPending = opponent.Attacking && opponent.move.hits.Count > 0 && dummy != Dummy.Block;
                if (dummy == Dummy.Block && opponent.Attacking && dist < 4.2f * Mathf.Max(1f, opponent.scale)) BeginBlock();
            }
            if (reactPending && Time.time - oppSeenT >= 0.22f - 0.02f * (round - 1))
            {
                reactPending = false;
                if (opponent.Attacking && opponent.move.hits.Count > 0 && opponent.moveT < opponent.move.hits[0].t0
                    && dist < 4.2f * Mathf.Max(1f, opponent.scale) && reactRoll < blockChance * (isClone ? 0.3f : 1f)) BeginBlock();
            }
            if (dummy == Dummy.Block) { inBlock = blockHoldT > 0f; inCrouch = crouchBlock; blockHoldT -= dt; return; }
            if (blockHoldT > 0f && CanAct) { blockHoldT -= dt; inBlock = true; inCrouch = crouchBlock; return; }
            if (opponent.Blocking) oppBlocks++;

            if (!CanAct) return;
            if (walkT > 0f) { walkT -= dt; inX = walkDir; }
            thinkT -= dt;
            if (thinkT > 0f) return;
            thinkT = Random.Range(0.25f, 0.65f) / aggression;
            if (opponent.InHitReaction && opponent.state == FState.Knockdown) { walkT = 0.3f; walkDir = -toward; return; }

            string pick = !isClone && scale <= 1.5f && dummy == Dummy.Fight ? BrainChoose(dist, reach, toward) : Choose(dist, reach);
            if (lastPatternStep) thinkT = 0.08f / aggression;          // the rest of a string follows quickly
            if (pick == "walk") { walkT = Random.Range(0.3f, 0.7f); walkDir = toward; inX = toward; return; }
            if (pick == "back") { walkT = Random.Range(0.25f, 0.5f); walkDir = -toward; inX = -toward; return; }
            if (pick == "dodge") { inDodge = true; inDodgeDir = -toward; return; }
            var m = GetMove(pick);
            if (m == null) return;
            FaceOpponent();
            StartMove(m);
            Cool(pick, CooldownOf(pick));
        }

        float oppSeenT, reactRoll; bool reactPending;

        // ---- the brain (design B7): observer + named patterns + punish rules; the old weighted pick stays for clones and the titan
        readonly BossObserver obs = new BossObserver();
        readonly Queue<string> patQueue = new Queue<string>();
        public int phase;                                   // 0 / 1 / 2 by health within the round (design B8)
        float baseAggression = 1f, baseBlock = 0.3f; bool phaseBusy;
        public readonly List<string> pickLog = new List<string>();     // for tests: what he chose and why
        void LogPick(string s) { if (pickLog.Count > 60) pickLog.RemoveAt(0); pickLog.Add(s); }
        bool lastPatternStep;

        bool InRecoveryWindow(Fighter f) => f.InRecovery;

        string BrainChoose(float dist, float reach, int toward)
        {
            lastPatternStep = false;
            // 1) finish the string he started (the tell was the start of it)
            if (patQueue.Count > 0)
            {
                string step = patQueue.Peek();
                bool fine = step == "walk" || step == "back" || Ready(step);
                if (!fine || dist > reach * 1.7f && step != "walk" && step != "vanish" && step != "orb" && step != "summon" && step != "barrage") patQueue.Clear();
                else { lastPatternStep = true; LogPick("step:" + step); return patQueue.Dequeue(); }
            }
            // too close: make room first
            if (dist < 1.35f * scale) { LogPick("backoff"); return "back"; }

            // 2) punish rules
            if (InRecoveryWindow(opponent) && dist < reach * 1.15f && Ready("jab")) { LogPick("punish:recovery"); return phase >= 1 && Ready("combo") ? "combo" : "jab"; }
            if (obs.BlockStreak > 2.5f && dist < reach && Ready("grab")) { LogPick("punish:turtle"); return "grab"; }
            string spam = obs.Spammed();
            if (spam != null && dist < reach * 1.5f)
            {
                string ans = spam == "thrust" || spam == "tailgrab" ? "grab" : "sweep";
                if (Ready(ans)) { LogPick("punish:spam " + spam); return ans; }
            }
            if (obs.JumpCount >= 2)
            {
                string ans = dist > 2.2f ? "orb" : "combo";
                if (Ready(ans)) { LogPick("punish:jumping"); return ans; }
            }
            if (obs.Retreat > 0.8f && dist > 2.6f && Ready("orb")) { LogPick("punish:retreat"); return "orb"; }

            // 3) a pattern that fits the distance, round and phase
            float total = 0f; var fit = new List<BossPatterns.Pattern>();
            foreach (var p in BossPatterns.All)
            {
                if (round < p.minRound || phase < p.minPhase || dist < p.minDist || dist > p.maxDist) continue;
                if (!Ready(p.steps[0])) continue;
                if (p.steps[0] == "summon" && director.CloneCount > 0) continue;
                fit.Add(p); total += p.weight;
            }
            if (fit.Count == 0) { LogPick("approach"); return dist > reach ? "walk" : "jab"; }
            float r = Random.value * total;
            foreach (var p in fit)
            {
                r -= p.weight;
                if (r > 0f) continue;
                for (int i = 1; i < p.steps.Length; i++) patQueue.Enqueue(p.steps[i]);
                LogPick("pattern:" + p.name);
                return p.steps[0];
            }
            return fit[0].steps[0];
        }

        // ---- difficulty (design B9): tells hold longer on Easy, recoveries shrink on Hard. Training and demo always play at Normal.
        readonly Dictionary<MoveDef, (float hold, float end)> baseTimes = new Dictionary<MoveDef, (float, float)>();

        public void ApplyDifficulty()
        {
            if (moves == null) return;
            float teleK = FightSetup.TelegraphK, lagK = FightSetup.RecoveryK;
            foreach (var m in moves.Values)
            {
                if (m == null) continue;
                if (!baseTimes.ContainsKey(m)) baseTimes[m] = (m.holdLen, m.endLag);
                var b0 = baseTimes[m];
                m.holdLen = b0.hold * teleK;
                float last = 0f; foreach (var w in m.hits) last = Mathf.Max(last, w.t1);
                float extra = m.hits.Count > 0 ? Mathf.Max(0f, m.Length - last) * (lagK - 1f) : 0f;
                m.endLag = Mathf.Max(0f, b0.end + extra);
            }
        }

        IEnumerator PhaseShift(int p)
        {
            phaseBusy = true;
            move = null; holding = null;
            flying = false;
            Cinematic("roar", 0.1f); invulnUntil = Time.time + 0.95f;
            director.OnBossPhase(round, p);
            yield return new WaitForSecondsRealtime(0.85f);
            EndCinematic();
            phase = p;
            aggression = baseAggression * (1f + 0.12f * p);          // a little faster each step ...
            blockChance = baseBlock + 0.05f * p;                    // ... and a little harder to hit, but never a wall
            phaseBusy = false;
        }

        void BeginBlock()
        {
            blockHoldT = Mathf.Min(0.9f, opponent.move.Length + 0.1f);
            crouchBlock = false;
            foreach (var h in opponent.move.hits) if (h.level == HitLevel.Low) crouchBlock = true;
        }

        float CooldownOf(string id)
        {
            switch (id)
            {
                case "orb": return 1.6f;
                case "barrage": return 9f;
                case "summon": return 16f;
                case "vanish": return 5f;
                case "grab": return 4f;
                case "overhead": return 2.5f;
                case "gslam": return 3.5f;
                default: return 0.6f;
            }
        }

        string Choose(float dist, float reach)
        {
            var w = new List<(string, float)>();
            void Add(string id, float weight) { if (weight > 0f && (id == "walk" || id == "back" || id == "dodge" || Ready(id))) w.Add((id, weight)); }
            bool titan = scale > 1.5f;
            if (isClone)
            {
                if (dist < reach) { Add("jab", 3); Add("combo", 2); Add("sweep", 1.5f); }
                else Add("walk", 4);
            }
            else if (titan)
            {
                if (dist < reach * 1.1f) { Add("gslam", 2.5f + phase); Add("gsweep", 2f + phase); Add("jab", 1.5f); Add("grab", 1f); }
                else { Add("walk", 2.5f); Add("orb", 2f); Add("gslam", dist < 9f ? 1.2f : 0f); }
            }
            else if (dist < reach)
            {
                // too close (side by side): step back first so the swing has room and he faces Hanuman again
                if (dist < 1.35f * scale) { Add("back", 5f); Add("jab", 1.5f); Add("grab", 0.6f); Add("sweep", 0.6f); }
                Add("jab", 3f); Add("combo", round >= 1 ? 2.5f : 0f); Add("sweep", 2f); Add("overhead", 1.2f);
                Add("grab", 1f + Mathf.Min(3f, oppBlocks * 0.02f));
                if (round >= 2) { Add("vanish", 0.6f); Add("dodge", 0.4f); }
                Add("back", 0.5f);
            }
            else if (dist < 5.5f)
            {
                Add("walk", 3f); Add("orb", round >= 1 ? 1.6f : 0f);
                if (round >= 2) { Add("vanish", 1f); Add("summon", director.CloneCount == 0 ? 0.8f : 0f); }
                Add("overhead", dist < 3.6f ? 1f : 0f);
            }
            else
            {
                Add("orb", 3f); Add("walk", 2f);
                if (round >= 2) { Add("barrage", 1.2f); Add("summon", director.CloneCount == 0 ? 1f : 0f); Add("vanish", 1f); }
            }
            if (w.Count == 0) return "walk";
            float total = 0f; foreach (var x in w) total += x.Item2;
            float r = Random.value * total;
            foreach (var x in w) { r -= x.Item2; if (r <= 0f) { if (x.Item1 == "grab") oppBlocks = 0; return x.Item1; } }
            return w[0].Item1;
        }

        public override HitResult ReceiveHit(Fighter attacker, HitWindow w, Vector3 point, int dirOverride = 0, bool projectile = false)
        {
            if (isClone)
            {
                var r = new HitResult();
                if (!gameObject.activeInHierarchy || w.grab) return r;
                r.connected = true;
                director.PopClone(this, point);
                return r;
            }
            return base.ReceiveHit(attacker, w, point, dirOverride, projectile);
        }
    }
}
