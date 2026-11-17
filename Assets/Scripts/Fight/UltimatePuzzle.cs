using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BalHanuman.Fight
{
    /// <summary>
    /// A puzzle Hanuman must solve before PANCHMUKHI is released. FightHud shows it in a modal panel while the fight is frozen.
    /// Run() builds its own UI under `panel`, plays out the puzzle (use unscaled time: the game is paused) and reports true (solved) or false (failed).
    /// To add a puzzle: subclass this and add it to UltimatePuzzles.Pick().
    /// </summary>
    public abstract class UltimatePuzzle
    {
        public abstract string Name { get; }
        public abstract IEnumerator Run(RectTransform panel, Action<bool> result);
    }

    public static class UltimatePuzzles
    {
        /// <summary>The puzzle for the current round (placeholder: one puzzle for now, tougher in later rounds).</summary>
        public static UltimatePuzzle Pick(int round)
        {
            return new LampMemoryPuzzle(length: 4 + Mathf.Clamp(round, 0, 3), reverse: true);
        }
    }

    /// <summary>
    /// "The Five Lamps": the lamps flare in a random order; relight them in the REVERSE order before the flame timer burns out.
    /// One wrong lamp and the puzzle fails.
    /// </summary>
    public class LampMemoryPuzzle : UltimatePuzzle
    {
        readonly int length; readonly bool reverse;
        public static int[] LastSequence;                     // kept for testing
        public LampMemoryPuzzle(int length, bool reverse) { this.length = Mathf.Max(3, length); this.reverse = reverse; }
        public override string Name => "THE FIVE LAMPS";

        static readonly Color[] Flame =
        {
            new Color(1f, 0.62f, 0.2f), new Color(0.78f, 0.45f, 1f), new Color(0.3f, 0.9f, 0.85f), new Color(1f, 0.35f, 0.4f), new Color(1f, 0.92f, 0.5f),
        };

        public override IEnumerator Run(RectTransform panel, Action<bool> result)
        {
            var S = UiKit.Skin;
            Vector2 mid = new Vector2(0.5f, 0.5f), top = new Vector2(0.5f, 1f);
            var title = UiKit.Title("PuzzleTitle", panel, Name, 84, TextAnchor.MiddleCenter, top, mid, new Vector2(0, -260), new Vector2(1400, 120), UiKit.Gold);

            // ---------------------------------------------------------------- how to play (the player starts when ready)
            var card = UiKit.Stretch("HowToPlay", panel);
            var plate = UiKit.Img("Plate", card, S.white, mid, mid, mid, new Vector2(0, -100), new Vector2(1500, 660), new Color(0.12f, 0.07f, 0.2f, 0.92f));
            plate.preserveAspect = false;
            UiKit.Title("Head", card, "HOW TO PLAY", 54, TextAnchor.MiddleCenter, mid, mid, new Vector2(0, 165), new Vector2(1300, 70), new Color(1f, 0.9f, 0.6f));
            string how =
                "<b>1.  WATCH</b>  -  the five lamps flare one after another.\n" +
                "<b>2.  REMEMBER</b>  -  keep the order in your head.\n" +
                (reverse ? "<b>3.  REPEAT IN REVERSE</b>  -  tap the lamp that flared <b>LAST</b> first, and the one that flared <b>FIRST</b> last.\n"
                         : "<b>3.  REPEAT</b>  -  tap the lamps in the same order.\n") +
                "\nYou have a limited time, and one wrong lamp fails the puzzle.\n" +
                "If you fail, you lose the star - weaken Ahiravana again to earn another.";
            var body = UiKit.Body("Rules", card, how, 34, TextAnchor.UpperLeft, mid, mid, new Vector2(0, -85), new Vector2(1340, 380), UiKit.Cream);
            body.lineSpacing = 1.15f;
            bool go = false;
            UiKit.PillButton("Start", card, "I'M READY", mid, new Vector2(0, -355), new Vector2(520, 110), () => go = true, true, 44);
            while (!go) yield return null;
            UnityEngine.Object.Destroy(card.gameObject);

            // ---------------------------------------------------------------- the board
            var info = UiKit.Body("PuzzleInfo", panel, "", 46, TextAnchor.MiddleCenter, top, mid, new Vector2(0, -370), new Vector2(1600, 70), UiKit.Cream, FontStyle.Bold);
            var sub = UiKit.Body("PuzzleSub", panel, "", 32, TextAnchor.MiddleCenter, top, mid, new Vector2(0, -430), new Vector2(1600, 50), new Color(1f, 0.85f, 0.55f));
            var big = UiKit.Title("Count", panel, "", 220, TextAnchor.MiddleCenter, mid, mid, new Vector2(0, -90), new Vector2(600, 300), new Color(1f, 0.85f, 0.4f, 0.95f));

            var lamps = new Image[5]; var glows = new Image[5]; var rings = new Image[5]; int tapped = -1;
            var board = new List<GameObject>();
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                float x = (i - 2) * 300f;
                var halo = UiKit.Img("Halo" + i, panel, S.softGlow != null ? S.softGlow : S.circle, mid, mid, mid, new Vector2(x, 0), new Vector2(330, 330), new Color(Flame[i].r, Flame[i].g, Flame[i].b, 0f));
                var lamp = UiKit.Img("Lamp" + i, panel, S.circle, mid, mid, mid, new Vector2(x, 0), new Vector2(210, 210), new Color(0.2f, 0.14f, 0.24f, 1f), true);
                var ring = UiKit.Img("Ring", lamp.transform, S.portraitRing != null ? S.portraitRing : S.circle, mid, mid, mid, Vector2.zero, new Vector2(226, 226), new Color(1f, 0.82f, 0.45f, 0.8f));
                var core = UiKit.Img("Core", lamp.transform, S.circle, mid, mid, mid, Vector2.zero, new Vector2(110, 110), new Color(0.35f, 0.25f, 0.3f, 1f));
                var b2 = lamp.gameObject.AddComponent<Button>(); b2.targetGraphic = lamp; b2.transition = Selectable.Transition.None;
                b2.onClick.AddListener(() => tapped = idx);
                lamps[i] = core; glows[i] = halo; rings[i] = ring;
                board.Add(halo.gameObject); board.Add(lamp.gameObject);
            }

            // progress dots under the lamps: one per step
            var dots = new Image[length];
            for (int k = 0; k < length; k++)
                { dots[k] = UiKit.Img("Dot" + k, panel, S.circle, mid, mid, mid, new Vector2((k - (length - 1) * 0.5f) * 56f, -210), new Vector2(34, 34), new Color(0.35f, 0.28f, 0.42f, 1f)); board.Add(dots[k].gameObject); }

            var timerLabel = UiKit.Body("TimerLabel", panel, "", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), mid, new Vector2(0, 215), new Vector2(900, 40), UiKit.Muted);
            var timerBack = UiKit.Img("TimerBack", panel, S.white, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), mid, new Vector2(0, 170), new Vector2(1100, 26), new Color(0.05f, 0.03f, 0.1f, 0.85f));
            timerBack.preserveAspect = false; timerBack.gameObject.SetActive(false);
            var timer = UiKit.Img("Timer", timerBack.transform, S.white, Vector2.zero, Vector2.one, mid, Vector2.zero, Vector2.zero, new Color(1f, 0.7f, 0.2f, 1f));
            timer.preserveAspect = false; timer.type = Image.Type.Filled; timer.fillMethod = Image.FillMethod.Horizontal; timer.fillAmount = 1f;
            timer.rectTransform.offsetMin = new Vector2(4, 4); timer.rectTransform.offsetMax = new Vector2(-4, -4);

            void Light(int i, bool on)
            {
                lamps[i].color = on ? Flame[i] : new Color(0.35f, 0.25f, 0.3f, 1f);
                glows[i].color = new Color(Flame[i].r, Flame[i].g, Flame[i].b, on ? 0.85f : 0f);
            }
            void Dots(int filled, Color c) { for (int k = 0; k < dots.Length; k++) dots[k].color = k < filled ? c : new Color(0.35f, 0.28f, 0.42f, 1f); }

            // random order, never the same lamp twice in a row
            var seq = new int[length]; int prev = -1;
            for (int k = 0; k < length; k++) { int n; do { n = UnityEngine.Random.Range(0, 5); } while (n == prev); seq[k] = n; prev = n; }
            LastSequence = seq;

            // get ready: 3 - 2 - 1 (the board stays hidden so the numbers are clear)
            foreach (var g in board) g.SetActive(false);
            info.text = "Get ready..."; sub.text = "The lamps will flare - watch carefully";
            for (int c = 3; c >= 1; c--) { big.text = c.ToString(); FightSfx.Play("tick", 0.9f, 0.9f); yield return new WaitForSecondsRealtime(0.8f); }
            big.text = "";
            foreach (var g in board) g.SetActive(true);

            // WATCH: slow enough to follow, with a counter
            info.text = "WATCH CAREFULLY";
            for (int k = 0; k < seq.Length; k++)
            {
                sub.text = "Lamp " + (k + 1) + " of " + seq.Length;
                Dots(k + 1, new Color(1f, 0.85f, 0.4f));
                Light(seq[k], true); FightSfx.Play("tick", 1f + 0.1f * k, 0.9f);
                yield return new WaitForSecondsRealtime(0.8f);
                Light(seq[k], false);
                yield return new WaitForSecondsRealtime(0.4f);
            }
            Dots(0, Color.white);

            // YOUR TURN
            var want = new List<int>(seq); if (reverse) want.Reverse();
            info.text = reverse ? "YOUR TURN - tap them in REVERSE order" : "YOUR TURN - tap them in the same order";
            info.color = new Color(0.7f, 1f, 0.7f);
            sub.text = reverse ? "Start with the lamp that flared LAST" : "Start with the lamp that flared FIRST";
            FightSfx.Play("rank", 1f, 0.9f);
            timerBack.gameObject.SetActive(true); timerLabel.text = "TIME LEFT";
            float limit = 5f + 1.7f * seq.Length, t = 0f; int step = 0; bool ok = true; float goT = 0f;
            tapped = -1;
            while (step < want.Count)
            {
                t += Time.unscaledDeltaTime; goT += Time.unscaledDeltaTime;
                timer.fillAmount = Mathf.Clamp01(1f - t / limit);
                timer.color = Color.Lerp(new Color(1f, 0.25f, 0.15f), new Color(1f, 0.75f, 0.25f), timer.fillAmount);
                if (t >= limit) { ok = false; info.text = "Time ran out!"; sub.text = "The flame burned out before you finished"; Debug.Log("[Puzzle] timed out at step " + step + " of " + want.Count); break; }
                if (tapped >= 0)
                {
                    int tap = tapped; tapped = -1;
                    if (tap == want[step])
                    {
                        step++; Dots(step, new Color(0.55f, 1f, 0.55f));
                        sub.text = step < want.Count ? "Good!  " + step + " of " + want.Count + " - keep going" : "";
                        Light(tap, true); FightSfx.Play("tick", 1.1f + 0.1f * step, 0.9f);
                        yield return new WaitForSecondsRealtime(0.25f); Light(tap, false);
                    }
                    else
                    {
                        lamps[tap].color = new Color(1f, 0.15f, 0.15f); glows[tap].color = new Color(1f, 0.1f, 0.1f, 0.85f);
                        rings[want[step]].color = new Color(0.4f, 1f, 0.5f, 1f);        // show which lamp was the right one
                        ok = false; info.text = "Wrong lamp!"; sub.text = "The green ring shows the lamp you needed";
                        Debug.Log("[Puzzle] wrong lamp " + tap + ", wanted " + want[step] + " (step " + step + ")"); break;
                    }
                }
                yield return null;
            }

            if (ok) { info.text = "The flames obey!"; sub.text = "PANCHMUKHI is yours"; }
            info.color = ok ? new Color(0.7f, 1f, 0.6f) : new Color(1f, 0.5f, 0.45f);
            if (ok) for (int i = 0; i < 5; i++) Light(i, true);
            FightSfx.Play(ok ? "rank" : "laugh", 1f, ok ? 0.9f : 0.5f);
            yield return new WaitForSecondsRealtime(ok ? 1.4f : 2.2f);
            Debug.Log("[Puzzle] " + (ok ? "solved" : "failed"));
            result(ok);
        }
    }
}
