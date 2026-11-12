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
        public float invulnUntil;
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
        public bool CanAct => controlEnabled && (state == FState.Idle || state == FState.Walk || state == FState.Crouch || state == FState.Block || state == FState.Jump);
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
            if (hitstop > 0f)
            {
                hitstop -= dt;
                // victims shiver during hit-stop
                puppet.transform.localPosition = InHitReaction ? (Vector3)(Random.insideUnitCircle * 0.05f * scale) : Vector3.zero;
                return;
            }
            puppet.transform.localPosition = Vector3.zero;
            float adt = dt * speedMul * (Attacking ? move.speedMul : 1f);

            inJump = inDodge = false;
            if (controlEnabled && director != null && director.ControlsLive) Think(dt);
            else { inX = 0; inBlock = inCrouch = false; }

            StepState(dt, adt);
            StepPhysics(dt);
            if (holding != null) CarryHeld();

            scale = Mathf.MoveTowards(scale, scaleTarget, dt * Mathf.Max(1.2f, Mathf.Abs(scaleTarget - scale) * 2.5f));
            puppet.transform.localScale = new Vector3(facing * scale, scale, 1f);
            puppet.velocity = vel; puppet.facing = facing;
            puppet.Tick(adt);
            if (state == FState.Attack && move != null) CheckHits();
            Timers(dt);
        }

        void Timers(float dt)
        {
            if (exprT > 0f) { exprT -= dt; if (exprT <= 0f) puppet.SetExpression(CurrentMood()); }
            if (burnT > 0f)
            {
                burnT -= dt;
                if (Random.value < dt * 18f) FightFx.Flames(Center + (Vector3)Random.insideUnitCircle * 0.4f * scale, 1, 0.8f * scale);
                if (Alive && !immortal) hp = Mathf.Max(1f, hp - dt * 10f);
            }
            if (Time.time < afterimageUntil)
            {
                afterimageTimer -= dt;
                if (afterimageTimer <= 0f) { afterimageTimer = 0.045f; puppet.Afterimage(afterimageColor, 0.22f, puppet.SortingOrder - 1); }
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
        }

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
            vel.y = jumpVel * Mathf.Sqrt(scale); vel.x = inX * walkSpeed * 1.1f * SpeedK;
            grounded = false; SetState(FState.Jump); ForceClip("jump", 0.06f);
            FightSfx.Play("jump", 1f, 0.6f);
            FightFx.Dust(transform.position, 4, 1.5f, 0, 0.6f * scale);
        }

        void Land()
        {
            FightFx.Dust(transform.position, 5, 2f, 0, 0.7f * scale);
            FightSfx.Play("land", 1f, 0.6f);
            if (scale > 1.5f && director != null) director.Shake(0.12f, 0.2f);
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
            SetState(FState.Attack);
            crouching = false;
            if (m.air) grounded = false;
            puppet.Play(m.clip, 0.06f);
            curClip = null;
            if (m.expr != Expr.Neutral) SetExpr(m.expr, Mathf.Min(0.6f, m.Length));
            if (m.telegraph) { puppet.Flash(new Color(1f, 0.25f, 0.2f, 0.85f), 0.3f); FightSfx.Play("tick", 0.6f, 0.8f); }
            return true;
        }

        public bool InCancelWindow => Attacking && moveT >= move.cancelFrom;

        void StepMove(float dt, float adt)
        {
            if (move == null) { ToIdle(); return; }
            moveT += adt;
            while (evIndex < move.events.Count && moveT >= move.events[evIndex].t) { var e = move.events[evIndex++]; e.act?.Invoke(this); if (move == null || state != FState.Attack) return; }
            float k = Mathf.Sqrt(scale) * (speedMul > 1f ? 1.3f : 1f);
            if (move.lungeSpeed != 0f && moveT >= move.lungeT0 && moveT < move.lungeT1) vel.x = facing * move.lungeSpeed * k;
            else if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 45f * dt);
            if (!whooshed && move.hits.Count > 0 && moveT >= move.hits[0].t0 - 0.04f)
            {
                whooshed = true;
                FightSfx.Play(string.IsNullOrEmpty(move.sfx) ? "whoosh" : move.sfx, scale > 1.5f ? 0.7f : 1f, 0.8f);
            }
            if (move.endOnLand && grounded && moveT > 0.08f)
            {
                var m = move; m.onLand?.Invoke(this);
                if (move == m) ToIdle();
                return;
            }
            if (moveT >= move.Length) ToIdle();
        }

        void CheckHits()
        {
            if (director == null) return;
            for (int i = 0; i < move.hits.Count; i++)
            {
                var w = move.hits[i];
                if (moveT < w.t0 || moveT >= w.t1) continue;
                Vector3 p = puppet.Point(w.joint, w.offset);
                float r = w.radius * scale;
                director.CheckProjectiles(this, p, r);
                foreach (var t in director.TargetsOf(this))
                {
                    if (t == null || !t.gameObject.activeInHierarchy || Done(i, t)) continue;
                    if (!t.Overlaps(p, r)) continue;
                    hitsDone.Add((i, t));
                    var res = t.ReceiveHit(this, w, p);
                    if (res.connected && w.grab && move.throwMove != null) { BeginThrow(t, move.throwMove); return; }
                    if (res.parried) return;
                    if (move == null) return;
                }
            }
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
        void BeginThrow(Fighter t, MoveDef throwMove)
        {
            holding = t;
            t.heldBy = this; t.move = null; t.SetState(FState.Grabbed); t.ForceClip("launched", 0.05f); t.vel = Vector2.zero;
            t.SetExpr(Expr.Shocked, 1f);
            StartMove(throwMove);
            holding = t;
        }

        void CarryHeld()
        {
            if (holding.state != FState.Grabbed) { holding = null; return; }
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

            if (state == FState.Block && facingAttacker && w.level != HitLevel.Unblockable)
            {
                bool ok = crouching ? w.level != HitLevel.Overhead : w.level != HitLevel.Low;
                if (ok)
                {
                    if (Time.time - BlockPressTime <= ParryWindow && (projectile || attacker.state == FState.Attack))
                    {
                        res.parried = true;
                        if (!projectile) attacker.GetParried(this);
                        director?.OnParry(this, projectile ? null : attacker, point);
                        blockStart = -10f; OnParried();
                        return res;
                    }
                    res.blocked = true;
                    float chip = w.damage * 0.08f * attacker.damageDealtMul * damageTakenMul;
                    hp = Mathf.Max(1f, hp - chip);
                    stunTimer = 0.14f + w.stun * 0.35f;
                    vel.x = dir * (w.knock.x * 0.7f + 1f);
                    SetState(FState.Blockstun); ForceClip(crouching ? "crouchblock" : "block", 0.03f);
                    puppet.Flash(new Color(0.6f, 0.9f, 1f, 0.6f), 0.1f);
                    director?.OnBlocked(attacker, this, w, point);
                    return res;
                }
            }

            res.connected = true;
            float dmg = w.damage * attacker.damageDealtMul * damageTakenMul;
            hp -= dmg;
            if (immortal) hp = Mathf.Max(1f, hp);
            if (w.burn) burnT = 3f;
            puppet.Flash(new Color(1f, 1f, 1f, 0.9f), 0.12f);
            director?.OnHit(attacker, this, w, point, dmg);

            if (superArmor && hp > 0f)
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
                SetState(FState.Launched); ForceClip("launched", 0.04f);
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
            stunTimer = 0.95f;
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
            invulnUntil = 0f; hitstop = 0f;
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
            if (state == FState.Attack && move != null) g *= move.gravityMul;
            if (state == FState.Launched) g *= 0.82f;
            if (flying && state != FState.Launched) g = 0f;
            if (!grounded || vel.y > 0f) vel.y -= g * dt;
            p += vel * dt;
            if (p.y <= 0f)
            {
                p.y = 0f;
                if (vel.y < 0f) vel.y = 0f;
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
