using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BalHanuman
{
    /// <summary>
    /// Development-only automated play-test. Drives the real input layer and logs PASS/FAIL lines prefixed with [BOT].
    /// Run in Play Mode: new GameObject().AddComponent&lt;BalHanuman.PlaytestBot&gt;().RunAll();
    /// </summary>
    public class PlaytestBot : MonoBehaviour
    {
        PlayerController2D p;
        GameSession gs;
        int pass, fail;

        void Log(string s) { Debug.Log("[BOT] " + s); }
        void Check(string name, bool ok, string detail = "") { if (ok) pass++; else fail++; Log((ok ? "PASS " : "FAIL ") + name + " " + detail); }
        WaitForSecondsRealtime W(float t) { return new WaitForSecondsRealtime(t); }
        void Tp(float x, float y) { p.TeleportTo(new Vector2(x, y)); }
        Vector2 Pos => p.transform.position;

        void Click()
        {
            var go = GameObject.Find("Dialogue");
            if (go != null) go.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }

        IEnumerator Dialogue(int max)
        {
            for (int i = 0; i < max; i++) { yield return W(0.25f); Click(); }
            yield return W(0.2f);
        }

        IEnumerator Interact() { GameInput.TouchInteractDown(); yield return W(0.3f); }

        public void VineTest() { StartCoroutine(Vine()); }

        IEnumerator Vine()
        {
            p = FindFirstObjectByType<PlayerController2D>();
            Tp(87.4f, 0.05f); yield return W(0.5f);
            GameInput.Up = true;
            for (int i = 0; i < 24; i++)
            {
                yield return W(0.25f);
                Log("vine t=" + (i * 0.25f).ToString("0.00") + " pos=" + Pos + " climbing=" + p.Climbing + " inVine=" + p.InVine + " vy=" + p.Body.linearVelocity.y.ToString("0.0"));
            }
            GameInput.Up = false;
            Log("SUMMARY vine");
        }


        // ---------- real keyboard path (Input System events, not the touch flags)
        Keyboard kb;
        void KeyDown(Key k) { InputSystem.QueueStateEvent(kb, new KeyboardState(k)); }
        void KeyUp() { InputSystem.QueueStateEvent(kb, new KeyboardState()); }

        public void KeyboardTest() { StartCoroutine(KeyTests()); }

        IEnumerator KeyTests()
        {
            p = FindFirstObjectByType<PlayerController2D>();
            gs = GameSession.Current;
            kb = Keyboard.current;
            Log("keyboard device present=" + (kb != null) + " hasFocus=" + Application.isFocused);
            if (kb == null) { kb = InputSystem.AddDevice<Keyboard>(); Log("added virtual keyboard"); }
            var cap = p.GetComponent<CapsuleCollider2D>();
            yield return W(1f);

            Tp(12f, 0.05f); yield return W(0.5f);
            KeyDown(Key.S); yield return W(0.5f);
            bool crouchS = p.Crouching; KeyUp(); yield return W(0.4f);
            Check("KEY S bends", crouchS, "crouching=" + crouchS);

            KeyDown(Key.DownArrow); yield return W(0.5f);
            bool crouchD = p.Crouching; KeyUp(); yield return W(0.4f);
            Check("KEY Down arrow bends", crouchD, "crouching=" + crouchD);

            float y0 = Pos.y; KeyDown(Key.W); yield return W(0.25f);
            float maxY = Pos.y; KeyUp(); yield return W(0.8f);
            Check("KEY W jumps", maxY - y0 > 0.5f, "rise=" + (maxY - y0).ToString("0.00"));

            KeyDown(Key.UpArrow); yield return W(0.25f);
            maxY = Pos.y; KeyUp(); yield return W(0.8f);
            Check("KEY Up arrow jumps", maxY - y0 > 0.5f, "rise=" + (maxY - y0).ToString("0.00"));

            KeyDown(Key.Space); yield return W(0.25f);
            maxY = Pos.y; KeyUp(); yield return W(0.8f);
            Check("KEY Space jumps", maxY - y0 > 0.5f, "rise=" + (maxY - y0).ToString("0.00"));

            float x0 = Pos.x; KeyDown(Key.D); yield return W(0.5f); KeyUp(); yield return W(0.3f);
            Check("KEY D moves right", Pos.x - x0 > 1.5f, "dx=" + (Pos.x - x0).ToString("0.0"));
            x0 = Pos.x; KeyDown(Key.A); yield return W(0.5f); KeyUp(); yield return W(0.3f);
            Check("KEY A moves left", x0 - Pos.x > 1.5f, "dx=" + (x0 - Pos.x).ToString("0.0"));

            x0 = Pos.x; KeyDown(Key.LeftShift); yield return W(0.15f);
            bool rolled = p.Rolling; KeyUp(); yield return W(0.8f);
            Check("KEY Shift rolls", rolled, "rolling=" + rolled);

            // vine: W climbs, S comes back down
            Tp(87.4f, 0.05f); yield return W(0.5f);
            KeyDown(Key.W); yield return W(1.0f);
            float climbed = Pos.y; bool climbing = p.Climbing; KeyUp();
            Check("KEY W climbs vine", climbing && climbed > 2f, "y=" + climbed.ToString("0.0") + " climbing=" + climbing);
            KeyDown(Key.S); yield return W(0.8f);
            float down = Pos.y; KeyUp(); yield return W(0.3f);
            Check("KEY S climbs down", down < climbed - 1f, "y=" + down.ToString("0.0"));

            // interact with E near the sage
            Tp(7.6f, 0.05f); yield return W(0.5f);
            KeyDown(Key.E); yield return W(0.2f); KeyUp(); yield return W(0.4f);
            bool open = GameObject.Find("Dialogue") != null;
            Check("KEY E interacts (opens dialogue)", open);
            yield return Dialogue(6);

            Log("SUMMARY keys pass=" + pass + " fail=" + fail);
        }

        public void RunAll() { StartCoroutine(All()); }

        IEnumerator All()
        {
            p = FindFirstObjectByType<PlayerController2D>();
            gs = GameSession.Current;
            yield return W(1f);

            // ---------- quest gate: basket cannot be taken / quest cannot complete before start
            Check("quest starts NotStarted", gs.quest.State == QuestState.NotStarted);
            Check("cannot complete early", !gs.quest.Complete() && gs.quest.State == QuestState.NotStarted);

            // ---------- sage dialogue starts quest
            Tp(7.6f, 0.05f); yield return W(0.5f);
            yield return Interact();
            yield return Dialogue(6);
            Check("sage dialogue starts quest", gs.quest.State == QuestState.FindBasket, gs.quest.State.ToString());

            // ---------- jump + run
            Tp(12f, 0.05f); yield return W(0.5f);
            GameInput.TouchJumpDown();
            float maxY = 0;
            float t = 0;
            while (t < 0.55f) { t += Time.deltaTime; maxY = Mathf.Max(maxY, Pos.y); yield return null; }
            GameInput.JumpHeldTouch = false;
            yield return W(1f);
            Check("jump reaches ~2-3.3 units", maxY > 1.8f && maxY < 3.4f, "maxY=" + maxY.ToString("0.00"));
            Check("lands grounded", p.Grounded, "y=" + Pos.y.ToString("0.00"));
            float x0 = Pos.x; GameInput.Right = true; yield return W(1f); GameInput.Right = false;
            Check("runs ~6 u/s", Pos.x - x0 > 4.5f, "dx=" + (Pos.x - x0).ToString("0.0"));

            // ---------- roll
            var cap = p.GetComponent<CapsuleCollider2D>();
            Tp(12f, 0.05f); yield return W(0.5f);
            float rx0 = Pos.x; GameInput.TouchRollDown(); yield return W(0.15f);
            bool rollSeen = p.Rolling; float rollH = cap.size.y;
            yield return W(0.9f);
            Check("roll starts with a small collider", rollSeen && rollH < 1f, "rolling=" + rollSeen + " h=" + rollH.ToString("0.00"));
            Check("roll moves forward and ends", Pos.x - rx0 > 3f && !p.Rolling && cap.size.y > 1.4f, "dx=" + (Pos.x - rx0).ToString("0.0") + " h=" + cap.size.y.ToString("0.00"));

            // ---------- bend (crouch)
            GameInput.Down = true; yield return W(0.35f);
            bool crouched = p.Crouching; float bendH = cap.size.y;
            GameInput.Down = false; yield return W(0.4f);
            Check("bend shrinks collider and releases", crouched && bendH < 1f && !p.Crouching && cap.size.y > 1.4f, "crouched=" + crouched + " h=" + bendH.ToString("0.00") + " after=" + cap.size.y.ToString("0.00"));

            // ---------- gap crossing (23 to 26.5)
            Tp(19.5f, 0.05f); yield return W(0.4f);
            GameInput.Right = true; float tt = 0; bool jumped = false;
            while (tt < 3f) { tt += Time.deltaTime; if (!jumped && Pos.x > 22.3f && p.Grounded) { GameInput.TouchJumpDown(); jumped = true; } if (tt > 1.5f) GameInput.JumpHeldTouch = false; if (jumped && Pos.x > 26.8f && p.Grounded) break; yield return null; }
            GameInput.Right = false; GameInput.JumpHeldTouch = false; yield return W(0.5f);
            Check("crosses first gap", Pos.x > 26.5f && Pos.y > -1f, "pos=" + Pos);

            // ---------- mango pickup
            int m0 = gs.Mangoes; Tp(27.0f, 0.05f); yield return W(0.3f); GameInput.Right = true; yield return W(0.9f); GameInput.Right = false;
            Check("mango pickup counts", gs.Mangoes > m0, m0 + "->" + gs.Mangoes);

            // ---------- checkpoint + water respawn
            Tp(44.5f, 0.05f); yield return W(0.6f);
            Tp(58.5f, -0.1f); yield return W(1.8f);
            Check("water respawns to checkpoint", Mathf.Abs(Pos.x - 44.5f) < 1.5f && Pos.y > -1f, "pos=" + Pos);
            Check("respawn kept mangoes", gs.Mangoes >= m0 + 1);

            // ---------- moving log carries the player
            var logs = FindObjectsByType<MovingPlatform>(FindObjectsSortMode.None);
            if (logs.Length > 0)
            {
                var l = logs[0].transform;
                Tp(l.position.x, l.position.y + 1.1f); yield return W(0.4f);
                float px0 = Pos.x, lx0 = l.position.x; yield return W(0.9f);
                float dp = Pos.x - px0, dl = l.position.x - lx0;
                Check("log carries player", Mathf.Abs(dp - dl) < 0.3f && Mathf.Abs(dl) > 0.2f && p.Grounded, "dPlayer=" + dp.ToString("0.00") + " dLog=" + dl.ToString("0.00"));
            }

            // ---------- vine climb
            Tp(87.4f, 0.05f); yield return W(0.5f);
            GameInput.Up = true; float ct = 0; bool sawClimb = false;
            while (ct < 5f && Pos.x < 89.2f) { ct += Time.deltaTime; if (p.Climbing) sawClimb = true; yield return null; }
            GameInput.Up = false; yield return W(1f);
            Check("climbs vine to cliff top", sawClimb && Pos.y > 5.3f && Pos.x > 89f, "pos=" + Pos + " climbed=" + sawClimb);

            // ---------- baby monkey
            Tp(92.0f, 5.55f); yield return W(0.5f);
            int m1 = gs.Mangoes;
            yield return Interact();
            yield return Dialogue(3);
            yield return W(2.4f);
            Check("baby monkey rescued", gs.MonkeyRescued && gs.Mangoes == m1 + 3, "rescued=" + gs.MonkeyRescued + " mangoes=" + gs.Mangoes);

            // ---------- lever + bridge
            Tp(101.5f, 0.05f); yield return W(0.6f);
            var bridge = FindFirstObjectByType<BridgeController>();
            Check("bridge starts raised", !bridge.Lowered);
            yield return Interact();
            yield return W(3.5f);
            Check("lever lowers bridge", bridge.Lowered);
            Tp(105.6f, 0.05f); yield return W(0.4f);
            GameInput.Right = true; yield return W(3.2f); GameInput.Right = false;
            Check("walks across bridge", Pos.x > 117f && Pos.y > -1f, "pos=" + Pos);

            // ---------- basket, portal, ending
            Check("cannot complete before basket", !gs.quest.Complete());
            Tp(142.2f, 0.05f); yield return W(0.5f);
            yield return Interact();
            Check("basket collected", gs.quest.State == QuestState.BasketCollected, gs.quest.State.ToString());
            Tp(148.6f, 0.05f); yield return W(0.5f);
            yield return Interact();
            yield return W(2.5f);
            Check("portal returns to sage area", Pos.x < 14f && gs.quest.State == QuestState.ReturnToSage, "pos=" + Pos + " state=" + gs.quest.State);
            Tp(7.6f, 0.05f); yield return W(0.5f);
            yield return Interact();
            yield return Dialogue(4);
            yield return W(7f);
            var complete = GameObject.Find("Complete");
            Check("quest completed", gs.quest.State == QuestState.Completed, gs.quest.State.ToString());
            Check("completion screen visible", complete != null);
            Log("SUMMARY pass=" + pass + " fail=" + fail);
        }
    }
}
