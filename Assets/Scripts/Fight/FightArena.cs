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
            public Transform root; public SpriteRenderer flame, glow, core; public bool lit = true; public float baseScale = 1f;
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

        Transform Layer(string name, float parallax)
        {
            var t = new GameObject(name).transform; t.SetParent(transform, false);
            layers.Add((t, parallax, 0f));
            return t;
        }

        static SpriteRenderer R(Transform p, Sprite s, Vector2 pos, Vector2 size, Color c, int order, float rot = 0f) => FightArt.Quad("p", p, s, pos, size, c, order, rot);

        void Make()
        {
            var rng = new System.Random(7);
            float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // ---- sky / cavern backdrop (moves almost with the camera)
            var far = Layer("E01_Far", 0.85f);
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

            // ---- temple layer
            var mid = Layer("E02_Temple", 0.45f);
            var stone = new Color(0.11f, 0.08f, 0.16f); var stoneLit = new Color(0.17f, 0.12f, 0.23f); var gold = new Color(0.55f, 0.4f, 0.17f);
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

            // ---- five sacred lamps (in front of the temple, behind the fighters)
            var lampLayer = Layer("E03_Lamps", 0.2f);
            foreach (float lx in LampX)
            {
                var lamp = new Lamp { root = new GameObject("Lamp").transform };
                lamp.root.SetParent(lampLayer, false); lamp.root.localPosition = new Vector2(lx, 0f);
                R(lamp.root, FightArt.Square, new Vector2(0, 0.08f), new Vector2(0.9f, 0.16f), gold, -120);
                R(lamp.root, FightArt.Capsule(0.16f), new Vector2(0, 2.3f), Vector2.one, gold, -120).drawMode = SpriteDrawMode.Sliced;
                lamp.root.GetChild(lamp.root.childCount - 1).GetComponent<SpriteRenderer>().size = new Vector2(0.16f, 2.3f);
                R(lamp.root, FightArt.Circle, new Vector2(0, 1.2f), new Vector2(0.4f, 0.2f), gold, -119);
                R(lamp.root, FightArt.Circle, new Vector2(0, 2.35f), new Vector2(0.9f, 0.32f), new Color(0.85f, 0.62f, 0.25f), -119);
                lamp.glow = R(lamp.root, FightArt.Soft, new Vector2(0, 2.8f), new Vector2(2.6f, 2.6f), new Color(0.8f, 0.35f, 1f, 0.55f), -118);
                lamp.flame = R(lamp.root, FightArt.Tri, new Vector2(0, 2.42f), new Vector2(0.38f, 0.75f), new Color(0.85f, 0.45f, 1f), -117);
                lamp.core = R(lamp.root, FightArt.Tri, new Vector2(0, 2.44f), new Vector2(0.2f, 0.42f), new Color(1f, 0.9f, 1f), -116);
                lamps.Add(lamp);
            }

            // ---- floor (fixed to the world)
            var floor = new GameObject("E05_Floor").transform; floor.SetParent(transform, false);
            R(floor, FightArt.Square, new Vector2(0, -3.0f), new Vector2(60f, 6f), new Color(0.09f, 0.07f, 0.12f), -100);
            R(floor, FightArt.Square, new Vector2(0, -0.05f), new Vector2(60f, 0.1f), new Color(0.75f, 0.55f, 0.22f), -99);
            for (int i = -14; i <= 14; i++)
            {
                R(floor, FightArt.Square, new Vector2(i * 2f, -0.8f), new Vector2(0.05f, 1.5f), new Color(0.05f, 0.04f, 0.08f), -99);
                R(floor, FightArt.Circle, new Vector2(i * 2f + 1f, -0.55f), new Vector2(0.9f, 0.18f), new Color(0.6f, 0.42f, 0.18f, 0.35f), -99);
            }
            R(floor, FightArt.Square, new Vector2(0, -1.6f), new Vector2(60f, 0.06f), new Color(0.05f, 0.04f, 0.08f), -99);
            R(floor, FightArt.Gradient, new Vector2(0, -1.2f), new Vector2(60f, 1.5f), new Color(0.75f, 0.3f, 1f, 0.12f), -98);

            // ---- foreground pillars and chains (move faster than the world)
            var fg = Layer("E06_Foreground", -0.3f);
            foreach (float px in new[] { -17f, 17f })
            {
                R(fg, FightArt.Square, new Vector2(px, 4f), new Vector2(2.4f, 14f), new Color(0.03f, 0.02f, 0.05f), 200);
                R(fg, FightArt.Square, new Vector2(px + Mathf.Sign(-px) * 1.15f, 4f), new Vector2(0.12f, 14f), new Color(0.6f, 0.3f, 0.2f, 0.6f), 201);
                for (int c = 0; c < 9; c++) R(fg, FightArt.Ring, new Vector2(px + Mathf.Sign(-px) * 2.2f, 9f - c * 0.45f), new Vector2(0.35f, 0.55f), new Color(0.04f, 0.03f, 0.06f), 200);
            }
            // low mist
            var mist = Layer("E09_Mist", 0.1f);
            for (int i = 0; i < 8; i++) R(mist, FightArt.Soft, new Vector2(-21f + i * 6f, 0.3f), new Vector2(9f, 1.6f), new Color(0.6f, 0.4f, 0.85f, 0.18f), -90);
        }

        void LateUpdate()
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (cam != null)
                foreach (var l in layers) l.t.position = new Vector3(cam.position.x * l.k, l.t.name == "E01_Far" ? cam.position.y * 0.5f : 0f, 0);

            float t = Time.time;
            for (int i = 0; i < lamps.Count; i++)
            {
                var lp = lamps[i];
                if (!lp.lit) continue;
                float f = 1f + Mathf.Sin(t * 13f + i * 1.7f) * 0.08f + Mathf.Sin(t * 23f + i) * 0.05f;
                lp.flame.transform.localScale = new Vector3(0.38f * (2f - f) * lp.baseScale, 0.75f * f * lp.baseScale, 1);
                lp.core.transform.localScale = new Vector3(0.2f * lp.baseScale, 0.42f * f * lp.baseScale, 1);
                lp.glow.transform.localScale = Vector3.one * 2.6f * (0.9f + 0.1f * f) * lp.baseScale;
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
            l.flame.enabled = l.core.enabled = false; l.glow.color = new Color(1f, 0.85f, 0.4f, 0.15f);
        }

        public void RelightAll()
        {
            foreach (var l in lamps) { l.lit = true; l.baseScale = 1f; l.flame.enabled = l.core.enabled = true; l.glow.color = new Color(0.8f, 0.35f, 1f, 0.55f); }
        }
    }
}
