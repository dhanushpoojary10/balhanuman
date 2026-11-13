using System;
using UnityEngine;

namespace BalHanuman.Fight
{
    /// <summary>
    /// Real Ahiravana art for the rig: part sprites and skin.json in Resources/Level02/Ahiravana (same format as the Hanuman skin,
    /// plus per-part scale, shading and sprite-swap groups for the six faces and the hand poses).
    /// When it is missing the rig keeps the procedural placeholder shapes.
    /// </summary>
    public static class AhiravanaSkin
    {
        const string Folder = "Level02/Ahiravana/";
        

        public static HanumanSkin.Data Get()
        {
            // not cached, so a re-cut skin shows up on the next play (statics survive play mode when domain reload is off)
            HanumanSkin.Data data = null;
            var ta = Resources.Load<TextAsset>(Folder + "skin");
            if (ta != null) { try { data = JsonUtility.FromJson<HanumanSkin.Data>(ta.text); } catch (Exception e) { Debug.LogWarning("Ahiravana skin: " + e.Message); } }
            return data;
        }

        public static Sprite Sprite(string name) => Resources.Load<Sprite>(Folder + name);
    }
}
