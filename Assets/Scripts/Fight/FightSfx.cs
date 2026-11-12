using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalHanuman.Fight
{
    /// <summary>Fight sounds. Loads Resources/Audio/Level02/&lt;name&gt; when the real file exists, otherwise synthesises a placeholder.
    /// Names match the S-xx list in Docs/Level02_AssetPrompts.md (sfx_gada_whoosh -> "whoosh" etc.).</summary>
    public static class FightSfx
    {
        const int Rate = 22050;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static AudioSource[] pool; static int next;
        public static float Volume = 0.7f;

        public static void Play(string name, float pitch = 1f, float vol = 1f, float pitchJitter = 0.06f)
        {
            if (pool == null || pool[0] == null)
            {
                var go = new GameObject("FightSfx");
                pool = new AudioSource[10];
                for (int i = 0; i < pool.Length; i++) { pool[i] = go.AddComponent<AudioSource>(); pool[i].playOnAwake = false; }
            }
            if (!clips.TryGetValue(name, out var c) || c == null)
            {
                c = Resources.Load<AudioClip>("Audio/Level02/" + name) ?? Build(name);
                clips[name] = c;
            }
            if (c == null) return;
            var src = pool[next]; next = (next + 1) % pool.Length;
            src.pitch = pitch * (1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter)) * Mathf.Max(0.5f, Time.timeScale < 0.99f ? 0.8f : 1f);
            src.PlayOneShot(c, Volume * vol);
        }

        static AudioClip Make(string name, float dur, Func<float, float> f)
        {
            int n = (int)(Rate * dur);
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        // simple low-passed noise so whooshes sound like air, not static
        class Noise
        {
            readonly System.Random r; float lp;
            public Noise(int seed) { r = new System.Random(seed); }
            public float White() => (float)r.NextDouble() * 2f - 1f;
            public float Low(float k) { lp += (White() - lp) * k; return lp; }
        }

        static float Exp(float t, float k) => Mathf.Exp(-t * k);
        static float Sin(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);

        static AudioClip Build(string name)
        {
            var n = new Noise(name.GetHashCode());
            switch (name)
            {
                case "whoosh":
                    return Make(name, 0.26f, t => n.Low(0.08f + 0.5f * Mathf.Sin(t / 0.26f * Mathf.PI) * 0.3f) * 3.2f * Mathf.Sin(Mathf.Clamp01(t / 0.26f) * Mathf.PI));
                case "whoosh_heavy":
                    return Make(name, 0.42f, t => n.Low(0.05f) * 4f * Mathf.Sin(Mathf.Clamp01(t / 0.42f) * Mathf.PI) + Sin(70f - 30f * t, t) * 0.2f * Mathf.Sin(Mathf.Clamp01(t / 0.42f) * Mathf.PI));
                case "hit":
                    return Make(name, 0.2f, t => (n.Low(0.5f) * 0.9f * Exp(t, 30f) + Sin(140f - 200f * t, t) * 0.8f * Exp(t, 18f)));
                case "hit_heavy":
                    return Make(name, 0.55f, t => (n.Low(0.35f) * Exp(t, 14f) + Sin(85f - 60f * t, t) * 1.1f * Exp(t, 7f) + Sin(1300f, t) * 0.08f * Exp(t, 10f)));
                case "slam":
                    return Make(name, 1.0f, t => (n.Low(0.12f) * 1.2f * Exp(t, 4f) + Sin(48f, t) * 1.2f * Exp(t, 3.5f) + n.White() * 0.25f * Exp(t, 25f)));
                case "clang":
                    return Make(name, 0.9f, t =>
                    {
                        float s = 0; float[] p = { 1f, 2.4f, 3.9f, 5.3f, 7.1f };
                        for (int i = 0; i < p.Length; i++) s += Sin(880f * p[i], t) * (0.35f / (i + 1)) * Exp(t, 4f + i * 2f);
                        return s + n.White() * 0.4f * Exp(t, 60f);
                    });
                case "block":
                    return Make(name, 0.18f, t => n.Low(0.6f) * 0.7f * Exp(t, 35f) + Sin(320f, t) * 0.3f * Exp(t, 30f));
                case "dodge":
                    return Make(name, 0.22f, t => n.Low(0.15f) * 2.2f * Mathf.Sin(Mathf.Clamp01(t / 0.22f) * Mathf.PI));
                case "jump":
                    return Make(name, 0.2f, t => n.Low(0.1f) * 1.6f * Exp(t, 12f) + Sin(200f + 300f * t, t) * 0.15f * Exp(t, 15f));
                case "land":
                    return Make(name, 0.2f, t => n.Low(0.3f) * 0.8f * Exp(t, 22f) + Sin(70f, t) * 0.6f * Exp(t, 20f));
                case "step_giant":
                    return Make(name, 0.6f, t => Sin(40f, t) * 1.2f * Exp(t, 6f) + n.Low(0.1f) * 0.6f * Exp(t, 8f));
                case "magic":
                    return Make(name, 0.7f, t => (Sin(500f + 900f * t, t) * 0.25f + Sin(750f + 1300f * t, t) * 0.15f) * Mathf.Sin(Mathf.Clamp01(t / 0.7f) * Mathf.PI));
                case "orb_cast":
                    return Make(name, 0.6f, t => (Sin(120f + 260f * t, t) * 0.45f + n.Low(0.2f) * 0.5f) * Mathf.Clamp01(t * 4f) * Exp(Mathf.Max(0, t - 0.4f), 12f));
                case "orb_blast":
                    return Make(name, 0.6f, t => (n.Low(0.25f) * 1.1f + Sin(90f, t) * 0.7f) * Exp(t, 7f) + Sin(600f - 500f * t, t) * 0.2f * Exp(t, 8f));
                case "teleport":
                    return Make(name, 0.4f, t => (Sin(1200f - 2400f * t, t) * 0.3f + n.Low(0.2f) * 0.8f) * Mathf.Sin(Mathf.Clamp01(t / 0.4f) * Mathf.PI));
                case "poof":
                    return Make(name, 0.35f, t => n.Low(0.2f) * 1.6f * Exp(t, 10f) * Mathf.Clamp01(t * 60f));
                case "fire":
                    return Make(name, 0.8f, t => (n.Low(0.3f) * 1.0f + n.White() * 0.15f * (n.White() > 0.9f ? 1f : 0f)) * Mathf.Clamp01(t * 10f) * Exp(t, 3f));
                case "grow":
                    return Make(name, 1.6f, t => (Sin(40f + 60f * t, t) * 0.9f + n.Low(0.06f) * 1.2f + Sin(220f + 200f * t, t) * 0.12f) * Mathf.Clamp01(t * 2f) * Exp(Mathf.Max(0, t - 1.1f), 6f));
                case "shrink":
                    return Make(name, 0.45f, t => (Sin(2200f - 3000f * t, t) * 0.25f + Sin(1600f - 2000f * t, t) * 0.15f) * Exp(t, 4f));
                case "power":
                    return Make(name, 1.0f, t => (Sin(220f, t) * 0.25f + Sin(330f, t) * 0.2f + Sin(440f, t) * 0.15f + Sin(660f, t) * 0.1f + n.Low(0.08f) * 0.6f) * Mathf.Clamp01(t * 8f) * Exp(t, 2.6f));
                case "thunder":
                    return Make(name, 1.2f, t => (n.Low(0.5f) * 1.4f * Exp(t, 9f) + n.Low(0.05f) * 1.2f * Exp(t, 2.5f) + Sin(50f, t) * 0.5f * Exp(t, 3f)));
                case "bell":
                    return Make(name, 2.4f, t =>
                    {
                        float s = 0; float[] p = { 1f, 2.76f, 5.4f, 8.93f }; float[] a = { 0.5f, 0.3f, 0.18f, 0.1f };
                        for (int i = 0; i < p.Length; i++) s += Sin(311f * p[i], t) * a[i] * Exp(t, 1.4f + i * 1.3f);
                        return s;
                    });
                case "ko":
                    return Make(name, 1.6f, t => (Sin(55f, t) * 1.2f * Exp(t, 2.5f) + n.Low(0.2f) * 1.3f * Exp(t, 5f) + Sin(110f, t) * 0.3f * Exp(t, 2f)));
                case "tick":
                    return Make(name, 0.08f, t => Sin(1500f, t) * 0.3f * Exp(t, 40f));
                case "rank":
                    return Make(name, 0.6f, t => (Sin(t < 0.12f ? 784f : t < 0.24f ? 988f : 1175f, t) * 0.35f) * Exp(t, 4f));
                case "roar":
                    return Make(name, 1.1f, t => (n.Low(0.12f) * 1.4f * (0.6f + 0.4f * Sin(28f, t)) + Sin(95f + 20f * Sin(6f, t), t) * 0.5f) * Mathf.Clamp01(t * 6f) * Exp(Mathf.Max(0, t - 0.6f), 5f));
                case "laugh":
                    return Make(name, 1.2f, t =>
                    {
                        float beat = Mathf.Repeat(t, 0.18f) / 0.18f;
                        float env = Mathf.Sin(beat * Mathf.PI) * Exp(t, 1.4f);
                        return (Sin(150f - 30f * t, t) * 0.5f + n.Low(0.3f) * 0.4f) * env;
                    });
                case "lamp_out":
                    return Make(name, 0.5f, t => n.Low(0.12f) * 1.5f * Exp(t, 6f) * Mathf.Clamp01(t * 40f) + Sin(900f - 600f * t, t) * 0.15f * Exp(t, 8f));
                case "ultimate":
                    return Make(name, 2.4f, t =>
                    {
                        float s = Sin(110f, t) * 0.3f + Sin(165f, t) * 0.25f + Sin(220f, t) * 0.2f + Sin(330f, t) * 0.12f + Sin(440f, t) * 0.08f;
                        return (s * (1f + 0.3f * Sin(5f, t)) + n.Low(0.05f) * 0.5f) * Mathf.Clamp01(t * 1.5f) * Exp(Mathf.Max(0, t - 1.6f), 4f);
                    });
                case "wind":
                    return Make(name, 1.0f, t => n.Low(0.04f + 0.03f * Sin(2f, t)) * 3f * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI));
                case "kick":
                    return Make(name, 0.18f, t => n.Low(0.4f) * 1.0f * Exp(t, 25f) + Sin(160f - 150f * t, t) * 0.6f * Exp(t, 22f));
                default:
                    return Make(name, 0.1f, t => Sin(660f, t) * 0.2f * Exp(t, 30f));
            }
        }
    }

    /// <summary>Placeholder battle drums: a synthesised dhol / tabla loop over a drone, intensity per round. Replace with M-02..M-05.</summary>
    public class FightMusic : MonoBehaviour
    {
        AudioSource src; float targetVol = 0.42f;
        static AudioClip[] loops = new AudioClip[3];

        public static FightMusic Create()
        {
            var go = new GameObject("FightMusic");
            var m = go.AddComponent<FightMusic>();
            m.src = go.AddComponent<AudioSource>(); m.src.loop = true; m.src.playOnAwake = false; m.src.volume = 0f;
            return m;
        }

        public void SetIntensity(int level)
        {
            level = Mathf.Clamp(level, 0, 2);
            string file = level == 0 ? "mus_duel" : level == 1 ? "mus_sorcery" : "mus_titans";
            var clip = Resources.Load<AudioClip>("Audio/Level02/" + file);
            if (clip == null) { if (loops[level] == null) loops[level] = Build(level); clip = loops[level]; }
            if (src.clip == clip && src.isPlaying) return;
            src.clip = clip; src.Play();
        }

        public void Fade(float v) { targetVol = v; }

        void Update() { src.volume = Mathf.MoveTowards(src.volume, targetVol, Time.unscaledDeltaTime * 0.6f); }

        static AudioClip Build(int level)
        {
            const int Rate = 22050;
            float bpm = level == 2 ? 120f : 150f;
            float beat = 60f / bpm;
            int bars = 4; float dur = bars * 4 * beat;
            int n = (int)(dur * Rate);
            var d = new float[n];
            var r = new System.Random(77 + level);
            float root = level == 1 ? 69.3f : 73.4f;      // C#2 / D2 drone
            // patterns in 16ths: 1 = dhol low, 2 = tabla high, 3 = clap
            int[] pat = level == 2
                ? new[] { 1, 0, 0, 0, 0, 0, 2, 0, 1, 0, 0, 0, 3, 0, 2, 2 }
                : new[] { 1, 0, 2, 0, 0, 2, 1, 0, 2, 0, 1, 2, 3, 0, 2, 2 };
            float six = beat / 4f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                // drone: tanpura-ish stack with slow beating
                float s = 0f;
                s += Mathf.Sin(2 * Mathf.PI * root * t) * 0.12f + Mathf.Sin(2 * Mathf.PI * root * 1.5f * t) * 0.07f + Mathf.Sin(2 * Mathf.PI * root * 2.003f * t) * 0.06f;
                s *= 0.8f + 0.2f * Mathf.Sin(2 * Mathf.PI * 0.25f * t);
                if (level == 1) s += Mathf.Sin(2 * Mathf.PI * root * 3.01f * t) * 0.04f * Mathf.Sin(2 * Mathf.PI * 0.5f * t);
                int step = (int)(t / six); float st = t - step * six;
                int p = pat[step % 16];
                if (p == 1) s += Mathf.Sin(2 * Mathf.PI * (75f - 40f * st) * st) * 0.9f * Mathf.Exp(-st * 11f);
                if (p == 2) s += (Mathf.Sin(2 * Mathf.PI * 380f * st) * 0.35f + ((float)r.NextDouble() - 0.5f) * 0.25f) * Mathf.Exp(-st * 30f);
                if (p == 3) s += ((float)r.NextDouble() - 0.5f) * 0.8f * Mathf.Exp(-st * 25f);
                // every bar: big taiko on beat one (heavier in the titan round)
                float bar = t % (4 * beat);
                if (bar < 0.5f) s += Mathf.Sin(2 * Mathf.PI * 50f * bar) * (level == 2 ? 1.0f : 0.5f) * Mathf.Exp(-bar * 6f);
                d[i] = Mathf.Clamp(s * 0.55f, -1f, 1f);
            }
            var clip = AudioClip.Create("fight_loop_" + level, n, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
