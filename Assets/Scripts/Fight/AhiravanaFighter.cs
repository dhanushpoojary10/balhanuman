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
            moves = MoveLib.Ahiravana(clips);
            hurtSize = new Vector2(0.95f, 2.85f);
            if (clone) { puppet.SetTint(new Color(0.8f, 0.6f, 1f, 0.7f)); damageDealtMul = 0.5f; aggression = 1.4f; }
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
            round = r;
            aggression = r == 1 ? 0.9f : r == 2 ? 1.1f : 1.25f;
            blockChance = r == 1 ? 0.3f : r == 2 ? 0.4f : 0.35f;
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
            float dx = opponent.transform.position.x - transform.position.x;
            float dist = Mathf.Abs(dx); int toward = dx >= 0 ? 1 : -1;
            float reach = 2.4f * scale;

            // reaction blocks: roll once for each new opponent attack that is close enough to matter
            if (opponent.move != lastSeenOppMove)
            {
                lastSeenOppMove = opponent.move;
                if (opponent.Attacking && dist < 4.2f * Mathf.Max(1f, opponent.scale) && (dummy == Dummy.Block || Random.value < blockChance * (isClone ? 0.3f : 1f)))
                {
                    blockHoldT = Mathf.Min(0.9f, opponent.move.Length + 0.1f);
                    crouchBlock = false;
                    foreach (var h in opponent.move.hits) if (h.level == HitLevel.Low) crouchBlock = true;
                }
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

            string pick = Choose(dist, reach);
            if (pick == "walk") { walkT = Random.Range(0.3f, 0.7f); walkDir = toward; inX = toward; return; }
            if (pick == "back") { walkT = Random.Range(0.25f, 0.5f); walkDir = -toward; inX = -toward; return; }
            if (pick == "dodge") { inDodge = true; inDodgeDir = -toward; return; }
            var m = GetMove(pick);
            if (m == null) return;
            FaceOpponent();
            StartMove(m);
            Cool(pick, CooldownOf(pick));
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
                if (dist < reach * 1.1f) { Add("gslam", 2.5f); Add("gsweep", 2f); Add("jab", 1.5f); Add("grab", 1f); }
                else { Add("walk", 2.5f); Add("orb", 2f); Add("gslam", dist < 9f ? 1.2f : 0f); }
            }
            else if (dist < reach)
            {
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
