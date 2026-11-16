using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalHanuman.Fight
{
    /// <summary>
    /// Per-move balance in one place: speed, damage, hit-stop, shake, stun, recovery, armor and the boss's tells.
    /// The defaults below are applied after MoveLib builds the moves. An optional Resources/Level02/combat_tuning.json can override any of them
    /// ({"moves":[{"id":"m1","damage":28}]}); fields that are left out keep the code value.
    /// </summary>
    public static class CombatTuning
    {
        [Serializable]
        public class MoveTune
        {
            public string id;
            public float speedMul = -1f, damage = -1f, hitstop = -1f, shake = -1f, stun = -1f, endLag = -1f, whiffLag = -1f, armorStart = -1f, holdAt = -1f, holdLen = -1f;
            public int armorHits = -1;
            public string tele;             // "Blockable", "Grab" or "Projectile"
        }
        [Serializable] class File { public MoveTune[] moves; }

        static MoveTune T(string id, float speed = -1f, float dmg = -1f, float hs = -1f, float sh = -1f, float stun = -1f, float endLag = -1f, int armorHits = -1, float armorStart = -1f,
            string tele = null, float holdAt = -1f, float holdLen = -1f)
            => new MoveTune { id = id, speedMul = speed, damage = dmg, hitstop = hs, shake = sh, stun = stun, endLag = endLag, armorHits = armorHits, armorStart = armorStart, tele = tele, holdAt = holdAt, holdLen = holdLen };

        // ---------------------------------------------------------------- Hanuman: distinct roles (see Docs/Level02_CombatDesign.md, B1)
        static readonly MoveTune[] Hanuman =
        {
            T("m1",  speed: 1.25f, dmg: 30f, hs: 0.04f, sh: 0.02f, stun: 0.36f),         // fast starter
            T("m2",  speed: 1.05f, dmg: 40f, hs: 0.06f, sh: 0.04f),                      // pressure
            T("m3",  speed: 0.95f, dmg: 85f, endLag: 0.12f, armorHits: 1, armorStart: 0.10f),   // finisher: armored swing, punishable on whiff
            T("k1",  speed: 1.2f,  dmg: 30f, hs: 0.04f, sh: 0.02f),                      // low-risk poke
            T("k2",  speed: 0.9f,  dmg: 45f),                                            // wall pusher
            T("thrust", endLag: 0.15f, hs: 0.07f),                                       // gap closer, loses to anything on whiff
            T("sweep", speed: 0.95f),
            T("charge1", armorHits: 1, armorStart: 0.10f),                               // heavy: soaks a hit while it swings
            T("charge2", armorHits: 2, armorStart: 0.10f),
            T("tailgrab", endLag: 0.30f),                                                // throws are punishable when they miss
            T("bodyslam", endLag: 0.30f),
        };

        // ---------------------------------------------------------------- Ahiravana: readable tells, committed heavies (design B7)
        static readonly MoveTune[] Boss =
        {
            T("jab",      tele: "Blockable",  holdAt: 0.08f, holdLen: 0.10f),           // startup 0.12 -> 0.22
            T("combo",    tele: "Blockable",  holdAt: 0.07f, holdLen: 0.12f),           // 0.10 -> 0.22, the string starts after a clear tell
            T("sweep",    tele: "Blockable",  holdAt: 0.07f, holdLen: 0.20f),           // 0.15 -> 0.35
            T("overhead", tele: "Blockable"),                                           // 0.46 already
            T("grab",     tele: "Grab",       holdAt: 0.12f, holdLen: 0.08f, endLag: 0.12f),   // 0.26 -> 0.34, red: you cannot block it
            T("orb",      tele: "Projectile", holdAt: 0.12f, holdLen: 0.15f, endLag: 0.10f, armorHits: 1, armorStart: 0.15f),
            T("barrage",  tele: "Projectile"),
            T("summon",   tele: "Projectile", armorHits: 2, armorStart: 0.15f),
            T("gslam",    tele: "Blockable",  armorHits: 1, armorStart: 0.30f),
            T("gsweep",   tele: "Blockable",  holdAt: 0.07f, holdLen: 0.20f),
        };

        public static void ApplyHanuman(Dictionary<string, MoveDef> m) { Apply(m, Hanuman); ApplyFile(m); }
        public static void ApplyBoss(Dictionary<string, MoveDef> m) { Apply(m, Boss); ApplyFile(m); }

        static void ApplyFile(Dictionary<string, MoveDef> m)
        {
            var ta = Resources.Load<TextAsset>("Level02/combat_tuning");
            if (ta == null) return;
            try { var f = JsonUtility.FromJson<File>(ta.text); if (f != null && f.moves != null) Apply(m, f.moves); }
            catch (Exception e) { Debug.LogWarning("combat_tuning.json: " + e.Message); }
        }

        static void Apply(Dictionary<string, MoveDef> moves, MoveTune[] tunes)
        {
            foreach (var t in tunes)
            {
                if (!moves.TryGetValue(t.id, out var d) || d == null) continue;
                if (t.speedMul > 0f) d.speedMul = t.speedMul;
                if (t.endLag >= 0f) d.endLag = t.endLag;
                if (t.whiffLag >= 0f) d.whiffLag = t.whiffLag;
                if (t.armorHits >= 0) d.armorHits = t.armorHits;
                if (t.armorStart >= 0f) d.armorStart = t.armorStart;
                if (t.holdAt >= 0f) d.holdAt = t.holdAt;
                if (t.holdLen >= 0f) d.holdLen = t.holdLen;
                if (!string.IsNullOrEmpty(t.tele) && Enum.TryParse(t.tele, out TeleClass tc)) d.tele = tc;
                if (d.hits.Count > 0)
                {
                    var w = d.hits[0];
                    if (t.damage >= 0f) w.damage = t.damage;
                    if (t.stun >= 0f) w.stun = t.stun;
                    foreach (var h in d.hits)
                    {
                        if (t.hitstop >= 0f) h.hitstop = t.hitstop;
                        if (t.shake >= 0f) h.shake = t.shake;
                    }
                }
            }
        }
    }
}
