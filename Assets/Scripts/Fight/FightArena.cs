using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BalHanuman.Fight
{
    /// <summary>
    /// Placeholder Patala temple arena built from procedural shapes: cavern, lava glow, temple and throne, five sacred lamps,
    /// stone floor, foreground pillars, mist and embers. Each layer parallaxes with the camera. Round moods are done with the global light.
    /// Swap the layers for E-01..E-09 art later (same names).
    /// </summary>
    public class FightArena : MonoBehaviour
    {
        public static readonly float[] LampX = { -8.4f, -4.2f, 0f, 4.2f, 8.4f };
        public readonly List<Lamp> lamps = new List<Lamp>();
        Light2D global;
        Color lightGoal = Color.white; float intensityGoal = 1f;
        readonly List<(Transform t, float k, float x0)> layers = new List<(Transform, float, float)>();
        Transform cam;
        float emberT;

        public class Lamp
        {
            public Transform root; public SpriteRenderer flame, glow, core, pool; public bool lit = true; public float baseScale = 1f, cs = 1f;
            public Vector2 flameSize = new Vector2(0.38f, 0.75f), coreSize = new Vector2(0.2f, 0.42f);   // at rest (the flicker scales these)
        }

        public static FightArena Build(Light2D light)
        {
            var go = new GameObject("Arena");
            var a = go.AddComponent<FightArena>();
            a.global = light;
            a.cam = Camera.main != null ? Camera.main.transform : null;
            a.Make();
            return a;
        }

        Transform Layer(string name, float parallax, float drift = 0f)
        {
            var t = new GameObject(name).transform; t.SetParent(transform, false);
            layers.Add((t, parallax, drift));          // drift: how far (world units) the layer sways side to side on its own
            return t;
        }

        // ---- painted, layered backdrop (Resources/Level02/Arena/bg_*.png): each layer scrolls at its own speed
        bool layeredBg;
        SpriteRenderer shafts;
        static Sprite Bg(string n) => Resources.Load<Sprite>("Level02/Arena/" + n);

        /// <summary>One backdrop strip: `wpp` world units per pixel, standing on `bottomY`, repeated (mirrored on every other copy so the joins are seamless).</summary>
        Transform ArtLayer(string name, Sprite s, float k, float bottomY, float wpp, int order, int copies, float alpha = 1f, float drift = 0f)
        {
            var t = Layer(name, k, drift);
            float sc = wpp * s.pixelsPerUnit, w = s.rect.width * wpp, h = s.rect.height * wpp;
            int half = copies / 2;
            for (int i = -half; i <= half; i++)
                R(t, s, new Vector2(i * w, bottomY + h * 0.5f), new Vector2(((i & 1) != 0 ? -1f : 1f) * sc, sc), new Color(1f, 1f, 1f, alpha), order);
            return t;
        }

        // ---- life in the backdrop: flickering glows, falling lava, rising embers, shimmering lava, lightning
        class Glow { public SpriteRenderer sr; public float a, speed, phase; public Vector3 baseScale; }
        readonly List<Glow> glows = new List<Glow>();
        Transform lyStruct, lySea;
        SpriteRenderer lightning; float lightningT = 6f, lightningAge = 99f;
        float emberBgT, fallBgT;
        static readonly float[] SeaEmberX = { -17f, -9f, -2f, 6f, 13f, 20f };
        float struW, seaW;      // layer widths in world units (for placing effects)

        /// <summary>World position, inside a layer, of pixel (px, py) of its picture, on the given (mirrored or not) copy.</summary>
        static Vector2 LayerPoint(Sprite s, int copy, float wpp, float bottomY, float px, float py)
        {
            float w = s.rect.width * wpp;
            float flip = (copy & 1) != 0 ? -1f : 1f;
            return new Vector2(copy * w + (px - s.rect.width * 0.5f) * wpp * flip, bottomY + (s.rect.height - py) * wpp);
        }

        void AddGlow(Transform layer, Sprite s, float wpp, float bottomY, int order, float px, float py, float size, Color c, float speed)
        {
            for (int i = -1; i <= 1; i++)
            {
                var sr = R(layer, FightArt.Soft, LayerPoint(s, i, wpp, bottomY, px, py), Vector2.one * size, c, order);
                glows.Add(new Glow { sr = sr, a = c.a, speed = speed * Random.Range(0.8f, 1.25f), phase = Random.value * 6.28f, baseScale = sr.transform.localScale });
            }
        }

        bool BuildLayers(Transform far)
        {
            var sky = Bg("bg_sky_cavern"); var sea = Bg("bg_lava_sea"); var fs = Bg("bg_far_structures"); var mid = Bg("bg_mid_ruins");
            if (sky == null || sea == null || fs == null || mid == null) return false;
            // cavern ceiling: follows the camera up and down at half speed ('E01_Far'), sits deep behind everything
            float skw = 0.0235f, skh = sky.rect.height * skw;
            R(far, FightArt.Square, new Vector2(0, 6f), new Vector2(160f, 80f), new Color(0.07f, 0.03f, 0.14f), -201);
            R(far, sky, new Vector2(0, -6f + skh * 0.5f), new Vector2(skw * sky.pixelsPerUnit, skw * sky.pixelsPerUnit), Color.white, -200);
            lyStruct = ArtLayer("B2_Structures", fs, 0.70f, -0.4f, 0.0165f, -190, 3);   // giant guardians, temple, bridge
            lySea = ArtLayer("B3_LavaSea", sea, 0.80f, -0.4f, 0.0115f, -185, 3);        // lava sea and shore in front of them
            var lyRuins = ArtLayer("B4_Ruins", mid, 0.45f, -0.3f, 0.0165f, -150, 3);    // columns, arches, braziers
            struW = fs.rect.width * 0.0165f; seaW = sea.rect.width * 0.0115f;
            ArtLayer("B3b_LavaShimmer", sea, 0.80f, -0.4f, 0.0115f, -184, 3, 0.22f, -0.3f);   // the same sea, wobbling a little: heat haze
            // glowing details: the braziers, the temple door, lanterns, the guardians' eyes, and pulses along the lava rivers
            var warm = new Color(1f, 0.55f, 0.18f, 0.55f); var violet = new Color(0.78f, 0.35f, 1f, 0.6f);
            AddGlow(lyRuins, mid, 0.0165f, -0.3f, -149, 335f, 500f, 1.5f, violet, 2.6f);
            AddGlow(lyRuins, mid, 0.0165f, -0.3f, -149, 1668f, 505f, 1.5f, violet, 2.3f);
            AddGlow(lyStruct, fs, 0.0165f, -0.4f, -189, 1512f, 490f, 1.7f, warm, 1.1f);
            AddGlow(lyStruct, fs, 0.0165f, -0.4f, -189, 1085f, 505f, 0.7f, warm, 3.1f);
            AddGlow(lyStruct, fs, 0.0165f, -0.4f, -189, 1268f, 505f, 0.7f, warm, 2.8f);
            foreach (float ex in new[] { 222f, 252f, 1757f, 1790f })
                AddGlow(lyStruct, fs, 0.0165f, -0.4f, -189, ex, 262f, 0.32f, new Color(1f, 0.7f, 0.3f, 0.9f), 1.7f);
            foreach (float rx in new[] { 160f, 620f, 1380f, 1880f })
                AddGlow(lySea, sea, 0.0115f, -0.4f, -184, rx, 590f, 3.2f, new Color(1f, 0.45f, 0.1f, 0.30f), 0.9f);
            // faint lightning behind the far structures
            lightning = R(far, FightArt.Square, new Vector2(0, 6f), new Vector2(160f, 80f), new Color(0.75f, 0.55f, 1f, 0f), -199);
            var sh = Bg("bg_light_shafts");
            if (sh != null)
            {
                var t = Layer("B5_Shafts", 0.95f); float w = 0.021f;   // tall enough that the top edge is always above the screen
                shafts = R(t, sh, new Vector2(0, -2f + sh.rect.height * w * 0.5f), new Vector2(w * sh.pixelsPerUnit, w * sh.pixelsPerUnit), new Color(1f, 1f, 1f, 0.16f), -95);
            }
            return true;
        }

        static SpriteRenderer R(Transform p, Sprite s, Vector2 pos, Vector2 size, Color c, int order, float rot = 0f) => FightArt.Quad("p", p, s, pos, size, c, order, rot);

        void Make()
        {
            var rng = new System.Random(7);
            float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // ---- sky / cavern backdrop (moves almost with the camera)
            var far = Layer("E01_Far", 0.85f);
            if (BuildLayers(far)) { layeredBg = true; BuildNear(rng); return; }
            // painted cavern (Resources/Level02/Arena/arena_far.png). Its dark ground line sits just under the floor's top edge.
            var art = Resources.Load<Sprite>("Level02/Arena/arena_far");
            if (art != null)
            {
                const float width = 28f;                                   // world units across
                float k = width / art.bounds.size.x;
                R(far, FightArt.Square, new Vector2(0, 6f), new Vector2(120, 60), new Color(0.09f, 0.04f, 0.17f), -201);   // fills past the picture's edges
                float groundFrac = 0.17f;                                   // the ground line is 17% up from the bottom of the picture
                float h = art.bounds.size.y * k;
                float cy = -1.4f + h * (0.5f - groundFrac);
                R(far, art, new Vector2(0, cy), new Vector2(k, k), Color.white, -200);
                foreach (float sx in new[] { -width, width })      // mirrored copies carry the cavern on when the camera pulls right back
                    R(far, art, new Vector2(sx, cy), new Vector2(-k, k), Color.white, -200);
                BuildNear(rng);
                return;
            }
            R(far, FightArt.Square, new Vector2(0, 6f), new Vector2(80, 30), new Color(0.06f, 0.04f, 0.12f), -200);
            R(far, FightArt.Gradient, new Vector2(0, -6f), new Vector2(80, 14f), new Color(0.55f, 0.18f, 0.12f, 0.85f), -199);
            R(far, FightArt.Gradient, new Vector2(0, -4f), new Vector2(80, 9f), new Color(0.35f, 0.12f, 0.45f, 0.6f), -198);
            for (int i = 0; i < 26; i++)      // stalactites hanging from the cavern roof
            {
                float x = -30f + i * 2.4f + Rand(-0.6f, 0.6f), h = Rand(2.5f, 6.5f);
                R(far, FightArt.Tri, new Vector2(x, 13.5f), new Vector2(Rand(1.2f, 2.4f), h), new Color(0.1f, 0.07f, 0.16f), -197, 180f);
            }
            for (int i = 0; i < 7; i++)       // distant demon statues on the cliffs
            {
                float x = -24f + i * 8f + Rand(-1f, 1f), h = Rand(4f, 7f);
                R(far, FightArt.Capsule(1.6f), new Vector2(x, 0.5f + h), new Vector2(1, 1), new Color(0.13f, 0.09f, 0.2f), -196).drawMode = SpriteDrawMode.Sliced;
                var st = far.GetChild(far.childCount - 1).GetComponent<SpriteRenderer>(); st.size = new Vector2(1.6f, h);
                R(far, FightArt.Circle, new Vector2(x, 0.9f + h + 0.6f), new Vector2(1.5f, 1.5f), new Color(0.13f, 0.09f, 0.2f), -196);
                R(far, FightArt.Soft, new Vector2(x + 0.2f, 0.9f + h + 0.7f), new Vector2(0.5f, 0.3f), new Color(1f, 0.3f, 0.2f, 0.7f), -195);
            }
            for (int i = 0; i < 9; i++) R(far, FightArt.Soft, new Vector2(-28f + i * 7f, -1.5f), new Vector2(Rand(6f, 10f), Rand(2f, 3.5f)), new Color(1f, 0.38f, 0.1f, 0.35f), -194);

            BuildNear(rng, true);
        }

        void BuildNear(System.Random rng, bool temple = false)
        {
            float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            // ---- temple layer (only with the placeholder cavern: the painted one has its own temple and statues)
            var gold = new Color(0.55f, 0.4f, 0.17f);
            var stone = new Color(0.11f, 0.08f, 0.16f); var stoneLit = new Color(0.17f, 0.12f, 0.23f);
            if (temple)
            {
            var mid = Layer("E02_Temple", 0.45f);
            R(mid, FightArt.Square, new Vector2(0, 2.2f), new Vector2(46f, 4.4f), new Color(0.1f, 0.08f, 0.15f), -150);
            for (int i = -6; i <= 6; i++)     // colonnade
            {
                float x = i * 3.6f;
                R(mid, FightArt.Square, new Vector2(x, 3.2f), new Vector2(0.9f, 6.4f), i % 2 == 0 ? stoneLit : stone, -148);
                R(mid, FightArt.Square, new Vector2(x, 6.5f), new Vector2(1.3f, 0.35f), gold, -147);
                R(mid, FightArt.Square, new Vector2(x, 0.2f), new Vector2(1.3f, 0.4f), gold, -147);
                if (i % 2 == 0) R(mid, FightArt.Soft, new Vector2(x + 1.8f, 4.8f), new Vector2(1.2f, 1.8f), new Color(0.75f, 0.3f, 1f, 0.3f), -146);
            }
            R(mid, FightArt.Square, new Vector2(0, 7f), new Vector2(46f, 0.8f), stoneLit, -147);
            for (int i = 0; i < 12; i++) R(mid, FightArt.Tri, new Vector2(-19.8f + i * 3.6f, 7.4f), new Vector2(1.4f, 1.6f), stone, -147);
            // throne and steps
            for (int s = 0; s < 3; s++) R(mid, FightArt.Square, new Vector2(0, 0.25f + s * 0.4f), new Vector2(6.5f - s * 1.4f, 0.4f), s % 2 == 0 ? stoneLit : stone, -145);
            R(mid, FightArt.Square, new Vector2(0, 2.8f), new Vector2(2.6f, 3.4f), new Color(0.2f, 0.08f, 0.26f), -144);
            R(mid, FightArt.Tri, new Vector2(0, 4.5f), new Vector2(3.2f, 2.2f), new Color(0.4f, 0.28f, 0.14f), -143);
            R(mid, FightArt.Circle, new Vector2(0, 5.2f), new Vector2(0.6f, 0.6f), new Color(0.9f, 0.4f, 1f), -142);
            R(mid, FightArt.Soft, new Vector2(0, 3.2f), new Vector2(6f, 6f), new Color(0.6f, 0.2f, 0.9f, 0.35f), -141);
            // torn banners
            foreach (float bx in new[] { -11f, -6.5f, 6.5f, 11f })
            {
                R(mid, FightArt.Capsule(1.1f), new Vector2(bx, 6.3f), Vector2.one, new Color(0.26f, 0.06f, 0.22f), -146).drawMode = SpriteDrawMode.Sliced;
                mid.GetChild(mid.childCount - 1).GetComponent<SpriteRenderer>().size = new Vector2(1.1f, 3.4f);
            }
            }

            // ---- painted props: carved pillars behind the fight (kept dark so the fighters stay readable) and two braziers at the edges
            var pillarArt = Resources.Load<Sprite>("Level02/Arena/pillar_intact");
            if (!temple && !layeredBg && pillarArt != null)
            {
                var pl = Layer("E02_Pillars", 0.45f);
                float pk = 6.8f / pillarArt.bounds.size.y;
                foreach (float px in new[] { -19f, -10.5f, 10.5f, 19f })
                    R(pl, pillarArt, new Vector2(px, 0.1f + 3.4f), new Vector2(pk, pk), new Color(0.62f, 0.56f, 0.72f), -148);
            }
            var braziers = Resources.Load<Sprite>("Level02/Arena/brazier");
            if (!temple && braziers != null)
            {
                var bl = Layer("E04_Braziers", 0.2f);
                float bk = 1.05f / braziers.bounds.size.y;
                foreach (float bx in new[] { -12.6f, 12.6f })
                {
                    R(bl, braziers, new Vector2(bx, 0.05f + 0.525f), new Vector2(bk, bk), Color.white, -121);
                    R(bl, FightArt.Soft, new Vector2(bx, 1.05f), new Vector2(2.2f, 1.6f), new Color(0.75f, 0.3f, 1f, 0.45f), -118);
                }
            }

            // ---- five sacred lamps (in front of the temple, behind the fighters)
            var lampLayer = Layer("E03_Lamps", 0.2f);
            var lampArt = Resources.Load<Sprite>("Level02/Arena/lamp_stand");
            var flameRaw = Resources.Load<Sprite>("Level02/Arena/lamp_flame");
            // same picture with its pivot at the base of the flame, so it grows upwards when it flickers
            var flameArt = flameRaw != null ? Sprite.Create(flameRaw.texture, flameRaw.rect, new Vector2(0.5f, 0f), flameRaw.pixelsPerUnit) : null;
            float[] lampShift = { -0.45f, 0.05f, 0.25f, 0.35f, -0.3f };          // uneven spacing, so the row doesn't read as a fence
            float[] lampScale = { 1.08f, 0.86f, 0.92f, 0.86f, 1.08f };           // outer pair nearer (bigger), inner three further back
            float[] lampHaze = { 1f, 0.82f, 0.78f, 0.82f, 1f };                   // further lamps are a little darker and hazier
            for (int li = 0; li < LampX.Length; li++)
            {
                float lx = LampX[li];
                var lamp = new Lamp { root = new GameObject("Lamp").transform };
                lamp.root.SetParent(lampLayer, false);
                bool painted = lampArt != null;
                lamp.root.localPosition = new Vector2(lx + (painted ? lampShift[li] : 0f), 0f);
                if (painted) lamp.root.localScale = Vector3.one * lampScale[li];
                if (lampArt != null)
                {   // painted lamp stand: the bowl is at about 94% of its height, where the flame burns
                    float lk = 2.6f / lampArt.bounds.size.y;
                    R(lamp.root, lampArt, new Vector2(0, 1.3f), new Vector2(lk, lk), new Color(lampHaze[li], lampHaze[li], Mathf.Min(1f, lampHaze[li] + 0.06f)), -120);
                }
                else
                {
                    R(lamp.root, FightArt.Square, new Vector2(0, 0.08f), new Vector2(0.9f, 0.16f), gold, -120);
                    R(lamp.root, FightArt.Capsule(0.16f), new Vector2(0, 2.3f), Vector2.one, gold, -120).drawMode = SpriteDrawMode.Sliced;
                    lamp.root.GetChild(lamp.root.childCount - 1).GetComponent<SpriteRenderer>().size = new Vector2(0.16f, 2.3f);
                    R(lamp.root, FightArt.Circle, new Vector2(0, 1.2f), new Vector2(0.4f, 0.2f), gold, -119);
                    R(lamp.root, FightArt.Circle, new Vector2(0, 2.35f), new Vector2(0.9f, 0.32f), new Color(0.85f, 0.62f, 0.25f), -119);
                }
                lamp.glow = R(lamp.root, FightArt.Soft, new Vector2(0, 2.8f), new Vector2(2.6f, 2.6f), new Color(0.8f, 0.35f, 1f, 0.55f), -118);
                lamp.flame = R(lamp.root, FightArt.Tri, new Vector2(0, 2.42f), new Vector2(0.38f, 0.75f), new Color(0.85f, 0.45f, 1f), -117);
                lamp.core = R(lamp.root, FightArt.Tri, new Vector2(0, 2.44f), new Vector2(0.2f, 0.42f), new Color(1f, 0.9f, 1f), -116);
                if (flameArt != null)
                {   // painted flame (white-hot base): the tall one is the flame, a smaller copy on top brightens its heart
                    float fk = 0.9f / flameArt.bounds.size.y;
                    lamp.flame.sprite = flameArt; lamp.flame.color = Color.white; lamp.flame.transform.localPosition = new Vector3(0, 2.36f, 0);
                    lamp.core.sprite = flameArt; lamp.core.color = new Color(1f, 0.95f, 1f, 0.7f); lamp.core.transform.localPosition = new Vector3(0, 2.38f, 0);
                    lamp.flameSize = new Vector2(fk, fk); lamp.coreSize = new Vector2(fk * 0.55f, fk * 0.55f);
                    lamp.flame.transform.localScale = new Vector3(fk, fk, 1); lamp.core.transform.localScale = new Vector3(fk * 0.55f, fk * 0.55f, 1);
                }
                if (painted)   // violet light pooling on the floor beneath each lit lamp
                    lamp.pool = R(lamp.root, FightArt.Soft, new Vector2(0, 0.06f), new Vector2(3.4f, 0.8f), new Color(0.75f, 0.35f, 1f, 0.38f), -98);
                lamps.Add(lamp);
            }

            // ---- floor (fixed to the world)
            var floor = new GameObject("E05_Floor").transform; floor.SetParent(transform, false);
            var floorArt = Resources.Load<Sprite>("Level02/Arena/floor_intact");
            if (floorArt != null)
            {   // painted floor strip: its gold top rail is the walk line (y 0); deeper zoom-outs see the dark stone below it
                float stripH = floorArt.bounds.size.y;
                R(floor, FightArt.Square, new Vector2(0, -3.0f), new Vector2(80f, 6f), new Color(0.08f, 0.05f, 0.12f), -100);
                var strip = R(floor, floorArt, new Vector2(0, -0.03f - stripH * 0.5f), Vector2.one, Color.white, -99);
                strip.drawMode = SpriteDrawMode.Tiled; strip.tileMode = SpriteTileMode.Continuous; strip.size = new Vector2(floorArt.bounds.size.x * 4f, stripH);
                goto floorDone;
            }
            R(floor, FightArt.Square, new Vector2(0, -3.0f), new Vector2(60f, 6f), new Color(0.09f, 0.07f, 0.12f), -100);
            R(floor, FightArt.Square, new Vector2(0, -0.05f), new Vector2(60f, 0.1f), new Color(0.75f, 0.55f, 0.22f), -99);
            for (int i = -14; i <= 14; i++)
            {
                R(floor, FightArt.Square, new Vector2(i * 2f, -0.8f), new Vector2(0.05f, 1.5f), new Color(0.05f, 0.04f, 0.08f), -99);
                R(floor, FightArt.Circle, new Vector2(i * 2f + 1f, -0.55f), new Vector2(0.9f, 0.18f), new Color(0.6f, 0.42f, 0.18f, 0.35f), -99);
            }
            R(floor, FightArt.Square, new Vector2(0, -1.6f), new Vector2(60f, 0.06f), new Color(0.05f, 0.04f, 0.08f), -99);
            R(floor, FightArt.Gradient, new Vector2(0, -1.2f), new Vector2(60f, 1.5f), new Color(0.75f, 0.3f, 1f, 0.12f), -98);
            floorDone:

            // ---- foreground pillars and chains (move faster than the world)
            var fg = Layer("E06_Foreground", -0.3f);
            var fgArt = Resources.Load<Sprite>("Level02/Arena/fg_pillar");
            if (fgArt != null)
            {   // painted foreground pillar, soft and dark; only seen at the screen edges when the camera pulls back
                float fk = 12f / fgArt.bounds.size.y;
                foreach (float px in new[] { -18.5f, 18.5f })
                    R(fg, fgArt, new Vector2(px, 2f), new Vector2(px < 0 ? fk : -fk, fk), Color.white, 200);
            }
            else
            foreach (float px in new[] { -17f, 17f })
            {
                R(fg, FightArt.Square, new Vector2(px, 4f), new Vector2(2.4f, 14f), new Color(0.03f, 0.02f, 0.05f), 200);
                R(fg, FightArt.Square, new Vector2(px + Mathf.Sign(-px) * 1.15f, 4f), new Vector2(0.12f, 14f), new Color(0.6f, 0.3f, 0.2f, 0.6f), 201);
                for (int c = 0; c < 9; c++) R(fg, FightArt.Ring, new Vector2(px + Mathf.Sign(-px) * 2.2f, 9f - c * 0.45f), new Vector2(0.35f, 0.55f), new Color(0.04f, 0.03f, 0.06f), 200);
            }
            // low mist
            var mistArt = layeredBg ? Bg("bg_mist") : null;
            if (mistArt != null)
            {
                ArtLayer("E09_Mist", mistArt, 0.1f, -0.2f, 0.010f, -90, 3, 0.26f, 1.2f);        // drifts slowly side to side
                ArtLayer("E09_Mist2", mistArt, 0.22f, -0.1f, 0.0135f, -89, 3, 0.14f, 2.0f);     // a second, slower bank at another depth
            }
            var rocksArt = layeredBg ? Bg("bg_near_rocks") : null;
            if (rocksArt != null)
            {   // rock outcrops with violet crystals frame the far ends of the arena (only the two edge clusters, one each side)
                var rl = Layer("E07_NearRocks", -0.12f);
                float rs = 0.009f;
                foreach (var (rx, cx) in new[] { (0f, -14.2f), (1240f, 14.2f) })
                {
                    var part = Sprite.Create(rocksArt.texture, new Rect(rocksArt.rect.x + rx, rocksArt.rect.y, 760f, rocksArt.rect.height), new Vector2(0.5f, 0f), 100f);
                    R(rl, part, new Vector2(cx, -0.35f), new Vector2(rs * 100f, rs * 100f), Color.white, -98);
                }
            }
            if (mistArt != null) return;
            var mist = Layer("E09_Mist", 0.1f);
            for (int i = 0; i < 8; i++) R(mist, FightArt.Soft, new Vector2(-21f + i * 6f, 0.3f), new Vector2(9f, 1.6f), new Color(0.6f, 0.4f, 0.85f, temple ? 0.18f : 0.05f), -90);   // the painted cavern has its own haze
        }

        void LateUpdate()
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (cam != null)
                foreach (var l in layers) l.t.position = new Vector3(cam.position.x * l.k + (l.x0 > 0f ? Mathf.Sin(Time.time * 0.12f) * l.x0 : l.x0 < 0f ? Mathf.Sin(Time.time * 0.9f) * -l.x0 : 0f), l.t.name == "E01_Far" ? cam.position.y * 0.5f : 0f, 0);

            float t = Time.time;
            if (shafts != null)
            {
                var sc2 = shafts.color; sc2.a = 0.14f + 0.04f * Mathf.Sin(t * 0.6f); shafts.color = sc2;
                shafts.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 0.25f) * 1.6f);       // the light slowly sways
            }
            foreach (var g in glows)
            {
                if (g.sr == null) continue;
                float f = 0.78f + 0.22f * Mathf.Sin(t * g.speed + g.phase) + 0.08f * Mathf.Sin(t * g.speed * 2.7f + g.phase * 2f);
                var gc = g.sr.color; gc.a = g.a * f; g.sr.color = gc;
                g.sr.transform.localScale = g.baseScale * (0.92f + 0.16f * f);
            }
            if (lightning != null)
            {
                lightningT -= Time.deltaTime;
                if (lightningT <= 0f) { lightningAge = 0f; lightningT = Random.Range(6f, 13f); }
                lightningAge += Time.deltaTime;
                float fl = lightningAge < 0.35f ? Mathf.Max(0f, Mathf.Sin(lightningAge * 40f)) * (1f - lightningAge / 0.35f) : 0f;
                var lc = lightning.color; lc.a = 0.16f * fl; lightning.color = lc;
            }
            if (lyStruct != null)
            {   // lava pours off the two cliffs, embers lift off the lava sea
                fallBgT -= Time.deltaTime;
                if (fallBgT <= 0f)
                {
                    fallBgT = 0.07f;
                    var fsp = Bg("bg_far_structures");
                    foreach (var (fx, fyTop) in new[] { (228f, 452f), (1822f, 482f) })
                        for (int i = -1; i <= 1; i++)
                        {
                            var pt = LayerPoint(fsp, i, 0.0165f, -0.4f, fx + Random.Range(-9f, 9f), fyTop);
                            BgSpark.Spawn(lyStruct, pt, new Vector2(0f, -Random.Range(1.8f, 2.8f)), Random.Range(0.7f, 1.1f), 0.14f, new Color(1f, 0.7f, 0.3f, 0.85f), 0f, -189);
                        }
                }
                emberBgT -= Time.deltaTime;
                if (emberBgT <= 0f && lySea != null)
                {
                    emberBgT = 0.12f;
                    float ex = SeaEmberX[Random.Range(0, SeaEmberX.Length)] + Random.Range(-3f, 3f);
                    BgSpark.Spawn(lySea, new Vector2(ex, -0.1f + Random.Range(0f, 0.7f)), new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.5f, 1.1f)), Random.Range(3f, 5f), Random.Range(0.06f, 0.12f), new Color(1f, 0.6f, 0.2f, 0.9f), Random.Range(0.1f, 0.4f), -183);
                }
            }
            for (int i = 0; i < lamps.Count; i++)
            {
                var lp = lamps[i];
                if (!lp.lit) continue;
                // slow, soft breathing: smooth noise plus a gentle swell, a slight sway from the base, and flares that ease in and out
                float f = 1f + (Mathf.PerlinNoise(t * 0.9f + i * 11.3f, 0.5f) - 0.5f) * 0.14f + Mathf.Sin(t * 2.2f + i * 1.7f) * 0.025f;
                float sway = (Mathf.PerlinNoise(t * 0.7f + i * 5.1f, 3.7f) - 0.5f) * 7f;
                lp.cs = Mathf.Lerp(lp.cs, lp.baseScale, 1f - Mathf.Exp(-6f * Time.deltaTime));
                float w = 1f - (f - 1f) * 0.35f;
                lp.flame.transform.localScale = new Vector3(lp.flameSize.x * w * lp.cs, lp.flameSize.y * f * lp.cs, 1);
                lp.flame.transform.localRotation = Quaternion.Euler(0, 0, sway);
                lp.core.transform.localScale = new Vector3(lp.coreSize.x * lp.cs, lp.coreSize.y * (1f + (f - 1f) * 0.8f) * lp.cs, 1);
                lp.core.transform.localRotation = Quaternion.Euler(0, 0, sway * 0.6f);
                lp.glow.transform.localScale = Vector3.one * 2.6f * (0.93f + 0.07f * f) * lp.cs;
                if (lp.pool != null) { var pc = lp.pool.color; pc.a = 0.3f + 0.1f * f * lp.cs; lp.pool.color = pc; }
            }
            emberT -= Time.deltaTime;
            if (emberT <= 0f && cam != null)
            {
                emberT = 0.08f;
                var p = new Vector3(cam.position.x + Random.Range(-12f, 12f), -0.5f, 0);
                var c = Color.Lerp(new Color(1f, 0.6f, 0.2f), new Color(0.85f, 0.4f, 1f), Random.value);
                var a = FightFx.Spawn(FightArt.Soft, p, Random.Range(3f, 5f), Vector2.one * 0.12f, Vector2.one * 0.05f, c, new Color(c.r, c.g, c.b, 0f), -60);
                a.vel = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.8f, 1.6f)); a.easeOut = false;
            }
            if (global != null)
            {
                global.color = Color.Lerp(global.color, lightGoal, Time.unscaledDeltaTime * 1.5f);
                global.intensity = Mathf.Lerp(global.intensity, intensityGoal, Time.unscaledDeltaTime * 1.5f);
            }
        }

        /// <summary>0 duel (warm), 1 sorcery (violet), 2 titans (red), 3 finale (gold), 4 dark (ultimate build-up).</summary>
        public void Mood(int m)
        {
            switch (m)
            {
                case 0: lightGoal = new Color(1f, 0.93f, 0.85f); intensityGoal = 1f; break;
                case 1: lightGoal = new Color(0.82f, 0.72f, 1f); intensityGoal = 0.95f; break;
                case 2: lightGoal = new Color(1f, 0.72f, 0.66f); intensityGoal = 0.95f; break;
                case 3: lightGoal = new Color(1f, 0.92f, 0.7f); intensityGoal = 1.15f; break;
                case 4: lightGoal = new Color(0.55f, 0.5f, 0.7f); intensityGoal = 0.6f; break;
            }
        }

        public void FlareLamps(float k) { foreach (var l in lamps) if (l.lit) l.baseScale = k; }

        public Vector3 LampFlamePos(int i) => lamps[i].flame.transform.position + Vector3.up * 0.2f;

        public void Extinguish(int i)
        {
            var l = lamps[i];
            if (!l.lit) return;
            l.lit = false;
            FightFx.Smoke(LampFlamePos(i), 6, new Color(0.85f, 0.8f, 0.95f, 0.6f), 0.8f);
            FightFx.Hit(LampFlamePos(i), FxKind.Gold, true, 0f);
            l.flame.enabled = l.core.enabled = false; l.glow.color = new Color(1f, 0.85f, 0.4f, 0.15f); if (l.pool != null) l.pool.color = new Color(0.75f, 0.35f, 1f, 0.04f);
        }

        public void RelightAll()
        {
            foreach (var l in lamps) { l.lit = true; l.baseScale = 1f; l.flame.enabled = l.core.enabled = true; l.glow.color = new Color(0.8f, 0.35f, 1f, 0.55f); if (l.pool != null) l.pool.color = new Color(0.75f, 0.35f, 1f, 0.38f); }
        }
    }

    /// <summary>A tiny glowing fleck that lives inside a backdrop layer (so it scrolls with that layer's depth): lava drops, embers.</summary>
    public class BgSpark : MonoBehaviour
    {
        Vector2 vel; float life, age, sway, phase; Color c0; SpriteRenderer sr;

        public static void Spawn(Transform layer, Vector2 localPos, Vector2 vel, float life, float size, Color c, float sway, int order)
        {
            var go = new GameObject("spark"); go.transform.SetParent(layer, false); go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * size;
            var sp = go.AddComponent<BgSpark>();
            sp.sr = go.AddComponent<SpriteRenderer>(); sp.sr.sprite = FightArt.Soft; sp.sr.sortingOrder = order; sp.sr.color = new Color(c.r, c.g, c.b, 0f);
            sp.vel = vel; sp.life = life; sp.sway = sway; sp.c0 = c; sp.phase = Random.value * 6.28f;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= life) { Destroy(gameObject); return; }
            var p = transform.localPosition + (Vector3)(vel * Time.deltaTime);
            p.x += Mathf.Sin(age * 2f + phase) * sway * Time.deltaTime;
            transform.localPosition = p;
            var c = c0; c.a = c0.a * Mathf.Sin(age / life * Mathf.PI);
            sr.color = c;
        }
    }

}
