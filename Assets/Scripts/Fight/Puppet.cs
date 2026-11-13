using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalHanuman.Fight
{
    public enum Ease { Smooth, Linear, Out, In, Hold, Back }

    /// <summary>A full-body pose: joint angles in degrees (limbs: 0 = hanging straight down, positive = swung forward;
    /// hips/torso/head: positive = leaning forward), hip offset, and optional leg IK targets (foot positions on the ground).</summary>
    public class Pose
    {
        public readonly float[] a = new float[(int)Puppet.J.Count];
        public Vector2 hip;
        public bool ikF, ikB;
        public Vector2 footF, footB;

        public Pose Copy()
        {
            var p = new Pose { hip = hip, ikF = ikF, ikB = ikB, footF = footF, footB = footB };
            System.Array.Copy(a, p.a, a.Length);
            return p;
        }

        public static void Lerp(Pose x, Pose y, float u, Pose r)
        {
            for (int i = 0; i < r.a.Length; i++) r.a[i] = x.a[i] + (y.a[i] - x.a[i]) * u;
            r.hip = Vector2.LerpUnclamped(x.hip, y.hip, u);
        }

        float this[Puppet.J j] { set => a[(int)j] = value; }

        // ---- fluent authoring
        public Pose Lean(float torso, float hips = 0f) { this[Puppet.J.Torso] = torso; this[Puppet.J.Hips] = hips; return this; }
        public Pose Head(float h) { this[Puppet.J.Head] = h; return this; }
        public Pose Hip(float x, float y) { hip = new Vector2(x, y); return this; }
        public Pose ArmF(float upper, float fore, float hand = 0f) { this[Puppet.J.ArmF] = upper; this[Puppet.J.ForeF] = fore; this[Puppet.J.HandF] = hand; return this; }
        public Pose ArmB(float upper, float fore, float hand = 0f) { this[Puppet.J.ArmB] = upper; this[Puppet.J.ForeB] = fore; this[Puppet.J.HandB] = hand; return this; }
        public Pose Wpn(float w) { this[Puppet.J.Weapon] = w; return this; }
        public Pose Tail(float t) { this[Puppet.J.Tail] = t; return this; }
        /// <summary>Feet planted at these ground positions (x forward of the root, y height above the floor); legs solved by IK.</summary>
        public Pose Feet(float fx, float bx, float fy = 0f, float by = 0f) { ikF = ikB = true; footF = new Vector2(fx, fy); footB = new Vector2(bx, by); return this; }
        /// <summary>Explicit leg angles (air poses).</summary>
        public Pose LegF(float thigh, float shin, float foot = 0f) { ikF = false; this[Puppet.J.ThighF] = thigh; this[Puppet.J.ShinF] = shin; this[Puppet.J.FootF] = foot; return this; }
        public Pose LegB(float thigh, float shin, float foot = 0f) { ikB = false; this[Puppet.J.ThighB] = thigh; this[Puppet.J.ShinB] = shin; this[Puppet.J.FootB] = foot; return this; }
    }

    /// <summary>Keyframed pose animation. Keys are eased into the next key; loops wrap the last key back to the first.</summary>
    public class PuppetClip
    {
        public struct Key { public float t; public Pose p; public Ease e; }
        public readonly List<Key> keys = new List<Key>();
        public bool loop;
        public float Length;

        public static PuppetClip Make(Puppet.Dims d, bool loop, float length, params (float t, Pose p, Ease e)[] ks)
        {
            var c = new PuppetClip { loop = loop, Length = length };
            foreach (var k in ks)
            {
                var p = k.p.Copy();
                Puppet.Resolve(p, d);
                c.keys.Add(new Key { t = k.t, p = p, e = k.e });
            }
            return c;
        }

        static float EaseF(Ease e, float u)
        {
            switch (e)
            {
                case Ease.Linear: return u;
                case Ease.Out: return 1f - (1f - u) * (1f - u) * (1f - u);
                case Ease.In: return u * u * u;
                case Ease.Hold: return 0f;
                case Ease.Back: { float s = 1.9f; u -= 1f; return u * u * ((s + 1f) * u + s) + 1f; }
                default: return u * u * (3f - 2f * u);
            }
        }

        public void Sample(float t, Pose into)
        {
            if (keys.Count == 0) return;
            if (loop && Length > 0f) t = Mathf.Repeat(t, Length);
            int i = keys.Count - 1;
            for (int k = 0; k < keys.Count; k++) if (keys[k].t > t) { i = k - 1; break; }
            if (i < 0) { Pose.Lerp(keys[0].p, keys[0].p, 0f, into); return; }
            var a = keys[i];
            Key b; float tb;
            if (i + 1 < keys.Count) { b = keys[i + 1]; tb = b.t; }
            else if (loop) { b = keys[0]; tb = Length; }
            else { Pose.Lerp(a.p, a.p, 0f, into); return; }
            float u = tb > a.t ? Mathf.Clamp01((t - a.t) / (tb - a.t)) : 1f;
            Pose.Lerp(a.p, b.p, EaseF(a.e, u), into);
        }
    }

    /// <summary>
    /// The skeleton: a hierarchy of joints posed from code. Right now each bone carries a procedural capsule sprite (placeholder art);
    /// when the real rig parts arrive the same joints get the real sprites, so every move keeps working.
    /// Also handles secondary motion (tail, cape, cloth), facial expressions, hit flashes and afterimages.
    /// </summary>
    public class Puppet : MonoBehaviour
    {
        public enum J { Hips, Torso, Head, ArmF, ForeF, HandF, ArmB, ForeB, HandB, ThighF, ShinF, FootF, ThighB, ShinB, FootB, Weapon, Tail, Count }

        public class Dims
        {
            public float thigh = 0.62f, shin = 0.6f, ankle = 0.08f, torso = 0.85f, upperArm = 0.5f, foreArm = 0.46f, hand = 0.14f;
            public float headR = 0.3f, neck = 0.12f, footLen = 0.32f, shoulderY = 0.74f, weaponLen = 1.15f;
            public float shoulderXF = 0.05f, shoulderXB = -0.05f, hipXF = 0.06f, hipXB = -0.06f;
            public float[] tailLens;        // per-segment tail lengths (from the art); null = 6 × 0.24
            public float HipBase => thigh + shin + ankle;
            public float TailLen(int i) => tailLens != null && tailLens.Length > 0 ? tailLens[Mathf.Min(i, tailLens.Length - 1)] : 0.24f;
            public int TailCount => tailLens != null && tailLens.Length > 0 ? tailLens.Length : 6;
        }

        public class Style
        {
            public Color skin, face, cloth, cloth2, gold, hair, eye = Color.white, pupil = new Color(0.1f, 0.06f, 0.04f);
            public bool tail, cape, horns, muzzle, glowEyes;
            public int weapon;          // 0 none, 1 gada, 2 trident
            public float bulk = 1f;
        }

        public Dims dims;
        public Style style;
        public readonly Transform[] joints = new Transform[(int)J.Count];
        public readonly Pose pose = new Pose();
        public Vector2 velocity;        // set by the fighter (world units/s) for secondary motion
        public float facing = 1f;
        float blinkSeed;

        readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        readonly List<Color> baseColors = new List<Color>();
        readonly List<Transform> tail = new List<Transform>();
        readonly List<Transform> cape = new List<Transform>();
        float[] tailOff, capeOff, tailVel, capeVel;
        Transform clothFront, clothBack, weaponTip;
        SortingGroup group;

        // clip playback
        PuppetClip clip; float clipT, fade, fadeDur;
        readonly Pose from = new Pose(), sampled = new Pose();

        // flash and tint
        Color flashColor; float flashT, flashDur;
        Color tint = Color.white;

        // face
        SpriteRenderer brow, eye, pupil, mouth, muzzle;
        Expr expr = Expr.Neutral;
        float eyeH = 1f, browA, browY, mouthW = 0.1f, mouthH = 0.03f, pupilS = 1f;
        readonly List<GameObject> extraHeads = new List<GameObject>();

        public Transform Joint(J j) => joints[(int)j];
        public Vector3 Point(J j, Vector2 local) => joints[(int)j].TransformPoint(local);
        public Vector3 WeaponTip => weaponTip != null ? weaponTip.position : Point(J.HandF, Vector2.zero);
        public Transform TailSeg(int i) => tail.Count == 0 ? null : tail[Mathf.Clamp(i, 0, tail.Count - 1)];
        public int TailCount => tail.Count;
        public int SortingOrder { get => group.sortingOrder; set => group.sortingOrder = value; }
        public float ClipTime => clipT;
        public PuppetClip Clip => clip;

        // ================================================================== build
        public static Puppet Build(Transform parent, Dims d, Style s, int sortingOrder)
        {
            var go = new GameObject("Rig");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<Puppet>();
            p.dims = d; p.style = s; p.blinkSeed = Random.value * 3.7f;
            p.group = go.AddComponent<SortingGroup>(); p.group.sortingOrder = sortingOrder;
            p.BuildRig();
            return p;
        }

        Transform NewJoint(J j, Transform parent, Vector2 pos)
        {
            var t = new GameObject(j.ToString()).transform;
            t.SetParent(parent, false); t.localPosition = pos;
            joints[(int)j] = t;
            return t;
        }

        SpriteRenderer Reg(SpriteRenderer sr) { renderers.Add(sr); baseColors.Add(sr.color); return sr; }
        SpriteRenderer Seg(string n, Transform t, float len, float w, Color c, int order) => Reg(FightArt.Segment(n, t, len, w, c, order));
        SpriteRenderer Q(string n, Transform t, Sprite s, Vector2 pos, Vector2 scale, Color c, int order, float rot = 0f) => Reg(FightArt.Quad(n, t, s, pos, scale, c, order, rot));

        static Color Dark(Color c, float k = 0.72f) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        void BuildRig()
        {
            var d = dims; var s = style; float b = s.bulk;
            var hips = NewJoint(J.Hips, transform, new Vector2(0, d.HipBase));
            var torso = NewJoint(J.Torso, hips, new Vector2(0, 0.04f));
            var head = NewJoint(J.Head, torso, new Vector2(0.02f, d.torso + d.neck));

            // ---- legs (back leg darker and behind)
            BuildLeg(J.ThighB, J.ShinB, J.FootB, hips, true);
            BuildLeg(J.ThighF, J.ShinF, J.FootF, hips, false);

            // ---- torso (capsule pointing up), pelvis, belt
            var tv = new GameObject("TorsoVis").transform; tv.SetParent(torso, false); tv.localRotation = Quaternion.Euler(0, 0, 180f);
            Seg("Chest", tv, d.torso * 0.62f, 0.66f * b, s.skin, 4);
            var belly = new GameObject("Belly").transform; belly.SetParent(torso, false); belly.localPosition = new Vector2(0.02f, 0.3f); belly.localRotation = Quaternion.Euler(0, 0, 180f);
            Seg("BellyVis", belly, 0.22f, 0.52f * b, Dark(s.skin, 0.95f), 4);
            Q("Pelvis", hips, FightArt.Circle, new Vector2(0, 0.02f), new Vector2(0.62f * b, 0.42f), s.cloth, 5);
            Q("Belt", hips, FightArt.Capsule(0.1f), new Vector2(0, 0.2f), Vector2.one, s.gold, 6).drawMode = SpriteDrawMode.Sliced;
            renderers[renderers.Count - 1].size = new Vector2(0.62f * b, 0.1f); renderers[renderers.Count - 1].transform.localRotation = Quaternion.Euler(0, 0, 90f);
            renderers[renderers.Count - 1].transform.localPosition = new Vector2(-0.31f * b, 0.2f);
            // necklace / chest ornament
            Q("Necklace", torso, FightArt.Circle, new Vector2(0.12f, d.torso - 0.12f), new Vector2(0.16f, 0.16f), s.gold, 6);

            // cloth flaps (front and back) that follow the thighs
            clothFront = new GameObject("ClothFront").transform; clothFront.SetParent(hips, false); clothFront.localPosition = new Vector2(0.08f, 0.05f);
            Seg("ClothFrontVis", clothFront, 0.42f, 0.3f, Dark(s.cloth, 1.08f), 7);
            clothBack = new GameObject("ClothBack").transform; clothBack.SetParent(hips, false); clothBack.localPosition = new Vector2(-0.12f, 0.08f);
            Seg("ClothBackVis", clothBack, 0.36f, 0.26f, s.cloth2, 3);

            // ---- arms
            BuildArm(J.ArmB, J.ForeB, J.HandB, torso, true);
            BuildArm(J.ArmF, J.ForeF, J.HandF, torso, false);

            // ---- head
            BuildHead(head);

            // ---- weapon in the front hand
            var w = NewJoint(J.Weapon, Joint(J.HandF), new Vector2(0, -d.hand * 0.6f));
            if (s.weapon == 1) BuildGada(w); else if (s.weapon == 2) BuildTrident(w);

            // ---- tail
            if (s.tail)
            {
                Transform prev = hips; Vector2 at = new Vector2(-0.24f, 0.05f);
                int nt = d.TailCount;
                for (int i = 0; i < nt; i++)
                {
                    var t = new GameObject("Tail" + i).transform; t.SetParent(prev, false); t.localPosition = at;
                    float w0 = Mathf.Lerp(0.16f, 0.08f, i / Mathf.Max(1f, nt - 1f));
                    Seg("TailVis" + i, t, d.TailLen(i), w0, i == nt - 1 ? Dark(s.skin, 0.8f) : s.skin, -1);
                    tail.Add(t); prev = t; at = new Vector2(0, -d.TailLen(i));
                }
                joints[(int)J.Tail] = tail[0];
                tailOff = new float[tail.Count]; tailVel = new float[tail.Count];
            }
            else joints[(int)J.Tail] = NewJoint(J.Tail, hips, Vector2.zero);

            // ---- cape (from the back of the shoulders)
            if (s.cape)
            {
                Transform prev = torso; Vector2 at = new Vector2(-0.22f, d.torso - 0.05f);
                for (int i = 0; i < 4; i++)
                {
                    var t = new GameObject("Cape" + i).transform; t.SetParent(prev, false); t.localPosition = at;
                    Seg("CapeVis" + i, t, 0.37f, Mathf.Lerp(0.5f, 0.62f, i / 3f), Dark(s.cloth2, 1f - i * 0.04f), -2);
                    cape.Add(t); prev = t; at = new Vector2(0, -0.37f);
                }
                capeOff = new float[cape.Count]; capeVel = new float[cape.Count];
            }
        }

        void BuildLeg(J thighJ, J shinJ, J footJ, Transform hips, bool back)
        {
            var d = dims; var s = style; float b = s.bulk;
            int o = back ? 1 : 8;
            Color cloth = back ? Dark(s.cloth) : s.cloth, skin = back ? Dark(s.skin) : s.skin, gold = back ? Dark(s.gold) : s.gold;
            var thigh = NewJoint(thighJ, hips, new Vector2(back ? d.hipXB : d.hipXF, 0));
            Seg("ThighVis", thigh, d.thigh, 0.32f * b, cloth, o);
            var shin = NewJoint(shinJ, thigh, new Vector2(0, -d.thigh));
            Seg("ShinVis", shin, d.shin, 0.22f * b, skin, o - 1 + (back ? 0 : 0));
            Q("Anklet", shin, FightArt.Circle, new Vector2(0, -d.shin + 0.06f), new Vector2(0.24f * b, 0.08f), gold, o + 1);
            var foot = NewJoint(footJ, shin, new Vector2(0, -d.shin));
            var fv = new GameObject("FootVis").transform; fv.SetParent(foot, false); fv.localRotation = Quaternion.Euler(0, 0, 90f); fv.localPosition = new Vector2(-0.06f, 0);
            Seg("Foot", fv, d.footLen, 0.15f, skin, o);
        }

        void BuildArm(J upperJ, J foreJ, J handJ, Transform torso, bool back)
        {
            var d = dims; var s = style; float b = s.bulk;
            int o = back ? 2 : 11;
            Color skin = back ? Dark(s.skin) : s.skin, gold = back ? Dark(s.gold) : s.gold;
            var up = NewJoint(upperJ, torso, new Vector2(back ? d.shoulderXB : d.shoulderXF, d.shoulderY));
            Q("Shoulder", up, FightArt.Circle, Vector2.zero, new Vector2(0.36f * b, 0.36f * b), skin, o);
            Seg("UpperVis", up, d.upperArm, 0.27f * b, skin, o);
            Q("Armband", up, FightArt.Circle, new Vector2(0, -d.upperArm * 0.45f), new Vector2(0.3f * b, 0.09f), gold, o + 1);
            var fore = NewJoint(foreJ, up, new Vector2(0, -d.upperArm));
            Seg("ForeVis", fore, d.foreArm, 0.22f * b, skin, o);
            Q("Cuff", fore, FightArt.Circle, new Vector2(0, -d.foreArm + 0.06f), new Vector2(0.26f * b, 0.1f), gold, o + 1);
            var hand = NewJoint(handJ, fore, new Vector2(0, -d.foreArm));
            Q("HandVis", hand, FightArt.Circle, new Vector2(0, -d.hand * 0.5f), new Vector2(0.24f * b, 0.24f * b), skin, back ? o : o + 3);
        }

        void BuildHead(Transform head)
        {
            var d = dims; var s = style;
            float r = d.headR;
            var hv = new GameObject("HeadVis").transform; hv.SetParent(head, false); hv.localPosition = new Vector2(0, r * 0.9f);
            Q("Neck", head, FightArt.Circle, new Vector2(0, 0.05f), new Vector2(0.26f, 0.3f), s.skin, 12);
            Q("Skull", hv, FightArt.Circle, Vector2.zero, new Vector2(r * 2f, r * 2.05f), s.skin, 13);
            Q("Ear", hv, FightArt.Circle, new Vector2(-r * 0.25f, r * 0.05f), new Vector2(r * 0.55f, r * 0.7f), Dark(s.face, 0.9f), 14);
            Q("Earring", hv, FightArt.Circle, new Vector2(-r * 0.25f, -r * 0.42f), new Vector2(r * 0.22f, r * 0.22f), s.gold, 15);
            Q("Face", hv, FightArt.Circle, new Vector2(r * 0.42f, -r * 0.05f), new Vector2(r * 1.25f, r * 1.4f), s.face, 14);
            if (s.muzzle) muzzle = Q("Muzzle", hv, FightArt.Circle, new Vector2(r * 0.82f, -r * 0.38f), new Vector2(r * 0.85f, r * 0.62f), Dark(s.face, 1.05f), 16);
            else muzzle = Q("Jaw", hv, FightArt.Circle, new Vector2(r * 0.6f, -r * 0.55f), new Vector2(r * 0.8f, r * 0.5f), s.face, 15);
            Q("Nose", hv, FightArt.Circle, new Vector2(r * 1.12f, -r * 0.22f), new Vector2(r * 0.22f, r * 0.18f), Dark(s.face, 0.55f), 18);
            eye = Q("Eye", hv, FightArt.Circle, new Vector2(r * 0.62f, r * 0.12f), new Vector2(r * 0.42f, r * 0.42f), s.eye, 17);
            pupil = Q("Pupil", eye.transform, FightArt.Circle, new Vector2(0.18f, 0f), new Vector2(0.55f, 0.6f), s.glowEyes ? s.eye : s.pupil, 18);
            if (s.glowEyes) { eye.color = new Color(0.15f, 0.05f, 0.2f); baseColors[renderers.IndexOf(eye)] = eye.color; pupil.color = s.eye; baseColors[renderers.IndexOf(pupil)] = s.eye; }
            brow = Q("Brow", hv, FightArt.Capsule(0.07f), new Vector2(r * 0.42f, r * 0.42f), Vector2.one, s.hair, 19, 90f);
            brow.drawMode = SpriteDrawMode.Sliced; brow.size = new Vector2(0.07f, r * 0.62f);
            mouth = Q("Mouth", hv, FightArt.Circle, new Vector2(r * 0.95f, -r * 0.55f), new Vector2(0.1f, 0.03f), new Color(0.3f, 0.06f, 0.06f), 19);
            // hair and crown
            Q("Hair", hv, FightArt.Circle, new Vector2(-r * 0.35f, r * 0.15f), new Vector2(r * 1.4f, r * 1.7f), s.hair, 12);
            if (!s.horns)
            {
                // tall golden mukuta with a red gem
                Q("CrownBand", hv, FightArt.Capsule(0.12f), new Vector2(-r * 0.75f, r * 0.62f), Vector2.one, s.gold, 20, 90f).drawMode = SpriteDrawMode.Sliced;
                renderers[renderers.Count - 1].size = new Vector2(0.12f, r * 1.5f);
                Q("Crown", hv, FightArt.Tri, new Vector2(-r * 0.05f, r * 0.66f), new Vector2(r * 1.2f, r * 0.95f), s.gold, 20);
                Q("CrownTip", hv, FightArt.Circle, new Vector2(-r * 0.05f, r * 1.62f), new Vector2(r * 0.22f, r * 0.22f), s.gold, 20);
                Q("CrownGem", hv, FightArt.Circle, new Vector2(r * 0.12f, r * 0.85f), new Vector2(r * 0.25f, r * 0.25f), new Color(0.9f, 0.1f, 0.15f), 21);
            }
            else
            {
                // black and gold spiked crown plus curved horns
                for (int i = 0; i < 4; i++)
                    Q("Spike" + i, hv, FightArt.Tri, new Vector2(Mathf.Lerp(-r * 0.6f, r * 0.45f, i / 3f), r * 0.7f), new Vector2(r * 0.42f, r * (i == 1 || i == 2 ? 1.1f : 0.8f)), i % 2 == 0 ? s.gold : new Color(0.1f, 0.08f, 0.12f), 20);
                Q("CrownGem", hv, FightArt.Circle, new Vector2(0, r * 0.8f), new Vector2(r * 0.25f, r * 0.25f), s.eye, 21);
                Q("Horn", hv, FightArt.Tri, new Vector2(-r * 0.4f, r * 0.55f), new Vector2(r * 0.35f, r * 0.9f), new Color(0.85f, 0.8f, 0.7f), 11, 40f);
            }
            pupilS = 1f;
        }

        void BuildGada(Transform w)
        {
            var s = style; float L = dims.weaponLen;
            Seg("Shaft", w, L, 0.1f, s.gold, 10);
            Q("Grip", w, FightArt.Capsule(0.12f), new Vector2(0, 0.05f), Vector2.one, new Color(0.7f, 0.12f, 0.1f), 11).drawMode = SpriteDrawMode.Sliced;
            renderers[renderers.Count - 1].size = new Vector2(0.12f, 0.35f);
            Q("Pommel", w, FightArt.Circle, new Vector2(0, 0.12f), new Vector2(0.16f, 0.16f), s.gold, 11);
            Q("Collar", w, FightArt.Circle, new Vector2(0, -L * 0.72f), new Vector2(0.24f, 0.12f), Dark(s.gold, 0.8f), 11);
            Q("HeadBall", w, FightArt.Circle, new Vector2(0, -L), new Vector2(0.58f, 0.62f), s.gold, 12);
            Q("HeadShine", w, FightArt.Circle, new Vector2(-0.1f, -L + 0.12f), new Vector2(0.18f, 0.14f), new Color(1f, 0.97f, 0.8f), 13);
            Q("HeadBand", w, FightArt.Circle, new Vector2(0, -L), new Vector2(0.6f, 0.12f), Dark(s.gold, 0.75f), 13);
            Q("Finial", w, FightArt.Tri, new Vector2(0, -L - 0.28f), new Vector2(0.18f, 0.24f), s.gold, 12, 180f);
            weaponTip = new GameObject("Tip").transform; weaponTip.SetParent(w, false); weaponTip.localPosition = new Vector2(0, -L);
        }

        void BuildTrident(Transform w)
        {
            float L = dims.weaponLen;
            var dark = new Color(0.12f, 0.1f, 0.15f); var gold = style.gold;
            Seg("Shaft", w, L + 0.45f, 0.08f, dark, 10);
            w.GetChild(w.childCount - 1).localPosition = new Vector3(0, 0.45f, 0);  // extends behind the hand too
            Q("Gem", w, FightArt.Circle, new Vector2(0, -L), new Vector2(0.2f, 0.2f), style.eye, 13);
            Q("Guard", w, FightArt.Circle, new Vector2(0, -L), new Vector2(0.5f, 0.12f), gold, 12);
            Q("Prong0", w, FightArt.Tri, new Vector2(0, -L - 0.02f), new Vector2(0.16f, 0.62f), dark, 12, 180f);
            Q("Prong1", w, FightArt.Tri, new Vector2(-0.2f, -L - 0.02f), new Vector2(0.12f, 0.45f), dark, 12, 165f);
            Q("Prong2", w, FightArt.Tri, new Vector2(0.2f, -L - 0.02f), new Vector2(0.12f, 0.45f), dark, 12, 195f);
            weaponTip = new GameObject("Tip").transform; weaponTip.SetParent(w, false); weaponTip.localPosition = new Vector2(0, -L - 0.35f);
        }

        // ================================================================== playback
        public void Play(PuppetClip c, float fadeTime = 0.08f, float startAt = 0f)
        {
            if (c == null) return;
            if (clip != null)
            {
                Pose.Lerp(pose, pose, 0f, from);
                // blend along the shortest way round (a 360° spin must not unwind into the next clip)
                c.Sample(startAt, sampled);
                for (int i = 0; i < from.a.Length; i++) from.a[i] = sampled.a[i] + Mathf.DeltaAngle(sampled.a[i], from.a[i]);
                fade = fadeTime; fadeDur = fadeTime;
            }
            else { fade = 0f; fadeDur = 0f; }
            clip = c; clipT = startAt;
        }

        public bool Finished => clip != null && !clip.loop && clipT >= clip.Length;

        public void Tick(float dt)
        {
            if (clip == null) return;
            clipT += dt;
            clip.Sample(clipT, sampled);
            if (fade > 0f)
            {
                fade -= dt;
                float u = 1f - Mathf.Clamp01(fade / Mathf.Max(0.0001f, fadeDur));
                u = u * u * (3f - 2f * u);
                Pose.Lerp(from, sampled, u, pose);
            }
            else Pose.Lerp(sampled, sampled, 0f, pose);
            ApplyPose(dt);
        }

        static Quaternion Z(float deg) => Quaternion.Euler(0, 0, deg);

        void ApplyPose(float dt)
        {
            var a = pose.a; var d = dims;
            Joint(J.Hips).localPosition = new Vector2(pose.hip.x, d.HipBase + pose.hip.y);
            Joint(J.Hips).localRotation = Z(-a[(int)J.Hips]);
            Joint(J.Torso).localRotation = Z(-a[(int)J.Torso]);
            Joint(J.Head).localRotation = Z(-a[(int)J.Head]);
            foreach (var j in new[] { J.ArmF, J.ForeF, J.HandF, J.ArmB, J.ForeB, J.HandB, J.ThighF, J.ShinF, J.ThighB, J.ShinB, J.Weapon })
                Joint(j).localRotation = Z(a[(int)j]);
            float hipsCcw = -a[(int)J.Hips];
            Joint(J.FootF).localRotation = Z(-(hipsCcw + a[(int)J.ThighF] + a[(int)J.ShinF]) + a[(int)J.FootF]);
            Joint(J.FootB).localRotation = Z(-(hipsCcw + a[(int)J.ThighB] + a[(int)J.ShinB]) + a[(int)J.FootB]);

            // cloth flaps hang between the thighs
            float fwd = velocity.x * facing;
            if (clothFront != null) clothFront.localRotation = Z(Mathf.Max(a[(int)J.ThighF], a[(int)J.ThighB]) * 0.8f + 4f - fwd * 2f);
            if (clothBack != null) clothBack.localRotation = Z(Mathf.Min(a[(int)J.ThighF], a[(int)J.ThighB]) * 0.6f - 8f - fwd * 3f);

            Secondary(dt, fwd);
            FaceTick(dt);
            FlashTick(dt);
        }

        void Spring(float[] off, float[] vel, int i, float target, float k, float damp, float dt)
        {
            float acc = (target - off[i]) * k - vel[i] * damp;
            vel[i] += acc * dt; off[i] += vel[i] * dt;
        }

        void Secondary(float dt, float fwd)
        {
            if (dt <= 0f) return;
            float time = Time.time;
            if (tail.Count > 0)
            {
                float baseA = pose.a[(int)J.Tail];
                for (int i = 0; i < tail.Count; i++)
                {
                    float drive = i == 0 ? Mathf.Clamp(-fwd * 3.5f + velocity.y * 2.2f, -40f, 40f) : tailOff[i - 1];
                    Spring(tailOff, tailVel, i, drive, 120f, 11f, dt);
                    float curl = i == 0 ? baseA : (i < 3 ? 14f : 22f);
                    float wave = Mathf.Sin(time * 3.2f - i * 0.75f) * (4f + i * 1.5f);
                    tail[i].localRotation = Z(curl + wave + tailOff[i] * (i == 0 ? 1f : 0.45f));
                }
            }
            if (cape.Count > 0)
            {
                float torsoCcw = -pose.a[(int)J.Hips] - pose.a[(int)J.Torso];
                for (int i = 0; i < cape.Count; i++)
                {
                    float drive = i == 0 ? Mathf.Clamp(-fwd * 5f + velocity.y * 2.5f, -60f, 30f) : capeOff[i - 1];
                    Spring(capeOff, capeVel, i, drive, 90f, 9f, dt);
                    float wave = Mathf.Sin(time * 2.4f - i * 0.9f) * 3f;
                    float hang = i == 0 ? -torsoCcw - 6f : 0f;
                    cape[i].localRotation = Z(hang + wave + capeOff[i] * (i == 0 ? 1f : 0.35f));
                }
            }
        }

        // ================================================================== face
        public void SetExpression(Expr e) { expr = e; if (variantGroups.Count > 0) SetVariant("head", HeadKey(e)); }

        void FaceTick(float dt)
        {
            if (eye == null) return;
            float eh = 1f, ba = 0f, by = 0f, mw = 0.09f, mh = 0.025f, ps = 1f;
            switch (expr)
            {
                case Expr.Focus: eh = 0.75f; ba = -12f; by = -0.02f; break;
                case Expr.Shout: eh = 0.9f; ba = -18f; by = -0.02f; mw = 0.13f; mh = 0.12f; break;
                case Expr.Hurt: eh = 0.15f; ba = 15f; by = 0.01f; mw = 0.12f; mh = 0.06f; break;
                case Expr.Smirk: eh = 0.65f; ba = 8f; by = 0.02f; mw = 0.12f; mh = 0.03f; break;
                case Expr.Roar: eh = 1.05f; ba = -24f; by = -0.03f; mw = 0.15f; mh = 0.16f; ps = 0.7f; break;
                case Expr.Shocked: eh = 1.25f; ba = 18f; by = 0.05f; mw = 0.07f; mh = 0.09f; ps = 0.6f; break;
                case Expr.Laugh: eh = 0.35f; ba = 10f; by = 0.02f; mw = 0.14f; mh = 0.11f; break;
                case Expr.Furious: eh = 0.85f; ba = -28f; by = -0.04f; mw = 0.12f; mh = 0.07f; ps = 0.8f; break;
                case Expr.Desperate: eh = 1.1f; ba = 22f; by = 0.04f; mw = 0.1f; mh = 0.07f; ps = 0.7f; break;
                case Expr.Dizzy: eh = 0.5f; ba = 10f; mw = 0.08f; mh = 0.05f; ps = 0.4f; break;
                case Expr.Serene: eh = 0.55f; ba = 4f; by = 0.01f; mw = 0.08f; mh = 0.02f; break;
            }
            float k = 1f - Mathf.Exp(-22f * Mathf.Max(dt, 0.016f));
            eyeH = Mathf.Lerp(eyeH, eh, k); browA = Mathf.Lerp(browA, ba, k); browY = Mathf.Lerp(browY, by, k);
            mouthW = Mathf.Lerp(mouthW, mw, k); mouthH = Mathf.Lerp(mouthH, mh, k); pupilS = Mathf.Lerp(pupilS, ps, k);
            float r = dims.headR;
            // blink now and then
            float blink = Mathf.Repeat(Time.time + blinkSeed, 3.7f) < 0.09f && expr != Expr.Roar && expr != Expr.Shocked ? 0.1f : 1f;
            eye.transform.localScale = new Vector3(r * 0.42f, r * 0.42f * eyeH * blink, 1f);
            pupil.transform.localScale = new Vector3(0.55f * pupilS, 0.6f * pupilS, 1f);
            brow.transform.localPosition = new Vector3(r * 0.42f, r * 0.42f + browY, 0);
            brow.transform.localRotation = Z(90f + browA);
            mouth.transform.localScale = new Vector3(mouthW, mouthH, 1f);
        }

        /// <summary>Panchmukhi: four divine heads appear around Hanuman's own (lion, eagle, boar, horse).</summary>
        public void ShowPanchmukhi(bool on)
        {
            if (on && extraHeads.Count == 0)
            {
                var head = Joint(J.Head);
                // Layout from Docs/Level02_AssetPrompts.md 1.8: back -> front, offsets in head heights (H) from the centre of Hanuman's head.
                // Every extra head sorts behind Hanuman's own head (skull is order 13) and never covers his face.
                float r = dims.headR, H = r * 2.05f; var centre = new Vector2(0f, r * 0.9f);
                (string n, Color c, Vector2 off, Color feature, int o)[] h =
                {
                    ("Garuda", new Color(0.9f, 0.2f, 0.15f), new Vector2(-0.75f, 0.10f), new Color(1f, 0.8f, 0.2f), 1),
                    ("Varaha", new Color(0.3f, 0.36f, 0.55f), new Vector2(-0.45f, 0.70f), new Color(0.95f, 0.95f, 0.9f), 3),
                    ("Hayagriva", new Color(0.95f, 0.93f, 0.88f), new Vector2(0.05f, 1.00f), new Color(0.7f, 0.7f, 0.75f), 5),
                    ("Narasimha", new Color(1f, 0.62f, 0.15f), new Vector2(0.50f, 0.70f), new Color(0.95f, 0.35f, 0.05f), 7),
                };
                var haloRoot = new GameObject("PM_HaloRoot").transform; haloRoot.SetParent(head, false);   // wrapper: PopHeads scales it 0 -> 1
                Q("PM_Halo", haloRoot, FightArt.Soft, centre + new Vector2(-0.1f, 0.5f) * H, Vector2.one * H * 3.2f, new Color(1f, 0.85f, 0.4f, 0.7f), 0);
                haloRoot.localScale = Vector3.zero; extraHeads.Add(haloRoot.gameObject);
                foreach (var x in h)
                {
                    var go = new GameObject("PM_" + x.n); go.transform.SetParent(head, false); go.transform.localPosition = centre + x.off * H;
                    int o = x.o;
                    if (x.n == "Narasimha") Q("Mane", go.transform, FightArt.Circle, new Vector2(-r * 0.3f, 0), new Vector2(r * 2.6f, r * 2.6f), new Color(0.85f, 0.4f, 0.05f), o);
                    if (x.n == "Hayagriva") Q("Mane", go.transform, FightArt.Circle, new Vector2(-r * 0.6f, 0.1f), new Vector2(r * 1.2f, r * 2.4f), new Color(0.9f, 0.9f, 1f), o);
                    Q("Skull", go.transform, FightArt.Circle, Vector2.zero, new Vector2(r * 2f, r * 2f), x.c, o);
                    Q("Snout", go.transform, x.n == "Garuda" ? FightArt.Tri : FightArt.Circle, new Vector2(r * 0.8f, -r * 0.2f), x.n == "Garuda" ? new Vector2(r * 0.6f, r * 0.9f) : new Vector2(r * 1f, r * 0.7f), x.feature, o + 1, x.n == "Garuda" ? -90f : 0f);
                    Q("Eye", go.transform, FightArt.Circle, new Vector2(r * 0.35f, r * 0.25f), new Vector2(r * 0.35f, r * 0.35f), new Color(1f, 0.95f, 0.6f), o + 1);
                    Q("Crown", go.transform, FightArt.Tri, new Vector2(0, r * 0.75f), new Vector2(r * 1.1f, r * 1f), style.gold, o + 1);
                    if (x.n == "Varaha") Q("Tusk", go.transform, FightArt.Tri, new Vector2(r * 1.1f, -r * 0.4f), new Vector2(r * 0.25f, r * 0.6f), Color.white, o + 1, -30f);
                    extraHeads.Add(go);
                    go.transform.localScale = Vector3.zero;
                }
                // the golden collar that hides where the necks meet (in front of the extra necks, behind Hanuman's head)
                var ringRoot = new GameObject("PM_NeckRingRoot").transform; ringRoot.SetParent(head, false);
                Q("PM_NeckRing", ringRoot, FightArt.Ring, new Vector2(0f, 0.05f), new Vector2(r * 1.6f, r * 0.8f), style.gold, 11);
                ringRoot.localScale = Vector3.zero; extraHeads.Add(ringRoot.gameObject);
            }
            foreach (var g in extraHeads) g.SetActive(on);
            if (on) StartCoroutine(PopHeads());
        }

        System.Collections.IEnumerator PopHeads()
        {
            for (int i = 0; i < extraHeads.Count; i++)
            {
                var g = extraHeads[i];
                float t = 0f;
                while (t < 0.22f)
                {
                    t += Time.unscaledDeltaTime;
                    float u = t / 0.22f; float s = 1f + Mathf.Sin(u * Mathf.PI) * 0.35f;
                    g.transform.localScale = Vector3.one * Mathf.Min(1f, u) * s;
                    yield return null;
                }
                g.transform.localScale = Vector3.one;
                yield return new WaitForSecondsRealtime(0.08f);
            }
        }

        // ================================================================== real art (skin)
        public bool Skinned { get; private set; }
        readonly System.Collections.Generic.Dictionary<string, SpriteRenderer> skinParts = new System.Collections.Generic.Dictionary<string, SpriteRenderer>();

        Transform JointByName(string n)
        {
            if (n.StartsWith("Tail") && n.Length > 4 && int.TryParse(n.Substring(4), out int ti)) return ti < tail.Count ? tail[ti] : null;
            if (n.StartsWith("Cape") && n.Length > 4 && int.TryParse(n.Substring(4), out int ci)) return ci < cape.Count ? cape[ci] : null;
            if (n == "ClothFront") return clothFront;
            if (n == "ClothBack") return clothBack;
            return System.Enum.TryParse(n, out J j) ? Joint(j) : null;
        }

        /// <summary>Replace the placeholder shapes with the real part sprites (the weapon stays). Each sprite is placed so its
        /// joint pixel sits exactly on its bone; hand variants other than the default are kept hidden for sprite swaps.</summary>
        public void ApplySkin(HanumanSkin.Data data, System.Func<string, Sprite> sprites = null)
        {
            if (data == null || data.parts == null) return;
            sprites = sprites ?? HanumanSkin.Sprite;
            var weapon = Joint(J.Weapon);
            bool skinWeapon = false;
            foreach (var p in data.parts) if (p.joint == "Weapon") skinWeapon = true;
            foreach (var r in renderers) if (r != null && (skinWeapon || !r.transform.IsChildOf(weapon))) r.enabled = false;
            foreach (var p in data.parts)
            {
                var j = JointByName(p.joint);
                var sp = sprites(p.name);
                if (j == null || sp == null) { Debug.LogWarning("Skin part skipped: " + p.name); continue; }
                var go = new GameObject("Skin_" + p.name);
                go.transform.SetParent(j, false);
                go.transform.localRotation = Quaternion.Euler(0, 0, p.rot);
                float k = data.k * (p.s > 0f ? p.s : 1f);
                go.transform.localScale = Vector3.one * (k * sp.pixelsPerUnit);
                // sprite pivot is its centre; move it so pixel (px, py) (y down from the top) lands on the joint
                go.transform.localPosition = Quaternion.Euler(0, 0, p.rot) * new Vector3(-(p.px - p.w * 0.5f) * k, (p.py - p.h * 0.5f) * k, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sp; sr.sortingOrder = p.order;
                if (p.shade > 0f) sr.color = new Color(p.shade, p.shade, p.shade, 1f);
                Reg(sr);
                skinParts[p.name] = sr;
                if (!string.IsNullOrEmpty(p.group))
                {   // sprite-swap group (faces, hand poses): only the default one shows
                    if (!variantGroups.TryGetValue(p.group, out var list)) variantGroups[p.group] = list = new System.Collections.Generic.List<SpriteRenderer>();
                    list.Add(sr); sr.enabled = p.def;
                    continue;
                }
                bool variant = p.name.Contains("_hand_") && !(p.name.EndsWith("near_fist") || p.name.EndsWith("far_fist"));
                if (variant) sr.enabled = false;
            }
            Skinned = true;
        }

        readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<SpriteRenderer>> variantGroups = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<SpriteRenderer>>();

        /// <summary>Show the variant of a sprite-swap group whose name ends with "_key" (e.g. group "hand_near", key "cast").</summary>
        public void SetVariant(string group, string key)
        {
            if (!variantGroups.TryGetValue(group, out var list)) return;
            bool found = false;
            foreach (var r in list) if (r != null && r.name.EndsWith("_" + key)) found = true;
            if (!found) return;
            foreach (var r in list) if (r != null) r.enabled = r.name.EndsWith("_" + key);
        }

        static string HeadKey(Expr e)
        {
            switch (e)
            {
                case Expr.Laugh: return "laugh";
                case Expr.Shocked: return "shocked";
                case Expr.Furious: case Expr.Roar: case Expr.Shout: return "furious";
                case Expr.Desperate: case Expr.Hurt: return "desperate";
                case Expr.Dizzy: return "defeated";
                default: return "smirk";
            }
        }

        /// <summary>Show one hand variant (e.g. "near_fist") and hide the other variants on that hand.</summary>
        public void SetHand(bool near, string variant)
        {
            string pre = near ? "han_hand_near_" : "han_hand_far_";
            if (!skinParts.ContainsKey(pre + variant)) return;
            foreach (var kv in skinParts) if (kv.Key.StartsWith(pre)) kv.Value.enabled = kv.Key == pre + variant;
        }

        // ================================================================== frame-art characters
        /// <summary>True for a character drawn from painted frames: the skeleton stays as an invisible hit and anchor rig (joint positions, Panchmukhi head anchor) and is never drawn.</summary>
        public bool FramesOnly { get; private set; }

        /// <summary>Hides every body shape of the skeleton for good. Shapes added later (the Panchmukhi heads) still show.</summary>
        public void HideBody()
        {
            FramesOnly = true;
            foreach (var r in renderers) if (r != null) r.enabled = false;
        }

        // ================================================================== flash, tint, afterimages
        public void Flash(Color c, float dur)
        {
            // painted art can't be tinted brighter than itself: a white flash becomes a hot red-gold flash on the real sprites
            if ((FramesOnly || Skinned) && c.r > 0.9f && c.g > 0.9f && c.b > 0.9f) c = new Color(1f, 0.55f, 0.4f, c.a);
            flashColor = c; flashT = dur; flashDur = dur;
        }
        public void SetTint(Color c) { tint = c; flashT = Mathf.Max(flashT, 0.0001f); }

        /// <summary>Colour a plain sprite the same way the rig is tinted and flashed (used by frame-art clips).</summary>
        public Color VisualColor()
        {
            float k = flashDur > 0f ? Mathf.Clamp01(flashT / flashDur) : 0f;
            return Color.Lerp(tint, flashColor, k * flashColor.a);
        }

        void FlashTick(float dt)
        {
            if (flashT <= 0f && tint == Color.white) return;
            flashT = Mathf.Max(0f, flashT - Time.deltaTime);
            float k = flashDur > 0f ? Mathf.Clamp01(flashT / flashDur) : 0f;
            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] == null) continue;
                var b = baseColors[i] * tint; b.a = baseColors[i].a * tint.a;
                renderers[i].color = Color.Lerp(b, flashColor, k * flashColor.a);
            }
        }

        /// <summary>Whole-body picture shown instead of the rig (set by the fighter); the rig keeps running underneath for hit points.</summary>
        public SpriteRenderer poseSprite;

        /// <summary>Hide or show every rig sprite without touching their enabled state (sprite-swap groups keep working).</summary>
        public void SetRigVisible(bool visible)
        {
            if (visible == rigVisible) return;
            rigVisible = visible;
            foreach (var r in renderers) if (r != null) r.forceRenderingOff = !visible;
        }
        bool rigVisible = true;

        /// <summary>The colour a sprite would currently show: tint and hit flash applied (same as the rig sprites get).</summary>
        public Color Tinted(Color c)
        {
            var b = c * tint; b.a = c.a * tint.a;
            float k = flashT > 0f && flashDur > 0f ? Mathf.Clamp01(flashT / flashDur) : 0f;
            return Color.Lerp(b, flashColor, k * flashColor.a);
        }

        public void Afterimage(Color c, float life, int order = 5)
        {
            if (poseSprite != null && poseSprite.enabled && poseSprite.sprite != null)
            {
                var go = new GameObject("ghost");
                go.transform.SetPositionAndRotation(poseSprite.transform.position, poseSprite.transform.rotation);
                go.transform.localScale = poseSprite.transform.lossyScale;
                var g = go.AddComponent<SpriteRenderer>();
                g.sprite = poseSprite.sprite; g.sortingOrder = order; g.color = c;
                var a = go.AddComponent<FxAnim>(); a.life = life; a.c0 = c; a.c1 = new Color(c.r, c.g, c.b, 0f);
                a.s0 = a.s1 = go.transform.localScale;
            }
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy || r.sprite == null) continue;
                var go = new GameObject("ghost");
                go.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                go.transform.localScale = r.transform.lossyScale;
                var g = go.AddComponent<SpriteRenderer>();
                g.sprite = r.sprite; g.drawMode = r.drawMode; if (r.drawMode != SpriteDrawMode.Simple) g.size = r.size;
                g.sortingOrder = order; g.color = c;
                var a = go.AddComponent<FxAnim>(); a.life = life; a.c0 = c; a.c1 = new Color(c.r, c.g, c.b, 0f);
                a.s0 = a.s1 = go.transform.localScale;
            }
        }

        // ================================================================== IK
        /// <summary>Turns IK foot targets into thigh/shin angles (2-bone solve, knees bend forward).</summary>
        public static void Resolve(Pose p, Dims d)
        {
            var H = new Vector2(p.hip.x, d.HipBase + p.hip.y);
            if (p.ikF) Solve(d, H, p.a[(int)J.Hips], new Vector2(p.footF.x, d.ankle + p.footF.y), out p.a[(int)J.ThighF], out p.a[(int)J.ShinF]);
            if (p.ikB) Solve(d, H, p.a[(int)J.Hips], new Vector2(p.footB.x, d.ankle + p.footB.y), out p.a[(int)J.ThighB], out p.a[(int)J.ShinB]);
        }

        static void Solve(Dims d, Vector2 H, float hipsA, Vector2 T, out float thigh, out float shin)
        {
            float L1 = d.thigh, L2 = d.shin;
            var dv = T - H;
            float dist = Mathf.Clamp(dv.magnitude, 0.05f, L1 + L2 - 0.002f);
            float td = Mathf.Atan2(dv.x, -dv.y) * Mathf.Rad2Deg;
            float cosA = Mathf.Clamp((L1 * L1 + dist * dist - L2 * L2) / (2f * L1 * dist), -1f, 1f);
            float alpha = Mathf.Acos(cosA) * Mathf.Rad2Deg;
            float tt = td + alpha;
            var K = H + new Vector2(Mathf.Sin(tt * Mathf.Deg2Rad), -Mathf.Cos(tt * Mathf.Deg2Rad)) * L1;
            var ks = T - K;
            float ts = Mathf.Atan2(ks.x, -ks.y) * Mathf.Rad2Deg;
            thigh = tt + hipsA;
            shin = Mathf.DeltaAngle(tt, ts);
        }
    }
}
