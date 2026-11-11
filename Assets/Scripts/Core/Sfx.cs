using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalHanuman
{
    /// <summary>Plays sound effects from Assets/Resources/Audio (made by Tools/AudioGen); falls back to a synthesised beep if a file is missing.</summary>
    public static class Sfx
    {
        public enum Id { Jump, Land, Step, Pickup, Lever, Bell, Splash, Chirp, Rescue, Click, Portal, Complete, Dialogue, PickupBig, Checkpoint, Conch }

        const int Rate = 22050;
        static readonly Dictionary<Id, AudioClip> clips = new Dictionary<Id, AudioClip>();
        static AudioSource source;
        public static float Volume = 0.55f;

        public static void Play(Id id, float pitch = 1f, float vol = 1f)
        {
            if (source == null)
            {
                var go = new GameObject("Sfx");
                UnityEngine.Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
            }
            if (!clips.TryGetValue(id, out var c)) { c = Resources.Load<AudioClip>("Audio/" + FileName(id)) ?? Build(id); clips[id] = c; }
            source.pitch = pitch;
            source.PlayOneShot(c, Volume * vol);
        }

        static string FileName(Id id)
        {
            switch (id)
            {
                case Id.Bell: return "temple_bell";
                case Id.PickupBig: return "pickup_big";
                default: return id.ToString().ToLowerInvariant();
            }
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

        static float Env(float t, float dur, float attack = 0.005f)
        {
            float a = Mathf.Clamp01(t / attack);
            float r = Mathf.Clamp01((dur - t) / dur);
            return a * r * r;
        }

        static AudioClip Build(Id id)
        {
            var rng = new System.Random(1234 + (int)id);
            switch (id)
            {
                case Id.Jump:
                    return Make("jump", 0.18f, t => Mathf.Sin(2 * Mathf.PI * (320f * t + 900f * t * t)) * 0.5f * Env(t, 0.18f));
                case Id.Land:
                    return Make("land", 0.12f, t => (Mathf.Sin(2 * Mathf.PI * 90f * t) * 0.6f + ((float)rng.NextDouble() - 0.5f) * 0.3f) * Env(t, 0.12f));
                case Id.Step:
                    return Make("step", 0.05f, t => ((float)rng.NextDouble() - 0.5f) * 0.25f * Env(t, 0.05f));
                case Id.Pickup:
                    return Make("pickup", 0.28f, t =>
                    {
                        float f = t < 0.1f ? 880f : 1320f;
                        return (Mathf.Sin(2 * Mathf.PI * f * t) * 0.45f + Mathf.Sin(2 * Mathf.PI * f * 2f * t) * 0.15f) * Env(t, 0.28f);
                    });
                case Id.Lever:
                    return Make("lever", 0.5f, t =>
                    {
                        float thump = Mathf.Sin(2 * Mathf.PI * 70f * t) * Mathf.Exp(-t * 9f);
                        float click = t > 0.18f && t < 0.24f ? ((float)rng.NextDouble() - 0.5f) * 0.8f : 0f;
                        return thump * 0.8f + click;
                    });
                case Id.Bell:
                    return Make("bell", 2.6f, t =>
                    {
                        float s = 0f;
                        float[] p = { 1f, 2.76f, 5.4f, 8.93f };
                        float[] a = { 0.5f, 0.3f, 0.18f, 0.1f };
                        for (int i = 0; i < p.Length; i++) s += Mathf.Sin(2 * Mathf.PI * 523f * p[i] * t) * a[i] * Mathf.Exp(-t * (1.6f + i * 1.3f));
                        return s;
                    });
                case Id.Splash:
                    return Make("splash", 0.45f, t => ((float)rng.NextDouble() - 0.5f) * 0.7f * Mathf.Exp(-t * 7f) * Mathf.Clamp01(t / 0.01f));
                case Id.Chirp:
                    return Make("chirp", 0.24f, t =>
                    {
                        float tt = t < 0.1f ? t : t - 0.12f;
                        if (t > 0.1f && t < 0.12f) return 0f;
                        float f = 2400f + 1400f * Mathf.Abs(Mathf.Sin(tt * 18f));
                        return Mathf.Sin(2 * Mathf.PI * f * t) * 0.18f * Env(tt < 0 ? 0 : tt, 0.12f);
                    });
                case Id.Rescue:
                    return Make("rescue", 0.6f, t =>
                    {
                        float f = t < 0.2f ? 523f : (t < 0.4f ? 659f : 784f);
                        return Mathf.Sin(2 * Mathf.PI * f * t) * 0.4f * Env(t, 0.6f);
                    });
                case Id.Click:
                    return Make("click", 0.05f, t => Mathf.Sin(2 * Mathf.PI * 1100f * t) * 0.35f * Env(t, 0.05f));
                case Id.Portal:
                    return Make("portal", 0.9f, t =>
                        (Mathf.Sin(2 * Mathf.PI * (220f * t + 500f * t * t)) * 0.35f + Mathf.Sin(2 * Mathf.PI * 1760f * t) * 0.1f * Mathf.Sin(t * 30f)) * Env(t, 0.9f, 0.05f));
                case Id.Complete:
                    return Make("complete", 1.2f, t =>
                    {
                        float[] n = { 523f, 659f, 784f, 1047f };
                        int i = Mathf.Min(3, (int)(t / 0.22f));
                        return Mathf.Sin(2 * Mathf.PI * n[i] * t) * 0.4f * Env(t, 1.2f);
                    });
                default: // Dialogue blip
                    return Make("dlg", 0.06f, t => Mathf.Sin(2 * Mathf.PI * 660f * t) * 0.25f * Env(t, 0.06f));
            }
        }
    }
}
