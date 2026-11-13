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
        public static bool Blends(string key) => key == "idle";

        public static Sprite Sprite(string name) => Resources.Load<Sprite>(Folder + name);

        /// <summary>Clip or move id -> the pictures it plays: each step starts at that fraction of the clip (loops repeat the whole run).</summary>
        public static readonly Dictionary<string, (float t, string name)[]> Frames = new Dictionary<string, (float, string)[]>
        {
            ["idle"] = new[] { (0f, "ai_01"), (0.167f, "ai_02"), (0.333f, "ai_03"), (0.5f, "ai_04"), (0.667f, "ai_05"), (0.833f, "ai_06") },
            ["walkf"] = new[] { (0.0f, "aw_01"), (0.125f, "aw_02"), (0.25f, "aw_03"), (0.375f, "aw_04"), (0.5f, "aw_05"), (0.625f, "aw_06"), (0.75f, "aw_07"), (0.875f, "aw_08") },
            ["walkb"] = new[] { (0.0f, "aw_08"), (0.125f, "aw_07"), (0.25f, "aw_06"), (0.375f, "aw_05"), (0.5f, "aw_04"), (0.625f, "aw_03"), (0.75f, "aw_02"), (0.875f, "aw_01") },
            ["crouch"] = new[] { (0f, "ap_07") },
            ["block"] = new[] { (0f, "ab_02") },
            ["crouchblock"] = new[] { (0f, "ap_09") },
            ["jump"] = new[] { (0f, "ap_10") },
            ["fall"] = new[] { (0f, "ap_11") },
            ["dodgef"] = new[] { (0f, "ap_12") },
            ["hitlow"] = new[] { (0f, "ah_03"), (0.45f, "ah_04"), (0.85f, "ai_01") },
            // trident attacks
            ["jab"] = new[] { (0f, "aj_01"), (0.1f, "aj_02"), (0.2f, "aj_03"), (0.3f, "aj_04"), (0.4f, "aj_05"), (0.6f, "aj_06"), (0.85f, "ai_01") },
            ["combo"] = new[] { (0f, "ac_01"), (0.06f, "ac_02"), (0.11f, "ac_03"), (0.16f, "ac_04"), (0.24f, "ac_05"), (0.37f, "ac_06"), (0.46f, "ac_07"), (0.56f, "ac_08"), (0.68f, "ac_09"), (0.85f, "ac_10") },
            ["sweep"] = new[] { (0f, "ap_07"), (0.18f, "bp_05"), (0.6f, "ap_07"), (0.85f, "ap_01") },
            ["overhead"] = new[] { (0f, "ap_01"), (0.25f, "bp_04"), (0.46f, "ap_12"), (0.8f, "ap_01") },
            // sorcery and grabs
            ["grab"] = new[] { (0f, "ap_01"), (0.3f, "cp_07"), (0.75f, "ap_01") },
            ["darkthrow"] = new[] { (0f, "cp_07"), (0.2f, "cp_08"), (0.45f, "cp_09"), (0.62f, "cp_10"), (0.85f, "ap_01") },
            ["orb"] = new[] { (0f, "ap_01"), (0.27f, "cp_01"), (0.42f, "cp_02"), (0.85f, "ap_01") },
            ["barrage"] = new[] { (0f, "ap_07"), (0.1f, "cp_03"), (0.26f, "cp_04"), (0.34f, "cp_03"), (0.42f, "cp_04"), (0.5f, "cp_03"), (0.58f, "cp_04"), (0.66f, "cp_03") },
            ["vanish"] = new[] { (0f, "cp_06"), (0.5f, "ap_07") },
            ["summon"] = new[] { (0f, "ap_01"), (0.2f, "cp_05"), (0.8f, "ap_01") },
            ["laugh"] = new[] { (0f, "cp_11") },
            ["roar"] = new[] { (0f, "cp_12") },
            ["transform"] = new[] { (0f, "cp_12") },
            ["victory"] = new[] { (0f, "cp_12") },
            ["fly"] = new[] { (0f, "cp_03"), (0.5f, "cp_04") },
            // reactions
            ["hit"] = new[] { (0f, "ah_01"), (0.17f, "ah_02"), (0.38f, "ah_03"), (0.62f, "ah_04"), (0.88f, "ai_01") },
            ["launched"] = new[] { (0f, "bp_10") },
            ["stunned"] = new[] { (0f, "bp_11") },
            ["kneel"] = new[] { (0f, "bp_12") },
        };
    }
}
