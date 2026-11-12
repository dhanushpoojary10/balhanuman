using System;
using UnityEngine;

namespace BalHanuman.Fight
{
    /// <summary>
    /// Real Hanuman art for the rig: the part sprites and skin.json in Resources/Level02/Hanuman
    /// (cut from the parts sheet). skin.json gives each part's bone, sorting order, joint pivot (pixels) and the measured proportions.
    /// When it is missing the rig falls back to the procedural placeholder shapes.
    /// </summary>
    public static class HanumanSkin
    {
        const string Folder = "Level02/Hanuman/";

        [Serializable] public class Part { public string name, joint, kind; public int order, w, h; public float px, py, length, rot; }
        [Serializable] public class DimsData { public float thigh, shin, ankle, torso, upperArm, foreArm, hand, shoulderY, neck, headR; public float[] tail; }
        [Serializable] public class Data { public float k; public Part[] parts; public DimsData dims; }

        static Data data; static bool loaded;

        public static Data Get()
        {
            if (loaded) return data;
            loaded = true;
            var ta = Resources.Load<TextAsset>(Folder + "skin");
            if (ta != null) { try { data = JsonUtility.FromJson<Data>(ta.text); } catch (Exception e) { Debug.LogWarning("Hanuman skin: " + e.Message); } }
            return data;
        }

        public static Sprite Sprite(string name) => Resources.Load<Sprite>(Folder + name);

        /// <summary>Rig proportions measured from the art (null when there is no skin).</summary>
        public static Puppet.Dims Dims()
        {
            var d = Get();
            if (d == null || d.dims == null) return null;
            var m = d.dims;
            return new Puppet.Dims
            {
                thigh = m.thigh, shin = m.shin, ankle = m.ankle, torso = m.torso, upperArm = m.upperArm, foreArm = m.foreArm,
                hand = m.hand, shoulderY = m.shoulderY, neck = m.neck, headR = m.headR, weaponLen = 1.15f,
                tailLens = m.tail, shoulderXF = 0.2f, shoulderXB = -0.16f, hipXF = 0.1f, hipXB = -0.1f,
            };
        }
    }
}
