using UnityEngine;

namespace BalHanuman
{
    /// <summary>
    /// Phone vibration (Android): single pulses and short strength-shaped patterns. Does nothing in the editor or on desktop.
    /// Can be switched off with PlayerPrefs "haptics". Count / Last record the most recent call so tests can see what fired.
    /// </summary>
    public static class Haptics
    {
        const string Key = "haptics";

        public static int Count; public static string Last = "";

        public static bool Enabled
        {
            get { try { return PlayerPrefs.GetInt(Key, 1) == 1; } catch { return true; } }
            set { try { PlayerPrefs.SetInt(Key, value ? 1 : 0); } catch { } }
        }

        public static void Tick() { Note("tick"); Pulse(8, 40); }
        public static void Light() { Note("light"); Pulse(14, 70); }
        public static void Medium() { Note("medium"); Pulse(28, 130); }
        public static void Heavy() { Note("heavy"); Pulse(55, 220); }

        // ------------------------------------------------------------------ fight patterns
        /// <summary>You land a hit. power 0..1 (damage), heavy hits get a snap plus a short tail.</summary>
        public static void Hit(float power, bool heavy)
        {
            power = Mathf.Clamp01(power);
            Note("hit");
            if (!heavy) { Pulse(10 + (long)(power * 14f), 50 + (int)(power * 90f)); return; }
            Wave(new long[] { 0, 28, 25, 22 }, new int[] { 0, 150 + (int)(power * 100f), 0, 70 });
        }

        /// <summary>Hanuman gets hit: a sharper double thump that grows with the damage taken.</summary>
        public static void Hurt(float power)
        {
            power = Mathf.Clamp01(power);
            Note("hurt");
            Wave(new long[] { 0, 30, 35, 45 + (long)(power * 45f) }, new int[] { 0, 200 + (int)(power * 55f), 0, 110 + (int)(power * 100f) });
        }

        /// <summary>A body-slam on the ground: heavy thump that rolls out.</summary>
        public static void Slam(float power = 1f)
        {
            power = Mathf.Clamp(power, 0.4f, 1.5f);
            Note("slam");
            Wave(new long[] { 0, (long)(60 * power), 30, 45, 30, 35 }, new int[] { 0, 255, 0, 170, 0, 90 });
        }

        /// <summary>Block / chip: dull short tap.</summary>
        public static void Block() { Note("block"); Pulse(12, 60); }

        /// <summary>Parry: crisp double click.</summary>
        public static void Parry() { Note("parry"); Wave(new long[] { 0, 18, 40, 30 }, new int[] { 0, 255, 0, 210 }); }

        /// <summary>The Panchmukhi ultimate: a rising heartbeat that ends in a heavy hit.</summary>
        public static void Ultimate()
        {
            Note("ultimate");
            Wave(new long[] { 0, 40, 90, 55, 70, 70, 50, 90, 30, 150 }, new int[] { 0, 120, 0, 160, 0, 200, 0, 240, 0, 255 });
        }

        static void Note(string what) { Count++; Last = what; }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static AndroidJavaClass effectClass;
        static int sdk = -1;
        static bool failed;

        static bool Ready()
        {
            if (failed || !Enabled) return false;
            try
            {
                if (vibrator == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using (var v = new AndroidJavaClass("android.os.Build$VERSION")) sdk = v.GetStatic<int>("SDK_INT");
                    if (sdk >= 26) effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                }
                if (vibrator == null) { failed = true; return false; }
                return true;
            }
            catch { failed = true; return false; }
        }

        static void Pulse(long ms, int amplitude)
        {
            if (!Ready()) return;
            try
            {
                if (sdk >= 26 && effectClass != null)
                {
                    using (var fx = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, Mathf.Clamp(amplitude, 1, 255)))
                        vibrator.Call("vibrate", fx);
                }
                else if (ms >= 40) Handheld.Vibrate();   // old phones cannot vary the strength; this call also makes Unity add the VIBRATE permission
                else vibrator.Call("vibrate", ms);
            }
            catch { failed = true; }
        }

        /// <summary>Strength-shaped pattern: timings[i] ms at amplitudes[i] (0 = pause). Phones without amplitude control get the on/off pattern.</summary>
        static void Wave(long[] timings, int[] amplitudes)
        {
            if (!Ready()) return;
            try
            {
                if (sdk >= 26 && effectClass != null)
                {
                    using (var fx = effectClass.CallStatic<AndroidJavaObject>("createWaveform", timings, amplitudes, -1))
                        vibrator.Call("vibrate", fx);
                }
                else vibrator.Call("vibrate", timings, -1);
            }
            catch { failed = true; }
        }
#else
        static void Pulse(long ms, int amplitude) { }
        static void Wave(long[] timings, int[] amplitudes) { }
#endif
    }
}
