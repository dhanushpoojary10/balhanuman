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
            Vector2 mid = new Vector2(0.5f, 0.5f);
            var title = UiKit.Title("PuzzleTitle", panel, Name, 84, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), mid, new Vector2(0, -260), new Vector2(1400, 120), UiKit.Gold);
            var info = UiKit.Body("PuzzleInfo", panel, "Watch the order the lamps flare...", 42, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), mid, new Vector2(0, -370), new Vector2(1500, 70), UiKit.Cream, FontStyle.Bold);

            // the five lamps
            var lamps = new Image[5]; var glows = new Image[5]; int tapped = -1;
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                float x = (i - 2) * 300f;
                var halo = UiKit.Img("Halo" + i, panel, S.softGlow != null ? S.softGlow : S.circle, mid, mid, mid, new Vector2(x, 0), new Vector2(330, 330), new Color(Flame[i].r, Flame[i].g, Flame[i].b, 0f));
                var lamp = UiKit.Img("Lamp" + i, panel, S.circle, mid, mid, mid, new Vector2(x, 0), new Vector2(210, 210), new Color(0.2f, 0.14f, 0.24f, 1f), true);
                var ring = UiKit.Img("Ring", lamp.transform, S.portraitRing != null ? S.portraitRing : S.circle, mid, mid, mid, Vector2.zero, new Vector2(226, 226), new Color(1f, 0.82f, 0.45f, 0.8f));
                var core = UiKit.Img("Core", lamp.transform, S.circle, mid, mid, mid, Vector2.zero, new Vector2(110, 110), new Color(0.35f, 0.25f, 0.3f, 1f));
                var b = lamp.gameObject.AddComponent<Button>(); b.targetGraphic = lamp; b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => tapped = idx);
                lamps[i] = core; glows[i] = halo;
            }
            var timerBack = UiKit.Img("TimerBack", panel, S.white, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), mid, new Vector2(0, 170), new Vector2(1100, 26), new Color(0.05f, 0.03f, 0.1f, 0.85f));
            timerBack.preserveAspect = false;
            var timer = UiKit.Img("Timer", timerBack.transform, S.white, Vector2.zero, Vector2.one, mid, Vector2.zero, Vector2.zero, new Color(1f, 0.7f, 0.2f, 1f));
            timer.preserveAspect = false; timer.type = Image.Type.Filled; timer.fillMethod = Image.FillMethod.Horizontal; timer.fillAmount = 0f;
            timer.rectTransform.offsetMin = new Vector2(4, 4); timer.rectTransform.offsetMax = new Vector2(-4, -4);

            void Light(int i, bool on)
            {
                lamps[i].color = on ? Flame[i] : new Color(0.35f, 0.25f, 0.3f, 1f);
                glows[i].color = new Color(Flame[i].r, Flame[i].g, Flame[i].b, on ? 0.85f : 0f);
            }

            // random order, never the same lamp twice in a row
            var seq = new int[length]; int prev = -1;
            for (int k = 0; k < length; k++) { int n; do { n = UnityEngine.Random.Range(0, 5); } while (n == prev); seq[k] = n; prev = n; }
            LastSequence = seq;
            yield return new WaitForSecondsRealtime(0.9f);

            // show the order
            for (int k = 0; k < seq.Length; k++)
            {
                Light(seq[k], true); FightSfx.Play("tick", 1f + 0.12f * k, 0.9f);
                yield return new WaitForSecondsRealtime(0.55f);
                Light(seq[k], false);
                yield return new WaitForSecondsRealtime(0.25f);
            }

            // the player's turn
            var want = new List<int>(seq); if (reverse) want.Reverse();
            info.text = reverse ? "Now relight them in REVERSE order!" : "Now relight them in the same order!";
            float limit = 4f + 1.6f * seq.Length, t = 0f; int step = 0; bool ok = true;
            tapped = -1;
            while (step < want.Count)
            {
                t += Time.unscaledDeltaTime;
                timer.fillAmount = Mathf.Clamp01(1f - t / limit);
                timer.color = Color.Lerp(new Color(1f, 0.25f, 0.15f), new Color(1f, 0.75f, 0.25f), timer.fillAmount);
                if (t >= limit) { ok = false; Debug.Log("[Puzzle] timed out at step " + step + " of " + want.Count); break; }
                if (tapped >= 0)
                {
                    int tap = tapped; tapped = -1;
                    if (tap == want[step])
                    {
                        Light(tap, true); FightSfx.Play("tick", 1.1f + 0.1f * step, 0.9f); step++;
                        yield return new WaitForSecondsRealtime(0.22f); Light(tap, false);
                    }
                    else
                    {
                        lamps[tap].color = new Color(1f, 0.15f, 0.15f); glows[tap].color = new Color(1f, 0.1f, 0.1f, 0.85f);
                        ok = false; Debug.Log("[Puzzle] wrong lamp " + tap + ", wanted " + want[step] + " (step " + step + ")"); break;
                    }
                }
                yield return null;
            }

            info.text = ok ? "The flames obey!" : "The flames waver...";
            info.color = ok ? new Color(0.7f, 1f, 0.6f) : new Color(1f, 0.5f, 0.45f);
            if (ok) for (int i = 0; i < 5; i++) Light(i, true);
            FightSfx.Play(ok ? "rank" : "laugh", 1f, ok ? 0.9f : 0.5f);
            yield return new WaitForSecondsRealtime(1.1f);
            Debug.Log("[Puzzle] " + (ok ? "solved" : "failed"));
            result(ok);
        }
    }
}
