using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalHanuman.Fight
{
    /// <summary>
    /// Frame-by-frame art for Hanuman (cut from the AI sprite sheets by the frame tool): idle, walk forward / back, jump, fall, dodge roll.
    /// frames.json lists each clip's fps, loop flag, world scale k (world units per source pixel) and, per frame, the pivot pixel that sits on the
    /// fighter's root. Clips that are missing fall back to the skeleton.
    /// </summary>
    public static class HanumanFrames
    {
        [Serializable] public class Frame { public string file; public float px, py, tx, ty; public int w, h; [NonSerialized] public Sprite sprite; }
        [Serializable] public class Clip { public string name; public float fps; public bool loop, hold; public float k; public Frame[] frames; public float Length => fps > 0 ? frames.Length / fps : 0f; }
        [Serializable] class Data { public Clip[] clips; }

        static Dictionary<string, Clip> clips; static bool loaded;

        static void Alias(string name, string src, int from, int to, float fps, bool loop)
        {
            if (clips.ContainsKey(name) || !clips.TryGetValue(src, out var c)) return;
            to = Mathf.Min(to, c.frames.Length - 1);
            var f = new Frame[to - from + 1];
            Array.Copy(c.frames, from, f, 0, f.Length);
            clips[name] = new Clip { name = name, fps = fps, loop = loop, hold = !loop, k = c.k, frames = f };
        }

        public static Clip Get(string name)
        {
            if (!loaded)
            {
                loaded = true; clips = new Dictionary<string, Clip>();
                var ta = Resources.Load<TextAsset>("Level02/Hanuman/frames/frames");
                if (ta != null)
                {
                    try
                    {
                        var d = JsonUtility.FromJson<Data>(ta.text);
                        foreach (var c in d.clips)
                        {
                            bool ok = true;
                            foreach (var f in c.frames) { f.sprite = Resources.Load<Sprite>(f.file); if (f.sprite == null) ok = false; }
                            if (ok) clips[c.name] = c; else Debug.LogWarning("Hanuman frames: missing sprites for clip " + c.name);
                        }
                    }
                    catch (Exception e) { Debug.LogWarning("Hanuman frames: " + e.Message); }
                    // poses that have no sheet of their own yet borrow the closest painted frames
                    Alias("roar", "overhead", 4, 7, 8f, true);        // arms and gada raised high: power-up, Panchmukhi
                    Alias("victory", "overhead", 4, 7, 6f, true);
                    Alias("fly", "fall", 0, 3, 8f, true);             // hovering
                    Alias("charge", "crouch", 0, 3, 8f, true);
                }
            }
            return name != null && clips.TryGetValue(name, out var cl) ? cl : null;
        }
    }
}
