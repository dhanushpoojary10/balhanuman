using System.Collections.Generic;
using UnityEngine;
using B = BalHanuman.Fight.FightInput.Btn;

namespace BalHanuman.Fight
{
    /// <summary>
    /// Adult Hanuman, player controlled. Joystick + MACE / KICK-TAIL / DODGE / ULTIMATE and six boons.
    /// Direction + button picks the move (see the move list in the HUD); moves chain into each other inside their cancel windows.
    /// </summary>
    public class HanumanFighter : Fighter
    {
        public static readonly PowerId[] Boons = { PowerId.Vayu, PowerId.Flight, PowerId.FireTail, PowerId.Shrink, PowerId.Vajra, PowerId.Growth };

        public float bhakti;                    // 0..3 meter (one bar per segment)
        public bool infiniteMeter;
        public readonly bool[] unlocked = new bool[7];
        readonly float[] powerEnd = new float[7];
        Dictionary<string, MoveDef> moves;

        string buffered; float bufferT;
        bool maceArmed, charging; float chargeStart;
        MoveDef chargeMove;
        float backPress = -10f, lastBackTap = -10f, lastFwdTap = -10f;
        bool wasBack, wasFwd, wasUp;
        float fireFxT;
        public bool demoBlock;                  // demo reel: hold block
        const float BaseWalk = 2.8f;

        public static int Cost(PowerId p) => p == PowerId.Growth ? 2 : p == PowerId.Panchmukhi ? 3 : 1;
        public static float Duration(PowerId p)
        {
            switch (p) { case PowerId.Vayu: return 6f; case PowerId.Flight: return 7f; case PowerId.FireTail: return 8f; case PowerId.Shrink: return 4f; case PowerId.Vajra: return 7f; case PowerId.Growth: return 10f; }
            return 0f;
        }

        public bool Active(PowerId p) => Time.time < powerEnd[(int)p];
        public float Remaining01(PowerId p) => Active(p) ? (powerEnd[(int)p] - Time.time) / Mathf.Max(0.01f, Duration(p)) : 0f;

        public void Init(FightDirector d)
        {
            director = d; displayName = "HANUMAN"; maxHp = hp = 1000f;
            walkSpeed = BaseWalk; jumpVel = 13f; mood = Expr.Focus;
            puppet = Puppet.Build(transform, MoveLib.HanumanDims(), MoveLib.HanumanStyle(), 20);
            puppet.ApplySkin(HanumanSkin.Get());
            moves = MoveLib.Hanuman(clips);
            chargeMove = new MoveDef { id = "chargehold", name = "Charging", clip = clips["charge"], expr = Expr.Roar };
            hurtSize = new Vector2(0.95f, 2.75f);
        }

        public override MoveDef GetMove(string id) => moves != null && moves.TryGetValue(id, out var m) ? m : null;
        public IEnumerable<string> MoveIds => moves.Keys;

        protected override float BlockPressTime => Mathf.Max(backPress, demoBlock ? Time.time - 0.05f : -10f);

        protected override Expr CurrentMood() => hp < maxHp * 0.3f ? Expr.Furious : Active(PowerId.Vajra) || Active(PowerId.Growth) ? Expr.Serene : mood;

        // ================================================================== input -> intent
        protected override void Think(float dt)
        {
            float x = FightInput.X, y = FightInput.Y;
            float rel = x * facing;
            bool back = rel < -0.4f, fwd = rel > 0.4f, upNow = y > 0.5f;
            if (back && !wasBack) { backPress = Time.time; if (Time.time - lastBackTap < 0.28f) { inDodge = true; inDodgeDir = -facing; } lastBackTap = Time.time; }
            if (fwd && !wasFwd) { if (Time.time - lastFwdTap < 0.28f) { inDodge = true; inDodgeDir = facing; } lastFwdTap = Time.time; }
            wasBack = back; wasFwd = fwd;

            inX = x; inCrouch = y < -0.5f;
            if ((upNow && !wasUp) || FightInput.Down(B.Jump)) inJump = true;
            wasUp = upNow;
            if (FightInput.Down(B.Dodge)) { inDodge = true; inDodgeDir = Mathf.Abs(x) > 0.3f ? Mathf.Sign(x) : -facing; }

            // smart block: holding back blocks only while something is coming
            inBlock = (back && Threatened()) || demoBlock;
            if (demoBlock) { inX = 0; }

            // ---- mace: tap = attack on release, hold = charge (instant on press in the air or mid-combo)
            if (FightInput.Down(B.Mace))
            {
                if (!grounded || flying || Attacking || scaleTarget < 0.9f) Command("M");
                else { maceArmed = true; }
            }
            if (maceArmed && FightInput.Held(B.Mace) && FightInput.HeldFor(B.Mace) > 0.26f && CanAct && grounded && !charging)
            {
                maceArmed = false; charging = true; chargeStart = Time.time;
                StartMove(chargeMove); FightSfx.Play("power", 1.4f, 0.5f);
            }
            if (FightInput.Up(B.Mace))
            {
                if (charging)
                {
                    charging = false;
                    if (move == chargeMove) StartMove(GetMove(Time.time - chargeStart >= 0.95f ? "charge2" : "charge1"));
                }
                else if (maceArmed) Command("M");
                maceArmed = false;
            }
            if (FightInput.Down(B.Kick)) Command("K");
            if (FightInput.Down(B.Ultimate)) director.TryUltimate(this);
            for (int i = 0; i < Boons.Length; i++) if (FightInput.Down(B.P1 + i)) UsePower(Boons[i]);

            if (bufferT > 0f)
            {
                bufferT -= dt;
                if (CanAct || InCancelWindow) { var m = Resolve(buffered); buffered = null; bufferT = 0f; if (m != null) StartMove(m); }
            }
        }

        bool Threatened()
        {
            if (director == null) return false;
            foreach (var f in director.TargetsOf(this))
                if (f.Attacking && Mathf.Abs(f.transform.position.x - transform.position.x) < 4.5f * Mathf.Max(1f, f.scale)) return true;
            return director.ProjectileNear(this, 5f);
        }

        void Command(string b) { buffered = b; bufferT = 0.3f; }

        MoveDef Resolve(string b)
        {
            float rel = FightInput.X * facing, y = FightInput.Y;
            bool fwd = rel > 0.4f, back = rel < -0.4f, up = y > 0.5f, down = y < -0.5f;
            bool air = !grounded || flying;
            string cur = Attacking ? move.id : null;
            if (b == "M")
            {
                if (scaleTarget < 0.9f) { powerEnd[(int)PowerId.Shrink] = 0f; return GetMove("rise"); }
                if (air)
                {
                    if (down) return GetMove(flying ? "meteor" : "dive");
                    if (cur == "air1") return GetMove("air2");
                    if (cur == "air2") return GetMove("air3");
                    return GetMove("air1");
                }
                if (scaleTarget > 1.5f && down) return GetMove("stomp");
                if (cur == "m1") return GetMove("m2");
                if (cur == "m2") return GetMove("m3");
                if (up) return GetMove("launcher");
                if (down) return GetMove("sweep");
                if (fwd) return GetMove("thrust");
                if (back) return GetMove("whirl");
                return GetMove("m1");
            }
            // kick / tail
            if (air) return GetMove("flykick");
            if (down) return GetMove("tailtrip");
            if (fwd) return GetMove("tailgrab");
            if (back) return GetMove("bodyslam");
            if (Active(PowerId.FireTail)) return GetMove("firewhip");
            if (cur == "k1") return GetMove("k2");
            return GetMove("k1");
        }

        /// <summary>Demo reel / training: perform a move right now if at all possible.</summary>
        public bool Perform(string id)
        {
            var m = GetMove(id);
            if (m == null) return false;
            if (!(CanAct || InCancelWindow || state == FState.Attack)) return false;
            return StartMove(m);
        }

        // ================================================================== state additions (flight, charge glow)
        protected override void StepState(float dt, float adt)
        {
            if (charging && move != chargeMove) charging = false;
            if (charging && move == chargeMove)
            {
                moveT = 0.01f; vel.x = 0f;
                float k = Mathf.Clamp01((Time.time - chargeStart) / 0.95f);
                if (Random.value < dt * (10f + 30f * k)) FightFx.Sparkle(puppet.WeaponTip, 1, 1.2f, k >= 1f ? new Color(1f, 0.9f, 0.4f) : new Color(1f, 0.7f, 0.3f), true);
                if (k >= 1f && Random.value < dt * 6f) puppet.Flash(new Color(1f, 0.9f, 0.5f, 0.6f), 0.1f);
                stateT += dt;
                return;
            }
            if (flying && (state == FState.Idle || state == FState.Walk || state == FState.Jump || state == FState.Crouch || state == FState.Block))
            {
                stateT += dt;
                FaceOpponent();
                float y = FightInput.Y;
                var goal = new Vector2(inX * 6.5f, y * 5.5f);
                if (transform.position.y < 1.2f && goal.y < 0f) goal.y = 0f;
                vel = Vector2.MoveTowards(vel, goal, 30f * dt);
                PlayClip("fly", 0.15f);
                if (inDodge) StartDodge(inDodgeDir);
                return;
            }
            base.StepState(dt, adt);
        }

        protected override void Update()
        {
            UpdatePowers();
            base.Update();
        }

        // ================================================================== powers
        public bool CanUse(PowerId p)
        {
            if (!unlocked[(int)p] || !Alive || director == null || !director.ControlsLive) return false;
            if (p != PowerId.Panchmukhi && Active(p)) return false;
            if (!infiniteMeter && bhakti < Cost(p) - 0.001f) return false;
            return CanAct || InCancelWindow || state == FState.Attack && move == chargeMove;
        }

        public void AddMeter(float v) { bhakti = Mathf.Clamp(bhakti + v, 0f, 3f); }

        public bool UsePower(PowerId p)
        {
            if (!CanUse(p)) { if (unlocked[(int)p]) FightSfx.Play("tick", 0.5f, 0.6f); return false; }
            if (p == PowerId.Panchmukhi) return director.TryUltimate(this);
            if (!infiniteMeter) bhakti -= Cost(p);
            powerEnd[(int)p] = Time.time + Duration(p);
            director.OnPower(this, p);
            switch (p)
            {
                case PowerId.Vayu:
                    StartMove(GetMove("vayudash"));
                    afterimageColor = new Color(0.55f, 0.95f, 1f, 0.55f);
                    FightSfx.Play("wind");
                    break;
                case PowerId.Flight:
                    flying = true; grounded = false; vel = new Vector2(0, 11f);
                    ToIdle(); PlayClip("fly", 0.1f);
                    FightSfx.Play("wind", 0.8f);
                    FightFx.Dust(transform.position, 10, 3f, 0, 1f);
                    for (int i = 0; i < 3; i++) FightFx.Streaks(Center + Vector3.down * i * 0.4f, 0, 2, new Color(0.8f, 0.95f, 1f));
                    break;
                case PowerId.FireTail:
                    StartMove(GetMove("roar"));
                    FightSfx.Play("fire", 0.9f); FightSfx.Play("power", 0.9f, 0.6f);
                    director.BurnClones();
                    break;
                case PowerId.Shrink:
                    scaleTarget = 0.38f;
                    FightFx.Sparkle(Center, 16, 1.4f, new Color(0.6f, 0.95f, 1f), true);
                    FightSfx.Play("shrink");
                    break;
                case PowerId.Vajra:
                    StartMove(GetMove("roar"));
                    FightSfx.Play("thunder"); FightSfx.Play("power", 1.1f, 0.6f);
                    FightFx.Pillar(transform.position, 4f, 1.4f, new Color(1f, 0.85f, 0.3f), 0.7f);
                    break;
                case PowerId.Growth:
                    StartMove(GetMove("roar"));
                    scaleTarget = 2.2f;
                    FightSfx.Play("grow");
                    FightFx.Pillar(transform.position, 9f, 2.6f, new Color(1f, 0.8f, 0.35f), 1.2f);
                    director.Shake(0.25f, 1.0f);
                    break;
            }
            return true;
        }

        public void ActivatePanchmukhiCost() { if (!infiniteMeter) bhakti = 0f; }

        void UpdatePowers()
        {
            bool vayu = Active(PowerId.Vayu), vajra = Active(PowerId.Vajra), growth = Active(PowerId.Growth), fire = Active(PowerId.FireTail);
            speedMul = vayu ? 1.45f : 1f;
            damageDealtMul = (vajra ? 1.3f : 1f) * (growth ? 1.8f : 1f) * (fire ? 1.15f : 1f);
            damageTakenMul = (vajra ? 0.25f : 1f) * (growth ? 0.6f : 1f);
            superArmor = vajra || growth;
            walkSpeed = BaseWalk * (scaleTarget < 0.9f ? 1.8f : 1f);
            if (vayu) { afterimageUntil = Time.time + 0.05f; afterimageColor = new Color(0.55f, 0.95f, 1f, 0.45f); }
            if (puppet != null) puppet.SetTint(vajra ? new Color(1f, 0.92f, 0.6f) : Color.white);
            if (vajra && Random.value < Time.deltaTime * 20f)
            {
                var a = FightFx.Spawn(FightArt.Soft, transform.position + new Vector3(Random.Range(-0.5f, 0.5f) * scale, Random.Range(0.2f, 2.4f) * scale), 0.5f,
                    Vector2.one * 0.5f * scale, Vector2.one * 0.1f, new Color(1f, 0.85f, 0.3f, 0.8f), new Color(1f, 0.6f, 0.1f, 0f), puppet.SortingOrder + 1);
                a.vel = Vector2.up * 2.5f;
            }
            if (fire && puppet != null)
            {
                fireFxT -= Time.deltaTime;
                if (fireFxT <= 0f) { fireFxT = 0.035f; var seg = puppet.TailSeg(Random.Range(2, puppet.TailCount)); if (seg != null) FightFx.Flames(seg.position, 1, 0.7f * scale); }
            }
            // timed powers ending
            if (flying && !Active(PowerId.Flight) && !(Attacking && move.id == "meteor")) { flying = false; if (state == FState.Idle) ToIdle(); }
            if (scaleTarget < 0.9f && !Active(PowerId.Shrink)) { scaleTarget = 1f; FightFx.Sparkle(Center, 10, 1f, new Color(0.6f, 0.95f, 1f), false); FightSfx.Play("grow", 1.8f, 0.5f); }
            if (scaleTarget > 1.5f && !Active(PowerId.Growth)) { scaleTarget = 1f; FightFx.Pillar(transform.position, 4f, 1.2f, new Color(1f, 0.8f, 0.35f), 0.6f); FightSfx.Play("shrink", 0.7f); }
            if (infiniteMeter) bhakti = 3f;
        }

        public void EndAllPowers()
        {
            for (int i = 0; i < powerEnd.Length; i++) powerEnd[i] = 0f;
            flying = false; scaleTarget = 1f; charging = false; buffered = null;
        }

        protected override void OnParried() { AddMeter(0.35f); }
    }
}
