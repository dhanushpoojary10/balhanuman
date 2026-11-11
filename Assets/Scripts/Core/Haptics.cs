using UnityEngine;

namespace BalHanuman
{
    /// <summary>Short phone vibration pulses (Android). Does nothing in the editor or on desktop. Can be switched off with PlayerPrefs "haptics".</summary>
    public static class Haptics
    {
        const string Key = "haptics";

        public static bool Enabled
        {
            get { try { return PlayerPrefs.GetInt(Key, 1) == 1; } catch { return true; } }
            set { try { PlayerPrefs.SetInt(Key, value ? 1 : 0); } catch { } }
        }

        public static void Tick() { Pulse(8, 40); }
        public static void Light() { Pulse(14, 70); }
        public static void Medium() { Pulse(28, 130); }
        public static void Heavy() { Pulse(55, 220); }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static AndroidJavaClass effectClass;
        static int sdk = -1;
        static bool failed;

        static void Pulse(long ms, int amplitude)
        {
            if (failed || !Enabled) return;
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
                if (vibrator == null) { failed = true; return; }
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
#else
        static void Pulse(long ms, int amplitude) { }
#endif
    }
}
