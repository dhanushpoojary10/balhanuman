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
        public static void RunCombat() { onlyCombat = true; Run(); }
        static bool onlyCombat;
        public static void RunBrain() { onlyBrain = true; Run(); }
        static bool onlyBrain, onlyBot, onlyStuck, onlyJuggle;
        public static void RunJuggle() { onlyJuggle = true; Run(); }
        public static void RunStuck() { onlyStuck = true; Run(); }
        public static void RunBot() { onlyBot = true; Run(); }
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
            if (onlyJuggle)
            {
                yield return StartCoroutine(KickFuzz(60f, true));
                yield return StartCoroutine(KickFuzz(45f, false));
                yield return StartCoroutine(JuggleTests());
                Log(""); Log("AUDIT DONE");
                FightInput.Reset(); Destroy(gameObject);
                yield break;
            }
            if (onlyStuck)
            {
                yield return StartCoroutine(StuckTests());
                Log(""); Log("AUDIT DONE");
                FightInput.Reset(); Destroy(gameObject);
                yield break;
            }
            if (onlyBot)
            {
                yield return StartCoroutine(BotTests());
                Log(""); Log("AUDIT DONE");
                FightInput.Reset(); Destroy(gameObject);
                yield break;
            }
            if (onlyBrain)
            {
                yield return StartCoroutine(JumpFeelTests());
                yield return StartCoroutine(BrainTests());
                Log(""); Log("AUDIT DONE");
                FightInput.Reset(); Destroy(gameObject);
                yield break;
            }
            if (onlyCombat)
            {
                FrameData();
                yield return StartCoroutine(ArmorTests());
                yield return StartCoroutine(ClashTests());
                yield return StartCoroutine(WhiffChainTests());
                yield return StartCoroutine(GuardTests());
                yield return StartCoroutine(ReactionTests());
                Log(""); Log("AUDIT DONE");
                FightInput.Reset(); Destroy(gameObject);
                yield break;
            }
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

        // ---------------------------------------------------------------------------------------------------- combat design checks
        void FrameDataRow(string who, string id, MoveDef m)
        {
            float sp = Mathf.Max(0.01f, m.speedMul);
            float t0 = 99f, t1 = 0f; foreach (var w in m.hits) { t0 = Mathf.Min(t0, w.t0); t1 = Mathf.Max(t1, w.t1); }
            float total = (m.Length + m.endLag) / sp + m.holdLen;
            if (m.hits.Count == 0) { Log(who + " " + id.PadRight(9) + " total=" + total.ToString("0.00") + " (no hit window)  tele=" + m.tele + " armor=" + m.armorHits + (m.armorHits > 0 ? "@" + m.armorStart.ToString("0.00") : "")); return; }
            float startup = t0 / sp + m.holdLen, active = (t1 - t0) / sp, recov = total - startup - active;
            var w0 = m.hits[0];
            Log(who + " " + id.PadRight(9) + " startup=" + startup.ToString("0.00") + " active=" + active.ToString("0.00") + " recovery=" + recov.ToString("0.00") + " total=" + total.ToString("0.00")
                + " dmg=" + w0.damage.ToString("0") + " hitstop=" + w0.hitstop.ToString("0.00") + " shake=" + w0.shake.ToString("0.00") + " stun=" + w0.stun.ToString("0.00") + " prio=" + w0.Prio
                + " cancel@" + (m.cancelFrom > 50f ? "-" : (m.cancelFrom / sp).ToString("0.00")) + " tele=" + m.tele + " armor=" + m.armorHits + (m.armorHits > 0 ? "@" + (m.armorStart / sp).ToString("0.00") : ""));
        }

        void FrameData()
        {
            Log(""); Log("== E. frame data (real seconds): startup includes the boss's tell hold");
            var ids = new List<string>(h.MoveIds); ids.Sort();
            foreach (var id in ids) { var m = h.GetMove(id); if (m != null && id != "tailthrow" && id != "liftslam" && id != "diveland") FrameDataRow("H", id, m); }
            foreach (var id in new[] { "jab", "combo", "sweep", "overhead", "grab", "orb", "barrage", "summon", "gslam", "gsweep" }) { var m = b.GetMove(id); if (m != null) FrameDataRow("B", id, m); }
        }

        IEnumerator ArmorTests()
        {
            Log(""); Log("== F. armor: repeated jab hits on an armored move (state after each hit; Attack = absorbed, Hitstun = interrupted)");
            var jabW = new HitWindow { damage = 45f, knock = new Vector2(2f, 0f), stun = 0.34f };
            foreach (var id in new[] { "m3", "charge1", "charge2", "m1" })
            {
                yield return StartCoroutine(Fresh(1.4f));
                h.Perform(id); var mv = h.GetMove(id);
                yield return new WaitForSecondsRealtime(Mathf.Max(0.02f, mv.armorStart / Mathf.Max(0.01f, mv.speedMul) + 0.03f));
                var sb = new StringBuilder(); float hp0 = h.hp;
                for (int i = 0; i < 4; i++)
                {
                    if (h.state != FState.Attack) break;
                    h.ReceiveHit(b, jabW, h.Center);
                    yield return null; yield return null;
                    sb.Append("hit" + (i + 1) + "->" + h.state + " ");
                    if (h.state == FState.Attack && h.hitstop > 0f) yield return new WaitForSecondsRealtime(0.1f);
                }
                Log((id + " (armorHits=" + mv.armorHits + ")").PadRight(26) + sb + " hpLoss=" + (hp0 - h.hp).ToString("0"));
            }
            foreach (var id in new[] { "summon", "orb", "gslam" })
            {
                yield return StartCoroutine(Fresh(1.4f));
                if (id == "gslam") { b.scaleTarget = b.scale = 2.2f; }
                b.StartMove(b.GetMove(id)); var mv = b.GetMove(id);
                yield return new WaitForSecondsRealtime(mv.armorStart + mv.holdLen + 0.05f);
                var w = h.GetMove("m1").hits[0]; var sb = new StringBuilder();
                for (int i = 0; i < 4; i++)
                {
                    if (b.state != FState.Attack) break;
                    b.ReceiveHit(h, w, b.Center);
                    yield return null; yield return null;
                    sb.Append("hit" + (i + 1) + "->" + b.state + " ");
                    if (b.state == FState.Attack) yield return new WaitForSecondsRealtime(0.12f);
                }
                Log(("boss " + id + " (armorHits=" + mv.armorHits + ")").PadRight(26) + sb);
            }
            yield return StartCoroutine(Fresh(1.4f));
            h.Perform("charge2"); yield return new WaitForSecondsRealtime(0.25f);
            h.ReceiveHit(b, new HitWindow { damage = 60f, level = HitLevel.Unblockable, knock = new Vector2(3f, 0f) }, h.Center); yield return null; yield return null;
            Log("unblockable vs armor".PadRight(26) + "-> " + h.state + " (expect Hitstun / Launched)");
        }

        IEnumerator ClashTests()
        {
            Log(""); Log("== G. priority: simultaneous active attacks");
            yield return StartCoroutine(Fresh(1.3f));
            var jab = b.GetMove("jab"); var m1 = h.GetMove("m1");
            b.StartMove(jab); h.StartMove(m1);
            float hp0 = h.hp, bp0 = b.hp;
            b.moveT = jab.hits[0].t0 + 0.005f; h.moveT = m1.hits[0].t0 + 0.005f;
            yield return new WaitForSecondsRealtime(0.5f);
            Log("jab vs gada jab (equal)".PadRight(30) + "hanumanHpLoss=" + (hp0 - h.hp).ToString("0") + " bossHpLoss=" + (bp0 - b.hp).ToString("0") + " (expect 0 / 0 = clash)");
            yield return StartCoroutine(Fresh(1.3f));
            var ov = b.GetMove("overhead");
            b.StartMove(ov); h.StartMove(m1);
            hp0 = h.hp; bp0 = b.hp;
            b.moveT = ov.hits[0].t0 + 0.005f; h.moveT = m1.hits[0].t0 + 0.005f;
            yield return new WaitForSecondsRealtime(0.5f);
            Log("overhead vs gada jab (heavy)".PadRight(30) + "hanumanHpLoss=" + (hp0 - h.hp).ToString("0") + " bossHpLoss=" + (bp0 - b.hp).ToString("0") + " (expect Hanuman hit, boss not)");
        }

        IEnumerator WhiffChainTests()
        {
            Log(""); Log("== H. whiff vs hit chain (taps 0.2s apart): the second move's start time shows the whiff lag");
            yield return StartCoroutine(Scenario("gada x2 on HIT (boss 1.4 m)", 1.4f, () => TapSeq(B.Mace, 2, 0.2f), 1.2f));
            yield return StartCoroutine(Scenario("gada x2 on WHIFF (boss 5 m)", 5.0f, () => TapSeq(B.Mace, 2, 0.2f), 1.2f));
            yield return StartCoroutine(Scenario("kick x2 on HIT", 1.4f, () => TapSeq(B.Kick, 2, 0.2f), 1.2f));
            yield return StartCoroutine(Scenario("kick x2 on WHIFF", 5.0f, () => TapSeq(B.Kick, 2, 0.2f), 1.2f));
        }

        IEnumerator GuardTests()
        {
            Log(""); Log("== I. guard: holding back against repeated boss jabs");
            yield return StartCoroutine(Fresh(1.6f));
            var sb = new StringBuilder(); float hp0 = h.hp;
            FightInput.Stick = new Vector2(-1, 0);
            yield return new WaitForSecondsRealtime(0.3f);
            for (int i = 0; i < 8; i++)
            {
                if (h.state == FState.Stunned) { sb.Append("[STUNNED] "); break; }
                b.StartMove(b.GetMove("jab"));
                yield return new WaitForSecondsRealtime(0.7f);
                sb.Append("jab" + (i + 1) + ":guard=" + h.guard.ToString("0") + "," + h.state + " ");
            }
            FightInput.Stick = Vector2.zero;
            Log("block spam".PadRight(14) + sb + " hpLoss=" + (hp0 - h.hp).ToString("0"));
            yield return new WaitForSecondsRealtime(2.5f);
            Log("guard after 2.5 s rest: " + h.guard.ToString("0") + " / " + h.guardMax.ToString("0"));
        }

        IEnumerator ReactionTests()
        {
            Log(""); Log("== J. boss reaction: set to block everything he can react to (blockChance 1), otherwise standing still");
            foreach (var id in new[] { "m1", "k1", "m3", "thrust", "charge1" })
            {
                int hits = 0, blocked = 0;
                for (int i = 0; i < 5; i++)
                {
                    yield return StartCoroutine(Fresh(1.4f));
                    b.dummy = AhiravanaFighter.Dummy.Fight; b.blockChance = 1f; b.aggression = 0.001f;
                    typeof(AhiravanaFighter).GetField("thinkT", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(b, 1000f);      // no attacks of his own, only reactions
                    float hp0 = b.hp;
                    h.Perform(id);
                    float s = Time.time; bool wasBlock = false;
                    while (Time.time - s < 1.3f) { if (b.state == FState.Blockstun || b.state == FState.Block) wasBlock = true; yield return null; }
                    if (b.hp < hp0 - 20f) hits++; else if (wasBlock) blocked++;
                }
                Log(id.PadRight(8) + " landed=" + hits + " blocked=" + blocked + " of 5");
            }
        }

        // ---------------------------------------------------------------------------------------------------- prompts 5-8 checks
        string Summarise(List<string> log)
        {
            var counts = new Dictionary<string, int>();
            foreach (var e in log) { string k = e.StartsWith("step:") ? "step" : e; counts[k] = counts.TryGetValue(k, out var n) ? n + 1 : 1; }
            var sb = new StringBuilder();
            foreach (var kv in counts) sb.Append(kv.Key + " x" + kv.Value + "; ");
            return sb.ToString();
        }

        IEnumerator BrainBot(string name, float dur, System.Func<IEnumerator> act)
        {
            yield return StartCoroutine(Fresh(1.8f));
            h.maxHp = h.hp = 1000000f; b.maxHp = b.hp = 1000000f; b.dummy = AhiravanaFighter.Dummy.Fight; b.SetRound(2);
            b.pickLog.Clear(); b.aggression = 1.6f;
            StartCoroutine(act());
            float t0 = Time.time; int hits = 0; float hp0 = h.hp;
            while (Time.time - t0 < dur) { h.hp = Mathf.Max(h.hp, 999000f); yield return null; }
            FightInput.Reset(); h.demoBlock = false;
            Log(name.PadRight(22) + Summarise(b.pickLog));
        }

        IEnumerator SpamM1() { for (int i = 0; i < 30; i++) { h.Perform("m1"); yield return new WaitForSecondsRealtime(0.32f); } }
        IEnumerator Turtle() { h.demoBlock = true; yield return new WaitForSecondsRealtime(20f); }
        IEnumerator JumpAbout() { for (int i = 0; i < 10; i++) { h.ForceJump(0f); yield return new WaitForSecondsRealtime(0.9f); } }
        IEnumerator BackAway() { float t = Time.time; while (Time.time - t < 20f) { FightInput.Stick = new Vector2(-h.facing, 0f); yield return null; } }

        IEnumerator BrainTests()
        {
            Log(""); Log("== K. boss brain: what he chooses against different players (8 s each)");
            yield return StartCoroutine(BrainBot("gada spam", 8f, SpamM1));
            yield return StartCoroutine(BrainBot("turtle (hold block)", 14f, Turtle));
            yield return StartCoroutine(BrainBot("jumping around", 8f, JumpAbout));
            yield return StartCoroutine(BrainBot("backing away", 8f, BackAway));
            yield return StartCoroutine(BrainBot("standing still", 8f, () => Idle()));

            Log(""); Log("== L. phases: health thresholds inside a round (60 % and 30 %), no refill");
            yield return StartCoroutine(Fresh(2.5f));
            d.forcePhases = true; b.SetRound(2); b.dummy = AhiravanaFighter.Dummy.Fight; b.maxHp = 1000f; b.hp = 1000f; h.maxHp = h.hp = 1000000f;
            var thinkF = typeof(AhiravanaFighter).GetField("thinkT", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            thinkF.SetValue(b, 1000f);
            Log("start        phase=" + b.phase + " aggression=" + b.aggression.ToString("0.00") + " blockChance=" + b.blockChance.ToString("0.00"));
            b.hp = 560f; yield return new WaitForSecondsRealtime(0.4f); string midState = b.state.ToString();
            yield return new WaitForSecondsRealtime(1.8f);
            Log("hp 56 %      during transition state=" + midState + "; after: phase=" + b.phase + " aggression=" + b.aggression.ToString("0.00") + " blockChance=" + b.blockChance.ToString("0.00") + " hp=" + b.hp.ToString("0"));
            b.hp = 250f; yield return new WaitForSecondsRealtime(2.2f);
            Log("hp 25 %      phase=" + b.phase + " aggression=" + b.aggression.ToString("0.00") + " blockChance=" + b.blockChance.ToString("0.00") + " hp=" + b.hp.ToString("0"));
            d.forcePhases = false;
        }

        IEnumerator Idle() { yield return new WaitForSecondsRealtime(20f); }

        IEnumerator JumpFeelTests()
        {
            Log(""); Log("== M. jump feel: height and air time, tap vs hold; landing recovery; fighter contact");
            foreach (var hold in new[] { 0.06f, 0.5f })
            {
                yield return StartCoroutine(Fresh(6f));
                float y0 = 0f, maxY = 0f, tLaunch = -1f, tLand = -1f; bool airborne = false;
                FightInput.TouchDown(B.Jump); float t0 = Time.time;
                while (Time.time - t0 < 1.4f)
                {
                    if (Time.time - t0 > hold) FightInput.TouchUp(B.Jump);
                    float y = h.transform.position.y; maxY = Mathf.Max(maxY, y);
                    if (!airborne && y > 0.02f) { airborne = true; tLaunch = Time.time - t0; }
                    if (airborne && tLand < 0f && y <= 0.001f && h.grounded) tLand = Time.time - t0;
                    yield return null;
                }
                FightInput.Reset();
                Log("jump " + (hold < 0.2f ? "tap " : "hold") + "   take-off after " + tLaunch.ToString("0.00") + " s, peak " + maxY.ToString("0.00") + " m, air time " + (tLand - tLaunch).ToString("0.00") + " s");
            }
            // landing recovery: attack pressed the moment he lands
            yield return StartCoroutine(Fresh(6f));
            h.ForceJump(0f);
            while (!h.grounded || h.transform.position.y > 0.01f) yield return null;
            while (h.transform.position.y > 0.001f) yield return null;
            float landT = Time.time; float actT = -1f;
            FightInput.TouchDown(B.Kick);
            while (Time.time - landT < 0.6f) { if (h.Attacking) { actT = Time.time - landT; break; } yield return null; }
            FightInput.TouchUp(B.Kick);
            Log("kick pressed on landing: starts " + (actT < 0 ? "never" : actT.ToString("0.00") + " s") + " after touchdown");
            // walking into the boss
            yield return StartCoroutine(Fresh(3.0f));
            b.dummy = AhiravanaFighter.Dummy.Idle;
            FightInput.Stick = new Vector2(1, 0);
            float flips = 0f, lastDx = 0f, maxOverlap = 0f; float tt = Time.time; float lastX = h.transform.position.x;
            while (Time.time - tt < 2.5f)
            {
                float x = h.transform.position.x; float dxm = x - lastX; if (Mathf.Sign(dxm) != Mathf.Sign(lastDx) && Mathf.Abs(dxm) > 0.0005f && Mathf.Abs(lastDx) > 0.0005f) flips++; if (Mathf.Abs(dxm) > 0.0005f) lastDx = dxm; lastX = x;
                maxOverlap = Mathf.Max(maxOverlap, Mathf.Max(0f, (h.pushWidth * h.scale + b.pushWidth * b.scale) * 0.5f - Mathf.Abs(b.transform.position.x - x)));
                yield return null;
            }
            FightInput.Reset();
            Log("walk into the boss for 2.5 s: direction reversals=" + flips + " (jitter), max overlap=" + maxOverlap.ToString("0.00") + " m");
            // thrust through the boss
            yield return StartCoroutine(Fresh(1.6f));
            b.dummy = AhiravanaFighter.Dummy.Idle;
            h.Perform("vayudash"); flips = 0f; lastDx = 0f; lastX = h.transform.position.x; maxOverlap = 0f; tt = Time.time;
            while (Time.time - tt < 1.0f)
            {
                float x = h.transform.position.x; float dxm = x - lastX; if (Mathf.Sign(dxm) != Mathf.Sign(lastDx) && Mathf.Abs(dxm) > 0.0005f && Mathf.Abs(lastDx) > 0.0005f) flips++; if (Mathf.Abs(dxm) > 0.0005f) lastDx = dxm; lastX = x;
                maxOverlap = Mathf.Max(maxOverlap, Mathf.Max(0f, (h.pushWidth * h.scale + b.pushWidth * b.scale) * 0.5f - Mathf.Abs(b.transform.position.x - x)));
                yield return null;
            }
            Log("wind dash into the boss: direction reversals=" + flips + ", max overlap=" + maxOverlap.ToString("0.00") + " m");
        }

        // ---------------------------------------------------------------------------------------------------- scripted player (prompt 12)
        /// <summary>
        /// A simple player: walks into range, chains gada, blocks what it can see coming after a 0.25 s reaction time, dodges grabs.
        /// Runs whole fights at a given round/difficulty and reports time-to-win, damage taken and when the boss changed phase.
        /// </summary>
        IEnumerator BotFight(string name, int round, FightSetup.Difficulty level)
        {
            yield return StartCoroutine(Fresh(2.5f));
            var oldLevel = FightSetup.Level; FightSetup.Level = level; FightSetup.Current = FightSetup.Mode.Story; d.ApplyDifficulty();
            d.forcePhases = true; d.noTrainingHeal = true;
            h.maxHp = h.hp = 1000f; b.maxHp = b.hp = 1000f; b.dummy = AhiravanaFighter.Dummy.Fight; b.SetRound(round); b.ApplyDifficulty();
            var thinkF = typeof(AhiravanaFighter).GetField("thinkT", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float t0 = Time.time; float seen = 0f; MoveDef seenMove = null; int lastPhase = 0; float p1 = -1f, p2 = -1f; int chain = 0; float nextAct = 0f; int parries = 0;
            while (Time.time - t0 < 120f && h.Alive && b.hp > 1.5f && b.Alive)
            {
                if (b.phase != lastPhase) { lastPhase = b.phase; if (lastPhase == 1) p1 = Time.time - t0; else p2 = Time.time - t0; }
                float dx = b.transform.position.x - h.transform.position.x, dist = Mathf.Abs(dx);
                if (b.move != seenMove) { seenMove = b.move; seen = Time.time; }
                bool threat = b.Attacking && Time.time - seen >= 0.25f && dist < 3.2f && b.move.hits.Count > 0 && b.moveT < b.move.hits[b.move.hits.Count - 1].t1;
                bool grab = threat && b.move.tele == TeleClass.Grab;
                h.demoBlock = threat && !grab;
                if (grab && h.CanAct) { FightInput.TouchDown(B.Dodge); yield return null; FightInput.TouchUp(B.Dodge); }
                else if (!threat && !b.IsOpen && b.state != FState.Hitstun) { }
                if (!threat)
                {
                    if (dist > 1.9f) FightInput.Stick = new Vector2(Mathf.Sign(dx) * 0.9f, 0f); else FightInput.Stick = Vector2.zero;
                    if (dist <= 2.1f && Time.time >= nextAct && (h.CanAct || h.InCancelWindow))
                    {
                        string[] seq = { "m1", "m2", "m3" };
                        h.Perform(seq[chain % 3]); chain++; nextAct = Time.time + 0.12f;
                        if (chain % 3 == 0) nextAct = Time.time + 0.5f;
                    }
                }
                else FightInput.Stick = Vector2.zero;
                yield return null;
            }
            FightInput.Reset(); h.demoBlock = false;
            bool won = b.hp <= 1.5f || !b.Alive;
            Log(name.PadRight(26) + (won ? "WON" : h.Alive ? "timeout" : "LOST") + " in " + (Time.time - t0).ToString("0.0") + " s, Hanuman lost " + (1000f - h.hp).ToString("0") + " hp, boss phases at " + (p1 < 0 ? "-" : p1.ToString("0.0") + "s") + " / " + (p2 < 0 ? "-" : p2.ToString("0.0") + "s") + ", picks: " + Summarise(b.pickLog));
            FightSetup.Level = oldLevel; FightSetup.Current = FightSetup.Mode.Training; d.forcePhases = false; d.noTrainingHeal = false;
        }

        IEnumerator BotTests()
        {
            Log(""); Log("== N. scripted player vs boss (a simple bot: chains gada, blocks what it sees after 0.25 s, dodges grabs)");
            yield return StartCoroutine(BotFight("round 1, Normal", 1, FightSetup.Difficulty.Normal));
            yield return StartCoroutine(BotFight("round 2, Normal", 2, FightSetup.Difficulty.Normal));
            yield return StartCoroutine(BotFight("round 1, Easy", 1, FightSetup.Difficulty.Easy));
            yield return StartCoroutine(BotFight("round 1, Hard", 1, FightSetup.Difficulty.Hard));
        }

        // ---------------------------------------------------------------------------------------------------- stuck-in-air hunt
        IEnumerator StuckHunt(string name, float dist, float dur, System.Func<IEnumerator> act, bool realBoss)
        {
            yield return StartCoroutine(Fresh(dist));
            h.maxHp = h.hp = 1000000f; b.maxHp = b.hp = 1000000f;
            if (realBoss) { b.dummy = AhiravanaFighter.Dummy.Fight; d.forcePhases = true; d.noTrainingHeal = true; b.SetRound(2); }
            StartCoroutine(act());
            float t0 = Time.time, airT = 0f, hAirT = 0f, worstB = 0f, worstH = 0f, lastLog = 0f; var sb = new StringBuilder();
            while (Time.time - t0 < dur)
            {
                h.hp = Mathf.Max(h.hp, 999000f);
                float by = b.transform.position.y, hy = h.transform.position.y;
                bool bStuck = by > 0.05f && !b.flying && Mathf.Abs(b.vel.y) < 0.05f; airT = bStuck ? airT + Time.deltaTime : 0f; worstB = Mathf.Max(worstB, airT);
                bool hStuck = hy > 0.05f && !h.flying && Mathf.Abs(h.vel.y) < 0.05f; hAirT = hStuck ? hAirT + Time.deltaTime : 0f; worstH = Mathf.Max(worstH, hAirT);
                if ((bStuck && airT > 0.3f || hStuck && hAirT > 0.3f) && Time.time - lastLog > 0.5f)
                { lastLog = Time.time; sb.Append("[t=" + (Time.time - t0).ToString("0.0") + " boss y=" + by.ToString("0.00") + " vy=" + b.vel.y.ToString("0.00") + " " + b.state + " grounded=" + b.grounded + " | hanuman y=" + hy.ToString("0.00") + " " + h.state + " grounded=" + h.grounded + "] "); }
                yield return null;
            }
            FightInput.Reset(); d.forcePhases = false; d.noTrainingHeal = false;
            Log(name.PadRight(30) + "longest boss hover=" + worstB.ToString("0.00") + "s, longest hanuman hover=" + worstH.ToString("0.00") + "s  " + sb);
        }

        IEnumerator StuckTests()
        {
            Log(""); Log("== S. stuck / hovering hunt (hover = airborne with no vertical speed)");
            yield return StartCoroutine(StuckHunt("kick spam x15, dummy boss", 1.4f, 7f, () => TapSeq(B.Kick, 15, 0.22f), false));
            yield return StartCoroutine(StuckHunt("kick spam x15, real boss", 1.4f, 14f, () => TapSeq(B.Kick, 15, 0.22f), true));
            yield return StartCoroutine(StuckHunt("kick+tail mix, real boss", 1.4f, 14f, KickMix, true));
            yield return StartCoroutine(StuckHunt("jump kick spam, real boss", 2.0f, 12f, JumpKick, true));
            yield return StartCoroutine(StuckHunt("gada spam, real boss", 1.4f, 14f, () => TapSeq(B.Mace, 20, 0.25f), true));
        }

        IEnumerator KickMix()
        {
            for (int i = 0; i < 8; i++)
            {
                FightInput.Stick = new Vector2(0, -1); yield return StartCoroutine(Tap(B.Kick)); yield return new WaitForSecondsRealtime(0.5f); FightInput.Stick = Vector2.zero;
                yield return StartCoroutine(Tap(B.Kick)); yield return new WaitForSecondsRealtime(0.3f); yield return StartCoroutine(Tap(B.Kick)); yield return new WaitForSecondsRealtime(0.6f);
            }
        }

        IEnumerator JumpKick()
        {
            for (int i = 0; i < 6; i++)
            {
                yield return StartCoroutine(Tap(B.Jump, 0.3f)); yield return new WaitForSecondsRealtime(0.35f);
                yield return StartCoroutine(Tap(B.Kick)); yield return new WaitForSecondsRealtime(1.2f);
            }
        }

        // ---------------------------------------------------------------------------------------------------- juggle limit
        IEnumerator JuggleTests()
        {
            Log(""); Log("== J2. juggle: how long can repeated hits keep the boss in the air? (dummy boss, ground player)");
            foreach (var opener in new[] { "tailtrip", "sweep", "launcher" })
            {
                yield return StartCoroutine(Fresh(1.3f));
                h.maxHp = h.hp = 1000000f; b.maxHp = b.hp = 1000000f;
                h.Perform(opener);
                float t0 = Time.time, airRun = 0f, longest = 0f, maxY = 0f; int hits = 0; float lastHp = b.hp;
                StartCoroutine(TapSeq(B.Kick, 20, 0.18f));
                while (Time.time - t0 < 6f)
                {
                    h.hp = Mathf.Max(h.hp, 999000f);
                    if (!b.grounded) { airRun += Time.deltaTime; longest = Mathf.Max(longest, airRun); } else airRun = 0f;
                    maxY = Mathf.Max(maxY, b.transform.position.y);
                    if (b.hp < lastHp - 0.5f) { hits++; lastHp = b.hp; }
                    // keep the player under the boss so every kick can connect
                    var hp = h.transform.position; var bp = b.transform.position;
                    if (h.CanAct && Mathf.Abs(bp.x - hp.x) > 1.2f) h.transform.position = new Vector3(bp.x - 1.1f, 0f, 0f);
                    yield return null;
                }
                FightInput.Reset();
                Log((opener + " then kick spam").PadRight(30) + "boss longest time in the air=" + longest.ToString("0.00") + " s, peak=" + maxY.ToString("0.0") + " m, hits landed=" + hits);
            }
        }

        // ---------------------------------------------------------------------------------------------------- kick fuzz against the real boss
        IEnumerator KickFuzzInput(float dur)
        {
            float t0 = Time.time; var rnd = new System.Random(7);
            while (Time.time - t0 < dur)
            {
                int k = rnd.Next(0, 6);
                float x = 0f, y = 0f;
                if (k == 1) y = -1f; else if (k == 2) x = 1f; else if (k == 3) x = -1f;
                if (k == 4) { yield return StartCoroutine(Tap(B.Jump, 0.25f)); yield return new WaitForSecondsRealtime(0.3f); }
                FightInput.Stick = new Vector2(x, y);
                yield return StartCoroutine(Tap(B.Kick));
                yield return new WaitForSecondsRealtime(0.1f + (float)rnd.NextDouble() * 0.25f);
                FightInput.Stick = Vector2.zero;
                if (k == 5 && h.CanAct) h.transform.position = new Vector3(Mathf.Clamp(b.transform.position.x - 1.2f, -9f, 9f), h.transform.position.y, 0f);
                var hp = h.transform.position; var bp = b.transform.position;
                if (h.grounded && Mathf.Abs(bp.x - hp.x) > 2.2f) h.transform.position = new Vector3(bp.x - (bp.x > hp.x ? 1.2f : -1.2f), hp.y, 0f);
            }
        }

        IEnumerator KickFuzz(float dur, bool phases)
        {
            yield return StartCoroutine(Fresh(1.6f));
            h.maxHp = h.hp = 1000000f; b.maxHp = b.hp = 1000f; b.dummy = AhiravanaFighter.Dummy.Fight; b.SetRound(2); d.noTrainingHeal = true; d.forcePhases = phases;
            StartCoroutine(KickFuzzInput(dur));
            float t0 = Time.time, airRun = 0f, longest = 0f, lastLog = -9f; var sb = new StringBuilder(); int revive = 0;
            while (Time.time - t0 < dur)
            {
                h.hp = Mathf.Max(h.hp, 999000f);
                if (b.hp <= 5f) { b.hp = 1000f; revive++; }
                bool air = !b.grounded && !b.flying;
                airRun = air ? airRun + Time.deltaTime : 0f; longest = Mathf.Max(longest, airRun);
                if (airRun > 1.6f && Time.time - lastLog > 1f)
                {
                    lastLog = Time.time;
                    string pose = b.puppet.poseSprite != null && b.puppet.poseSprite.sprite != null ? b.puppet.poseSprite.sprite.name : "rig";
                    sb.Append("[t=" + (Time.time - t0).ToString("0.0") + " y=" + b.transform.position.y.ToString("0.00") + " vy=" + b.vel.y.ToString("0.00") + " " + b.state + " move=" + (b.move != null ? b.move.id : "-") + " flying=" + b.flying + " hitstop=" + b.hitstop.ToString("0.00") + " pose=" + pose + " phase=" + b.phase + "] ");
                }
                yield return null;
            }
            FightInput.Reset(); d.noTrainingHeal = false; d.forcePhases = false;
            Log("kick fuzz " + dur + "s phases=" + phases + ": longest time airborne=" + longest.ToString("0.00") + " s, boss defeated " + revive + "x  " + sb);
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
