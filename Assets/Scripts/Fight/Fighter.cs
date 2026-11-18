using System.Collections.Generic;
using UnityEngine;

namespace BalHanuman.Fight
{
    /// <summary>
    /// Shared fighting-game body: kinematic movement on a flat arena, the state machine (walk, block, jump, attack, hitstun,
    /// launch, knockdown, dodge...), move playback with root motion and hit windows, and taking hits (block, parry, armor, juggle).
    /// Subclasses supply the brain (player input or AI) by filling the intent fields in Think().
    /// </summary>
    public abstract class Fighter : MonoBehaviour
    {
        public const float ParryWindow = 0.2f;

        [System.NonSerialized] public FightDirector director;
        [System.NonSerialized] public Fighter opponent;
        [System.NonSerialized] public Puppet puppet;

        public string displayName = "Fighter";
        public float maxHp = 1000f, hp = 1000f;
        public Vector2 vel;
        public bool grounded = true;
        public int facing = 1;
        public float scale = 1f, scaleTarget = 1f;
        public FState state = FState.Idle;
        public MoveDef move;
        public float moveT, stateT;
        public float speedMul = 1f;
        public float walkSpeed = 3.6f, jumpVel = 13f, gravity = 36f;
        public float damageDealtMul = 1f, damageTakenMul = 1f;
        public bool superArmor, flying, immortal, isClone;
        public bool controlEnabled = true;
        public float hitstop;

        // squash-and-stretch pop when struck: compresses along the blow, then wobbles back
        float squashAmt, squashT, squashDur;
        public void Squash(float amount, float dur) { squashAmt = amount; squashT = squashDur = Mathf.Max(0.05f, dur); }
        protected Vector2 SquashFactor
        {
            get
            {
                if (squashT <= 0f) return Vector2.one;
                float u = squashT / squashDur;
                float k = u * Mathf.Cos((1f - u) * 7f) * squashAmt;
                return new Vector2(1f - k * 0.6f, 1f + k * 0.5f);
            }
        }
        /// <summary>Applies facing, size and the hit squash to the drawn body.</summary>
        protected virtual void ApplyVisualScale() { var q = SquashFactor; puppet.transform.localScale = new Vector3(facing * scale * q.x, scale * q.y, 1f); }
        public float invulnUntil;

        // guard: blocking drains it, an empty guard is a guard break; it refills while not blocking
        public float guard = 160f, guardMax = 160f; float guardIdle;
        public float parryWindow = ParryWindow;          // widened by assists
        public float counterUntil;                       // after a parry or perfect dodge: the next hit this fighter lands is a counter (+30%)
        int armorLeft; float holdT; bool holdFired, moveConnected;

        // jump feel and landing (design B4)
        public const float JumpWindup = 0.05f, ApexHang = 0.6f, FallGravityK = 1.25f, JumpCutK = 0.65f;
        float jumpWind, landLock; bool jumpCutOk;
        /// <summary>Counts down after touching down from a jump while the landing pictures play (they outlast the input lock).</summary>
        protected float landVis; protected const float LandVisLen = 0.2f;
        public float pushV;                              // soft separation: a small push velocity applied by the director, fades out
        protected virtual bool JumpHeld => true;         // the player lets go early for a short hop
        public Vector2 hurtSize = new Vector2(0.95f, 2.6f);
        public float pushWidth = 0.9f;
        public float burnT;
        public int juggle;

        // intent (written by Think)
        protected float inX;                // world direction -1..1
        protected bool inCrouch, inBlock, inJump, inDodge;
        protected float inDodgeDir;

        protected readonly Dictionary<string, PuppetClip> clips = new Dictionary<string, PuppetClip>();
        protected Expr mood = Expr.Focus;
        Expr exprOverride; float exprT;
        protected float stunTimer, blockStart;
        public bool crouching;
        int evIndex; bool whooshed;
        readonly List<(int w, Fighter t)> hitsDone = new List<(int, Fighter)>();
        public Fighter holding, heldBy;
        string curClip;
        protected string CurClipName => curClip;
        SpriteRenderer shadow;
        protected float afterimageUntil, afterimageTimer; protected Color afterimageColor = new Color(0.5f, 0.9f, 1f, 0.5f);

        public Vector2 Pos => transform.position;
        public float Height => hurtSize.y * scale * HurtHeightK;
        public Vector3 Center => transform.position + Vector3.up * Height * 0.55f;
        public Vector3 HeadPos => puppet != null ? puppet.Joint(Puppet.J.Head).position + Vector3.up * puppet.dims.headR * scale : Center;
        public bool Alive => state != FState.Dead && hp > 0f;
        public bool Blocking => state == FState.Block;
        public bool InHitReaction => state == FState.Hitstun || state == FState.Launched || state == FState.Knockdown || state == FState.Stunned || state == FState.Grabbed || state == FState.Blockstun;
        public bool Attacking => state == FState.Attack && move != null;
        public bool CanAct => controlEnabled && landLock <= 0f && jumpWind <= 0f && (state == FState.Idle || state == FState.Walk || state == FState.Crouch || state == FState.Block || state == FState.Jump);
        public bool IsInvulnerable => Time.time < invulnUntil || state == FState.Getup || state == FState.Knockdown || state == FState.Cinematic || state == FState.Dead
                                      || (state == FState.Attack && move != null && move.invulnerable && moveT >= move.invulnT0 && (move.invulnT1 < 0f || moveT <= move.invulnT1));

        float HurtHeightK
        {
            get
            {
                if (state == FState.Knockdown || state == FState.Dead) return 0.3f;
                if (crouching && (state == FState.Crouch || state == FState.Block)) return 0.66f;
                if (state == FState.Launched) return 0.75f;
                return 1f;
            }
        }

        // ================================================================== lifecycle
        protected virtual void Start()
        {
            hp = maxHp;
            var sh = FightArt.Quad("Shadow", transform, FightArt.Soft, Vector2.zero, new Vector2(1.6f, 0.35f), new Color(0, 0, 0, 0.45f), -3);
            shadow = sh;
            PlayClip("idle");
        }

        protected abstract void Think(float dt);
        public abstract MoveDef GetMove(string id);

        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || puppet == null) return;
            if (squashT > 0f) squashT -= dt;
            if (landLock > 0f) landLock -= dt;
            if (landVis > 0f) landVis -= dt;
            // safety net: a fighter can only stay Grabbed while the one holding him is still in his throw. If that ever stops
            // (interrupted, cancelled, hit), he is let go and falls instead of hanging in the air forever.
            if (state == FState.Grabbed && (heldBy == null || heldBy.holding != this || !heldBy.Attacking))
            {
                heldBy = null; vel = Vector2.zero; grounded = transform.position.y <= 0.001f;
                SetState(FState.Launched); ForceClip("launched", 0.05f);
            }
            if (hitstop > 0f)
            {
                hitstop -= dt;
                ApplyVisualScale();
                // the freeze is never dead still: the one who was hit shudders hard, the one who hit shudders a little
                float amp = InHitReaction ? 0.05f : Attacking ? 0.016f : 0f;
                SetShake((Vector3)(Random.insideUnitCircle * amp * scale));
                return;
            }
            SetShake(Vector3.zero);
            float adt = dt * speedMul * (Attacking ? move.speedMul : 1f);
            if (Attacking && move.holdLen > 0f && moveT >= move.holdAt && holdT < move.holdLen)
            {
                // the tell: the move waits on its wind-up picture
                if (!holdFired) { holdFired = true; if (move.tele != TeleClass.None) OnTelegraph(move); }
                holdT += dt; adt = 0f;
            }

            if (jumpWind > 0f && hitstop <= 0f)
            {
                jumpWind -= dt;
                if (state != FState.Crouch) jumpWind = 0f;
                else if (jumpWind <= 0f) { if (grounded) LaunchJump(); }
            }
            inJump = inDodge = false;
            if (controlEnabled && director != null && director.ControlsLive) Think(dt);
            else { inX = 0; inBlock = inCrouch = false; }

            StepState(dt, adt);
            StepPhysics(dt);
            if (holding != null) CarryHeld();

            scale = Mathf.MoveTowards(scale, scaleTarget, dt * Mathf.Max(1.2f, Mathf.Abs(scaleTarget - scale) * 2.5f));
            ApplyVisualScale();
            puppet.velocity = vel; puppet.facing = facing;
            puppet.Tick(adt);
            AfterTick(adt);
            if (state == FState.Attack && move != null) CheckHits();
            Timers(dt);
        }

        void Timers(float dt)
        {
            if (exprT > 0f) { exprT -= dt; if (exprT <= 0f) puppet.SetExpression(CurrentMood()); }
            if (state != FState.Block && state != FState.Blockstun)
            {
                guardIdle += dt;
                if (guardIdle > 0.8f && guard < guardMax) guard = Mathf.Min(guardMax, guard + 35f * dt);
            }
            if (burnT > 0f)
            {
                burnT -= dt;
                if (Random.value < dt * 6f) FightFx.Flames(Center + (Vector3)Random.insideUnitCircle * 0.4f * scale, 1, 0.6f * scale);
                if (Alive && !immortal) hp = Mathf.Max(1f, hp - dt * 10f);
            }
            if (Time.time < afterimageUntil)
            {
                afterimageTimer -= dt;
                if (afterimageTimer <= 0f) { afterimageTimer = 0.045f; SpawnAfterimage(); }
            }
            if (shadow != null)
            {
                float h = transform.position.y;
                shadow.transform.position = new Vector3(transform.position.x, 0.02f, 0);
                float k = Mathf.Clamp01(1f - h / 6f);
                shadow.transform.localScale = new Vector3(1.6f * scale * (0.5f + 0.5f * k) / Mathf.Max(0.01f, transform.lossyScale.x), 0.35f * scale * (0.5f + 0.5f * k), 1);
                shadow.color = new Color(0, 0, 0, 0.45f * k);
            }
        }

        protected virtual Expr CurrentMood() => mood;

        public void SetExpr(Expr e, float dur)
        {
            if (puppet == null) return;
            exprOverride = e; exprT = dur; puppet.SetExpression(e);
        }

        public void SetMood(Expr e) { mood = e; if (exprT <= 0f && puppet != null) puppet.SetExpression(e); }

        // ================================================================== clips
        public void PlayClip(string name, float fade = 0.1f, bool restart = false)
        {
            if (!restart && curClip == name) return;
            if (!clips.TryGetValue(name, out var c)) return;
            curClip = name;
            puppet.Play(c, fade);
            OnPlayClip(name);
        }

        /// <summary>Hooks for subclasses that draw some clips from frame art instead of the skeleton.</summary>
        protected virtual void OnPlayClip(string name) { }
        protected virtual void AfterTick(float adt) { }

        /// <summary>Offsets the drawn body (hit-stop shudder). Characters drawn from frames override it to move their sprite too.</summary>
        protected virtual void SetShake(Vector3 o) { puppet.transform.localPosition = o; }
        protected virtual void SpawnAfterimage() { puppet.Afterimage(afterimageColor, 0.22f, puppet.SortingOrder - 1); }

        protected void ForceClip(string name, float fade = 0.08f) { curClip = null; PlayClip(name, fade); }

        // ================================================================== state machine
        void SetState(FState s) { if (state != s) { state = s; stateT = 0f; } }

        public void FaceOpponent()
        {
            if (opponent == null || !opponent.Alive && opponent.state != FState.Dead) return;
            float dx = opponent.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.15f) facing = dx > 0 ? 1 : -1;
        }

        protected virtual void StepState(float dt, float adt)
        {
            stateT += dt;
            switch (state)
            {
                case FState.Idle: case FState.Walk: case FState.Crouch: case FState.Block:
                    GroundNeutral(dt); break;
                case FState.Jump:
                    if (!flying) vel.x = Mathf.MoveTowards(vel.x, inX * walkSpeed * 1.1f * SpeedK, 18f * dt);
                    if (grounded && stateT > 0.05f) Land();
                    else PlayClip(vel.y > 1f ? "jump" : "fall", 0.15f);
                    break;
                case FState.Attack: StepMove(dt, adt); break;
                case FState.Hitstun: case FState.Blockstun:
                    stunTimer -= dt;
                    vel.x = Mathf.MoveTowards(vel.x, 0f, 14f * dt);
                    if (!grounded) { SetState(FState.Launched); break; }
                    if (stunTimer <= 0f) ToIdle();
                    break;
                case FState.Launched:
                    PlayClip("launched", 0.08f);
                    if (grounded && stateT > 0.08f)
                    {
                        FightFx.Dust(transform.position, 8, 2.5f, 0, 0.9f * scale);
                        FightSfx.Play("land", 0.8f, 0.9f);
                        if (director != null) director.Shake(0.08f * scale, 0.2f);
                        vel = Vector2.zero; juggle = 0;
                        SetState(hp <= 0f && !immortal ? FState.Dead : FState.Knockdown);
                        PlayClip("down", 0.1f);
                    }
                    break;
                case FState.Knockdown:
                    vel.x = Mathf.MoveTowards(vel.x, 0f, 20f * dt);
                    if (stateT > 0.65f) { SetState(FState.Getup); PlayClip("getup", 0.1f); }
                    break;
                case FState.Getup:
                    if (stateT > 0.42f) ToIdle();
                    break;
                case FState.Dodge:
                    vel.x = Mathf.MoveTowards(vel.x, 0f, 22f * dt);
                    if (stateT > 0.36f) ToIdle();
                    break;
                case FState.Stunned:
                    stunTimer -= dt;
                    vel.x = Mathf.MoveTowards(vel.x, 0f, 14f * dt);
                    if (Random.value < dt * 8f) FightFx.Sparkle(HeadPos + Vector3.up * 0.2f, 1, 0.4f, new Color(1f, 0.95f, 0.5f), false);
                    if (stunTimer <= 0f) ToIdle();
                    break;
                case FState.Dead:
                    vel.x = Mathf.MoveTowards(vel.x, 0f, 20f * dt);
                    PlayClip("down", 0.1f);
                    break;
            }
        }

        float SpeedK => Mathf.Sqrt(Mathf.Max(0.3f, scale)) * (speedMul > 1f ? 1.35f : 1f);

        void GroundNeutral(float dt)
        {
            if (!grounded && !flying) { SetState(FState.Jump); return; }
            if (jumpWind > 0f) { vel.x = Mathf.Lerp(vel.x, 0f, 1f - Mathf.Exp(-20f * dt)); return; }      // crouching for take-off
            FaceOpponent();
            if (inDodge) { StartDodge(inDodgeDir); return; }
            if (inJump && grounded && !flying) { DoJump(); return; }
            crouching = inCrouch;
            if (inBlock)
            {
                if (state != FState.Block) { blockStart = Time.time; SetState(FState.Block); }
                PlayClip(crouching ? "crouchblock" : "block", 0.06f);
                vel.x = Mathf.MoveTowards(vel.x, 0f, 30f * dt);
                return;
            }
            if (inCrouch)
            {
                SetState(FState.Crouch); PlayClip("crouch", 0.08f);
                vel.x = Mathf.MoveTowards(vel.x, 0f, 30f * dt);
                return;
            }
            if (Mathf.Abs(inX) > 0.2f)
            {
                SetState(FState.Walk);
                vel.x = Mathf.MoveTowards(vel.x, inX * walkSpeed * SpeedK, 40f * dt);
                PlayClip(inX * facing > 0 ? "walkf" : "walkb", 0.12f);
            }
            else
            {
                SetState(FState.Idle);
                vel.x = Mathf.MoveTowards(vel.x, 0f, 40f * dt);
                PlayClip("idle", 0.15f);
            }
        }

        public void ToIdle()
        {
            move = null; holding = null; crouching = false;
            SetState(grounded || flying ? FState.Idle : FState.Jump);
            PlayClip(grounded || flying ? "idle" : "fall", 0.12f);
        }

        void DoJump()
        {
            // a very short crouch before take-off (0.05 s) so the jump has weight
            jumpWind = JumpWindup; SetState(FState.Crouch); crouching = false; PlayClip("crouch", 0.02f);
            vel.x *= 0.5f;
        }

        void LaunchJump()
        {
            jumpWind = 0f; jumpCutOk = true;
            vel.y = jumpVel * Mathf.Sqrt(scale); vel.x = inX * walkSpeed * 1.1f * SpeedK;
            grounded = false; SetState(FState.Jump); ForceClip("jump", 0.06f);
            FightSfx.Play("jump", 1f, 0.6f);
            FightFx.Dust(transform.position, 2, 1.5f, 0, 0.6f * scale);     // take-off: a puff only, the cloud is for landing
        }

        void Land()
        {
            FightFx.Dust(transform.position, 5, 2f, 0, 0.7f * scale);
            FightSfx.Play("land", 1f, 0.6f);
            if (scale > 1.5f && director != null) director.Shake(0.12f, 0.2f);
            landLock = scale > 1.5f ? 0.12f : 0.06f;     // a beat of recovery on landing
            if (landVis <= 0f) landVis = LandVisLen;
            ToIdle();
        }

        public void StartDodge(float dir)
        {
            if (dir == 0f) dir = -facing;
            SetState(FState.Dodge); move = null;
            vel.x = dir * 10.5f * Mathf.Sqrt(scale);
            invulnUntil = Time.time + 0.3f;
            ForceClip(dir * facing > 0 ? "dodgef" : "dodgeb", 0.05f);
            FightSfx.Play("dodge");
            afterimageUntil = Mathf.Max(afterimageUntil, Time.time + 0.22f);
            FightFx.Dust(transform.position, 4, 2f, -dir * 2f, 0.6f);
        }

        // ================================================================== moves
        public bool StartMove(MoveDef m)
        {
            if (m == null) return false;
            move = m; moveT = 0f; evIndex = 0; whooshed = false; hitsDone.Clear();
            if (holding != null && !beginningThrow) { var drop = holding; holding = null; if (drop.heldBy == this) drop.heldBy = null; }     // a new move lets go of whoever was held
            jumpWind = 0f; armorLeft = m.armorHits; holdT = 0f; holdFired = false; moveConnected = false;
            SetState(FState.Attack);
            crouching = false;
            if (m.air) grounded = false;
            if (m.airStall && !flying) vel.y = Mathf.Clamp(vel.y, -1.5f, 2.5f);
            puppet.Play(m.clip, 0.06f);
            curClip = null;
            if (m.expr != Expr.Neutral) SetExpr(m.expr, Mathf.Min(0.6f, m.Length));
            if (m.tele != TeleClass.None) { if (m.holdLen <= 0f) { holdFired = true; OnTelegraph(m); } }       // moves with a hold show their tell when the hold starts
            else if (m.telegraph) { puppet.Flash(new Color(1f, 0.25f, 0.2f, 0.85f), 0.3f); FightSfx.Play("tick", 0.6f, 0.8f); }
            return true;
        }

        /// <summary>Cancels open at cancelFrom when the move connected (hit, block or parry); a whiff makes you wait a little longer.</summary>
        public bool InCancelWindow => Attacking && moveT >= move.cancelFrom + (moveConnected ? 0f : move.whiffLag);

        /// <summary>The boss's tell: coloured flash, a ring closing on his head, and a cue sound for the kind of attack.</summary>
        protected virtual void OnTelegraph(MoveDef m) { FightFx.Telegraph(this, m.tele, Mathf.Max(0.25f, m.holdLen)); }

        bool move_invuln() => Attacking && move.invulnerable;

        /// <summary>The cancel window is open and the move connected: the next command chains (HUD ring on the Gada button).</summary>
        public bool ChainOpen => Attacking && moveConnected && moveT >= move.cancelFrom;

        /// <summary>Past its hit and with a long recovery still to go: a good moment to hit him.</summary>
        public bool InRecovery
        {
            get
            {
                if (!Attacking || move.hits.Count == 0) return false;
                float last = 0f; foreach (var w in move.hits) last = Mathf.Max(last, w.t1);
                float recover = (move.Length + move.endLag - last) / Mathf.Max(0.01f, move.speedMul);
                return moveT > last + 0.03f && recover > 0.2f;
            }
        }

        /// <summary>Shown as OPEN over the boss: stunned (parry / guard break) or deep in recovery.</summary>
        public bool IsOpen => state == FState.Stunned || InRecovery;

        bool InActiveWindow(out int prio)
        {
            prio = 0;
            if (!Attacking) return false;
            bool any = false;
            foreach (var w in move.hits) if (!w.grab && moveT >= w.t0 && moveT < w.t1) { any = true; prio = Mathf.Max(prio, w.Prio); }
            return any;
        }

        /// <summary>Two equal attacks meeting: both fighters rock back for an instant, no damage.</summary>
        public void Clash(Fighter other, Vector3 point)
        {
            Rock(other.transform.position.x < transform.position.x ? 1 : -1);
            other.Rock(other.transform.position.x < transform.position.x ? -1 : 1);
            director?.OnClash(this, other, point);
        }

        void Rock(int away)
        {
            move = null; holding = null; crouching = false;
            stunTimer = 0.18f; vel.x = away * 3.2f; hitstop = Mathf.Max(hitstop, 0.07f);
            SetState(FState.Hitstun); ForceClip("hit", 0.03f);
            puppet.Flash(new Color(1f, 1f, 1f, 0.7f), 0.1f);
        }

        void GuardBreak(Fighter by)
        {
            guard = guardMax * 0.4f; guardIdle = 0f;
            move = null; crouching = false;
            stunTimer = 0.8f; vel.x = -facing * 3.5f;
            SetState(FState.Stunned); ForceClip("stunned", 0.04f);
            SetExpr(Expr.Shocked, 0.8f);
            puppet.Flash(new Color(0.6f, 0.9f, 1f, 0.9f), 0.25f);
        }

        void StepMove(float dt, float adt)
        {
            if (move == null) { ToIdle(); return; }
            moveT += adt;
            while (evIndex < move.events.Count && moveT >= move.events[evIndex].t) { var e = move.events[evIndex++]; e.act?.Invoke(this); if (move == null || state != FState.Attack) return; }
            float k = Mathf.Sqrt(scale) * (speedMul > 1f ? 1.3f : 1f);
            if (move.lungeSpeed != 0f && moveT >= move.lungeT0 && moveT < move.lungeT1)
            {
                // eased lunge: speeds up, peaks mid-window, tapers off (same distance as the old flat speed within ~10%)
                float u = Mathf.InverseLerp(move.lungeT0, move.lungeT1, moveT);
                float shape = 1.25f * (0.25f + 0.75f * Mathf.Sin(u * Mathf.PI));
                vel.x = Mathf.Lerp(vel.x, facing * move.lungeSpeed * k * shape, 1f - Mathf.Exp(-40f * dt));
            }
            else if (grounded) vel.x = Mathf.Lerp(vel.x, 0f, 1f - Mathf.Exp(-10f * dt));          // glide to a stop instead of braking dead
            if (!whooshed && move.hits.Count > 0 && moveT >= move.hits[0].t0 - 0.04f)
            {
                whooshed = true;
                FightSfx.Play(string.IsNullOrEmpty(move.sfx) ? "whoosh" : move.sfx, scale > 1.5f ? 0.7f : 1f, 0.8f);
                if (move.hits[0].joint == Puppet.J.Weapon) FightFx.SwingArc(this, move, SwingTint);      // painted crescent trailing the weapon
            }
            if (move.endOnLand && grounded && moveT > 0.08f)
            {
                var m = move; m.onLand?.Invoke(this);
                if (move == m) ToIdle();
                return;
            }
            if (moveT >= move.Length + move.endLag) ToIdle();
        }

        void CheckHits()
        {
            if (director == null) return;
            for (int i = 0; i < move.hits.Count; i++)
            {
                var w = move.hits[i];
                if (moveT < w.t0 || moveT >= w.t1) continue;
                Vector3 p = AttackPoint(w);
                float r = w.radius * scale;
                director.CheckProjectiles(this, p, r);
                foreach (var t in director.TargetsOf(this))
                {
                    if (t == null || !t.gameObject.activeInHierarchy || Done(i, t)) continue;
                    Vector3 hp = p;
                    if (!t.Overlaps(p, r) && !(SweptHit(t, p, r, out hp))) continue;
                    hitsDone.Add((i, t));
                    var res = t.ReceiveHit(this, w, hp);
                    if (res.connected || res.blocked || res.parried) moveConnected = true;
                    if (res.clashed) return;
                    if (res.connected && w.grab && move.throwMove != null) { BeginThrow(t, move.throwMove); return; }
                    if (res.parried) return;
                    if (move == null) return;
                }
            }
        }

        /// <summary>
        /// Point-blank hits: the arm and weapon reach from the body out to the tip, so an opponent standing right beside (or inside) the swing is hit too,
        /// instead of the weapon tip passing beyond them. Only the side the fighter is facing counts.
        /// </summary>
        bool SweptHit(Fighter t, Vector3 tip, float r, out Vector3 hit)
        {
            hit = tip;
            Vector3 from = transform.position + new Vector3(0.2f * facing * scale, Height * 0.55f, 0f);
            for (int k = 1; k <= 6; k++)
            {
                Vector3 s = Vector3.Lerp(from, tip, k / 7f);
                if ((s.x - transform.position.x) * facing < 0f) continue;
                if (t.Overlaps(s, r * 0.6f)) { hit = s; return true; }
            }
            return false;
        }

        bool Done(int w, Fighter t) { foreach (var h in hitsDone) if (h.w == w && h.t == t) return true; return false; }

        public bool Overlaps(Vector3 p, float r)
        {
            float w = hurtSize.x * scale * (state == FState.Knockdown || state == FState.Dead ? 2f : 1f);
            float h = Height;
            var c = (Vector2)transform.position + new Vector2(0f, h * 0.5f);
            float dx = Mathf.Max(Mathf.Abs(p.x - c.x) - w * 0.5f, 0f);
            float dy = Mathf.Max(Mathf.Abs(p.y - c.y) - h * 0.5f, 0f);
            return dx * dx + dy * dy <= r * r;
        }

        // ================================================================== grabs
        bool beginningThrow;
        void BeginThrow(Fighter t, MoveDef throwMove)
        {
            holding = t;
            t.heldBy = this; t.move = null; t.SetState(FState.Grabbed); t.ForceClip("launched", 0.05f); t.vel = Vector2.zero;
            t.SetExpr(Expr.Shocked, 1f);
            beginningThrow = true; StartMove(throwMove); beginningThrow = false;
            holding = t;
        }

        /// <summary>World position of a hit window this frame (the skeleton joint, or the measured weapon tip for frame art).</summary>
        protected virtual Vector3 AttackPoint(HitWindow w) => puppet.Point(w.joint, w.offset);

        /// <summary>World position of the weapon (for impact effects): the skeleton's, or the painted frame's measured tip.</summary>
        public virtual Vector3 WeaponPoint => puppet.WeaponTip;

        /// <summary>Colour of the swing-arc trail (the slash sheet is white and tinted).</summary>
        public virtual Color SwingTint => new Color(0.82f, 0.5f, 1f, 0.9f);

        void CarryHeld()
        {
            if (holding.state != FState.Grabbed) { holding = null; return; }
            if (move != null && move.holdPath != null && move.holdPath.Length > 0)
            {
                var hp = move.holdPath; Vector2 q = new Vector2(hp[hp.Length - 1].y, hp[hp.Length - 1].z);
                if (moveT <= hp[0].x) q = new Vector2(hp[0].y, hp[0].z);
                else for (int i = 1; i < hp.Length; i++)
                    if (moveT <= hp[i].x)
                    {
                        float u = Mathf.InverseLerp(hp[i - 1].x, hp[i].x, moveT);
                        q = Vector2.Lerp(new Vector2(hp[i - 1].y, hp[i - 1].z), new Vector2(hp[i].y, hp[i].z), u);
                        break;
                    }
                var c = transform.position + new Vector3(q.x * facing * scale, q.y * scale, 0f);
                holding.transform.position = new Vector3(c.x, Mathf.Max(0f, c.y - holding.Height * 0.5f), 0);
                holding.grounded = false;
                return;
            }
            var hand = move != null ? puppet.Point(move.holdJoint, move.holdOffset) : puppet.Point(Puppet.J.HandF, new Vector2(0, -0.1f));
            holding.transform.position = new Vector3(hand.x, Mathf.Max(0f, hand.y - holding.Height * 0.45f), 0);
            holding.grounded = false;
        }

        /// <summary>Called from a throw move's event: lets go with a velocity (x relative to this fighter's facing).</summary>
        public void ReleaseHeld(Vector2 v, float damage, bool heavy = true)
        {
            var t = holding; holding = null;
            if (t == null) return;
            t.heldBy = null;
            t.vel = new Vector2(v.x * facing, v.y);
            t.SetState(FState.Launched);
            float dmg = damage * damageDealtMul * t.damageTakenMul;
            t.hp -= dmg; if (t.immortal) t.hp = Mathf.Max(1f, t.hp);
            director?.OnThrow(this, t, dmg);
            t.puppet.Flash(Color.white, 0.12f);
            t.SetExpr(Expr.Hurt, 0.6f);
        }

        // ================================================================== being hit
        /// <summary>Time the player committed to blocking (parry timing). The AI blocks on reaction, the player by a fresh press.</summary>
        protected virtual float BlockPressTime => blockStart;

        public virtual HitResult ReceiveHit(Fighter attacker, HitWindow w, Vector3 point, int dirOverride = 0, bool projectile = false)
        {
            var res = new HitResult();
            if (!Alive || state == FState.Grabbed) return res;
            if (IsInvulnerable)
            {
                if (state == FState.Dodge || Time.time < invulnUntil) { res.dodged = true; director?.OnDodge(this, attacker); }
                return res;
            }
            int dir = transform.position.x >= attacker.transform.position.x ? 1 : -1;
            if (Mathf.Abs(transform.position.x - attacker.transform.position.x) < 0.05f) dir = attacker.facing;
            if (dirOverride != 0) dir = dirOverride;
            bool facingAttacker = facing == -dir;

            if (w.grab)
            {
                if (!grounded || state == FState.Knockdown) return res;
                res.connected = true; return res;
            }

            // two swings in their active frames at once: the stronger one wins, equal ones clash
            if (!projectile && attacker.Attacking && !attacker.move.invulnerable && !move_invuln() && InActiveWindow(out int myPrio))
            {
                int atk = w.Prio;
                if (atk < myPrio) return res;
                if (atk == myPrio && atk < 2) { Clash(attacker, point); res.clashed = true; return res; }
            }

            if (state == FState.Block && facingAttacker && w.level != HitLevel.Unblockable)
            {
                bool ok = crouching ? w.level != HitLevel.Overhead : w.level != HitLevel.Low;
                if (ok)
                {
                    if (Time.time - BlockPressTime <= parryWindow && (projectile || attacker.state == FState.Attack))
                    {
                        res.parried = true;
                        counterUntil = Time.time + 0.7f;
                        if (!projectile) attacker.GetParried(this);
                        director?.OnParry(this, projectile ? null : attacker, point);
                        blockStart = -10f; OnParried();
                        return res;
                    }
                    res.blocked = true;
                    float chip = w.damage * 0.10f * attacker.damageDealtMul * damageTakenMul;
                    hp = Mathf.Max(1f, hp - chip);
                    guardIdle = 0f;
                    guard -= w.damage * (w.heavy ? 1.5f : 1f) * attacker.damageDealtMul * (w.Prio >= 3 ? 1.5f : 1f);
                    if (guard <= 0f)
                    {
                        GuardBreak(attacker);
                        director?.OnGuardBreak(this, attacker, point);
                        return res;
                    }
                    stunTimer = 0.14f + w.stun * 0.35f;
                    vel.x = dir * (w.knock.x * 0.7f + 1f);
                    SetState(FState.Blockstun); ForceClip(crouching ? "crouchblock" : "block", 0.03f);
                    puppet.Flash(new Color(0.6f, 0.9f, 1f, 0.6f), 0.1f);
                    director?.OnBlocked(attacker, this, w, point);
                    return res;
                }
            }

            res.connected = true;
            // armor: some moves soak up a few hits (half damage, no reaction) once their armor has started. Unblockable hits break through.
            bool armored = Attacking && armorLeft > 0 && moveT >= move.armorStart && w.Prio < 3 && !projectile;      // a reflected orb is not soaked by armor
            if (armored) armorLeft--;
            bool counter = attacker.counterUntil > Time.time;
            float dmg = w.damage * attacker.damageDealtMul * damageTakenMul * (armored ? 0.5f : 1f) * (counter ? 1.3f : 1f);
            if (counter) attacker.counterUntil = 0f;
            hp -= dmg;
            if (immortal) hp = Mathf.Max(1f, hp);
            if (w.burn) burnT = 3f;
            puppet.Flash(armored ? new Color(1f, 0.85f, 0.4f, 0.8f) : new Color(1f, 1f, 1f, 0.9f), 0.12f);
            director?.OnHit(attacker, this, w, point, dmg);
            if (counter) director?.OnCounterHit(attacker, this, point);

            if ((superArmor || armored) && hp > 0f)
            {
                vel.x = dir * w.knock.x * 0.25f;
                SetExpr(Expr.Furious, 0.3f);
                return res;
            }

            move = null; holding = null; crouching = false;
            SetExpr(Expr.Hurt, Mathf.Max(0.35f, w.stun + 0.1f));
            bool dying = hp <= 0f && !immortal;
            if (w.launch || !grounded || w.knockdown || dying)
            {
                float vy = w.launch ? w.knock.y : (!grounded ? Mathf.Max(3.2f, w.knock.y * 0.6f) : Mathf.Max(5f, w.knock.y));
                if (!grounded) vy *= Mathf.Pow(0.88f, juggle);
                if (dying) vy = Mathf.Max(vy, 8f);
                juggle++;
                vel = new Vector2(dir * Mathf.Max(w.knock.x, dying ? 6f : 0f), vy);
                grounded = false;
                SetState(FState.Launched); if (curClip != "launched") ForceClip("launched", 0.04f);       // a re-hit mid-air must not restart the tumble
            }
            else
            {
                stunTimer = w.stun;
                vel.x = dir * w.knock.x;
                SetState(FState.Hitstun); ForceClip(w.level == HitLevel.Low ? "hitlow" : "hit", 0.03f);
            }
            if (dying) director?.OnKO(this, attacker);
            return res;
        }

        protected virtual void OnParried() { }

        public void GetParried(Fighter by)
        {
            move = null; holding = null;
            stunTimer = 1.0f;
            SetState(FState.Stunned); ForceClip("stunned", 0.06f);
            vel.x = -facing * 3f;
            SetExpr(Expr.Shocked, 0.95f);
            puppet.Flash(new Color(0.5f, 0.9f, 1f, 0.9f), 0.25f);
        }

        /// <summary>Dazed and open to attack for t seconds.</summary>
        public void Stun(float t)
        {
            move = null; holding = null; stunTimer = t; crouching = false;
            SetState(FState.Stunned); ForceClip("stunned", 0.1f);
        }

        /// <summary>Scripted jump (demo reel / training).</summary>
        public void ForceJump(float vx)
        {
            move = null; vel = new Vector2(vx, jumpVel * Mathf.Sqrt(scale)); grounded = false;
            SetState(FState.Jump); ForceClip("jump", 0.06f); FightSfx.Play("jump", 1f, 0.6f);
        }

        /// <summary>Director cinematics: freeze the brain and play a named clip.</summary>
        public void Cinematic(string clipName, float fade = 0.15f)
        {
            move = null; holding = null; SetState(FState.Cinematic); ForceClip(clipName, fade);
        }

        public void EndCinematic() { if (state == FState.Cinematic) ToIdle(); }

        public void ResetForRound(float x, int face)
        {
            transform.position = new Vector3(x, 0, 0);
            vel = Vector2.zero; grounded = true; facing = face; hp = maxHp; burnT = 0f; juggle = 0;
            move = null; holding = null; heldBy = null; crouching = false; flying = false;
            scale = scaleTarget = 1f; speedMul = 1f; superArmor = false; damageDealtMul = damageTakenMul = 1f;
            invulnUntil = 0f; hitstop = 0f; guard = guardMax; guardIdle = 0f; counterUntil = 0f;
            puppet.SetTint(Color.white);
            SetState(FState.Idle); ForceClip("idle", 0.05f);
            puppet.SetExpression(mood);
        }

        // ================================================================== physics
        void StepPhysics(float dt)
        {
            if (state == FState.Grabbed) return;
            var p = (Vector2)transform.position;
            float g = gravity;
            if (state == FState.Attack && move != null)
            {
                if (move.gravityMul >= 1f) g *= move.gravityMul;
                else
                {
                    // light moves float, but the float fades into a normal fall over the move instead of lasting for all of it
                    float ramp = Mathf.Clamp01(moveT / Mathf.Max(0.25f, move.Length * 2.2f));      // stays floaty through a 3-swing air chain, but never turns into hovering
                    g *= Mathf.Lerp(move.gravityMul, 1f, ramp * ramp);
                }
            }
            if (state == FState.Launched) g *= 0.82f;
            if (state == FState.Jump && !flying)
            {
                float cut = jumpVel * JumpCutK * Mathf.Sqrt(scale);
                if (jumpCutOk && vel.y > cut && !JumpHeld && stateT > 0.04f) { vel.y = cut; jumpCutOk = false; }     // let go early: a short hop
                if (vel.y <= 0f) jumpCutOk = false;
                if (Mathf.Abs(vel.y) < 2f) g *= ApexHang;                    // a beat of hang at the top
                else if (vel.y < 0f) g *= FallGravityK;                      // then a snappier fall
            }
            if (flying && state != FState.Launched) g = 0f;
            if (!grounded || vel.y > 0f) vel.y -= g * dt;
            p += vel * dt;
            if (Mathf.Abs(pushV) > 0.01f) { p.x += pushV * dt; pushV *= Mathf.Exp(-12f * dt); } else pushV = 0f;
            // nobody climbs past a jump and a half (air attacks used to be able to stack lift without limit)
            float ceiling = 4.2f * Mathf.Max(1f, scale);
            if (!flying && p.y > ceiling && vel.y > 0f) vel.y = Mathf.MoveTowards(vel.y, 0f, 90f * dt);
            if (p.y <= 0f)
            {
                p.y = 0f;
                if (vel.y < 0f) vel.y = 0f;
                if (!grounded && state == FState.Jump && landLock <= 0f) { landLock = scale > 1.5f ? 0.12f : 0.06f; landVis = LandVisLen; }     // recovery starts the frame he touches down
                grounded = true;
            }
            else grounded = false;
            float half = director != null ? director.arenaHalf : 10f;
            float lim = half - pushWidth * scale * 0.5f;
            p.x = Mathf.Clamp(p.x, -lim, lim);
            if (flying) p.y = Mathf.Min(p.y, 5.2f);
            transform.position = new Vector3(p.x, p.y, 0f);
        }
    }
}
