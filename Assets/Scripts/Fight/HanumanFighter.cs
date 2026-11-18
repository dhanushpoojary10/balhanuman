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
        public bool star;                       // earned by one long combo; PANCHMUKHI needs it (and a solved puzzle)
        public readonly bool[] unlocked = new bool[7];
        readonly float[] powerEnd = new float[7];
        Dictionary<string, MoveDef> moves;

        // frame-by-frame art (idle, walk, jump, fall, roll): shown instead of the skeleton while one of those clips is active
        Transform frameRoot; SpriteRenderer frameSr, blendSr;
        /// <summary>Fade each painted picture into the next between frames (off with PlayerPrefs fightBlend = 0).</summary>
        public static bool BlendFrames = true;
        HanumanFrames.Clip frameClip; float frameT; bool framesVisible;
        HanumanFrames.Clip curVisual; int curIdx; float parryStart = -10f;

        string buffered; float bufferT;
        bool maceArmed, chargeFull; public bool charging; float chargeStart, chargeBuzz;
        MoveDef chargeMove;
        float backPress = -10f, lastBackTap = -10f, lastFwdTap = -10f;
        bool wasBack, wasFwd, wasUp;
        int airUsed; MoveDef airCounted;
        float jumpAt = -1f, lastUpSeen = -10f, lastAtkT = -10f; string lastAtkId;     // delayed stick-up jump, launcher input memory, combo chain memory
        float fireFxT, vajraFxT, boonFxT, stepT;
        readonly float[] powerStart = new float[7]; readonly bool[] wasOn = new bool[7];
        static Material glowMat; SpriteRenderer glowSr;                   // boon outline: a soft halo of his sprite in the boon's colour
        SpriteRenderer floorGlow; float floorGlowA;                      // fire tail: warm light on the floor under him
        Transform mandalaRoot; SpriteRenderer mandalaSr; float mandalaA;     // glowing yantra on the floor during Panchmukhi
        public bool panchGlow;                  // set by the director while the Panchmukhi form is shown
        public bool demoBlock;                  // demo reel: hold block
        const float BaseWalk = 2.8f;
        float fwdEnter = -10f, backEnter = -10f;                         // when the stick last moved into forward / back
        bool bufFwd, bufBack, bufFar;                                    // what the stick meant at the moment the buffered command was pressed

        public static int Cost(PowerId p) => p == PowerId.Growth ? 2 : p == PowerId.Panchmukhi ? 3 : 1;
        public static float Duration(PowerId p)
        {
            switch (p) { case PowerId.Vayu: return 6f; case PowerId.Flight: return 7f; case PowerId.FireTail: return 8f; case PowerId.Shrink: return 4f; case PowerId.Vajra: return 7f; case PowerId.Growth: return 10f; }
            return 0f;
        }

        public bool Active(PowerId p) => Time.time < powerEnd[(int)p];

        public static Color BoonColor(PowerId p)
        {
            switch (p)
            {
                case PowerId.Vayu: return new Color(0.45f, 0.95f, 1f);
                case PowerId.Flight: return new Color(0.7f, 0.88f, 1f);
                case PowerId.FireTail: return new Color(1f, 0.5f, 0.12f);
                case PowerId.Shrink: return new Color(0.5f, 1f, 0.8f);
                case PowerId.Vajra: return new Color(0.6f, 0.8f, 1f);
                case PowerId.Growth: return new Color(1f, 0.8f, 0.3f);
            }
            return new Color(1f, 0.85f, 0.4f);
        }

        public override Color SwingTint => Active(PowerId.Vajra) ? new Color(0.75f, 0.88f, 1f, 0.95f) : Active(PowerId.Growth) ? new Color(1f, 0.85f, 0.4f, 0.95f)
            : Active(PowerId.Vayu) ? new Color(0.7f, 1f, 1f, 0.9f) : new Color(1f, 0.93f, 0.78f, 0.9f);
        public float Remaining01(PowerId p) => Active(p) ? (powerEnd[(int)p] - Time.time) / Mathf.Max(0.01f, Duration(p)) : 0f;

        public void Init(FightDirector d)
        {
            director = d; displayName = "HANUMAN"; maxHp = hp = 1000f;
            walkSpeed = BaseWalk; jumpVel = 13f; mood = Expr.Focus;
            puppet = Puppet.Build(transform, MoveLib.HanumanDims(), MoveLib.HanumanStyle(), 20);
            puppet.HideBody();      // Hanuman is drawn only from his painted frames; the skeleton is an invisible anchor
            frameRoot = new GameObject("FrameArt").transform; frameRoot.SetParent(transform, false);
            var fgo = new GameObject("Sprite"); fgo.transform.SetParent(frameRoot, false);
            frameSr = fgo.AddComponent<SpriteRenderer>(); frameSr.sortingOrder = 20; framesVisible = true;
            var bgo = new GameObject("SpriteBlend"); bgo.transform.SetParent(frameRoot, false);
            blendSr = bgo.AddComponent<SpriteRenderer>(); blendSr.sortingOrder = 21; blendSr.enabled = false;
            try { BlendFrames = PlayerPrefs.GetInt("fightBlend", 1) == 1; } catch { BlendFrames = true; }
            moves = MoveLib.Hanuman(clips);
            chargeMove = new MoveDef { id = "chargehold", name = "Charging", clip = clips["charge"], expr = Expr.Roar };
            chargeMove.frameClip = "charge"; chargeMove.frameFrom = 0; chargeMove.frameTo = 7; chargeMove.frameLoop = true;
            hurtSize = new Vector2(0.95f, 2.75f);
            parryWindow = ParryWindow + FightSetup.ParryBonus;
        }


        /// <summary>For tests: the painted clip and frame currently shown, e.g. "gada1:3".</summary>
        public string DebugFrame => curVisual != null ? curVisual.name + ":" + curIdx : "-";

        public override MoveDef GetMove(string id) => moves != null && moves.TryGetValue(id, out var m) ? m : null;
        public IEnumerable<string> MoveIds => moves.Keys;

        protected override float BlockPressTime => Mathf.Max(backPress, demoBlock ? Time.time - 0.05f : -10f);

        protected override bool JumpHeld => FightInput.Held(B.Jump) || FightInput.Y > 0.5f;

        protected override Expr CurrentMood() => hp < maxHp * 0.3f ? Expr.Furious : Active(PowerId.Vajra) || Active(PowerId.Growth) ? Expr.Serene : mood;

        // ================================================================== input -> intent
        protected override void Think(float dt)
        {
            float x = FightInput.X, y = FightInput.Y;
            float rel = x * facing;
            bool back = rel < -0.4f, fwd = rel > 0.4f, upNow = y > 0.5f;
            if (back && !wasBack) { backPress = Time.time; backEnter = Time.time; if (Time.time - lastBackTap < 0.28f) { inDodge = true; inDodgeDir = -facing; } lastBackTap = Time.time; }
            if (fwd && !wasFwd) { fwdEnter = Time.time; if (Time.time - lastFwdTap < 0.28f) { inDodge = true; inDodgeDir = facing; } lastFwdTap = Time.time; }
            wasBack = back; wasFwd = fwd;

            inX = x; inCrouch = y < -0.5f;
            // Pushing UP jumps, but a hair late: UP + GADA must be able to mean Sky Launcher instead of jump-then-air-swing.
            // An attack pressed inside that short window cancels the jump. The JUMP button and keyboard Space jump instantly.
            if (upNow) lastUpSeen = Time.time;
            if (upNow && !wasUp) jumpAt = Time.time + 0.11f;
            if (jumpAt > 0f && (FightInput.Down(B.Mace) || FightInput.Down(B.Kick))) jumpAt = -1f;
            if (jumpAt > 0f && Time.time >= jumpAt) { inJump = true; jumpAt = -1f; }
            if (FightInput.Down(B.Jump)) inJump = true;
            wasUp = upNow;
            // combo memory: a chain can continue for a moment after the previous move ends
            if (Attacking) { lastAtkId = move.id; lastAtkT = Time.time; }
            else if (InHitReaction) lastAtkId = null;
            // air attacks per jump: swing, backhand, spike, then one kick or dive. Landing resets it.
            if (grounded) airUsed = 0;
            if (!Attacking) airCounted = null;
            else if (move.air && move != airCounted) { airCounted = move; airUsed++; }
            if (FightInput.Down(B.Dodge)) { inDodge = true; inDodgeDir = Mathf.Abs(x) > 0.3f ? Mathf.Sign(x) : -facing; }

            // smart block: holding back blocks only while something is coming
            inBlock = (back && Threatened()) || demoBlock;
            if (demoBlock) { inX = 0; }

            // ---- mace: the swing starts the moment you press (no waiting for release). On a plain standing press, keeping the finger down
            // after the swing turns into a charge once the swing ends.
            if (FightInput.Down(B.Mace))
            {
                bool directional = Mathf.Abs(rel) > 0.4f || Mathf.Abs(y) > 0.5f || Time.time - lastUpSeen < 0.15f;
                bool chaining = lastAtkId != null && Time.time - lastAtkT < 0.3f;
                bool canCharge = grounded && !flying && !Attacking && scaleTarget >= 0.9f && !directional && !chaining;
                Command("M");
                if (canCharge) maceArmed = true;
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
                if (f.Attacking && Mathf.Abs(f.transform.position.x - transform.position.x) < 3.2f * Mathf.Max(1f, f.scale)) return true;
            return director.ProjectileNear(this, 5f);
        }

        float NearestFoeDist()
        {
            float best = 99f;
            if (director == null) return best;
            foreach (var f in director.TargetsOf(this))
                if (f != null && f.Alive) best = Mathf.Min(best, Mathf.Abs(f.transform.position.x - transform.position.x));
            return best;
        }

        /// <summary>
        /// Forward / back + attack only means the special move when the stick was just flicked that way (within 0.3 s), so walking toward the boss and
        /// tapping an attack gives the normal jab or kick. The gap-closing thrust also comes out from a held forward when he is out of reach.
        /// What the stick meant is captured when the button is pressed, so a command waiting in the buffer cannot change meaning.
        /// </summary>
        void Command(string b)
        {
            buffered = b; bufferT = 0.38f;
            float rel = FightInput.X * facing;
            bufFwd = rel > 0.4f && Time.time - fwdEnter < 0.3f;
            bufBack = rel < -0.4f && Time.time - backEnter < 0.3f;
            bufFar = rel > 0.4f && NearestFoeDist() > 2.4f;
        }

        MoveDef Resolve(string b)
        {
            float y = FightInput.Y;
            bool fwd = bufFwd, back = bufBack, farFwd = bufFar, up = y > 0.5f || Time.time - lastUpSeen < 0.15f, down = y < -0.5f;
            bool air = !grounded || flying;
            string cur = Attacking ? move.id : (Time.time - lastAtkT < 0.3f ? lastAtkId : null);
            if (b == "M")
            {
                if (scaleTarget < 0.9f) { powerEnd[(int)PowerId.Shrink] = 0f; return GetMove("rise"); }
                if (air)
                {
                    if (!flying && airUsed >= 4) return null;
                    if (down) return GetMove(flying ? "meteor" : "dive");
                    if (cur == "air1") return GetMove("air2");
                    if (cur == "air2") return GetMove("air3");
                    if (cur == "air3" && !flying) return null;          // the spike ends the air chain: no endless loop
                    return GetMove("air1");
                }
                if (scaleTarget > 1.5f && down) return GetMove("stomp");
                if (cur == "m1") return GetMove("m2");
                if (cur == "m2") return GetMove("m3");
                if (up) return GetMove("launcher");
                if (down) return GetMove("sweep");
                if (fwd || farFwd) return GetMove("thrust");
                if (back) return GetMove("whirl");
                return GetMove("m1");
            }
            // kick / tail
            if (air) return !flying && airUsed >= 4 ? null : GetMove("flykick");
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

        // ================================================================== frame-by-frame art
        protected override void OnPlayClip(string name)
        {
            frameClip = HanumanFrames.Get(name);
            frameT = 0f;
        }

        /// <summary>Shows the frame art while a clip that has frames is active (otherwise the skeleton), and advances the frames.</summary>
        protected override void AfterTick(float adt)
        {
            HanumanFrames.Clip c = null; int idx = 0; int nextIdx = -1; float frac = 0f; bool allowBlend = true;
            if (state == FState.Attack && move != null && move.frameClip != null)
            {
                // a frame-art attack: the frame follows the move clock, so the hit windows line up with the art
                c = HanumanFrames.Get(move.frameClip);
                if (c != null)
                {
                    int to = move.frameTo >= 0 ? move.frameTo : c.frames.Length - 1;
                    int cnt = to - move.frameFrom + 1;
                    float clock = charging && move == chargeMove ? Time.time - chargeStart : moveT;      // the charge hold keeps moveT frozen
                    int step = Mathf.FloorToInt(clock * c.fps);
                    if (move.frameStretch) step = Mathf.FloorToInt(Mathf.Clamp01(moveT / Mathf.Max(0.01f, move.Length)) * cnt);
                    idx = move.frameFrom + (move.frameLoop && !move.frameHold && !move.frameStretch ? step % cnt : Mathf.Min(step, cnt - 1));
                    // blend towards the next picture, except around the hit itself (the contact pose stays crisp)
                    float pos = move.frameStretch ? Mathf.Clamp01(moveT / Mathf.Max(0.01f, move.Length)) * cnt : clock * c.fps;
                    frac = pos - Mathf.Floor(pos);
                    int rel = idx - move.frameFrom;
                    if (rel < cnt - 1) nextIdx = idx + 1;
                    else if (move.frameLoop && !move.frameHold && !move.frameStretch) nextIdx = move.frameFrom;
                    foreach (var w in move.hits)
                        if (moveT >= w.t0 - 1.2f / c.fps && moveT < w.t1 + 0.5f / c.fps) { allowBlend = false; break; }
                }
            }
            else if (state == FState.Block && Time.time - parryStart < 0.35f && HanumanFrames.Get("parry") != null)
            {
                c = HanumanFrames.Get("parry");
                idx = Mathf.Min(c.frames.Length - 1, Mathf.FloorToInt((Time.time - parryStart) * c.fps));
            }
            else if (landVis > 0f && (state == FState.Idle || state == FState.Walk) && HanumanFrames.Get("land") != null)
            {
                // touching down: squash and rise back into the ready stance instead of snapping to the idle
                c = HanumanFrames.Get("land");
                float lp = Mathf.Clamp01(1f - landVis / LandVisLen) * c.frames.Length;
                idx = Mathf.Min(c.frames.Length - 1, Mathf.FloorToInt(lp));
                frac = lp - Mathf.Floor(lp);
                if (idx < c.frames.Length - 1) nextIdx = idx + 1;
            }
            if (c == null)
            {
                // every other pose: the clip that is playing, or the idle if it has no painted frames
                c = state != FState.Attack && CurClipName != null && frameClip != null ? frameClip : HanumanFrames.Get("idle");
                frameT += adt;
                int cnt = c.frames.Length;
                idx = Mathf.FloorToInt(frameT * c.fps);
                idx = c.loop ? idx % cnt : Mathf.Min(idx, cnt - 1);
                float cpos = frameT * c.fps; frac = cpos - Mathf.Floor(cpos);
                if (c.loop) nextIdx = (idx + 1) % cnt; else if (idx < cnt - 1) nextIdx = idx + 1;
                if (c.name == "launched" && cnt >= 4)
                {
                    // the tumble follows the flight instead of looping on a clock: thrown back, spinning, head-first, feet-first to land
                    float vy = vel.y;
                    idx = vy > 4f ? 0 : vy > -1f ? 1 : vy > -5f ? 2 : 3;
                    nextIdx = -1;
                }
            }
            curVisual = c; curIdx = idx;
            var f = c.frames[idx];
            frameSr.sprite = f.sprite;
            ApplyVisualScale();
            // place the sprite so the frame's pivot pixel lands on the fighter's root (the sprite's own pivot is its centre, 100 px per unit)
            var t = frameSr.transform;
            t.localScale = Vector3.one * (c.k * 100f);
            t.localPosition = new Vector3(-(f.px - f.w * 0.5f) * c.k, (f.py - f.h * 0.5f) * c.k, 0f);
            var baseCol = puppet.VisualColor();
            frameSr.color = baseCol;
            if (BlendFrames && allowBlend && nextIdx >= 0 && nextIdx < c.frames.Length && nextIdx != idx && frac > 0.4f)
            {
                // the next picture fades in over this one while this one thins out: soft in-betweens at screen refresh rate
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 1f, frac));
                var f2 = c.frames[nextIdx];
                blendSr.enabled = true; blendSr.sprite = f2.sprite;
                var bt = blendSr.transform;
                bt.localScale = Vector3.one * (c.k * 100f);
                bt.localPosition = new Vector3(-(f2.px - f2.w * 0.5f) * c.k, (f2.py - f2.h * 0.5f) * c.k, 0f);
                blendSr.color = new Color(baseCol.r, baseCol.g, baseCol.b, baseCol.a * a);
                frameSr.color = new Color(baseCol.r, baseCol.g, baseCol.b, baseCol.a * (1f - 0.55f * a));
            }
            else blendSr.enabled = false;
            UpdateGlow();
        }

        protected override void SetShake(Vector3 o) { base.SetShake(o); if (frameRoot != null) frameRoot.localPosition = o; }

        public bool DebugBlending => blendSr != null && blendSr.enabled;

        protected override void ApplyVisualScale()
        {
            base.ApplyVisualScale();
            if (frameRoot != null) { var q = SquashFactor; frameRoot.localScale = new Vector3(facing * scale * q.x, scale * q.y, 1f); }
        }

        protected override Vector3 AttackPoint(HitWindow w)
        {
            if (w.frameTip && curVisual != null)
            {
                var f = curVisual.frames[curIdx]; float k = curVisual.k;
                var o = new Vector2(f.tx * k + w.offset.x, f.ty * k + w.offset.y) * scale;
                return transform.position + new Vector3(o.x * facing, o.y, 0f);
            }
            return base.AttackPoint(w);
        }

        public override Vector3 WeaponPoint
        {
            get
            {
                if (curVisual == null) return base.WeaponPoint;
                var f = curVisual.frames[curIdx]; float k = curVisual.k;
                return transform.position + new Vector3(f.tx * k * scale * facing, f.ty * k * scale, 0f);
            }
        }

        protected override void SpawnAfterimage()
        {
            if (frameSr.sprite == null) return;
            var ls = (Vector2)frameSr.transform.lossyScale;
            var c1 = new Color(afterimageColor.r, afterimageColor.g, afterimageColor.b, 0f);
            FightFx.Spawn(frameSr.sprite, frameSr.transform.position, 0.22f, ls, ls, afterimageColor, c1, frameSr.sortingOrder - 1, frameSr.transform.eulerAngles.z);
        }

        // ================================================================== state additions (flight, charge glow)
        protected override void StepState(float dt, float adt)
        {
            if (charging && move != chargeMove) charging = false;
            if (charging && move == chargeMove)
            {
                moveT = 0.01f; vel.x = 0f;
                float k = Mathf.Clamp01((Time.time - chargeStart) / 0.95f);
                chargeBuzz -= dt;
                if (chargeBuzz <= 0f) { Haptics.Tick(); chargeBuzz = Mathf.Lerp(0.22f, 0.07f, k); director?.Rumble(0.012f + 0.03f * k, 0.15f); }
                if (k >= 1f && !chargeFull) { chargeFull = true; Haptics.Medium(); director?.Kick(Vector2.up, 0.25f); }
                if (k < 1f) chargeFull = false;
                if (Random.value < dt * (10f + 30f * k)) FightFx.Sparkle(WeaponPoint, 1, 1.2f, k >= 1f ? new Color(1f, 0.9f, 0.4f) : new Color(1f, 0.7f, 0.3f), true);
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
            powerEnd[(int)p] = Time.time + Duration(p); powerStart[(int)p] = Time.time;
            director.OnPower(this, p);
            switch (p)
            {
                case PowerId.Vayu:
                    StartMove(GetMove("vayudash"));
                    afterimageColor = new Color(0.55f, 0.95f, 1f, 0.55f);
                    FightSfx.Play("wind");
                    FightFx.Flip("wind", Center, 2.4f * scale, 0.45f, new Color(1f, 1f, 1f, 0.85f), FightFx.Order + 2);
                    break;
                case PowerId.Flight:
                    flying = true; grounded = false; vel = new Vector2(0, 11f);
                    ToIdle(); PlayClip("fly", 0.1f);
                    FightSfx.Play("wind", 0.8f);
                    FightFx.Flip("wind", transform.position + Vector3.up * 0.3f, 2.8f * scale, 0.5f, new Color(1f, 1f, 1f, 0.85f), FightFx.Order + 2, 0f, false, false, 14f, 0.5f);
                    FightFx.Dust(transform.position, 10, 3f, 0, 1f);
                    FightFx.Spawn(FightArt.Ring, new Vector3(transform.position.x, 0.05f, 0f), 0.45f, new Vector2(0.5f, 0.12f), new Vector2(4.5f, 0.8f),
                        new Color(0.8f, 0.95f, 1f, 0.9f), new Color(0.8f, 0.95f, 1f, 0f), FightFx.Behind);     // the gust slams a ring into the floor
                    for (int i = 0; i < 3; i++) FightFx.Streaks(Center + Vector3.down * i * 0.4f, 0, 2, new Color(0.8f, 0.95f, 1f));
                    break;
                case PowerId.FireTail:
                    StartMove(GetMove("roar"));
                    FightSfx.Play("fire", 0.9f); FightSfx.Play("power", 0.9f, 0.6f);
                    var tip = puppet != null ? puppet.TailSeg(puppet.TailCount - 1) : null;           // the tail itself ignites
                    FightFx.Flip("fireburst", tip != null ? tip.position : Center, 1.8f * scale, 0.4f, Color.white, FightFx.Order + 2);
                    director.BurnClones();
                    break;
                case PowerId.Shrink:
                    scaleTarget = 0.38f;
                    FightFx.Sparkle(Center, 16, 1.4f, new Color(0.6f, 0.95f, 1f), true);
                    FightFx.Flip("aura", Center, 2.4f * scale, 0.4f, Color.white, FightFx.Order + 2, 0f, false, false, 14f, 1f, 1f, null, true);   // the ring collapses inward with him
                    FightSfx.Play("shrink");
                    break;
                case PowerId.Vajra:
                    StartMove(GetMove("roar"));
                    FightSfx.Play("thunder"); FightSfx.Play("power", 1.1f, 0.6f);
                    FightFx.Flip("bolt", transform.position, 5f, 0.4f, Color.white, FightFx.Order + 3);        // Indra's thunderbolt strikes him
                    break;
                case PowerId.Growth:
                    StartMove(GetMove("roar"));
                    scaleTarget = 2.2f;
                    FightSfx.Play("grow");
                    FightFx.Pillar(transform.position, 9f, 2.6f, new Color(1f, 0.8f, 0.35f), 1.2f);
                    FightFx.Still("crack", Random.Range(0, 4), new Vector3(transform.position.x, 0.04f, 0f), 2.0f, 1.6f, Color.white, FightFx.Behind - 1, Random.value < 0.5f, 1f, 1.9f);  // the floor splits as he grows
                    director.Shake(0.25f, 1.0f);
                    break;
            }
            if (p != PowerId.Shrink) FightFx.Aura(Center, (p == PowerId.Growth ? 3.6f : 2.4f) * scale, Color.white);     // golden bloom: every boon is a gift of devotion
            return true;
        }

        public void ActivatePanchmukhiCost() { if (!infiniteMeter) bhakti = 0f; }

        void UpdatePowers()
        {
            bool vayu = Active(PowerId.Vayu), vajra = Active(PowerId.Vajra), growth = Active(PowerId.Growth), fire = Active(PowerId.FireTail);
            speedMul = vayu ? 1.45f : 1f;
            damageDealtMul = (vajra ? 1.3f : 1f) * (growth ? 1.8f : 1f) * (fire ? 1.15f : 1f);
            damageTakenMul = (vajra ? 0.25f : 1f) * (growth ? 0.6f : 1f) * FightSetup.DamageTakenK;
            superArmor = vajra || growth;
            walkSpeed = BaseWalk * (scaleTarget < 0.9f ? 1.8f : 1f);
            if (vayu) { afterimageUntil = Time.time + 0.05f; afterimageColor = new Color(0.55f, 0.95f, 1f, 0.45f); }
            if (puppet != null) puppet.SetTint(vajra ? new Color(1f, 0.92f, 0.6f) : Color.white);
            if (vajra && Random.value < Time.deltaTime * 8f)
            {
                var a = FightFx.Spawn(FightArt.Soft, transform.position + new Vector3(Random.Range(-0.5f, 0.5f) * scale, Random.Range(0.2f, 2.4f) * scale), 0.5f,
                    Vector2.one * 0.5f * scale, Vector2.one * 0.1f, new Color(1f, 0.85f, 0.3f, 0.8f), new Color(1f, 0.6f, 0.1f, 0f), puppet.SortingOrder + 1);
                a.vel = Vector2.up * 2.5f;
            }
            if (puppet != null)
            {
                int fxOrder = puppet.SortingOrder + 1;
                if (vajra) { vajraFxT -= Time.deltaTime; if (vajraFxT <= 0f) { vajraFxT = 1.2f; FightFx.Flip("crackle", Center, 2.4f * scale, 0.5f, new Color(1f, 1f, 1f, 0.6f), fxOrder + 1, 0f, Random.value < 0.5f, true, 14f, 1f, 1f, transform); } }
            }
            UpdateMandala(vayu, vajra, growth, fire);
            UpdateBoonFx(vayu, growth, fire);
            if (fire && puppet != null)
            {
                fireFxT -= Time.deltaTime;
                if (fireFxT <= 0f) { fireFxT = 0.16f; var seg = puppet.TailSeg(Mathf.Max(0, puppet.TailCount - 1 - Random.Range(0, 2))); if (seg != null) FightFx.Flames(seg.position, 1, 0.5f * scale); }
            }
            // timed powers ending
            if (flying && !Active(PowerId.Flight) && !(Attacking && move.id == "meteor")) { flying = false; if (state == FState.Idle) ToIdle(); }
            if (scaleTarget < 0.9f && !Active(PowerId.Shrink)) { scaleTarget = 1f; FightFx.Sparkle(Center, 10, 1f, new Color(0.6f, 0.95f, 1f), false); FightSfx.Play("grow", 1.8f, 0.5f); }
            if (scaleTarget > 1.5f && !Active(PowerId.Growth)) { scaleTarget = 1f; FightFx.Pillar(transform.position, 4f, 1.2f, new Color(1f, 0.8f, 0.35f), 0.6f); FightSfx.Play("shrink", 0.7f); }
            if (infiniteMeter) bhakti = 3f;
        }

        /// <summary>The newest boon that is still on (its colour drives the outline).</summary>
        int LatestBoon()
        {
            int best = -1; float t = -1f;
            foreach (var b in Boons) if (Active(b) && powerStart[(int)b] > t) { t = powerStart[(int)b]; best = (int)b; }
            return best;
        }

        /// <summary>Soft coloured halo of the current frame behind him; pulses, and flickers in the last 1.5 s so the player sees it running out.</summary>
        void UpdateGlow()
        {
            if (frameSr == null) return;
            int b = LatestBoon();
            if (glowSr == null)
            {
                if (b < 0) return;
                if (glowMat == null)
                {
                    var sh = Resources.Load<Shader>("Level02/Shaders/FightGlow");
                    if (sh == null || !sh.isSupported) return;
                    glowMat = new Material(sh);
                    glowMat.SetFloat("_Grow", 1.12f); glowMat.SetFloat("_Spread", 10f);
                }
                var g = new GameObject("BoonGlow"); g.transform.SetParent(frameSr.transform, false); g.transform.localScale = Vector3.one * 1.12f;
                glowSr = g.AddComponent<SpriteRenderer>(); glowSr.sharedMaterial = glowMat; glowSr.sortingOrder = frameSr.sortingOrder - 1;
            }
            if (b < 0) { glowSr.enabled = false; return; }
            var c = BoonColor((PowerId)b);
            float a = 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
            if (powerEnd[b] - Time.time < 1.5f && Mathf.Repeat(Time.time * 8f, 1f) < 0.45f) a *= 0.25f;
            glowSr.enabled = frameSr.enabled;
            glowSr.sprite = frameSr.sprite;
            glowSr.color = new Color(c.r, c.g, c.b, 0.85f * a);
        }

        /// <summary>Small per-boon touches while a boon lasts, only when something happens (moving, flying, stepping).</summary>
        void UpdateBoonFx(bool vayu, bool growth, bool fire)
        {
            float dt = Time.deltaTime;
            bool moving = Mathf.Abs(vel.x) > 1f;
            bool shrink = Active(PowerId.Shrink), flight = Active(PowerId.Flight);

            // a boon running out: a small puff in its colour (shrink and giant already have their own)
            for (int i = 0; i < Boons.Length; i++)
            {
                var b = Boons[i]; bool on = Active(b);
                if (wasOn[i] && !on && b != PowerId.Shrink && b != PowerId.Growth && Alive) FightFx.Sparkle(Center, 10, 1f * scale, BoonColor(b), false);
                wasOn[i] = on;
            }

            boonFxT -= dt;
            if (boonFxT <= 0f)
            {
                boonFxT = 0.1f;
                if (vayu && grounded && moving && Random.value < 0.4f)             // wind ribbon at the feet while he runs
                    FightFx.Flip("wind", transform.position + Vector3.up * 0.25f * scale, 1.3f * scale, 0.35f, new Color(1f, 1f, 1f, 0.7f), FightFx.Behind, 0f, vel.x < 0f, false, 14f, 0.4f);
                if (flight && flying)                                                // soft air pushed down under his feet
                {
                    var a = FightFx.Spawn(FightArt.Soft, transform.position + new Vector3(Random.Range(-0.3f, 0.3f), 0.1f, 0f), 0.4f, Vector2.one * 0.5f, Vector2.one * 0.15f,
                        new Color(0.8f, 0.95f, 1f, 0.45f), new Color(0.8f, 0.95f, 1f, 0f), 19);
                    a.vel = new Vector2(-vel.x * 0.2f, -2.5f);
                }
                if (fire && puppet != null)                                          // embers drifting off the tail tip
                {
                    var tip = puppet.TailSeg(puppet.TailCount - 1);
                    if (tip != null)
                    {
                        var e = FightFx.Spawn(FightArt.Soft, tip.position + (Vector3)Random.insideUnitCircle * 0.15f, Random.Range(0.6f, 0.9f), Vector2.one * 0.12f, Vector2.one * 0.04f,
                            new Color(1f, 0.75f, 0.3f, 1f), new Color(1f, 0.3f, 0.05f, 0f), FightFx.Order);
                        e.vel = new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(1f, 1.8f));
                    }
                }
                if (shrink && moving)                                                // tiny sparkle trail
                    FightFx.Spawn(FightArt.Star, Center + (Vector3)Random.insideUnitCircle * 0.2f, 0.35f, Vector2.one * 0.22f, Vector2.one * 0.04f,
                        Color.white, new Color(0.5f, 1f, 0.8f, 0f), FightFx.Order, Random.Range(0f, 90f));
            }

            // meteor dive: a short fire streak behind him
            if (Attacking && move != null && move.id == "meteor" && Random.value < dt * 25f)
                FightFx.Flames(transform.position + Vector3.up * 0.6f * scale, 1, 0.6f);

            // giant: every step thumps the floor
            if (growth && grounded && state == FState.Walk)
            {
                stepT -= dt;
                if (stepT <= 0f)
                {
                    stepT = 0.45f;
                    FightFx.Dust(transform.position + Vector3.right * facing * 0.4f * scale, 4, 1.2f, 0f, 0.5f * scale);
                    director?.Shake(0.05f, 0.12f);
                    FightSfx.Play("step_giant", 1f, 0.5f);
                }
            }
            else stepT = 0.15f;

            // fire tail: warm light on the floor under him
            floorGlowA = Mathf.MoveTowards(floorGlowA, fire ? 1f : 0f, dt * 3f);
            if (floorGlow == null && floorGlowA > 0f)
            {
                floorGlow = new GameObject("FireFloorGlow").AddComponent<SpriteRenderer>();
                floorGlow.sprite = FightArt.Soft; floorGlow.sortingOrder = -2;
            }
            if (floorGlow != null)
            {
                floorGlow.enabled = floorGlowA > 0.01f;
                floorGlow.transform.position = new Vector3(transform.position.x, 0.06f, 0f);
                floorGlow.transform.localScale = new Vector3(3f * scale, 0.55f * scale, 1f);
                float lift = Mathf.Clamp01(1f - transform.position.y / 3f);
                floorGlow.color = new Color(1f, 0.45f, 0.1f, 0.3f * floorGlowA * lift * (0.85f + 0.15f * Mathf.Sin(Time.time * 9f)));
            }
        }

        /// <summary>Called by the director for each of his hits that connects: the boon adds its own spark at the impact.</summary>
        public void OnBoonHit(Vector3 point)
        {
            if (Active(PowerId.Vajra)) FightFx.Flip("crackle", point, 1.0f, 0.25f, new Color(1f, 1f, 1f, 0.9f), FightFx.Order + 5, Random.Range(0f, 360f));
            if (Active(PowerId.Vayu)) FightFx.Flip("slash", point, 1.2f, 0.18f, new Color(0.7f, 1f, 1f, 0.85f), FightFx.Order + 3, Random.Range(-20f, 20f), facing < 0, false, 14f, 0.6f);
        }

        /// <summary>A white yantra glowing on the floor under his feet while any boon is on, tinted by the boon (it turns slowly).</summary>
        void UpdateMandala(bool vayu, bool vajra, bool growth, bool fire)
        {
            var sheet = VfxSheets.Get("mandalaw");
            if (sheet == null) return;
            bool on = panchGlow;
            if (mandalaRoot == null)
            {
                if (!on) return;
                mandalaRoot = new GameObject("BoonMandala").transform;
                var disc = new GameObject("Disc"); disc.transform.SetParent(mandalaRoot, false);
                mandalaSr = disc.AddComponent<SpriteRenderer>(); mandalaSr.sprite = sheet.frames[0]; mandalaSr.sortingOrder = -2;
            }
            mandalaA = Mathf.MoveTowards(mandalaA, on ? 1f : 0f, Time.unscaledDeltaTime * 2f);
            if (mandalaA <= 0.01f) { if (mandalaRoot.gameObject.activeSelf) mandalaRoot.gameObject.SetActive(false); return; }
            if (!mandalaRoot.gameObject.activeSelf) mandalaRoot.gameObject.SetActive(true);
            var tint = new Color(1f, 0.85f, 0.4f);
            float w = 4f * Mathf.Max(0.4f, scale);                                           // world width of the disc
            float k = w / Mathf.Max(0.1f, mandalaSr.sprite.bounds.size.x);
            mandalaRoot.position = new Vector3(transform.position.x, 0.1f, 0f);               // lies on the floor, squashed flat
            mandalaRoot.localScale = new Vector3(k, k * 0.3f, 1f);
            mandalaSr.transform.Rotate(0f, 0f, -15f * Time.deltaTime);
            float lift = Mathf.Clamp01(1f - transform.position.y / 3.5f);                        // fades out as he leaves the ground
            mandalaSr.color = new Color(tint.r, tint.g, tint.b, 0.5f * mandalaA * lift);
        }

        void OnDestroy()
        {
            if (mandalaRoot != null) Destroy(mandalaRoot.gameObject);
            if (floorGlow != null) Destroy(floorGlow.gameObject);
        }

        public void EndAllPowers()
        {
            for (int i = 0; i < powerEnd.Length; i++) powerEnd[i] = 0f;
            flying = false; scaleTarget = 1f; charging = false; buffered = null;
        }

        protected override void OnParried() { AddMeter(0.35f); parryStart = Time.time; }
    }
}
