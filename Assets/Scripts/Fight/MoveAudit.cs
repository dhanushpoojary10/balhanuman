#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using B = BalHanuman.Fight.FightInput.Btn;

namespace BalHanuman.Fight
{
    /// <summary>
    /// Development tool: drives Hanuman through every move (directly, then through the real input path) against an idle dummy and writes
    /// a report to Temp/move_audit.txt. Start with MoveAudit.Run() while the fight scene is playing.
    /// </summary>
    public class MoveAudit : MonoBehaviour
    {
        public static void Run() { new GameObject("MoveAudit").AddComponent<MoveAudit>(); }
        public static void RunInput() { skipDirect = true; Run(); }
        public static void RunDefence() { skipDirect = true; onlyDefence = true; Run(); }
        static bool onlyDefence;
        static bool skipDirect;

        FightDirector d; HanumanFighter h; AhiravanaFighter b;
        readonly StringBuilder rep = new StringBuilder();
        const string Out = "Temp/move_audit.txt";

        void Log(string s) { rep.AppendLine(s); File.WriteAllText(Out, rep.ToString()); }

        IEnumerator Fresh(float dist, bool dummyBlock = false)
        {
            FightInput.Reset();
            d.Restart(FightSetup.Mode.Training);
            yield return new WaitForSecondsRealtime(1.6f);
            d = FightDirector.I;
            h = d.hanuman; b = d.boss;
            h.infiniteMeter = true; for (int i = 0; i < h.unlocked.Length; i++) h.unlocked[i] = true;
            b.dummy = dummyBlock ? AhiravanaFighter.Dummy.Block : AhiravanaFighter.Dummy.Idle;
            b.maxHp = b.hp = 100000f;
            h.transform.position = new Vector3(-2f, 0f, 0f); b.transform.position = new Vector3(-2f + dist, 0f, 0f);
            h.facing = 1; b.facing = -1; h.vel = b.vel = Vector2.zero;
            yield return new WaitForSecondsRealtime(0.25f);
        }

        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            d = FightDirector.I;
            Time.timeScale = 1f;
            Log("MOVE AUDIT  " + System.DateTime.Now);
            yield return StartCoroutine(Fresh(1.5f));
            var ids = new List<string>(h.MoveIds);
            ids.Sort();
            Log("");
            Log("== A. direct moves (id | start ok | duration vs authored | first hit at | damage at 1.4m | at 2.2m | dx | frames used | end)");
            foreach (var id in ids)
            {
                if (skipDirect) break;
                if (id == "tailthrow" || id == "liftslam" || id == "diveland") continue;       // throw halves / landing run as part of their parents
                bool air = id.StartsWith("air") || id == "dive" || id == "meteor" || id == "flykick";
                var row = new StringBuilder();
                float[] dists = { 1.4f, 2.2f };
                string res = "";
                foreach (float dist in dists)
                {
                    yield return StartCoroutine(Fresh(dist));
                    if (id == "rise") { h.scaleTarget = h.scale = 0.38f; }
                    if (air) { h.ForceJump(0f); yield return new WaitForSecondsRealtime(id == "dive" || id == "meteor" ? 0.38f : 0.3f); }
                    if (id == "meteor") { h.flying = true; }
                    float hp0 = b.hp, t0 = Time.time, x0 = h.transform.position.x, hitAt = -1f;
                    bool ok = h.Perform(id);
                    var mv = h.GetMove(id);
                    var seen = new HashSet<string>();
                    float maxDx = 0f;
                    while (Time.time - t0 < 3.5f && h.state == FState.Attack)
                    {
                        if (hitAt < 0f && b.hp < hp0) hitAt = Time.time - t0;
                        seen.Add(h.DebugFrame);
                        maxDx = Mathf.Max(maxDx, Mathf.Abs(h.transform.position.x - x0));
                        yield return null;
                    }
                    float dur = Time.time - t0;
                    if (hitAt < 0f && b.hp < hp0) hitAt = dur;
                    yield return new WaitForSecondsRealtime(0.4f);
                    string clipNames = "";
                    var names = new HashSet<string>(); foreach (var s in seen) names.Add(s.Split(':')[0]);
                    foreach (var n in names) clipNames += n + " ";
                    float dmg = hp0 - b.hp;
                    if (dist == 1.4f)
                        row.Append(id + " | " + (ok ? "ok" : "REFUSED") + " | " + dur.ToString("0.00") + " vs " + (mv != null ? mv.Length.ToString("0.00") : "?") + " | " + (hitAt < 0 ? "-" : hitAt.ToString("0.00")) + " | " + dmg.ToString("0") + " | ");
                    else row.Append(dmg.ToString("0") + " | " + maxDx.ToString("0.0") + " | " + clipNames + "| end=" + h.state + (h.grounded ? "" : "(air)") + " boss=" + b.state);
                    res = "";
                    if (dist == 1.4f && mv != null && mv.hits.Count > 0 && dmg <= 0f) res = "  <-- NO HIT at 1.4m";
                    if (dist == 2.2f && mv != null && mv.hits.Count > 0 && dmg <= 0f) res = "  (no reach to 2.2m)";
                    if (dur > (mv != null ? mv.Length : 0f) + 0.4f && !(id == "dive" || id == "meteor" || id == "flykick")) res += "  <-- LONGER THAN AUTHORED";
                    if (h.state == FState.Attack) res += "  <-- STUCK IN ATTACK";
                    if (dist == 2.2f) row.Append(res);
                }
                Log(row.ToString());
            }

            if (!onlyDefence) yield return StartCoroutine(InputTests());
            yield return StartCoroutine(DefenceTests());
            if (!skipDirect) yield return StartCoroutine(PowerTests());

            Log(""); Log("AUDIT DONE");
            FightInput.Reset();
            Destroy(gameObject);
        }

        // ---------------------------------------------------------------------------------------------------- input path
        string Seq(float dur)
        {
            var s = new StringBuilder(); string last = null; float t0 = Time.time;
            return s.ToString();
        }

        IEnumerator Tap(B btn, float hold = 0.07f)
        {
            FightInput.TouchDown(btn); yield return new WaitForSecondsRealtime(hold); FightInput.TouchUp(btn);
        }

        /// <summary>Records every move/state change for dur seconds into a one-line trace.</summary>
        IEnumerator Watch(float dur, StringBuilder into)
        {
            string last = null, lastBoss = null; float t0 = Time.time, hpPrev = b.hp;
            while (Time.time - t0 < dur)
            {
                string cur = h.Attacking ? h.move.id : h.state.ToString();
                if (h.charging) cur = "CHARGE";
                if (cur != last) { into.Append(cur + "@" + (Time.time - t0).ToString("0.00") + " "); last = cur; }
                if (b.hp < hpPrev - 0.5f) { into.Append("[hit-" + (hpPrev - b.hp).ToString("0") + "@" + (Time.time - t0).ToString("0.00") + "] "); hpPrev = b.hp; }
                string bs = b.state.ToString();
                if (bs != lastBoss) { if (lastBoss != null) into.Append("{boss:" + bs + "@" + (Time.time - t0).ToString("0.00") + "} "); lastBoss = bs; }
                yield return null;
            }
        }

        IEnumerator Scenario(string name, float dist, System.Func<IEnumerator> act, float watch, bool airStart = false)
        {
            yield return StartCoroutine(Fresh(dist));
            var tr = new StringBuilder(); float hp0 = b.hp;
            StartCoroutine(Watch(watch, tr));
            yield return StartCoroutine(act());
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, watch - 0.1f));
            FightInput.Stick = Vector2.zero;
            Log(name.PadRight(34) + " dmg=" + (hp0 - b.hp).ToString("0").PadLeft(4) + "  " + tr);
        }

        IEnumerator InputTests()
        {
            Log(""); Log("== B. real input path (stick + buttons). Trace = move@time");
            yield return StartCoroutine(Scenario("tap Mace once", 1.4f, () => TapSeq(B.Mace, 1, 0.3f), 1.3f));
            yield return StartCoroutine(Scenario("tap Mace x3 (0.28s apart)", 1.4f, () => TapSeq(B.Mace, 3, 0.28f), 2.2f));
            yield return StartCoroutine(Scenario("tap Mace x3 (0.45s apart)", 1.4f, () => TapSeq(B.Mace, 3, 0.45f), 2.6f));
            yield return StartCoroutine(Scenario("fwd + Mace (thrust)", 1.6f, () => DirTap(1, 0, B.Mace), 1.3f));
            yield return StartCoroutine(Scenario("back + Mace (whirl)", 1.4f, () => DirTap(-1, 0, B.Mace), 1.6f));
            yield return StartCoroutine(Scenario("down + Mace (sweep)", 1.4f, () => DirTap(0, -1, B.Mace), 1.3f));
            yield return StartCoroutine(Scenario("up + Mace (launcher) then air x3", 1.3f, AirCombo, 3.2f));
            yield return StartCoroutine(Scenario("hold Mace 0.5s (charge1)", 1.6f, () => HoldBtn(B.Mace, 0.5f), 2.2f));
            yield return StartCoroutine(Scenario("hold Mace 1.3s (charge2)", 1.6f, () => HoldBtn(B.Mace, 1.3f), 3.0f));
            yield return StartCoroutine(Scenario("Kick tap x2 (k1,k2)", 1.4f, () => TapSeq(B.Kick, 2, 0.3f), 1.8f));
            yield return StartCoroutine(Scenario("down + Kick (tail trip)", 1.4f, () => DirTap(0, -1, B.Kick), 1.3f));
            yield return StartCoroutine(Scenario("fwd + Kick (tail grab/throw)", 1.4f, () => DirTap(1, 0, B.Kick), 3.0f));
            yield return StartCoroutine(Scenario("back + Kick (body slam)", 1.2f, () => DirTap(-1, 0, B.Kick), 3.0f));
            yield return StartCoroutine(Scenario("jump then Kick (flying kick)", 1.6f, () => JumpThen(B.Kick, 0, 0), 2.0f));
            yield return StartCoroutine(Scenario("jump then down + Mace (dive)", 1.6f, () => JumpThen(B.Mace, 0, -1), 2.0f));
            yield return StartCoroutine(Scenario("jump then Mace x3 (air combo)", 1.4f, () => JumpThen(B.Mace, 0, 0, 3), 2.2f));
            yield return StartCoroutine(Scenario("Dodge button", 1.4f, () => TapSeq(B.Dodge, 1, 0.1f), 1.0f));
            yield return StartCoroutine(Scenario("double-tap forward (dash dodge)", 1.4f, DoubleTapFwd, 1.0f));
            yield return StartCoroutine(Scenario("Jump button", 1.4f, () => TapSeq(B.Jump, 1, 0.1f), 1.2f));
            yield return StartCoroutine(Scenario("walk forward 1s", 3.5f, WalkFwd, 1.4f));
        }

        IEnumerator TapSeq(B btn, int n, float gap)
        {
            for (int i = 0; i < n; i++) { yield return StartCoroutine(Tap(btn)); yield return new WaitForSecondsRealtime(gap); }
        }

        IEnumerator DirTap(float x, float y, B btn)
        {
            FightInput.Stick = new Vector2(x, y); yield return new WaitForSecondsRealtime(0.1f);
            yield return StartCoroutine(Tap(btn)); yield return new WaitForSecondsRealtime(0.15f);
            FightInput.Stick = Vector2.zero;
        }

        IEnumerator HoldBtn(B btn, float t)
        {
            FightInput.TouchDown(btn); yield return new WaitForSecondsRealtime(t); FightInput.TouchUp(btn);
        }

        IEnumerator AirCombo()
        {
            FightInput.Stick = new Vector2(0, 1); yield return new WaitForSecondsRealtime(0.1f);
            yield return StartCoroutine(Tap(B.Mace)); yield return new WaitForSecondsRealtime(0.15f);
            FightInput.Stick = Vector2.zero;
            yield return new WaitForSecondsRealtime(0.45f);
            for (int i = 0; i < 3; i++) { yield return StartCoroutine(Tap(B.Mace)); yield return new WaitForSecondsRealtime(0.3f); }
        }

        IEnumerator JumpThen(B btn, float x, float y, int n = 1)
        {
            yield return StartCoroutine(Tap(B.Jump)); yield return new WaitForSecondsRealtime(0.38f);
            FightInput.Stick = new Vector2(x, y);
            for (int i = 0; i < n; i++) { yield return StartCoroutine(Tap(btn)); yield return new WaitForSecondsRealtime(0.28f); }
            FightInput.Stick = Vector2.zero;
        }

        IEnumerator DoubleTapFwd()
        {
            for (int i = 0; i < 2; i++) { FightInput.Stick = new Vector2(1, 0); yield return new WaitForSecondsRealtime(0.07f); FightInput.Stick = Vector2.zero; yield return new WaitForSecondsRealtime(0.08f); }
        }

        IEnumerator WalkFwd() { FightInput.Stick = new Vector2(1, 0); yield return new WaitForSecondsRealtime(1.0f); FightInput.Stick = Vector2.zero; }

        // ---------------------------------------------------------------------------------------------------- defence and specials
        IEnumerator AttackAt(float lead)
        {
            var jab = b.GetMove("jab"); float t0 = jab.hits[0].t0;
            b.StartMove(jab);
            float guard = Time.time;
            while (b.moveT < t0 - lead && Time.time - guard < 1.5f) yield return null;
        }

        IEnumerator DefenceTests()
        {
            Log(""); Log("== D. defence, reflect, meteor, named combo");
            // parry: tap back just before the jab lands
            yield return StartCoroutine(Fresh(1.6f));
            float hp0 = h.hp; var tr = new StringBuilder(); StartCoroutine(Watch(1.8f, tr));
            yield return StartCoroutine(AttackAt(0.10f));
            FightInput.Stick = new Vector2(-1, 0); yield return new WaitForSecondsRealtime(0.35f); FightInput.Stick = Vector2.zero;
            yield return new WaitForSecondsRealtime(1.2f);
            Log("parry (tap back 0.10s early)".PadRight(34) + " hanumanHpLoss=" + (hp0 - h.hp).ToString("0") + " bossState=" + b.state + "  " + tr);

            // parry too early / late, and plain block when already holding back
            yield return StartCoroutine(Fresh(1.6f));
            hp0 = h.hp; tr = new StringBuilder(); StartCoroutine(Watch(1.8f, tr));
            FightInput.Stick = new Vector2(-1, 0); yield return new WaitForSecondsRealtime(0.5f);
            yield return StartCoroutine(AttackAt(0.0f));
            yield return new WaitForSecondsRealtime(0.6f); FightInput.Stick = Vector2.zero;
            yield return new WaitForSecondsRealtime(0.8f);
            Log("block (back held early)".PadRight(34) + " hanumanHpLoss=" + (hp0 - h.hp).ToString("0") + " (jab damage " + b.GetMove("jab").hits[0].damage + ")  " + tr);

            // no input at all: takes the hit
            yield return StartCoroutine(Fresh(1.6f));
            hp0 = h.hp; tr = new StringBuilder(); StartCoroutine(Watch(1.6f, tr));
            yield return StartCoroutine(AttackAt(0.0f));
            yield return new WaitForSecondsRealtime(1.2f);
            Log("no defence".PadRight(34) + " hanumanHpLoss=" + (hp0 - h.hp).ToString("0") + "  " + tr);

            // perfect dodge
            yield return StartCoroutine(Fresh(1.6f));
            hp0 = h.hp; tr = new StringBuilder(); StartCoroutine(Watch(1.8f, tr));
            yield return StartCoroutine(AttackAt(0.10f));
            yield return StartCoroutine(Tap(B.Dodge)); float ts = Time.timeScale; yield return new WaitForSecondsRealtime(0.15f); ts = Mathf.Min(ts, Time.timeScale);
            yield return new WaitForSecondsRealtime(1.3f);
            Log("perfect dodge (0.10s early)".PadRight(34) + " hanumanHpLoss=" + (hp0 - h.hp).ToString("0") + " slowMoSeen=" + (ts < 0.9f) + "  " + tr);

            // reflect the dark orb with the gada
            yield return StartCoroutine(Fresh(5.0f));
            float bhp0 = b.hp; hp0 = h.hp; tr = new StringBuilder(); StartCoroutine(Watch(3.2f, tr));
            b.StartMove(b.GetMove("orb"));
            float g = Time.time; while (!d.ProjectileNear(h, 6.0f) && Time.time - g < 2.5f) yield return null;
            float seenAt = Time.time - g;
            yield return StartCoroutine(Tap(B.Mace)); yield return new WaitForSecondsRealtime(1.8f);
            Log("orb reflect (tap gada as it nears)".PadRight(34) + " orbNearAt=" + seenAt.ToString("0.00") + " bossHpLoss=" + (bhp0 - b.hp).ToString("0") + " hanumanHpLoss=" + (hp0 - h.hp).ToString("0") + "  " + tr);

            // orb with no reaction
            yield return StartCoroutine(Fresh(5.0f));
            hp0 = h.hp; b.StartMove(b.GetMove("orb")); yield return new WaitForSecondsRealtime(3.0f);
            Log("orb, no reaction".PadRight(34) + " hanumanHpLoss=" + (hp0 - h.hp).ToString("0"));

            // flight + meteor
            yield return StartCoroutine(Fresh(2.0f));
            tr = new StringBuilder(); StartCoroutine(Watch(3.0f, tr)); bhp0 = b.hp;
            yield return StartCoroutine(Tap(B.P2)); yield return new WaitForSecondsRealtime(0.7f);
            FightInput.Stick = new Vector2(0.5f, 0); yield return new WaitForSecondsRealtime(0.4f);
            FightInput.Stick = new Vector2(0, -1); yield return new WaitForSecondsRealtime(0.1f);
            yield return StartCoroutine(Tap(B.Mace)); yield return new WaitForSecondsRealtime(1.6f); FightInput.Stick = Vector2.zero;
            Log("flight, then down + gada (meteor)".PadRight(34) + " bossHpLoss=" + (bhp0 - b.hp).ToString("0") + "  " + tr);

            // Vanara Fury: GADA, GADA, TAIL, UP+GADA
            yield return StartCoroutine(Fresh(1.4f));
            tr = new StringBuilder(); StartCoroutine(Watch(3.0f, tr)); bhp0 = b.hp;
            yield return StartCoroutine(Tap(B.Mace)); yield return new WaitForSecondsRealtime(0.24f);
            yield return StartCoroutine(Tap(B.Mace)); yield return new WaitForSecondsRealtime(0.24f);
            yield return StartCoroutine(Tap(B.Kick)); yield return new WaitForSecondsRealtime(0.30f);
            FightInput.Stick = new Vector2(0, 1); yield return StartCoroutine(Tap(B.Mace)); yield return new WaitForSecondsRealtime(0.15f); FightInput.Stick = Vector2.zero;
            yield return new WaitForSecondsRealtime(1.8f);
            Log("Vanara Fury (J J K UP+J)".PadRight(34) + " bossHpLoss=" + (bhp0 - b.hp).ToString("0") + "  " + tr);
        }

        // ---------------------------------------------------------------------------------------------------- powers
        IEnumerator PowerTests()
        {
            Log(""); Log("== C. boons and ultimate");
            string[] names = { "Vayu", "Flight", "FireTail", "Shrink", "Vajra", "Growth" };
            for (int i = 0; i < 6; i++)
            {
                yield return StartCoroutine(Fresh(2.0f));
                var tr = new StringBuilder(); StartCoroutine(Watch(2.4f, tr));
                float hp0 = b.hp; bool inactive = !h.Active(HanumanFighter.Boons[i]);
                yield return StartCoroutine(Tap(B.P1 + i));
                yield return new WaitForSecondsRealtime(0.3f);
                bool active = h.Active(HanumanFighter.Boons[i]);
                string extra = "scale=" + h.scale.ToString("0.00") + " flying=" + h.flying + " speedMul=" + h.speedMul.ToString("0.00");
                // try a follow-up attack under the boon
                FightInput.Stick = Vector2.zero;
                if (i == 3) { yield return new WaitForSecondsRealtime(0.5f); yield return StartCoroutine(Tap(B.Mace)); }               // shrink -> rise
                else if (i == 5) { yield return new WaitForSecondsRealtime(1.0f); FightInput.Stick = new Vector2(0, -1); yield return new WaitForSecondsRealtime(0.1f); yield return StartCoroutine(Tap(B.Mace)); FightInput.Stick = Vector2.zero; }   // growth -> stomp
                else if (i == 2) { yield return new WaitForSecondsRealtime(0.8f); yield return StartCoroutine(Tap(B.Kick)); }             // fire -> firewhip
                else if (i == 1) { FightInput.Stick = new Vector2(1, 0.4f); yield return new WaitForSecondsRealtime(0.5f); FightInput.Stick = Vector2.zero; yield return StartCoroutine(Tap(B.Mace)); }
                else yield return StartCoroutine(Tap(B.Mace));
                yield return new WaitForSecondsRealtime(1.6f);
                Log(("power " + names[i]).PadRight(34) + " activated=" + (inactive && active) + " " + extra + " dmgAfter=" + (hp0 - b.hp).ToString("0") + "  " + tr);
            }

            yield return StartCoroutine(Fresh(2.0f));
            h.bhakti = 3f;
            float t0 = Time.unscaledTime;
            bool started = d.TryUltimate(h);
            float live0 = -1f;
            while (Time.unscaledTime - t0 < 40f)
            {
                if (live0 < 0f && d.ControlsLive && Time.unscaledTime - t0 > 2f) { live0 = Time.unscaledTime - t0; break; }
                yield return null;
            }
            Log("ultimate".PadRight(34) + " started=" + started + " controls back after " + live0.ToString("0.0") + "s  bossHp=" + b.hp.ToString("0") + " hanumanState=" + h.state);
        }
    }
}
#endif
