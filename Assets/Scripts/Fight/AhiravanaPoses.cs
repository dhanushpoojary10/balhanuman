using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalHanuman.Fight
{
    /// <summary>
    /// Ahiravana drawn as whole-body poses (Resources/Level02/Ahiravana/poses): each clip plays as a short run of full pictures.
    /// Clips without pictures yet fall back to the cut-out rig, so poses can be added sheet by sheet.
    /// </summary>
    public static class AhiravanaPoses
    {
        [Serializable] public class Pose { public string name; public int w, h; public float px, py, k; }   // px, py: the feet point, in pixels from the top-left
        [Serializable] public class Data { public float k; public Pose[] poses; }                        // k: world units per pixel

        const string Folder = "Level02/Ahiravana/poses/";

        public static Data Get()
        {
            var ta = Resources.Load<TextAsset>(Folder + "poses");
            if (ta == null) return null;
            try { return JsonUtility.FromJson<Data>(ta.text); } catch (Exception e) { Debug.LogWarning("Ahiravana poses: " + e.Message); return null; }
        }

        /// <summary>Clips whose frames dissolve into each other (slow, soft motion such as breathing).</summary>
        public static bool Blends(string key)
        {
            switch (key)
            {
                case "idle": case "walkf": case "walkb": case "stunned": case "kneel": case "victory": case "getup": case "hit": case "hitlow": case "down": case "block": case "crouch": case "crouchblock": case "jump": case "fall": case "dodgef": case "dodgeb": return true;
            }
            return false;
        }

        public static Sprite Sprite(string name) => Resources.Load<Sprite>(Folder + name);

        /// <summary>Clip or move id -> the pictures it plays: each step starts at that fraction of the clip (loops repeat the whole run).</summary>
        public static readonly Dictionary<string, (float t, string name)[]> Frames = new Dictionary<string, (float, string)[]>
        {
            ["idle"] = new[] { (0.000f, "s15_00"), (0.125f, "s15_01"), (0.250f, "s15_02"), (0.375f, "s15_03"), (0.500f, "s16_00"), (0.625f, "s16_01"), (0.750f, "s16_02"), (0.875f, "s16_03") },
            ["walkf"] = new[] { (0.000f, "s17_00"), (0.125f, "s17_01"), (0.250f, "s17_02"), (0.375f, "s17_03"), (0.500f, "s18_00"), (0.625f, "s18_01"), (0.750f, "s18_02"), (0.875f, "s18_03") },
            ["walkb"] = new[] { (0.000f, "s18_03"), (0.125f, "s18_02"), (0.250f, "s18_01"), (0.375f, "s18_00"), (0.500f, "s17_03"), (0.625f, "s17_02"), (0.750f, "s17_01"), (0.875f, "s17_00") },
            ["crouch"] = new[] { (0.000f, "s33_03") },
            ["block"] = new[] { (0.000f, "s44_01") },
            ["crouchblock"] = new[] { (0.000f, "s45_01") },
            ["jump"] = new[] { (0.000f, "s33_00"), (0.350f, "s33_01") },
            ["fall"] = new[] { (0.000f, "s33_02") },
            ["dodgef"] = new[] { (0.000f, "s34_00"), (0.150f, "s34_01"), (0.400f, "s34_02"), (0.800f, "s34_03") },
            ["dodgeb"] = new[] { (0.000f, "s35_00"), (0.150f, "s35_01"), (0.400f, "s35_02"), (0.800f, "s35_03") },
            ["hit"] = new[] { (0.000f, "s42_00"), (0.120f, "s42_01"), (0.300f, "s42_02"), (0.600f, "s42_03") },
            ["hitlow"] = new[] { (0.000f, "s43_00"), (0.400f, "s43_01") },
            ["launched"] = new[] { (0.000f, "s48_00"), (0.250f, "s48_01"), (0.500f, "s48_02"), (0.750f, "s48_03") },
            ["down"] = new[] { (0.000f, "s46_02"), (0.500f, "s46_03") },
            ["getup"] = new[] { (0.000f, "s47_00"), (0.300f, "s47_01"), (0.550f, "s47_02"), (0.800f, "s47_03") },
            ["stunned"] = new[] { (0.000f, "s49_00"), (0.250f, "s49_01"), (0.500f, "s49_02"), (0.750f, "s49_03") },
            ["kneel"] = new[] { (0.000f, "s50_01"), (0.500f, "s50_02") },
            ["laugh"] = new[] { (0.000f, "s51_00"), (0.120f, "s51_01"), (0.250f, "s51_02"), (0.400f, "s51_01"), (0.520f, "s51_02"), (0.640f, "s51_01"), (0.780f, "s15_00") },
            ["victory"] = new[] { (0.000f, "s53_01"), (0.500f, "s53_02") },
            ["roar"] = new[] { (0.000f, "s52_00"), (0.200f, "s52_01"), (0.400f, "s52_02"), (0.650f, "s52_03"), (0.850f, "s15_00") },
            ["transform"] = new[] { (0.000f, "s52_00"), (0.200f, "s52_01"), (0.400f, "s52_02"), (0.650f, "s52_03"), (0.850f, "s15_00") },
            ["fly"] = new[] { (0.000f, "s40_00") },
            ["jab"] = new[] { (0.000f, "s19_00"), (0.220f, "s19_01"), (0.550f, "s19_02"), (0.850f, "s21_01") },
            ["combo"] = new[] { (0.000f, "s19_00"), (0.090f, "s19_01"), (0.170f, "s19_02"), (0.220f, "s19_03"), (0.310f, "s20_00"), (0.450f, "s20_01"), (0.580f, "s20_02"), (0.660f, "s20_03"), (0.740f, "s21_00"), (0.900f, "s21_01") },
            ["sweep"] = new[] { (0.000f, "s24_00"), (0.120f, "s24_01"), (0.220f, "s24_02"), (0.340f, "s24_03"), (0.500f, "s25_00"), (0.800f, "s25_01") },
            ["gsweep"] = new[] { (0.000f, "s24_00"), (0.120f, "s24_01"), (0.220f, "s24_02"), (0.340f, "s24_03"), (0.500f, "s25_00"), (0.800f, "s25_01") },
            ["overhead"] = new[] { (0.000f, "s22_00"), (0.170f, "s22_01"), (0.300f, "s22_02"), (0.450f, "s22_03"), (0.520f, "s23_00"), (0.580f, "s23_01"), (0.700f, "s23_02"), (0.850f, "s23_03") },
            ["gslam"] = new[] { (0.000f, "s22_00"), (0.150f, "s22_01"), (0.300f, "s22_02"), (0.420f, "s22_03"), (0.460f, "s23_00"), (0.520f, "s23_01"), (0.660f, "s23_02"), (0.850f, "s23_03") },
            ["stomp"] = new[] { (0.000f, "s22_00"), (0.170f, "s22_01"), (0.300f, "s22_02"), (0.450f, "s22_03"), (0.520f, "s23_00"), (0.580f, "s23_01"), (0.700f, "s23_02"), (0.850f, "s23_03") },
            ["grab"] = new[] { (0.000f, "s30_00"), (0.200f, "s30_01"), (0.380f, "s30_02"), (0.550f, "s30_03"), (0.850f, "s21_01") },
            ["darkthrow"] = new[] { (0.000f, "s30_03"), (0.140f, "s31_00"), (0.280f, "s31_01"), (0.500f, "s31_02"), (0.640f, "s31_03"), (0.720f, "s32_00"), (0.880f, "s32_01") },
            ["orb"] = new[] { (0.000f, "s26_00"), (0.120f, "s26_01"), (0.220f, "s26_02"), (0.300f, "s26_03"), (0.380f, "s27_00"), (0.450f, "s27_01"), (0.550f, "s27_02"), (0.720f, "s27_03") },
            ["barrage"] = new[] { (0.000f, "s40_00"), (0.260f, "s40_01"), (0.420f, "s40_02"), (0.570f, "s40_03"), (0.700f, "s41_00"), (0.880f, "s41_01") },
            ["vanish"] = new[] { (0.000f, "s36_00"), (0.120f, "s36_01"), (0.220f, "s36_02"), (0.320f, "s36_03"), (0.450f, "s37_00"), (0.580f, "s37_01"), (0.740f, "s36_03"), (0.840f, "s36_02"), (0.920f, "s36_01") },
            ["summon"] = new[] { (0.000f, "s38_00"), (0.100f, "s38_01"), (0.200f, "s38_02"), (0.300f, "s38_03"), (0.450f, "s39_00"), (0.520f, "s39_01"), (0.650f, "s39_02"), (0.820f, "s39_03") },
        };
    }
}
