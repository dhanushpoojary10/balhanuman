# Level 2 "Patala Yuddha": Production Asset Prompts (v3)

This version follows the **Master System Prompt** (production-ready 2D asset generator).
It covers one arena and one duel: **adult Hanuman vs Ahiravana**. Characters animate on a **Unity 2D skeletal rig**; the fight code is already built and waiting for this art.

**This document is for IMAGE generation.** Audio has its own pipeline in **section 7**. Do not send section 7 to an image tool.

---

## 0. Read first

### 0.1 Animation approach (Master Prompt §10)
| What | Method | Why |
|---|---|---|
| Hanuman, Ahiravana, illusion clones | **Unity 2D skeletal rig**: you generate a model sheet, a master pose and separated **near-side and far-side rig parts**. I build the bones, meshes and weights, and the ~45 moves are already animated in code (`MoveLib.cs`). | 45+ moves × 12–32 frames is about 900 frames per character, which AI cannot keep consistent. A rig gives smooth blending, slow-motion, giant and shrink forms, and tail, cape and cloth physics. |
| Facial expressions | **Sprite-swap** face layers (same head, only the face changes) | They react to hits, attacks and taunts. |
| VFX, lamp flames, destruction | **Frame-by-frame sprite sheets** (Master §5) | Effects can't be rigged. |
| Arena, props, UI | Individual static PNGs | — |

> A rig-parts file is **not** a rigged character and a pose is **not** an animation. The parts become animations only after the rig in **section 1.5** is built and validated. Every animation in Master §8 maps to a rig clip in **1.9** and **2.9**.

### 0.2 Identity lock: ADULT HANUMAN ONLY
Every Hanuman asset here is **adult Lord Hanuman** ([HANUMAN] below). Never use the child Bal Hanuman, his top bun, his cute proportions or Level 1 art.
- The approved identity is **H-01** (model sheet) and **H-02** (master pose). Attach H-01 to every Hanuman prompt.
- Wherever the master prompt says "Bal Hanuman", read it as "adult Hanuman (H-01)" for this level.
- Extra negative prompt for every Hanuman asset: *child, kid, baby, chubby, cute toddler, top bun, small body*.

### 0.3 Transparency (Master §6)
Every standalone asset is a real **RGBA PNG** (or a transparent layer in a PSB) with background **alpha = 0**. No black, white, green, magenta or checkerboard backgrounds, and no frames or labels.
- **The tool supports transparency** (GPT-Image "transparent background", Leonardo transparency, Recraft): add *"transparent background, PNG with alpha channel"*.
- **The tool doesn't** (e.g. Midjourney). Use the extraction workflow:
  1. Generate on flat **#00FF00** (VFX: flat black).
  2. Remove it: Photoshop Remove Background + 1px defringe, remove.bg, or send me the raw file and I key it.
  3. **Verify:** an alpha channel exists, the corners are alpha 0, there's no green or dark fringe, and soft glows keep partial alpha.
  4. Never deliver the keyed-colour version.
- I alpha-check every file you drop in and return failures with the reason.

### 0.4 Style anchors (paste first in every prompt of that category)
Character art uses **four variants**. They share one look and differ only in camera and background, so the generator never has to choose between a style anchor and the prompt.

Shared look (part of all four):
*premium hand-painted semi-3D cartoon character, high-end animated feature quality, Indian mythological epic, painterly textures on a 3D-cartoon form, soft warm key light from the upper left, cool violet rim light from behind, rich saturated colours, polished gold and bronze jewellery with controlled highlights, detailed expressive face, clean readable silhouette.*

**[CHAR-STYLE-GAME]**: rig poses, rig parts, weapons, Panchmukhi guide and heads
> [shared look], orthographic side view, flat even game-asset camera with no perspective distortion, single character, transparent background, PNG with alpha channel.

**[CHAR-STYLE-SHEET]**: H-01 / A-01 model sheets only
> [shared look], character model sheet, the same single character shown in several views side by side in one row, orthographic views with no perspective, identical scale in every view, transparent background, PNG with alpha channel.

**[CHAR-STYLE-PORTRAIT]**: HUD portraits and VS poses
> [shared look], three-quarter view, slight dynamic camera, character only, transparent background, PNG with alpha channel.

**[CHAR-STYLE-SPLASH]**: full-screen cinematic stills only
> [shared look], cinematic illustration with dramatic perspective and a low heroic camera angle, full detailed environment background, atmospheric lighting, opaque full-frame image, 16:9.

**[ENV-STYLE]**
> Premium hand-painted 2D game environment, painterly anime-watercolour finish, ancient Indian temple architecture of the underworld Patala, deep indigo and violet stone, ember-orange lava light, gold and bronze details, soft atmospheric depth, strictly orthographic side view at the fighters' eye level, no top-down perspective, horizontal ground line, clean silhouettes.

**[VFX-STYLE]**
> Hand-painted stylised anime game VFX, bold readable shapes, bright saturated core fading to soft translucent edges, isolated effect only, transparent background, PNG with alpha channel, soft edges kept as partial transparency.

**[UI-STYLE]** (modern and compact, with an Indian identity)
> Modern minimal mobile game UI element with a subtle Indian mythological identity, clean frosted dark-glass surface, thin fine gold outline (2–3 px), small refined lotus or temple accent used sparingly, subtle soft gradient, gentle inner glow, flat clean shapes, no heavy bevels, no thick ornate filigree, compact, crisp edges, no text, no letters, transparent background, PNG with alpha channel.

### 0.5 Negative prompts (pick the set that matches the asset type)
**[NEG-BASE]** (every asset):
> text, letters, numbers, labels, watermark, signature, logo, decorative border, frame, mood board, photorealistic, blurry, low resolution, jpeg artifacts, extra limbs, extra fingers, deformed hands, inconsistent style, gore, blood

Then add **one** of these:

| Asset type | Add | Notes |
|---|---|---|
| **Standalone game assets**: rig poses, parts, weapons, heads, props, arena cut-out layers, UI, portraits, VS poses | `white background, black background, coloured background, gradient background, checkerboard, floor shadow, drop shadow, cropped, cut off, multiple characters, collage, presentation sheet, top-down view, isometric` | transparent output |
| **Model sheets** (H-01, A-01) | `white background, black background, coloured background, gradient background, checkerboard, floor shadow, cropped, different characters, inconsistent views, perspective distortion` | several views of **one** character are wanted, so "multiple characters" and "presentation sheet" are deliberately **not** banned |
| **Sprite sheets** (VFX, lamps) | `white background, black background, coloured background, checkerboard, unequal cells, overlapping frames, grid lines, frame numbers` | multiple frames are wanted |
| **Full-frame art**: splashes, `arena_far`, `vs_background`, `panel_victory_bg` | `transparent background, empty background, cropped character, cut off` | an opaque, detailed background is wanted |

### 0.6 Unity scale system (PPU, camera and screen checked together)
| Setting | Value |
|---|---|
| Fight camera | orthographic, size **3.6** normally (7.2 world units visible vertically), zooming out to **8.6** for giants and flight (17.2 units) |
| Target screens | 1920×1080 (16:9) to 2400×1080 (20:9) landscape |
| World units on screen | at size 3.6 on a 1080 px screen, **1 unit = 150 px** |
| Characters | PPU **440**. Hanuman is 1200 px tall in the art, which is **2.73 units** in game and about **410 px** on screen (about 38% of screen height). The art is about 3× the display size, so it stays sharp even at zoom-ins and on 1440p screens. |
| Ahiravana | 1300 px, which is **2.95 units** |
| Arena | PPU **100**; sizes in section 3 are chosen to fill the view at the widest zoom (17.2 × 38 units) |
| VFX | PPU **256** |
| UI | Image (UI) on a 1920×1080 reference canvas (match 0.5); PPU doesn't apply |

I verify scale in-engine after import by comparing the characters, floor and lamps side by side at both zoom levels before any asset is marked done.

**Sorting** (matches the code): Background -200 to -90 · Lamps -120 · Fighters 8–20 (sorting groups) · VFX 120+ · Foreground 200+.
**Facing:** Hanuman faces **right**, Ahiravana faces **left** (he can be generated facing right; I mirror him).
**Folders:** `Assets/ArtHQ/Level02/<Category>/`, with the exact file and layer names below.
**Spec line:** `size · layout · fps/loop · pivot · PPU · sorting · alpha`

### 0.7 Workflow per asset (Master §13)
1. Generate **one** asset. 2. Check it against its spec and 0.3. 3. Drop it in and tell me: I validate alpha, size, slicing and scale, import it, and tick the checklist (section 9). 4. Fix flagged items before moving on.

---

## 1. HANUMAN (adult) · `Assets/ArtHQ/Level02/Hanuman/`

**[HANUMAN]** (paste after [CHAR-STYLE-GAME])
> Adult Lord Hanuman (grown man, not a child), mighty divine vanara warrior, heroic muscular build, broad chest, narrow waist, powerful limbs, warm golden-orange fur-toned skin, lighter reddish-tan face and muzzle, noble heroic monkey-god face with a gently protruding muzzle, large expressive kind-but-fierce eyes, red vertical tilak on the forehead, tall golden mukuta crown with a red gem, black hair under the crown, golden ear ornaments, layered gold necklaces with a red-gem pendant, sacred thread across the chest, golden armbands, wrist cuffs and anklets, red-and-saffron silk dhoti with gold borders to the knee, gold belt, saffron sash with flowing ends, long thick monkey tail with a curled tip, barefoot.

### H-01 Model sheet (identity lock: first)
`3072×1536 · 4 views in one row · reference only · RGBA`
> [CHAR-STYLE-SHEET] [HANUMAN] Character model sheet: the same character four times in one row with equal spacing, front view, three-quarter view, side profile facing right, back view. Neutral A-pose, arms slightly away from the body, full body head to toe, identical proportions, costume and colours in every view, even lighting.

### H-02 Master rig pose (every part is cut from this)
`1536×1536 · single · pivot bottom-centre between the feet · PPU 440 · RGBA` · character **exactly 1200 px** from soles to the top of the head (crown extra).
> [CHAR-STYLE-GAME] [HANUMAN] Full body, orthographic side profile facing right, turned about 20 degrees toward the camera so the far arm and far leg are partly visible behind the body. Neutral rig pose: standing tall, feet a shoulder-width apart one slightly in front of the other, knees very slightly bent, both arms hanging slightly away from the torso with a clear gap, the far arm visible behind the torso, hands open and relaxed, elbows slightly bent, tail extended back and slightly up in a gentle curve clear of the legs, sash and dhoti hanging naturally, no weapon, calm confident expression, full body in frame, nothing cropped.

**Target proportions** (match these so the code's IK lengths fit; I re-measure your art and update the rig numbers, so close is fine). Measured from the soles, as a fraction of the 1200 px height:
ankle 0.03 · knee 0.25 · hip joint 0.47 · waist 0.50 · shoulder joint 0.76 · neck base 0.81 · top of head 1.00 · upper arm length 0.18 · forearm 0.17 · hand 0.06 · thigh 0.22 · shin 0.22 · foot length 0.12 · tail about 0.55 total.

### 1.3 Rig parts: near side AND far side (H-03 to H-20)
**Method: cut by hand first, then use AI only to fill hidden areas.**
1. Open H-02 in Photoshop, Photopea or Krita. **Select each part by hand** (lasso or pen) and copy it to its **own layer at its exact original position**. Include about 15% past each joint (a rounded "ball" end that hides the seam when the joint bends).
2. Where a part was hidden (the far limbs behind the body, the shoulder under the arm, the hip under the thigh), select only the empty area and use AI fill:
   > Complete the hidden part of the [PART], identical painting style, lighting, colours and anatomy, rounded joint end, nothing else.
3. **Far-side parts are drawn separately**, not copied from the near side. They are slightly narrower from the turn, sit further into the shadow side, and keep their own silhouette. Don't just darken them.
4. Name each layer exactly as below. Put H-02 at the bottom as a hidden reference layer named `_ref`.
5. Add a hidden layer **`_joints`**: a small red dot at the centre of every joint in the bone table (1.5). I use it to place the bones exactly.
6. Deliver **one file, `hanuman_rig.psb`**, 1536×1536, every part on its own transparent layer in its original position. Separate PNGs are an optional backup.

| Layer (file) | Part | Pivot = bone origin |
|---|---|---|
| `han_head` | head with crown, hair, ears, earrings and the face drawn in, mouth closed, short neck stump | neck base |
| `han_jaw` | closed lower jaw and chin of the muzzle (see the face/jaw rule in 1.7) | jaw hinge |
| `han_mouth_inner` | dark mouth interior with tongue and lower teeth, shown when the jaw opens | jaw hinge |
| `han_neck` | neck piece that overlaps both head and torso (hides the seam) | neck base |
| `han_torso` | chest and belly from neck base to waist, with necklaces and sacred thread, no arms | waist |
| `han_pelvis` | hips with dhoti waistband, gold belt and sash knot | hip line centre |
| `han_shoulder_near` / `han_shoulder_far` | rounded shoulder/deltoid caps that sit over the arm joint | shoulder joint |
| `han_upperarm_near` / `han_upperarm_far` | upper arm, shoulder to elbow, with armband | shoulder |
| `han_forearm_near` / `han_forearm_far` | forearm, elbow to wrist, with cuff | elbow |
| `han_hand_near_open` / `_fist` / `_grip` / `_palm` | near hand: open · fist · gripping an empty cylindrical handle (the mace slots in) · palm pushing forward | wrist |
| `han_hand_far_open` / `_fist` / `_grip` | far hand: open · fist · gripping a handle (for two-handed gada swings) | wrist |
| `han_hip_near` / `han_hip_far` | rounded hip caps of dhoti cloth that cover the top of each thigh | hip joint |
| `han_thigh_near` / `han_thigh_far` | thigh in dhoti silk, hip to knee | hip |
| `han_shin_near` / `han_shin_far` | bare lower leg with anklet | knee |
| `han_foot_near` / `han_foot_far` | bare foot, side view | ankle |
| `han_tail_1` … `han_tail_6` | tail in 6 segments, thick base to curled tip | top of each segment |
| `han_cloth_front` / `han_cloth_back` | hanging dhoti flaps | waistband |
| `han_sash_1` / `han_sash_2` | the two flowing sash ends | knot |
| `han_hair_1` / `han_hair_2` | loose hair strands under the crown | top |
| `han_necklace` | lower necklace layer that can swing | top |

### 1.5 Bone specification (I build this in the Unity Skinning Editor)
The main bone names match the code (`Puppet.cs`), so every authored move drives the real art. `Neck`, `Jaw`, the `PM_` head bones and the Titan part swap are new; I add them to the code when the PSB is imported.

```
Root (between the feet, on the ground)
└─ Hips                         ← han_pelvis, han_hip_near/far, han_cloth_front/back, han_sash_1/2
   ├─ Torso                     ← han_torso, han_necklace
   │  ├─ Neck → Head            ← han_neck, han_head, han_hair_1/2
   │  │        └─ Jaw           ← han_jaw
   │  ├─ ArmB (far shoulder)    ← han_shoulder_far, han_upperarm_far
   │  │  └─ ForeB → HandB       ← han_forearm_far → han_hand_far_*
   │  └─ ArmF (near shoulder)   ← han_shoulder_near, han_upperarm_near
   │     └─ ForeF → HandF       ← han_forearm_near → han_hand_near_*
   │                 └─ Weapon  ← gada (H-21), held in the grip
   ├─ ThighB → ShinB → FootB    ← far leg parts
   ├─ ThighF → ShinF → FootF    ← near leg parts
   └─ Tail0 → Tail1 → … → Tail5 ← han_tail_1 … han_tail_6
```

| Rule | Spec |
|---|---|
| Joint positions | from the `_joints` layer; bone lengths = distance between joint dots |
| Sprite meshes | auto-geometry (outline detail 6, alpha tolerance 10, subdivide 20); torso and belly get extra interior vertices |
| Weights | limbs, hands, feet and head: **100% to their own bone** (rigid parts, and the overlapping ball-ends hide the seams). Torso: blended between Hips and Torso across the waist. Tail segments: 70% own bone and 30% the next, for a smooth curve. Cloth flaps and sash: their own bones, procedural spring in code. |
| IK | legs: **2-bone IK in code** (feet planted while walking and fighting, knees bend forward). Arms: FK (authored angles). No Unity IK components are needed. |
| Sorting (back → front) | Tail · far arm (ArmB/ForeB/HandB) · far leg · cloth_back · pelvis · torso · neck · head · jaw · hip_near · near leg · cloth_front · sash · weapon · near arm (ArmF/ForeF) · near hand (above the weapon shaft) |
| Sprite swap | hand variants and expressions use a **Sprite Library** with categories `HandNear`, `HandFar`, `Face` |
| Validation (before I mark the rig done) | 1) every bone rotates ±120° with no gaps at the joints · 2) every move in 1.9 plays with no part popping, z-fighting or detaching · 3) feet stay on the floor in idle and walk · 4) the mace sits in the grip in every attack · 5) the tail curls smoothly through all 6 segments · 6) giant (×2.2) and shrink (×0.38) scales look correct · 7) both facings look correct |

### 1.6 Gada (approve the normal one first)
`1024×1024 · pivot at the centre of the red grip, 25% of the length from the pommel · head pointing RIGHT (Hanuman's facing) · PPU 440 · RGBA` · about 520 px long at H-02 scale.
1. `gada_normal.png`:
   > [CHAR-STYLE-GAME] The legendary golden gada of Lord Hanuman, large round ornate golden head with carved lotus patterns and a pointed finial, thick collar ring, long golden shaft with a red cloth grip, ornate pommel, lying horizontally with the head on the right.
2. **After approval**, attach `gada_normal.png` as the reference and generate the variants:
   - `gada_charged.png`: *"exactly this mace, unchanged shape, with the head glowing white-gold, cracks of light across the carvings, glow as partial transparency"*
   - `gada_fire.png`: *"exactly this mace, unchanged shape, with the head wrapped in swirling flames and embers as partial transparency"*

### 1.7 Expressions: face swaps on the identical head
**Face/jaw rule (applies to both characters):**
- The **face layer ends at the upper-lip line**. It contains the eyes, brows, forehead tilak, upper muzzle and upper teeth, and **never the lower jaw or chin**.
- The **lower jaw is always the separate `han_jaw` layer on the `Jaw` bone**. Mouths open by rotating that bone, never by painting an open mouth into the face.
- Behind the jaw sits **`han_mouth_inner`**: a dark mouth interior with tongue and lower teeth, pivot at the jaw hinge, on the `Head` bone. It is visible only when the jaw opens, so there is never a gap and never two jaws.
- Each expression = **face layer + jaw angle** (set by me in the Sprite Library and the animation):

| Expression | Jaw rotation (opening downward) |
|---|---|
| focus, smirk, serene | 0° (closed) |
| hurt | 6° (gritted) |
| shout | 18° |
| roar | 26° |

`han_jaw` (closed lower jaw and chin, pivot at the hinge) and `han_mouth_inner` (pivot at the hinge) are both layers in `hanuman_rig.psb`.

Don't regenerate the head. **Duplicate the `han_head` layer** and use AI fill on **only the face area** (eyes, brows, muzzle, mouth). The crown, hair, ears, neck stump, jaw hinge and outline must stay pixel-identical. Name each layer `han_face_<expr>` inside the same PSB.
> Repaint only the face down to the upper-lip line with the expression: [EXPRESSION]; the mouth area below the upper lip stays closed and unchanged (the jaw opens separately); keep everything outside the selection identical.

| Layer | [EXPRESSION] | Used for |
|---|---|---|
| `han_face_focus` | calm, focused, slightly narrowed eyes | idle / fighting |
| `han_face_shout` | fierce battle cry: eyes wide, brows down, upper lip raised showing the upper teeth | attacks (jaw 18°) |
| `han_face_hurt` | wincing: eyes squeezed shut, upper teeth clenched | hit reactions (jaw 6°) |
| `han_face_smirk` | confident smirk, one brow raised | parry, perfect dodge |
| `han_face_roar` | furious roar: eyes glowing gold, brows hard down, upper lip curled showing the upper teeth | boons, ultimate (jaw 26°) |
| `han_face_serene` | serene, determined, soft divine glow | victory, Vajra |

### 1.8 Panchmukhi form: attachment and transformation
**Head arrangement.** Generate the arrangement guide first:
`pm_arrangement_ref.png` · 1536×1536 · reference only
> [CHAR-STYLE-GAME] [HANUMAN] Same pose and side view as the reference (H-02 attached), upper body only, in the Panchmukhi form. Hanuman's own head stays exactly where it is, frontmost and fully visible. Four divine heads, all facing right in the same side view and the same size as his head, rise on short necks from behind his neck and upper back in a fan-shaped arc behind and above his head: Garuda (eagle) furthest back at head height, Varaha (boar) back and higher, Hayagriva (horse) at the top centre, Narasimha (lion) in front and higher. Each head is partly hidden by the head in front of it; none of them covers Hanuman's face. A golden collar ring covers where the necks join.

**Layering and placement (back → front).** H = Hanuman's head height; offsets are from the centre of his head, x toward the facing direction:

| Order | Layer | Position | Overlap |
|---|---|---|---|
| 1 (back) | `pm_halo_ring` / aura | centred behind all heads | behind everything |
| 2 | `pm_garuda` | x −0.75H, y +0.10H | about 30% hidden by Varaha and by Hanuman's hair and crown back |
| 3 | `pm_varaha` | x −0.45H, y +0.70H | about 25% hidden by Hayagriva |
| 4 | `pm_hayagriva` | x +0.05H, y +1.00H | about 20% hidden by Narasimha; its neck is hidden by Hanuman's crown |
| 5 | `pm_narasimha` | x +0.50H, y +0.70H | in front of Hayagriva, **behind** Hanuman's crown tip, clear of his face |
| 6 | `pm_neck_ring` | wraps the base of Hanuman's neck | covers every extra neck stump; in front of the extra necks, behind Hanuman's head and jaw |
| 7 (front) | `han_head`, face, jaw | unchanged | always frontmost; **no extra head may overlap Hanuman's eyes or muzzle** |

All the extra necks converge behind Hanuman's neck base, so only `pm_neck_ring` shows where they meet.

Then cut **each extra head as its own layer** in `hanuman_rig.psb` (same method as 1.3), at its exact position from the guide:

| Layer | Deity | Same size as | Bone (child of `Neck`) |
|---|---|---|---|
| `pm_narasimha` | lion face, fiery golden mane, roaring with fangs | `han_head` ±5% | `PM_Narasimha` (sorting 5) |
| `pm_garuda` | eagle face, curved golden beak, red-and-gold crest | `han_head` ±5% | `PM_Garuda` (sorting 2) |
| `pm_varaha` | boar face, white tusks, dark blue-grey skin | `han_head` ±5% | `PM_Varaha` (sorting 3) |
| `pm_hayagriva` | horse face, flowing white mane | `han_head` ±5% | `PM_Hayagriva` (sorting 4) |
| `pm_neck_ring` | a golden collar/aura ring that covers where all five necks meet | — | `Neck` (sorting 6) |

Each head includes its own short neck stump that tucks under `pm_neck_ring`, matches the crown style of Hanuman's mukuta, and uses the same lighting.
**In game:** the heads scale up from 0 one by one on their bones (already coded), with these overlays:

| File | Spec | Prompt (after [VFX-STYLE]) |
|---|---|---|
| `pm_head_reveal.png` | 10 frames · 10×1 · cell 512 · 5120×512 · 24 fps · non-loop | a burst of golden light and lotus petals blooming open from a point, used as each head appears |
| `pm_aura_loop.png` | 12 frames · 6×2 · cell 512 · 3072×1024 · 20 fps · loop · bottom-centre | radiant golden-saffron aura with five flame tongues rising around an empty upper-body area |
| `pm_halo_ring.png` | single · 1024×1024 | large golden circular halo with lotus petals and rings behind the five heads, no script |

The ten-armed form appears **only in the splash still** (H-25). In gameplay he keeps two arms.

### 1.9 Hanuman animation coverage (Master §8), built from the rig
| Required | Rig clip / move id (authored in code) |
|---|---|
| Idle (breathing, blinking, tail, cloth) | `idle` + procedural blink, tail and cloth springs |
| Walk / run | `walkf`, `walkb` (a run isn't used in a one-arena duel) |
| Jump / fall | `jump`, `fall` |
| Roll | `dodgef`, `dodgeb` |
| Light attacks / combos | `m1 → m2 → m3`, `k1 → k2`, `air1–3` |
| Heavy / weapon attacks | `thrust`, `whirl`, `launcher`, `sweep`, `charge1/2`, `dive`, `meteor` |
| Special attacks | `vayudash`, `firewhip`, `rise`, `stomp`, `finalblow`, `tailgrab`, `bodyslam` |
| Hit / knockback / recovery | `hit`, `hitlow`, `launched`, `down`, `getup`, `stunned` |
| Death / victory | `down` (KO) / `victory`, `roar` |

### 1.10 Portraits and stills
- **Portraits** `512×512 · UI Image · RGBA`: > [CHAR-STYLE-PORTRAIT] [HANUMAN] Head-and-shoulders portrait, three-quarter view facing right, centred, [STATE], rim light. Files: `han_portrait` (determined) · `_hurt` (bruised, gritting teeth) · `_power` (eyes glowing gold).
- **H-25 `splash_panchmukhi.png`** `2560×1440 · opaque full frame`: > [CHAR-STYLE-SPLASH] [HANUMAN] Epic splash art: Hanuman in the Panchmukhi form, five heads in a crown-like arc (Hanuman centre, Narasimha, Garuda, Varaha, Hayagriva), ten arms holding the gada, trident, sword, lotus and discus, radiant golden halo, dark underworld temple, five sacred lamps being blown out by divine wind, low heroic angle, 16:9.
- **`splash_victory.png`** `2560×1440 · opaque`: > [CHAR-STYLE-SPLASH] [HANUMAN] Hanuman standing victorious on a cracked temple floor, gada on his shoulder, other hand raised in blessing, tail curled, golden light streaming through the dark cavern, embers and marigold petals, 16:9.
- **`vs_hanuman.png`** `1536×2048 · RGBA`: > [CHAR-STYLE-PORTRAIT] [HANUMAN] Full body dynamic fighting stance facing right, gada in both hands ready to strike, knees bent, tail raised, wind in the sash.

---

## 2. AHIRAVANA · `Assets/ArtHQ/Level02/Ahiravana/`

**[AHIRAVANA]**
> Ahiravana, sorcerer king of Patala, tall imposing rakshasa demon lord, lean powerful build, deep blue-violet skin with glowing purple tattoo markings on the arms and chest, sharp angular face, small curved horns, long wild black hair, glowing violet eyes, small fangs, cruel smug smirk, tall black-and-gold spiked crown with a purple gem, heavy gold-and-obsidian shoulder armour with skull motifs, dark purple silk robe, long tattered cape with gold trim, black-and-gold armoured belt, gold arm cuffs, clawed hands with dark nails, menacing but stylised, no gore.

### A-01 Model sheet · A-02 Master rig pose
- A-01 `3072×1536 · reference only`: > [CHAR-STYLE-SHEET] [AHIRAVANA] Character model sheet: the same character four times in one row with equal spacing, front, three-quarter, side profile facing left, back. Neutral A-pose, full body, identical costume and proportions, even lighting.
- A-02 `1536×1536 · pivot bottom-centre · PPU 440 · RGBA` · **exactly 1300 px** soles to the top of the head (crown extra): > [CHAR-STYLE-GAME] [AHIRAVANA] Full body, orthographic side profile facing left, turned about 20 degrees toward the camera so the far arm and leg are partly visible. Neutral rig pose: standing tall, feet apart one slightly in front, arms slightly away from the body with a gap, clawed hands open, cape hanging straight behind and not covering the arms or legs, no weapon, smug expression, nothing cropped.
- Target proportions: same fractions as Hanuman (1.2), except shoulder 0.78 and upper arm 0.19.

### 2.3 Rig parts: near and far side → `ahiravana_rig.psb`
Same cut-then-fill method, `_ref` and `_joints` layers, and delivery rules as 1.3.

| Layer | Part | Pivot |
|---|---|---|
| `ahi_head` / `ahi_jaw` / `ahi_neck` | head with spiked crown, horns and hair · fanged jaw · neck | neck base / hinge / neck base |
| `ahi_torso` | chest and belly with glowing tattoos, no arms | waist |
| `ahi_pelvis` | hips with armoured belt | hip line |
| `ahi_pauldron_near` / `_far` | skull shoulder armour pieces | shoulder |
| `ahi_upperarm_near` / `_far` · `ahi_forearm_near` / `_far` | arm segments with gold cuffs | shoulder / elbow |
| `ahi_hand_near_claw` / `_fist` / `_grip` / `_cast` | near hand: open claw · fist · gripping a handle · casting with a violet glow in the palm | wrist |
| `ahi_hand_far_claw` / `_grip` / `_cast` | far hand variants (grabs and casting use the far hand) | wrist |
| `ahi_hip_near` / `_far` | robe hip caps over the thighs | hip |
| `ahi_thigh_near` / `_far` · `ahi_shin_near` / `_far` · `ahi_foot_near` / `_far` | leg segments | hip / knee / ankle |
| `ahi_cape_1` … `ahi_cape_4` | cape in 4 vertical segments, shoulder to tattered hem | top |
| `ahi_robe_front` / `ahi_robe_back` | hanging robe panels | waist |
| `ahi_hair_1` / `ahi_hair_2` | loose hair locks | top |

**Bones:** identical hierarchy to 1.5, except the tail is replaced by `Cape0 → Cape1 → Cape2 → Cape3` (child of `Torso`, at the back of the shoulders), and `Weapon` (trident) sits in `HandF`. Cape weights: 70% own bone and 30% the next, plus a procedural spring. Sorting: cape behind everything, then the far arm, far leg, robe_back, pelvis, torso, head, near leg, robe_front, weapon, near arm. Same validation list as 1.5.

### 2.4 Trident · held orb
- `ahira_trident.png` `1024×1024 · PPU 440` · **orientation: horizontal, prongs pointing LEFT** (the way Ahiravana faces, toward Hanuman) · **pivot = centre of the hand grip, 35% of the length from the butt end**, so about 65% of the weapon extends forward for thrusts and sweeps: > [CHAR-STYLE-GAME] Dark sorcerer's trident lying horizontally with the three prongs pointing left, three curved obsidian prongs edged in gold, glowing purple gem at the prong base, small skull ornament, long black-and-gold shaft wrapped in violet cloth, with a clearly marked grip wrap about one third of the way from the right (butt) end.
- For the same reason, **the gada (1.6) points its head RIGHT**, the way Hanuman faces; its pivot is the centre of the red grip, 25% of the length from the pommel.
- `ahira_orb_held.png` `512×512 · centre`: > [VFX-STYLE] Sphere of dark sorcery, swirling violet and black energy, crackling magenta lightning, bright core.

### 2.5 Expressions (face swaps on the identical head, same method and face/jaw rule as 1.7)
`ahi_jaw` (closed fanged lower jaw) and `ahi_mouth_inner` are layers in `ahiravana_rig.psb`.
Layers (jaw angle): `ahi_face_smug` (0°) · `_laugh` (eyes squeezed with glee, upper fangs showing; jaw 22°, with the head tilt done by animation) · `_shocked` (wide eyes; jaw 12°) · `_furious` (snarl, blazing eyes, upper fangs bared: the **enrage** face; jaw 16°) · `_desperate` (fearful eyes; jaw 8°) · `_defeated` (dazed, half-closed eyes; jaw 4°).

### 2.6 TITAN FORM (phase 3): design decision
**Decision:** Titan Ahiravana **is physically larger and changes proportions, not only appearance.**

| Aspect | Spec |
|---|---|
| Size | the whole rig scales **×2.1** (about 6.2 units tall). The camera already zooms out for it. |
| Proportions | a **separate Titan part set on its own Titan skeleton** (see Skeleton): bulkier torso, bigger shoulders, thicker and longer forearms, larger clawed hands, heavier legs, horns, monster jaw. Because bone names and hierarchy match, every move still works. |
| Skeleton | **a dedicated Titan skeleton**: same bone **names and hierarchy** as the normal rig, but its own **rest pose** and bone lengths, taken from the `_joints` layer of the Titan master (A-T0). Changing sprites alone does not change bone lengths, so the parts are never just swapped onto the old bones. |
| How it switches | at the transformation flash (V-36) the normal rig object is hidden and the Titan rig object is shown at the same root position, already scaled ×2.1. The code drives bones **by name with local rotations**, so every move plays on the Titan skeleton unchanged. Leg IK uses the Titan's own measured bone lengths. |
| Proportion targets | relative to the normal rig: upper arm about +10%, forearm about +15%, hands about +30% (targets for the A-T0 art; the real lengths come from its `_joints`) |
| Reach / range | all hitboxes scale with size; forearm length adds about 15% extra reach |
| Moves | keeps `jab`, `grab`, `orb` (scaled) and adds Titan-only **`gslam`** (trident slam that sends a ground shockwave) and **`gsweep`** (wide low sweep). Both are telegraphed. |
| Transformation | `transform` clip: roar, then a ×1.0 → ×2.1 scale over 1.2 s, with the purple pillar VFX, the rig switch hidden by a VFX flash at 0.5 s, camera shake and zoom out |

**Titan part set → `ahiravana_titan.psb`** (1536×1536, cut from a Titan master pose):
- `A-T0 ahira_titan_master.png` (generate first, with A-02 attached as the reference): > [CHAR-STYLE-GAME] [AHIRAVANA] The same character transformed into a colossal demon titan, **identical pose, camera angle and facing as the reference**, much bulkier torso and shoulders, thicker longer forearms, huge clawed hands, heavier legs, large curved ram horns, monstrous widened fanged jaw, cracked skin with glowing purple magma veins, burning violet eyes, wisps of dark aura, same costume enlarged and torn, drawn at the same 1300 px height (the game scales it up).
- Cut layers with the **same names as 2.3** but prefixed `titan_` (e.g. `titan_ahi_torso`, `titan_ahi_forearm_near`), plus `titan_ahi_horns` and `titan_ahi_spikes` (back spikes). Add a **`_joints` layer marking every Titan joint**; it defines the Titan skeleton's rest pose.

### 2.7 Portraits and VS
- `ahira_portrait_calm` / `_angry` / `_broken` / `_titan` `512×512`: > [CHAR-STYLE-PORTRAIT] [AHIRAVANA] Head-and-shoulders portrait, three-quarter view facing left, centred, [cruel smirk / furious, glowing eyes / cracked crown, desperate / titan horns and magma veins], purple rim light.
- `vs_ahiravana.png` `1536×2048 · RGBA`: > [CHAR-STYLE-PORTRAIT] [AHIRAVANA] Full body menacing stance facing left, trident across the body, other hand raised with a glowing purple orb, cape billowing, evil grin.

### 2.9 Boss animation coverage (built from the rig)
| Required | Rig clip / move id |
|---|---|
| Idle, walk, turning | `idle`, `walkf`, `walkb`; turning = facing flip with a cross-fade |
| Attack anticipation | the windup key in every move + a red telegraph flash on `overhead`, `grab`, `gslam`, `gsweep` |
| Basic / heavy attacks | `jab`, `combo`, `sweep`, `overhead`, `grab → darkthrow` |
| Projectile / summon / teleport | `orb`, `barrage`, `summon`, `vanish` (+ VFX in section 5) |
| Damage / stagger | `hit`, `hitlow`, `launched`, `stunned` |
| Phase transition / enrage | `laugh` (R2), `transform` + Titan part swap (R3), `ahi_face_furious` + `dark_aura` |
| Defeat / retreat / victory | `kneel` (finale), `down` / `laugh` |

Illusion clones reuse the normal rig with a tint, so no extra art is needed.

---

## 3. ARENA · `Assets/ArtHQ/Level02/Arena/`

### 3.0 One camera for the whole arena
- **Strictly orthographic side view at the fighters' chest height.** The horizon line is at about 60% of the image height in every layer, and there is no top-down angle anywhere.
- The floor is drawn as a **flat side-on band**: a thin visible top edge (max 40 px) and the front face below it.
- **Depth bands**, from back to front:

| Band | Layers | Contrast / colour |
|---|---|---|
| Far | E-01 | low contrast, cool, hazy |
| Back | E-02 temple, E-03 lamps | medium contrast, desaturated violet |
| **Fighter plane** | floor top edge, characters | highest contrast and saturation (reserved for fighters and VFX) |
| Foreground | E-05 | near-black silhouettes, only at the left and right edges |

- **Keep the middle clear:** in E-02 the central 40% of the width, below 55% of the height, must be **dark and low-detail** (deep shadow, the throne pushed far back and small). Nothing bright or busy sits behind the fighters' bodies. Pillars stand **at least 600 px apart** and are darker than the fighters.

### 3.1 Layers
| ID | File | Spec | Prompt (after [ENV-STYLE]) |
|---|---|---|---|
| E-01 | `arena_far.png` | **4096×2048** · opaque · pivot centre · PPU 100 (41×20.5 units: fills the widest zoom) · -200 · **seamless left-right, tested** | Vast underground cavern of Patala, dark rock ceiling with hanging stalactites, distant lava rivers and violet fire far below, giant demon statues carved into faraway cliffs, purple mist, faint glowing runes, very distant and hazy, no foreground objects. |
| E-02 | `arena_temple.png` | **4096×1536** · **RGBA: fully transparent above the roofline and the stalactite gaps** · pivot bottom-centre · PPU 100 · -150 | Ancient temple of the goddess Mahamaya seen straight from the side: a symmetrical colonnade of dark carved stone pillars with gold bands, spaced widely; a small distant throne on high steps far in the back centre, deep in shadow; guardian statues at the far left and right; torn purple banners; braziers with violet flames only near the sides; the central area behind the fighters dark and calm; transparent sky above the roof. |
| E-03 | `lamp_stand.png` | 512×1024 · RGBA · bottom-centre · PPU 100 · -120 | Tall ornate bronze-and-gold diya lamp stand, tiered, wide oil bowl on top, serpents coiling round the stem, purple gem at the base, **no flame**, pure side view. |
| E-04 | `floor_intact.png` | **4096×512** · RGBA · pivot **top-centre = walk line** · PPU 100 · -100 · **seamless left-right, tested** | Temple arena floor in pure side view: a thin bright top edge of polished black stone with gold inlay, then the front face of large carved stone blocks with mandala reliefs, worn edges, faint purple energy cracks, no top-down perspective. |
| E-05 | `fg_pillar.png` | 1024×2160 · RGBA · bottom-centre · PPU 100 · Foreground 200 | Massive dark carved stone pillar very close to the camera, near-black silhouette with a thin ember rim light on one edge, iron chains hanging, slight depth blur. |
| E-06 | `fog_strip.png` | 4096×512 · RGBA soft alpha · PPU 100 · -90 · **seamless, tested** | Soft wispy horizontal band of low violet mist, translucent. |
| E-07 | `lightshaft.png` | 512×2048 · RGBA · PPU 100 · -95 | A single soft diagonal golden light shaft with floating dust motes, translucent. |
| E-08 | `altar_captives.png` *(optional)* | 1536×1024 · RGBA · bottom-centre · PPU 100 · -130 | Dark sacrificial altar with gold chains and a glowing violet circle, Rama and Lakshmana resting unconscious beside it in soft golden light, peaceful, side view. |

**Seamless test (required, never assumed):** in Photoshop use Filter → Other → Offset by half the width (wrap around). Repair the seam that appears in the middle with the healing brush or AI fill, then offset back. Or send it to me and I tile it 3× and check the seam before import.

### 3.2 Floor destruction: identical framing
Generate `floor_intact` first. Make the others by **duplicating it and inpainting only the damage**, so the size, top edge and ground line stay pixel-identical.
- `floor_cracked.png`: > Add heavy cracks spreading from the centre across the existing floor; keep everything else identical.
- `floor_shattered.png`: > Smash the centre of the existing floor into a crater with broken lifted blocks and lava glowing in the cracks; keep the top-edge height and everything outside the damage identical.

### 3.3 Destructible pillar: 5 states, identical framing (Master §9)
`1024×2048 each · RGBA · bottom-centre · PPU 100 · -110`. Generate `pillar_intact`, then **duplicate and inpaint** each next state from the previous one.
| File | State |
|---|---|
| `pillar_intact` | tall carved black-stone pillar, gold rings, demon-face carving, pure side view |
| `pillar_damaged` | chips and first cracks |
| `pillar_cracked` | deep cracks across the shaft, glowing faintly inside |
| `pillar_breaking` | shaft split, the top half tilting, fragments separating |
| `pillar_destroyed` | only the broken stump |
| `pillar_debris_1` … `_8` | 8 separate chunks, `256×256`, pivot centre |

*Gameplay:* a giant hit or a thrown fighter advances it a state. A BoxCollider2D trigger sits on the shaft (not drawn). Debris pieces are thrown by code.

### 3.4 The five sacred lamps
| File | Spec | Prompt (after [VFX-STYLE]) |
|---|---|---|
| `lamp_flame_loop.png` | 8 · 8×1 · cell 256×512 · 2048×512 · 12 fps loop · bottom-centre · PPU 256 · -117 | sprite sheet of 8 sequential frames of one looping magical lamp flame, violet with a white-pink core and orange tips, the same base point in every cell, the last frame flowing into the first |
| `lamp_flame_flare.png` | 8 · same layout · 12 fps loop | the same flame twice as tall and violent |
| `lamp_extinguish.png` | 10 · 10×1 · 256×512 · 2560×512 · 20 fps non-loop | the same flame bending in a divine wind, shrinking and snuffing into a curl of white-violet smoke that fades out |

---

## 4. WHAT EVERY SPRITE SHEET MUST PASS (VFX and lamps)
After generation, **I measure and verify** (and so should you, if you can):
1. The canvas is exactly `cols × cell` by `rows × cell` px.
2. Each frame sits inside its cell (nothing crossing a border; check by overlaying a grid).
3. The anchor point is the same in every cell (centre, or bottom-centre for ground effects).
4. Frames are in time order with no duplicates or skipped steps.
5. Real alpha with soft edges (0.3).
6. A looping sheet's last frame flows into its first (play it at the stated fps).

If a sheet fails, regenerate it, or generate it as **2 batches of 8** with the last frame of batch 1 attached ("continue this exact effect from this frame").

## 5. VFX · `Assets/ArtHQ/Level02/VFX/`
Add to every sheet prompt: *"sprite sheet, [N] sequential animation frames in a [C]×[R] grid, every cell identical size, effect centred on the same point in every cell, nothing crossing cell borders, transparent background."*
All VFX: **PPU 256 · VFX 120+ · pivot centre (ground effects bottom-centre)**.

| ID | File | Frames · layout · cell · canvas | fps · loop | Prompt (after [VFX-STYLE]) |
|---|---|---|---|---|
| V-01 | `hit_light.png` | 8 · 8×1 · 256 · 2048×256 | 30 · no | quick white-yellow star-shaped impact spark with tiny streaks, appearing then fading |
| V-02 | `hit_heavy.png` | 12 · 6×2 · 512 · 3072×1024 | 30 · no | powerful orange-gold impact: bright flash, expanding shockwave ring, flying sparks, dissipating smoke |
| V-03 | `parry_clang.png` | 8 · 8×1 · 512 · 4096×512 | 30 · no | bright cyan-white metallic clash, cross-shaped flare with sharp shards |
| V-04 | `gada_trail.png` | single · 1024×256 | — | horizontal swoosh strip, white-gold leading edge on the right fading to translucent orange on the left |
| V-05 | `gada_swing_arc.png` | 6 · 6×1 · 512 · 3072×512 | 30 · no | crescent slash arc of golden energy sweeping and dissolving |
| V-06 | `gada_charge.png` | 12 · 6×2 · 256 · 1536×512 | 20 · loop | golden particles spiralling inward to a point, intensifying |
| V-07 | `shockwave_ground.png` | 10 · 5×2 · 512×256 · 2560×512 · bottom-centre | 24 · no | flat ground shockwave racing outward with dust, pure side view |
| V-08 | `ground_crack.png` | 6 · 6×1 · 512×256 · 3072×256 · bottom-centre | 20 · no | cracks spreading across stone in side view, glowing gold-orange, then dimming |
| V-09 | `dust_puff.png` | 8 · 8×1 · 256 · 2048×256 · bottom-centre | 20 · no | soft brown-grey dust puff expanding and fading |
| V-10 | `dust_land.png` | 8 · 8×1 · 512×256 · 4096×256 · bottom-centre | 24 · no | wide flat dust burst spreading left and right |
| V-11 | `debris_1..8.png` | 8 files · 128×128 | — | individual small dark stone chunks with broken edges |
| V-12 | `dash_streaks.png` | 6 · 6×1 · 512×256 · 3072×256 | 30 · no | white-cyan speed streaks shooting backward and thinning |
| V-13 | `wind_swirl.png` | 12 · 6×2 · 256 · 1536×512 | 20 · loop | pale cyan wind spiral with leaf-like streaks |
| V-14 | `fire_loop.png` | 12 · 6×2 · 256 · 1536×512 · bottom-centre | 20 · loop | stylised flame, orange-red outer, yellow-white core, licking upward |
| V-15 | `fire_whip_arc.png` | 8 · 8×1 · 512 · 4096×512 | 30 · no | crescent arc of fire slashing across and breaking into embers |
| V-16 | `fire_burst.png` | 12 · 6×2 · 512 · 3072×1024 | 24 · no | fiery explosion with embers flying outward |
| V-17 | `ember.png` | single · 64×64 | — | one soft glowing orange ember |
| V-18 | `growth_pillar.png` | 12 · 12×1 · 256×1024 · 3072×1024 · bottom-centre | 24 · no | massive pillar of golden light erupting upward, ring at the base, rising sparkles |
| V-19 | `shrink_sparkle.png` | 8 · 8×1 · 256 · 2048×256 | 24 · no | cyan-white sparkles collapsing into a tiny point |
| V-20 | `vajra_aura.png` | 12 · 6×2 · 512 · 3072×1024 · bottom-centre | 20 · loop | golden flame aura around an **empty** human-shaped area, small white lightning crackles |
| V-21 | `vajra_lightning.png` | 6 · 6×1 · 512 · 3072×512 | 30 · no | white-gold branching lightning striking and fading |
| V-22 | `panchmukhi_burst.png` | 16 · 4×4 · 512 · 2048×2048 | 24 · no | huge radiant burst of golden-saffron light with a mandala halo expanding and light rays |
| V-23 | `halo_mandala.png` | single · 1024×1024 | — | golden circular mandala halo with lotus petals and rings, no script |
| V-24 | `dark_orb_fly.png` | 8 · 8×1 · 256 · 2048×256 | 20 · loop | flying violet-black orb trailing purple wisps |
| V-25 | `dark_explode.png` | 12 · 6×2 · 512 · 3072×1024 | 24 · no | dark violet magic explosion, purple-black smoke, magenta lightning |
| V-26 | `teleport_out.png` / `teleport_in.png` | 12 · 6×2 · 256×512 · 1536×1024 · bottom-centre | 24 · no | purple smoke and violet sparks swirling inward (out) / bursting outward (in) |
| V-27 | `clone_poof.png` | 8 · 8×1 · 256 · 2048×256 | 24 · no | purple smoke puff dissolving |
| V-28 | `dark_aura.png` | 12 · 6×2 · 512 · 3072×1024 · bottom-centre | 20 · loop | dark violet flame aura around an **empty** silhouette area (enrage / titan) |
| V-29 | `summon_circle.png` | 12 · 6×2 · 512×256 · 3072×512 · bottom-centre | 20 · loop | glowing violet occult circle on the floor, **seen edge-on as a flat ellipse** (side view), runes turning, no script |
| V-30 | `ground_wave.png` | 8 · 8×1 · 512×256 · 4096×256 · bottom-centre | 20 · loop | low travelling wave of violet energy and rock shards along the ground |
| V-31 | `ko_burst.png` | 12 · 6×2 · 512 · 3072×1024 | 24 · no | dramatic K.O. impact: white flash, red-orange shockwave, radial speed lines |
| V-32 | `revive_sparkle.png` | 10 · 10×1 · 256 · 2560×256 | 20 · no | golden sparkles converging and flashing |
| V-33 | `petal_1..8.png` | 8 files · 128×128 | — | individual falling marigold petals and jasmine flowers |
| V-34 | `screen_flash.png` | single · 512×512 | — | soft radial white glow fading to fully transparent edges |
| V-35 | `speedlines.png` | single · 2048×1024 | — | radial anime speed lines converging to a transparent centre |
| V-36 | `titan_transform_flash.png` | 12 · 6×2 · 512×1024 · 3072×2048 · bottom-centre | 24 · no | towering column of violet-black energy and cracking light engulfing a figure-sized empty area (hides the Titan part swap) |

---

## 6. UI (modern, compact) · `Assets/ArtHQ/Level02/UI/`
Direction: **modern and compact first, Indian identity second.** Thin gold lines (2–3 px), small icons, subtle gradients, frosted dark-glass panels, no bulky bevels. All files are individual · Image (UI) · RGBA · **no text** (text is a Unity font).
9-slice items: keep the decoration in the corners and the middle plain. Border widths are given.

| ID | File | Size | Prompt (after [UI-STYLE]) | Notes |
|---|---|---|---|---|
| U-01 | `hp_frame.png` | 1200×96 | slim horizontal health bar frame, thin gold outline, rounded ends, dark glass channel, tiny lotus accent at the left end | 9-slice 40 px · mirrored for the boss |
| U-02 | `hp_fill_hero.png` / U-03 `hp_fill_boss.png` | 512×48 | smooth saffron-to-gold (hero) / violet-to-magenta (boss) fill with a soft top highlight | Filled |
| U-04 | `bhakti_segment.png` | 256×48 | slim capsule meter segment, thin gold outline, dark glass inside | used 3× |
| U-05 | `bhakti_fill.png` | 256×40 | glowing saffron-white energy fill | |
| U-06 | `round_dot_empty.png` / `_won.png` | 64×64 | small round dot, dark glass / glowing gold | |
| U-07 | `banner_plate.png` | 1600×280 | slim horizontal announcement band, dark glass fading out at both ends, thin gold lines above and below | 9-slice |
| U-08 | `banner_ko.png` | 1600×400 | dark glass band with a cracked gold light streak through it | |
| U-09 | `vs_background.png` | 2560×1440 · **opaque** | split versus background, gold glow left, violet right, clean diagonal light split | |
| U-10 | `vs_emblem.png` | 384×384 | small round medallion, thin gold ring, half gold and half violet glass, empty centre for the "VS" text | sits on the VS split |
| U-11 | `joystick_base.png` / `joystick_knob.png` | 320 / 140 | thin gold ring on translucent dark glass / small glass knob with a gold rim | about 35% alpha |
| U-12 | `btn_gada.png` | **200×200** | round glass action button, thin gold ring, small clean gada icon in the centre | + `_pressed` (slightly darker, inner glow) |
| U-13 | `btn_tail.png` | **160×160** | same style, small curled tail icon | + `_pressed` |
| U-14 | `btn_jump.png` / `btn_dodge.png` | **140×140** | same style, small upward chevron / small curved roll arrow | + `_pressed` |
| U-15 | `btn_ultimate.png` | **180×180** | same style with a thin five-point gold ring, small five-faced sun icon | + `_ready` (soft saffron glow ring) |
| U-16 | `icon_vayu/flight/fire/shrink/vajra/growth/panchmukhi.png` | **128×128** each | minimal flat line icon in thin gold inside a small glass circle: wind swirl · winged figure · flame tail · tiny figure with sparkles · vajra thunderbolt · rising giant · five-faced silhouette | 7 files |
| U-17 | `icon_cooldown.png` | 128×128 | dark translucent circle | radial Filled |
| U-18 | `combo_plate.png` | 480×140 | slim angled glass strip with a thin gold edge | |
| U-19 | `rank_good/great/mighty/jai.png` | 480×140 each | slim badge strip, escalating: plain bronze line → silver line with small wings → gold line with a flame accent → saffron-gold line with a small lotus halo | |
| U-20 | `combo_name_ribbon.png` | 900×140 | slim saffron brush-stroke ribbon with a thin gold edge | |
| U-21 | `boon_card.png` | 640×640 | clean glass card, thin gold frame, small lotus corners | 9-slice 64 px |
| U-22 | `prompt_tap/hold/swipe.png` | 128×128 | thin glowing ring with a minimal tap / hold / swipe glyph | |
| U-23 | `panel_glass.png` | 800×1000 | frosted dark-glass panel, thin gold outline, tiny lotus corner accents | 9-slice 48 px · move list / pause |
| U-24 | `panel_victory_bg.png` | 2560×1440 · **opaque** | soft golden temple light, gentle rays, a few floating marigold petals, bokeh | |
| U-25 | `star_empty.png` / `star_full.png` | 96×96 | minimal lotus-star outline / filled gold | |
| U-26 | `btn_menu.png` | 600×120 | slim rounded glass button, thin gold outline | 9-slice 40 px · + `_pressed` |
| U-27 | `btn_pause.png` | 96×96 | small round glass button with two vertical bar shapes | |

---

## 7. AUDIO: separate pipeline (NOT for image tools) · `Assets/Resources/Audio/Level02/`

| Stage | Tool / method | Check before handing over |
|---|---|---|
| 1. Music | a music generator (e.g. Suno / Udio / Stable Audio), **instrumental only**, or a composer | the loop point is seamless (play it on repeat 3× with no click or gap); BPM matches the prompt |
| 2. SFX | an SFX generator (e.g. ElevenLabs Sound Effects) or a sound library | short; no music bed; trimmed silence at the start (≤ 10 ms); 2–3 variations of hits and whooshes |
| 3. Voice | voice actor recording (best) or TTS (e.g. ElevenLabs) with a consistent voice per character | the same voice for every line of a character; no background noise; dry (no reverb) |
| 4. Mix | Audacity / Reaper / Audition | music about −16 LUFS integrated; SFX peaks ≤ −1 dBFS; voice about −18 LUFS; nothing clipping |
| 5. Export | | music: **OGG Vorbis q6, stereo, 44.1 kHz**; SFX and voice: **WAV 16-bit mono 44.1 kHz**; exact file names below |
| 6. Validation | I import them, check the length, loops and loudness in Unity, and replace the placeholder synth sounds by name | |

**Music**: `mus_duel` (2:00 loop): high-energy Indian epic battle theme, fast tabla and dhol, aggressive sitar and shehnai lead, orchestral strings, heavy cinematic percussion, 150 BPM, D minor · `mus_sorcery` (2:00 loop): dark mystical battle music, eerie female vocal drones, sarangi, ritual drums, 140 BPM · `mus_titans` (2:00 loop): colossal boss theme, slow war drums, deep brass, male choir, veena and shehnai, half-time 120 BPM · `mus_panchmukhi` (1:00): sacred chant building from one voice to a full choir, bells, conch, strings, triumphant climax · `mus_victory` (0:30): joyful Indian orchestral fanfare, dhol, brass, flute, bells.

**SFX**: `whoosh` heavy mace swing · `whoosh_heavy` huge slow swing · `hit` punchy body impact · `hit_heavy` mace on armour, metallic ring and bass boom · `slam` hammer smashing stone, rubble · `clang` weapon clash with shimmer · `block` dull blocked hit · `kick` kick whoosh and impact · `dodge` swoosh with cloth flutter · `jump` / `land` · `wind` rushing gust · `fire` ignition whoomp · `shrink` descending sparkle · `grow` colossal rising rumble · `step_giant` ground-shaking footstep · `thunder` thunder with holy resonance · `power` divine chord swell · `ultimate` huge holy chord, bells, conch, wind · `lamp_out` flame snuffed by wind · `orb_cast` dark charging hum · `orb_blast` dark explosion · `teleport` reverse whoosh and smoky pop · `poof` illusion puff · `magic` spell shimmer · `bell` temple bell · `ko` huge final impact with reverb tail · `tick` combo tick · `rank` short triumphant flourish · `roar` demon roar · `laugh` evil echoing laugh.

**Voice**:
- **Hanuman** (deep, warm, powerful): "Jai Shri Ram!", "Your illusions end here, Ahiravana.", efforts ×6, hurt ×4, "Pawan-putra!", "Burn!", a roar, "By Lord Rama's grace… PANCHMUKHI!", "Dharma always wins."
- **Ahiravana** (silky, cruel, echoing): laughs (long and short), "A monkey… in my kingdom? How amusing.", "Which one of me will you strike?", "Witness the true form of Patala's king!", hurt ×4, "No… the lamps! NOOO!"
- **Announcer:** "Round one / two / Final round", "Fight!", "K.O.!", "Perfect!", "Jai Hanuman!"

---

## 8. Generation order (one asset at a time, reviewed before the next)
1. **H-01 → H-02** (+ proportion check) → `hanuman_rig.psb` (near and far parts, `_joints`) → I rig and validate (1.5).
2. `gada_normal` → approve → charged and fire variants.
3. Hanuman faces (1.7) → Panchmukhi guide and heads (1.8).
4. **A-01 → A-02** → `ahiravana_rig.psb` → Ahiravana faces → trident → **A-T0 → `ahiravana_titan.psb`**.
5. Arena: E-01, E-02 (check the centre is clear), E-04 + destruction states, lamps, E-05, pillar states.
6. Core VFX: V-01, 02, 03, 04, 07, 09, 14, 18, 22, 24, 25, 26, 31, 36, plus the Panchmukhi overlays.
7. Core UI: U-01…U-05, U-11…U-16.
8. Audio (section 7 pipeline, in parallel with any of the above).
9. Everything else.

## 9. Completeness checklist (Master §12)
| Item | Covered by | Status |
|---|---|---|
| Background layers | E-01, E-02, E-06, E-07 | to generate |
| Foreground layers | E-05 | to generate |
| Platforms / terrain | E-04 floor (a duel has no platforms) | to generate |
| Decorative props | E-03, E-08 | to generate |
| Interactive objects | the five lamps (3.4) | to generate |
| Destructible objects | pillar (3.3), floor states (3.2) | to generate |
| Player rig + animations | 1.3 parts, 1.5 bones, 1.9 clips | clips coded · art to generate · rig to validate |
| Panchmukhi form | 1.8 heads, bones, overlays | to generate |
| Enemy animations | clones = boss rig (no separate enemy) | — |
| Boss rig + animations | 2.3, 2.9 | clips coded · art to generate |
| Boss phase 3 (Titan) | 2.6 Titan part set + V-36 | to generate |
| Boss arena | section 3 | to generate |
| Combat VFX | V-01…V-31, V-36 | to generate |
| Environmental VFX | E-06, E-07, V-17, lamp sheets | to generate |
| Collectibles | none in this duel | N/A, intentionally |
| Power-ups | boons are abilities: U-16 icons + VFX | to generate |
| Dialogue portraits | 1.10, 2.7 | to generate |
| NPC animations | captives in E-08 (static, optional) | optional |
| UI | section 6 | to generate |
| Death / respawn effects | V-31, V-32 | to generate |
| Victory effects | splash, V-33, U-24, `mus_victory` | to generate |
| Audio | section 7 (separate pipeline) | to produce |

**Honest status:** the fight logic, rig animation and placeholder art work in Unity. **No production art exists yet**, and the rigs aren't built until the PSB files arrive. Nothing here is production-ready until each row has a validated file.
