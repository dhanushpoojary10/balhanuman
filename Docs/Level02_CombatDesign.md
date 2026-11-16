# Level 2 "Patala Yuddha" — Combat Redesign (audit, design, plan, prompts)

Status: **design only. No game code has been changed for this document.**
Evidence comes from reading the scripts, dumping the live move tables from the running game (`Temp/moves_dump.txt`), and the `MoveAudit` / `PhysicsDiag` runs done earlier. Proposed numbers are design targets to be validated with those tools, not measured results. Nothing has been tested on a phone.

---

## A. Current gameplay audit

### A1. What is implemented (verified in code)
| Area | What exists |
|---|---|
| Fighters | `Fighter` base: custom kinematic physics (no Rigidbody), state machine (Idle/Walk/Crouch/Block/Jump/Attack/Hitstun/Blockstun/Launched/Knockdown/Getup/Dodge/Stunned/Grabbed/Cinematic/Dead), hit reception, juggle, throws |
| Move data | All moves are code in `MoveLib` (`MoveDef`, `HitWindow`, `MoveEvent`). Hanuman hit windows open on specific painted frames and follow the measured weapon/foot/tail tip per frame |
| Hanuman | m1-m3 gada combo; thrust, whirlwind, launcher, sweep; hold-to-charge smashes (2 levels); kicks k1-k2, tail trip, tail grab+throw, lift+slam, fire whip; air1-3, dive, meteor, flying kick; dodge roll; block; parry; six boons; Panchmukhi (now behind a puzzle) |
| Boss | `AhiravanaFighter`: weighted-random move picker by distance and round, reaction-block rolls, grab/orb/barrage/vanish/summon, clones, titan form, dummy modes (Idle/Block/Fight) |
| Rounds | R1 duel, R2 sorcery, R3 titan (boss HP floors at 1, then the finale: five lamps + puzzle) |
| Feel | Hit-stop, camera shake/punch/kick/rumble, squash on hit, pooled procedural effects, haptic patterns, combo counter and named combos, slow-mo on parry/perfect dodge |
| Tools | `MoveAudit`, `PhysicsDiag` (editor/dev builds only), training room and demo reel |

### A2. What works well (keep)
- Frame-timed hit windows that follow the painted art. Hits land when the art shows them.
- Every move starts, hits and returns to idle; chains work through the real input path (audit: all pass).
- Defence set functions: parry, perfect dodge, orb reflect, block (audit: all pass).
- Pooled effects, rate-limited camera, clean air-attack limits (after the recent fixes).
- Boons all function and are visually distinct.

### A3. What feels weak, with evidence
1. **Every light attack is the same attack.** `m1, m2, k1, k2, sweep, launcher` all have length 0.36 s, startup 0.14, active end 0.23, recovery 0.14, hit-stop 0.06, shake 0.04, stun 0.38-0.40, damage 35-50. Only `thrust` (0.18/0.32, lunge 4.5), `m3` and the charge moves differ. There is no fast-vs-slow, safe-vs-risky trade.
2. **Cancels ignore what happened.** `cancelFrom` equals the end of the active window, regardless of hit, whiff or block. Spamming a chain is free: no commitment on a miss, no reward for confirming a hit.
3. **`MoveDef.armor` is never read.** It is set on `finalblow`, `roar`, `summon` and both throw moves, but `Fighter.ReceiveHit` only looks at the fighter-level `superArmor` (Vajra/Giant). Heavy and "armored" moves can be interrupted by any jab, so the boss's `summon` and Hanuman's `finalblow` have no protection.
4. **Boss attacks are not readable.** `jab` startup 0.12 s and `combo` startup 0.10 s with no telegraph; a normal visual reaction is about 0.2-0.3 s, so these cannot be reacted to. Only `overhead` (0.46), `grab` (0.26), `gslam` (0.62) and `gsweep` (0.15) have `telegraph = true`. The parry window is 0.2 s, so reaction-parrying a jab is impossible.
5. **The boss's block is omniscient.** On each new player move within 4.2 m he rolls 30-40% (by round) at the moment the move starts, with zero reaction delay and no regard for which move it is. It feels random and unfair, not readable.
6. **The boss picks randomly.** Weighted random by distance and round, think timer 0.25-0.65 s ÷ aggression. One adaptive rule exists (grab weight grows with how often you block). No memory of spam, jumping, turtling or whiffing, and no patterns.
7. **Boss moves have no commitment.** `cancelFrom = 99` (he cannot cancel) but any hit interrupts him because of #3. Heavy moves (overhead, gslam) are not "high risk, high reward" for him, and recovery windows (0.21-0.53 s) only matter if you are not hitting him anyway.
8. **Blocking is nearly free.** Holding back auto-blocks while any enemy is attacking within 4.5 m (`Threatened()`), chip damage is 8%, there is no guard meter and no guard break. Only the boss's grab beats it.
9. **Defence rewards are thin and invisible.** Parry: boss stunned 0.95 s, +0.35 Bhakti, 0.3x slow-mo for 0.6 s, banner. Perfect dodge: +0.3 Bhakti, slow-mo, banner. Neither opens a defined counter window or is shown on the HUD.
10. **Juggles are limited only by physics.** Launch velocity decays by 0.88^n and air attacks are capped, but there is no damage scaling or total-hit limit.
11. **Physics is functional but flat.** Jump velocity 13, gravity 36 gives a 2.35 m apex and 0.72 s air time. No jump anticipation, no variable jump height, no landing recovery, no apex hang. Fighter separation moves positions directly each frame, which can jitter during lunges.
12. **Impact tiers are only partly distinct.** Light hit-stop 0.06 / shake 0.04; heavy 0.10-0.14 / 0.10-0.33. Only three hit-sound variants per type; no distinct "medium".
13. **Touch layout asks too much of one thumb.** Five action buttons (Gada, Kick, Jump, Dodge, Ultimate) and a row of six boon buttons all sit on the right/bottom. Using a boon means leaving the attack buttons.
14. **No difficulty or assist options.** Both fighters have 1000 HP; Hanuman needs about 7-8 combos per round, the boss needs about 13 light hits.

### A4. What is missing
Guard meter and guard break; armor and attack priority; on-hit / on-block / on-whiff cancel rules; combo damage scaling and a hit limit; counter windows after parry and perfect dodge; boss pattern library, observer and reaction delay; HP-threshold phases inside a round; boss armor and recovery punish rules; jump feel (anticipation, variable height, landing); soft fighter separation; per-move frame data display in training; difficulty and touch assists.

### A5. What must not change
The frame-timed hit window system and measured tips; `MoveDef` / `HitWindow` / `FightDirector` event hooks; pooled effects; the boon set and the Panchmukhi puzzle gate; the move list and training room; the dev test tools; the art (no new art is assumed unless listed in section E).

---

## B. Combat design proposal

All timings are expressed per move and applied with the existing levers: hit-window frames, `cancelFrom`, `speedMul`, trimming `frameFrom/frameTo`, knockback, stun, hit-stop and damage. No new art is required for sections B1-B9.

### B1. Player moves: distinct roles, risk and reward
Targets (seconds). "Adv" = who is plus after a connected hit. Values to be tuned with `MoveAudit`.

| Move | Role | Startup | Active | Recovery | Dmg | Hit-stop / shake | Notes (risk / reward) |
|---|---|---|---|---|---|---|---|
| m1 gada jab | fast starter | 0.10 | 0.08 | 0.12 | 30 | 0.04 / 0.02 | Safe-ish, low damage, opens chains. `speedMul` about 1.2 |
| m2 backhand | pressure | 0.13 | 0.09 | 0.16 | 40 | 0.06 / 0.04 | Only chains from m1 on hit or block |
| m3 earth-splitter | finisher | 0.32 | 0.11 | 0.28 | 85 | 0.12 / 0.16 | Knockdown, armor on startup, punishable on whiff |
| k1 / k2 kick | low-risk poke / wall pusher | 0.11 / 0.16 | 0.08 | 0.14 / 0.20 | 30 / 45 | 0.04 / 0.06 | k1 fast, short reach; k2 pushes far |
| thrust | gap-closer | 0.18 | 0.14 | 0.20 | 55 | 0.07 | Lunge, beats retreating boss, loses to anything on whiff |
| whirlwind | anti-surround / clone clear | 0.23 | 0.39 | 0.15 | 40 + 45 | 0.06 / 0.06 | Two hits; punishable if blocked |
| launcher | combo starter | 0.14 | 0.13 | 0.09 | 50 | 0.09 / 0.08 | Anti-air; links to air chain only on hit |
| sweep / tail trip | low pokes | 0.14-0.15 | 0.09 | 0.16 | 45 / 35 | 0.06 | Beat standing guard; lose to jump |
| charge 1 / 2 | heavy guard-break | 0.46 | 0.08 | 0.29 | 90 / 135 | 0.14 / 0.22-0.33 | Super-armored after 0.30 s of charge; huge risk if baited |
| air1-air3 | air chain | 0.17 | 0.12 | 0.13 | 35 / 38 / 60 | 0.06 / 0.11 | One chain per jump (already limited); spike launches downward |
| dive / meteor | punisher / mix-up | 0.06 | until land | landing 0.30 | 55 / 88 | 0.10 | Fast but lands with real recovery (use the `diveland` frames) |
| tail grab / lift-slam | throws | 0.11 / 0.09 | 0.11 | 0.35 on whiff | 75 / 95 | 0.10 | Beat block; lose to everything else and to backing off |

### B2. Combo and cancel rules
1. **Hit-confirm cancels.** A cancel is allowed on hit or block from `cancelFrom`; a whiff adds 0.12 s of recovery before any new move. Spamming on a miss is punishable.
2. **Fixed chain graph.** m1 → m2 → m3; m1 or m2 → launcher; k1 → k2; launcher → air chain; any gada → thrust only on hit. Documented in the training room.
3. **Damage scaling.** 100% / 90% / 80% / 70% / 60% / 50% floor from the 6th hit onward.
4. **Juggle limit.** After a launcher, at most 3 more air hits (already near this), then forced knockdown. Optional total cap of 12 hits per combo.
5. **Interruption priority.** Normal < heavy < armored < unblockable. Same priority = clash (both stagger 0.15 s with a spark). Armor absorbs light hits (50% damage, one hit per armor charge); throws beat armor and block.

### B3. Defence
- **Guard meter ("Dharma guard").** Blocking drains guard by the incoming damage; chip stays low (about 10%); an empty guard causes a 0.8 s guard-break stun. Guard refills when not blocking. Charge smashes and grabs deal extra guard damage.
- **Parry.** Window 0.16 s but now fair because attacks are telegraphed (section B7). Success: boss recoil 1.0 s, a visible 0.7 s **counter window** (HUD ring + sound), the next attack deals +30% damage, +0.35 Bhakti. Parry on a projectile reflects it.
- **Perfect dodge.** Slow-mo 0.35 s, a 0.6 s counter window, +0.3 Bhakti. A roll away from a telegraphed attack should be a clear reward.
- **Auto-block change.** Holding back blocks only when the threat is within reach, not while any enemy attack is anywhere within 4.5 m.

### B4. Physics and movement
| Item | Proposal | Parameter |
|---|---|---|
| Walk | Keep analog stick; ease to speed in 0.08 s, stop in 0.06 s | `walkAccel`, `walkDecel` |
| Jump | 0.05 s anticipation (reuse the jump clip's first frame), variable height (release early cuts upward speed to 40%), 0.10 s apex hang (gravity × 0.6), fall gravity × 1.25 | `jumpWindup`, `jumpCutK`, `apexHang`, `fallGravityK` |
| Air control | 0.75 of ground control; no change of direction while attacking | `airControl` |
| Landing | 0.06 s recovery after a normal jump, 0.12 s after dive/meteor/heavy, with dust | `landRecovery` |
| Dash/roll | Velocity curve (fast start, glide), distance by tap vs double-tap | `rollSpeed`, `rollGlide` |
| Attack movement | Lunges already eased; ground friction applies only after the move, no sliding through the boss | existing `lungeSpeed` |
| Knockback tiers | light 1.2-2 m/s, heavy 3.5-6, launcher 15 vy, ultimate 9, scaled by size | per hit window |
| Fighter separation | Replace the per-frame position push with a velocity-based soft push, skip it in hit-stop and while crossing in the air | `pushStrength`, `pushDeadzone` |
| Size changes | Reach, hit radius, camera and knockback scale with `scale` (Giant/Shrink) | existing `scale` |

### B5. Game feel (restrained)
Impact tiers, applied automatically from the hit window (damage, `heavy`, `launch`, `knockdown`):

| Tier | When | Hit-stop | Shake | Camera | Flash | Sound | Haptic | Effect budget |
|---|---|---|---|---|---|---|---|---|
| 0 light | jabs, kicks | 0.04 | 0.02 | none | none | 3 variants | tick | up to 12 sprites |
| 1 medium | thrust, whirl, air | 0.07 | 0.06 | small kick | none | 3 variants | light | up to 18 |
| 2 heavy | m3, charge, sweep KD, launcher | 0.12 | 0.14 | kick + zoom pulse | brief | heavy x3 | medium | up to 28 |
| 3 ultimate | Panchmukhi, finale | 0.22 | 0.30 | cinematic | full | unique | long | up to 45, live cap 110 |

Rules: rapid-hit attenuation (already in), camera kick cooldown 0.15 s (already in), no zoom pulse below tier 2. Anticipation cues: charge glow for Hanuman, wind-up hold plus flash plus sound for the boss. Recovery shown by holding the recovery picture.

Readability HUD: a ring pulse on the Gada button when a cancel is open, guard bar under each health bar, an "OPEN" marker over the boss during recovery and after parry, telegraph colours (yellow = blockable, red = grab/unblockable, purple = projectile), a vulnerability tint on Hanuman while in recovery.

### B6. Hanuman's tactical identity
| Boon | Role | Tactical use | Risk / counter |
|---|---|---|---|
| Vayu | pressure / escape | Fast multi-hit dash with i-frames; dash-cancel into an attack | Ends in a short recovery if blocked |
| Flight | zone / air control | Air movement and meteor; hover limited (meter drains) | Slow turns, vulnerable to orbs and barrage |
| Fire Tail | armor break / damage over time | Fire whip that breaks guard and burns clones | Short reach, slow recovery |
| Shrink | evasion | 0.38 size avoids sweeps and orbs; rise uppercut | Low damage, fragile |
| Vajra | tank / trade | Armor and damage reduction, slower movement | Time-limited, no mobility |
| Giant | zone / stomp | Large reach and stomp; slow | Big target, slower dodge |
| Panchmukhi | ultimate | Puzzle-gated final blow | Cinematic, costs three bars |

### B7. Ahiravana AI
Replace weighted randomness with a small, readable brain made of four parts:
1. **Observer** (last 6 s): spam count per move, block ratio, jump ratio, distance bias, whiffs, how long Hanuman has been in recovery.
2. **Pattern library:** named sequences with tells, for example *Jab-Jab-Sweep* (mix low/high), *Feint-Grab* (punishes turtling), *Orb-Step-Overhead* (punishes backing away), *Anti-Air Orb* (punishes jumping), *Armored Sweep* (punishes spam).
3. **Reaction rules:** blocks only when a telegraphed move is on screen for at least 0.18-0.30 s; never blocks during his own recovery; blocks heavy hits less than light.
4. **Punish rules:** punishes a whiffed heavy within its recovery; counters the same move spammed 3 times; grabs a turtle after 2.5 s of continuous block.

Move timing changes (readability): damaging boss moves get a wind-up hold on their existing first picture so startup is at least 0.30 s for heavy/sweep/overhead and at least 0.20 s with a clear wind-up pose for jabs. Heavy moves get 0.35-0.55 s recovery windows and one-hit armor on orb, summon and gslam so they commit. Combo moves keep their speed only with a clear tell (flash + sound) before the string.

### B8. Phases (inside each round, by boss HP)
| Round | 100-60% | 60-30% | 30-0% | What is new |
|---|---|---|---|---|
| R1 duel | Jab, combo, sweep, overhead, grab with readable tells | Adds two-string combos and feint-grab | Aggression +15%, fewer idle gaps | Learn the vocabulary |
| R2 sorcery | Orb (reflectable), shadow step | Adds illusions (real one flashes) | Adds barrage, step-behind attack with smoke tell | Projectile and mix-up play |
| R3 titan | Slow wide swings, big recovery | Adds ground wave and stomp | Enrage: shorter recovery, wider sweeps, immortal until finale | Spacing and punishing recovery |
| Finale | Five lamps + puzzle gate | | | Unchanged |

Transitions: 0.8 s invulnerable roar, slow-mo and banner at each threshold; no HP refill. Each step adds a pattern or mechanic, not just numbers. Aggression and block chance rise only slightly (about +10% per step).

### B9. Difficulty and touch assists
Easy / Normal / Hard. Easy: damage taken ×0.7, parry window +0.06 s, boss telegraph +25%, longer boss recovery, optional auto-block assist. Hard: telegraph -10%, boss recovery -15%, no assist. Touch: larger hit slop on buttons, boon access without leaving the attack thumb (see F12).

---

## C. Roadmap

### Critical
1. Implement `MoveDef.armor` and attack priority/clash (it is set but never used).
2. Boss readability: telegraph holds, startup and recovery windows, remove the zero-delay omniscient block.
3. Distinct frame data per move (using `speedMul`, trims, cancel windows, hit-stop, knockback) and hit-confirm cancels.
4. Touch layout so boons and attacks are usable with one thumb each.
5. Defence reward loop: guard meter, parry and perfect-dodge counter windows with HUD feedback.

### High priority
6. Boss pattern brain (observer, patterns, punish rules).
7. HP-threshold phases and transitions.
8. Jump and landing feel; soft fighter separation.
9. Combo damage scaling and juggle limit.
10. Difficulty/assist options.
11. Impact tiers and distinct audio per tier.
12. HUD readability cues.

### Medium priority
13. Facing change delay and cross-up rules; wall behaviour.
14. Camera framing for jumps, giant form and phase transitions.
15. Training room frame-data overlay.

### Optional polish
16. Ground crack decals and screen-flash grading.
17. Extra afterimages and boon-specific trails (trails were removed on request; do not re-add without asking).
18. Replay/recap of the best combo.

---

## D. Technical implementation plan

Common rules: keep `Fighter` / `MoveDef` / `HitWindow` / `FightDirector`; defaults reproduce today's behaviour; new tunables live in one `CombatTuning` ScriptableObject (per-move overrides keyed by move id, boss pattern weights, phase thresholds, difficulty multipliers). If the asset is missing the code uses current values.

| # | Feature | Scripts / systems | New components | Configurable | Edge cases | Testing |
|---|---|---|---|---|---|---|
| F1 | Tuning data layer | `MoveLib`, `FightTypes`, `HanumanFighter.Init`, `AhiravanaFighter` | `CombatTuning` (ScriptableObject), loader | all per-move fields, phase thresholds, difficulty | missing keys, old saved values, domain-reload-off statics | MoveAudit table matches before/after with default asset |
| F2 | Armor, priority, clash | `Fighter.ReceiveHit`, `MoveDef` (`armorHits`, `priority`), `FightDirector.OnHit` | clash effect | armor hits, damage absorb %, clash stun | multi-hit moves count per window; throws ignore armor; armor must not stack with Vajra super armor | MoveAudit "trade" scenarios; boss summon interrupted vs not |
| F3 | Cancel rules and hit-confirm | `Fighter.StepMove`, `HanumanFighter.Resolve`, `HitWindow` flags | per-move `hitConnected/blocked` flags | whiff penalty, chain graph | buffered input during hit-stop; chain memory vs new rules | input-path scenarios: tap spam hit vs whiff |
| F4 | Guard, parry, dodge rewards | `Fighter.ReceiveHit`, `FightDirector.OnParry/OnDodge`, `FightHud` | guard meter, counter window state, ring UI | guard size, regen, parry window, counter bonus | parry vs projectile; counter window expiry; assist widens window | defence scenarios in MoveAudit; guard-break test |
| F5 | Jump and landing feel | `Fighter` (DoJump, Land, StepPhysics), `FightInput` (jump release) | landing lock timer | windup, cut factor, apex hang, fall K | jump + air attack stall interplay, ceiling, flight, giant scale | PhysicsDiag jump scenarios (height, air time) |
| F6 | Soft separation | `FightDirector.Separate`, `Fighter.StepPhysics` | per-fighter push velocity | strength, deadzone | grabs, knockdown, clones, arena wall | PhysicsDiag walk-into-boss and lunge-through scenarios |
| F7 | Impact tiers, audio and haptics | `FightDirector.OnHit`, `FightFx`, `FightCamera`, `FightSfx`, `Haptics` | tier enum derived from window | per-tier numbers | spam attenuation, effect cap, domain-reload-off pool | PhysicsDiag spam: camera and frame-time stats |
| F8 | Boss brain | `AhiravanaFighter.Think/Choose` | `BossObserver`, `BossPatterns` | pattern weights, reaction delay, punish thresholds | clones use a simple brain; dummy modes preserved; cooldowns | scripted bot runs; log pattern choices and responses |
| F9 | Phases and transitions | `FightDirector` round loop, `AhiravanaFighter.SetRound` | phase controller | thresholds, per-phase multipliers | R3 immortal flow, finale trigger, training/demo unaffected | scripted bot runs per phase; HP never refills |
| F10 | Difficulty and assists | `FightSetup`, `FightHud`/menu, `Fighter` (damage/window), boss | settings panel, PlayerPrefs | multipliers | persisted choice, training ignores | each level run with the bot; compare win time |
| F11 | HUD readability | `FightHud` | guard bar, OPEN marker, cancel ring, telegraph icons | colours, sizes | small screens, safe areas | screenshot checks at 16:9 and 20:9 |
| F12 | Touch layout | `FightHud.BuildControls`, `FightInput` | boon radial or long-press strip | button sizes, slop | simultaneous touches, accidental presses | thumb-reach checklist on a phone |
| F13 | Test harness upgrades | `MoveAudit`, `PhysicsDiag` | frame-data report, bot player | thresholds | stays out of release builds | run both and diff reports |

Performance rules: no per-frame allocations in new code; reuse the effect pool; cap live effects at 110; no new third-party packages.

---

## E. Asset requests (ordered by value)

**Hanuman (painted sheets, green-screen grids as before)**
1. Jump anticipation 2 frames and landing 3 frames.
2. Roundhouse 8, rising giant uppercut 8-16, tail-throw slam half 8.
3. Guard-break stagger 6 and parry counter flash pose 4.
4. Real Panchmukhi five-headed pose sheet.
5. (Already have, unused: a second whirlwind variant, sheet 39.)

**Ahiravana (whole-body pictures)**
6. Anticipation poses for jab, combo and sweep (a clear wind-up frame each), guard-break stagger 4, enrage pose 4.

**Effects (transparent sheets, 6-8 frames)**
7. Gold slash arc, heavy impact burst, spark burst, shockwave ring, ground-crack decal, parry flash, guard-break shatter, boss telegraph icons (yellow / red / purple).

**UI**
8. Guard bar art, "OPEN" marker, cancel ring for the Gada button, difficulty buttons.

**Audio**
9. Hit sounds x3 per tier (light/medium/heavy), parry clang, guard break, boss telegraph cue x3 types, whoosh variants, landing thud, phase-transition sting.

---

## F. Implementation prompts

Each prompt is independent. Give them one at a time; do not combine.

### Prompt 1 — Armor and attack priority
- **Expected behaviour:** `MoveDef.armor` becomes real. Moves with `armorHits = N` absorb N hits (taking 50% damage, no hit reaction) after an optional `armorStart` time. Add `priority` (normal < heavy < armored < unblockable). When two attacks land in the same frame the higher priority wins; equal priority clashes (both stagger 0.15 s, spark effect). Throws ignore armor and block.
- **Affected systems:** `Fighter.ReceiveHit`, `FightTypes`, `MoveLib` (set armor on charge2, finalblow, roar, summon, gslam, orb), `FightDirector.OnHit`.
- **Constraints:** keep `superArmor` (Vajra/Giant) working; defaults must reproduce today's behaviour for moves without armor; no new packages; no per-frame allocations.
- **Acceptance:** a jab during `summon` or `charge2` no longer interrupts it; the third jab does; Vajra still absorbs everything; throws still work; clash shows an effect and staggers both.
- **Testing:** new MoveAudit "trade" scenarios (jab into armored move, armored vs armored, throw vs armor); run the existing A/B/C/D audits and compare to the before report.

### Prompt 2 — Distinct move frame data and hit-confirm cancels
- **Expected behaviour:** apply the B1 table using `speedMul`, trimmed frames, cancel windows, hit-stop, knockback and damage. Cancels are allowed on hit or block from `cancelFrom`; a whiff adds 0.12 s recovery. Chain graph as in B2.
- **Affected:** `MoveLib` (`ApplyFrameArt`), `MoveDef`, `Fighter.StepMove/CheckHits`, `HanumanFighter.Resolve`.
- **Constraints:** hit windows must still line up with the painted frames; no new art; training-room move list unchanged; defaults overridable via `CombatTuning`.
- **Acceptance:** MoveAudit shows seven visibly different timing profiles (startup / active / recovery / hit-stop); whiff-spam is punishable (boss `jab` lands during the extra recovery); on-hit chains still flow.
- **Testing:** MoveAudit section A and B reports before/after; manual spam test on a phone; PhysicsDiag gada/kick spam still smooth.

### Prompt 3 — Guard meter, parry and perfect-dodge counter windows
- **Expected behaviour:** guard meter drains on block, refills when not blocking; empty guard = 0.8 s guard-break stun. Parry opens a 0.7 s counter window (+30% damage on the next hit) with a HUD ring; perfect dodge opens 0.6 s. Auto-block only when the threat is in reach.
- **Affected:** `Fighter.ReceiveHit`, `FightDirector.OnParry/OnDodge`, `FightHud`, `HanumanFighter.Threatened`.
- **Constraints:** boss uses the same guard rules; assist widens the window via `CombatTuning`; no new art required (use existing parry/stunned frames).
- **Acceptance:** blocking five heavy hits breaks the guard; a parry shows the window and the counter deals the bonus; not blocking regenerates guard.
- **Testing:** defence scenarios in MoveAudit (parry, block spam, dodge); verify no state gets stuck after guard break.

### Prompt 4 — Boss readability (telegraph, startup, recovery)
- **Expected behaviour:** hold the wind-up picture so heavy/sweep/overhead startup is at least 0.30 s and jabs at least 0.20 s; add a colour/flash/sound telegraph per attack class (yellow blockable, red grab/unblockable, purple projectile); heavy moves have 0.35-0.55 s recovery and one-hit armor.
- **Affected:** `MoveLib.Ahiravana`, `AhiravanaPoses` (hold times), `AhiravanaFighter`, `FightFx`, `FightSfx`.
- **Constraints:** use existing pictures only; clones keep faster simple moves; no change to damage.
- **Acceptance:** every damaging boss move has a visible and audible tell at least 0.2 s before the hit; MoveAudit shows startup/recovery per move; a player who only reacts (no prediction) can block or dodge heavy attacks.
- **Testing:** MoveAudit boss table; reaction test (parry a jab pressed at 0.2 s after the tell).

### Prompt 5 — Boss brain (observer, patterns, punish rules)
- **Expected behaviour:** replace weighted-random `Choose` with the B7 brain. Remove the instant omniscient block; add a reaction delay by attack class.
- **Affected:** `AhiravanaFighter.Think/Choose`; new `BossObserver`, `BossPatterns`.
- **Constraints:** keep Dummy modes and clones; no allocation per frame; patterns defined in data.
- **Acceptance:** logged pattern choices respond to spam, turtling and jumping within 3 repetitions; pure random picks are gone; the boss never blocks during its own recovery.
- **Testing:** scripted bot player (mashing, turtling, jump-spam) and a log of boss responses; compare against the old behaviour.

### Prompt 6 — Boss phases inside each round
- **Expected behaviour:** B8 thresholds with 0.8 s transitions and per-phase patterns; no HP refill; R3 immortal logic and the finale preserved.
- **Affected:** `FightDirector`, `AhiravanaFighter.SetRound`, `HUD` banner.
- **Constraints:** training and demo reel unaffected.
- **Acceptance:** crossing 60% and 30% triggers a transition and new patterns; finale still triggers at R3 end.
- **Testing:** scripted full playthrough; verify banners, patterns and finale trigger.

### Prompt 7 — Jump, landing and soft separation
- **Expected behaviour:** B4 jump anticipation, variable height, apex hang, fall gravity, landing recovery; velocity-based soft separation.
- **Affected:** `Fighter` (DoJump, Land, StepPhysics), `FightInput`, `FightDirector.Separate`.
- **Constraints:** air-attack limits and the height ceiling stay; flight unaffected; grabs and knockdown ignore separation.
- **Acceptance:** tap vs hold jump gives two heights; landing from a dive has recovery; walking into the boss and lunging through produces no jitter.
- **Testing:** PhysicsDiag jump and lunge scenarios (height, air time, position jitter).

### Prompt 8 — Impact tiers, audio and haptics mapping
- **Expected behaviour:** B5 tier table derived automatically from the hit window; per-tier hit-stop, shake, flash, sound and haptic; effect budgets enforced.
- **Affected:** `FightDirector.OnHit`, `FightFx`, `FightCamera`, `FightSfx`, `Haptics`.
- **Constraints:** no zoom pulse below tier 2; spam stays calm; pooling retained.
- **Acceptance:** PhysicsDiag spam shows camera RMS and frozen time at or below the current numbers; tiers are audibly and visibly different.
- **Testing:** PhysicsDiag spam scenarios; a phone feel check.

### Prompt 9 — HUD readability
- **Expected behaviour:** guard bars, OPEN marker over the boss, cancel ring on the Gada button, telegraph icons and colours.
- **Affected:** `FightHud`.
- **Constraints:** safe areas; no overlap with the stick zone; existing painted buttons reused.
- **Acceptance:** every item in "the player must always understand" is visible.
- **Testing:** screenshots at 16:9 and 20:9.

### Prompt 10 — Touch layout and boon access
- **Expected behaviour:** boons usable without leaving the attack thumb (radial from a long press on Ultimate, or a compact strip next to the stick); larger hit slop; no overlap with the joystick.
- **Affected:** `FightHud.BuildControls`, `FightInput`.
- **Constraints:** keyboard unchanged; multi-touch safe.
- **Acceptance:** a boon can be fired in under 0.4 s while holding a gada attack.
- **Testing:** thumb-reach checklist on a phone.

### Prompt 11 — Difficulty and assists
- **Expected behaviour:** Easy / Normal / Hard with the B9 multipliers, stored in PlayerPrefs, shown in the pause menu.
- **Affected:** `FightSetup`, `FightHud`, `Fighter`, boss.
- **Constraints:** training ignores it; Normal equals today's balance.
- **Acceptance:** Normal timings match the current audit; Easy/Hard change the listed values only.
- **Testing:** scripted bot runs per difficulty.

### Prompt 12 — Test harness upgrades
- **Expected behaviour:** MoveAudit prints a frame-data table (startup/active/recovery/advantage); PhysicsDiag adds jump and separation scenarios; a simple bot plays whole rounds and reports time-to-kill and damage taken.
- **Affected:** `MoveAudit`, `PhysicsDiag`.
- **Constraints:** editor and development builds only.
- **Acceptance:** reports run unattended and can be diffed between versions.
- **Testing:** run before and after each prompt above.

---

## Decisions needed before implementation
1. Guard meter yes/no (it changes blocking from free to limited).
2. How hard should Normal be? Today the boss is easy to out-trade because it cannot be reacted to and is interrupted by any hit.
3. Boon access on touch: radial from Ultimate, or a compact strip near the stick?
4. Which asset requests (section E) you can produce soon; the first four unlock the most.
