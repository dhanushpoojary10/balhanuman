using System.Collections.Generic;
using UnityEngine;

namespace BalHanuman.Fight
{
    /// <summary>
    /// What Ahiravana has noticed about Hanuman in the last few seconds: which moves he spams, how long he has been blocking,
    /// how often he jumps and whether he is backing away. The boss brain reads this to pick answers instead of rolling dice.
    /// </summary>
    public class BossObserver
    {
        const float Window = 6f;
        struct Ev { public float t; public string id; }
        readonly List<Ev> moves = new List<Ev>();
        readonly List<float> jumps = new List<float>();
        MoveDef lastMove; FState lastState;

        public float BlockStreak { get; private set; }
        public float Retreat { get; private set; }

        public void Reset() { moves.Clear(); jumps.Clear(); lastMove = null; BlockStreak = Retreat = 0f; }

        public void Tick(Fighter opp, float dt, int towardOpp)
        {
            float now = Time.time;
            if (opp.Attacking && opp.move != lastMove) { moves.Add(new Ev { t = now, id = opp.move.id }); lastMove = opp.move; }
            if (!opp.Attacking) lastMove = null;
            if (opp.state == FState.Jump && lastState != FState.Jump && opp.state != FState.Launched) jumps.Add(now);
            lastState = opp.state;
            bool blocking = opp.state == FState.Block || opp.state == FState.Blockstun;
            BlockStreak = blocking ? BlockStreak + dt : 0f;
            bool away = opp.vel.x * -towardOpp > 1.5f && opp.grounded;
            Retreat = away ? Retreat + dt : Mathf.Max(0f, Retreat - dt * 2f);
            moves.RemoveAll(e => now - e.t > Window); jumps.RemoveAll(t => now - t > Window);
        }

        public int Count(string id) { int n = 0; foreach (var e in moves) if (e.id == id) n++; return n; }
        public int JumpCount => jumps.Count;

        /// <summary>The attack Hanuman has used three or more times in the window (null if none).</summary>
        public string Spammed()
        {
            string best = null; int bestN = 2;
            foreach (var e in moves) { int n = Count(e.id); if (n > bestN) { bestN = n; best = e.id; } }
            return best;
        }
    }

    /// <summary>Named attack strings, each with a tell the player can learn. Gated by round, phase and distance.</summary>
    public static class BossPatterns
    {
        public class Pattern
        {
            public string name; public string[] steps; public int minRound, minPhase; public float minDist, maxDist, weight;
            public Pattern(string n, float w, int r, int p, float d0, float d1, params string[] s) { name = n; weight = w; minRound = r; minPhase = p; minDist = d0; maxDist = d1; steps = s; }
        }

        public static readonly Pattern[] All =
        {
            // close range (reach about 2.4)
            new Pattern("JabJabSweep",  3.0f, 1, 0, 0f, 2.6f, "jab", "jab", "sweep"),          // two highs then a low: hold block, then crouch
            new Pattern("ComboSweep",   2.0f, 1, 0, 0f, 2.6f, "combo", "sweep"),
            new Pattern("Overhead",     1.2f, 1, 0, 0f, 3.0f, "overhead"),                      // slow and loud: dodge or jump it
            new Pattern("FeintGrab",    2.0f, 1, 1, 0f, 2.2f, "jab", "grab"),                   // punishes players who only block
            new Pattern("ShadowCombo",  1.3f, 2, 0, 0f, 3.5f, "vanish", "combo"),               // teleports in, then strikes
            // mid range
            new Pattern("OrbOnly",      2.0f, 1, 0, 2.6f, 9.0f, "orb"),
            new Pattern("OrbOverhead",  2.0f, 1, 1, 2.6f, 5.5f, "orb", "walk", "overhead"),     // punishes backing away
            new Pattern("Illusions",    1.4f, 2, 1, 2.6f, 9.0f, "summon"),
            new Pattern("ShadowStep",   1.0f, 2, 0, 2.6f, 9.0f, "vanish"),
            // far
            new Pattern("OrbBarrage",   1.5f, 2, 2, 5.0f, 12f, "barrage"),
        };
    }
}
