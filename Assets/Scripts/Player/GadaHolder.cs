using System;
using System.Collections;
using UnityEngine;

namespace BalHanuman
{
    /// <summary>
    /// Hanuman's gada once he has earned it.
    /// With pose art (the usual case): while he carries it, his own frames are swapped for "holding the gada" poses
    /// (standing / running), and the strike plays wind-up -> swing -> impact -> recovery -> victory.
    /// Without pose art it falls back to a separate gada sprite resting on his shoulder.
    /// </summary>
    public class GadaHolder : MonoBehaviour
    {
        public PlayerController2D player;

        [Header("Pose art (facing right, feet at the pivot)")]
        public Sprite idlePose, runPose;
        public Sprite windup, swing, impact, recover, victory;

        [Header("Fallback: separate gada on the shoulder")]
        [Tooltip("Length of the gada in world units (Hanuman is about 1.8 tall).")]
        public float length = 1.6f;
        [Tooltip("Where his hand holds it, from his feet, when facing right.")]
        public Vector2 grip = new Vector2(0.22f, 0.72f);
        [Tooltip("Resting tilt: positive leans the head back over his shoulder.")]
        public float carryAngle = 42f;
        [Tooltip("How far down in front the head comes at the end of a strike.")]
        public float strikeAngle = -105f;

        Transform pivot;
        SpriteRenderer sr;
        float angle, swingT = -1f;
        bool equipped, striking;
        Transform visual;
        float runPhase;

        public bool Equipped => equipped;
        bool HasPoses => idlePose != null && runPose != null;

        /// <summary>Gives Hanuman the gada (with a little pop).</summary>
        public void Equip(Sprite gada)
        {
            if (equipped) return;
            if (player == null) player = GetComponent<PlayerController2D>();
            equipped = true;
            visual = player.spriteRenderer != null ? player.spriteRenderer.transform : null;
            if (HasPoses) { StartCoroutine(PopVisual()); return; }
            if (gada == null) return;
            pivot = new GameObject("GadaPivot").transform;
            pivot.SetParent(transform, false);
            var go = new GameObject("Gada");
            go.transform.SetParent(pivot, false);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = gada;
            float h = gada.bounds.size.y;
            float scale = length / Mathf.Max(0.01f, h);
            go.transform.localScale = Vector3.one * scale;
            go.transform.localPosition = new Vector3(0f, length * 0.36f - gada.bounds.center.y * scale, 0f);
            angle = carryAngle;
            StartCoroutine(PopIn());
        }

        IEnumerator PopIn()
        {
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.35f)
            {
                pivot.localScale = Vector3.one * (0.4f + 0.6f * (1f - Mathf.Pow(1f - t, 3f)) + 0.12f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            pivot.localScale = Vector3.one;
        }

        IEnumerator PopVisual()
        {
            if (visual == null) yield break;
            var baseScale = visual.localScale;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.35f) { visual.localScale = baseScale * (1f + 0.12f * Mathf.Sin(t * Mathf.PI)); yield return null; }
            visual.localScale = baseScale;
        }

        // ---------------------------------------------------------------- strike
        /// <summary>
        /// Plays the strike. onImpact fires the moment the gada lands (break things then). Returns when he has
        /// finished his victory pose.
        /// </summary>
        public IEnumerator Strike(Action onImpact, Action onVictory = null)
        {
            if (!HasPoses || windup == null || swing == null || impact == null)
            {
                // fallback: swing the shoulder gada
                swingT = 0f;
                yield return new WaitForSeconds(0.16f);
                onImpact?.Invoke();
                yield return new WaitForSeconds(0.6f);
                yield break;
            }
            striking = true;
            var anim = player.animator; bool had = anim != null && anim.enabled;
            if (anim != null) anim.enabled = false;
            var r = player.spriteRenderer;
            r.sprite = windup; yield return new WaitForSeconds(0.24f);
            r.sprite = swing; yield return new WaitForSeconds(0.08f);
            r.sprite = impact; onImpact?.Invoke(); yield return new WaitForSeconds(0.4f);
            if (recover != null) { r.sprite = recover; yield return new WaitForSeconds(0.25f); }
            if (victory != null) { r.sprite = victory; onVictory?.Invoke(); yield return new WaitForSeconds(0.9f); }
            if (anim != null) anim.enabled = had;
            striking = false;
        }

        // ---------------------------------------------------------------- per frame
        void LateUpdate()
        {
            if (!equipped || player == null) return;
            if (HasPoses) { UpdatePoses(); return; }
            UpdateShoulderGada();
        }

        /// <summary>Swaps his frame for the gada pose after the animator has picked its own (climb and roll keep theirs).</summary>
        void UpdatePoses()
        {
            var r = player.spriteRenderer;
            if (r == null || striking || !r.enabled) { ResetBob(); return; }
            if (player.Climbing || player.Rolling) { ResetBob(); return; }
            float vx = player.Body != null ? Mathf.Abs(player.Body.linearVelocity.x) : 0f;
            bool moving = vx > 0.6f || !player.Grounded;
            r.sprite = moving ? runPose : idlePose;
            // a little bounce while running on the ground (one pose, so the bob sells the stride)
            if (visual != null)
            {
                if (moving && player.Grounded)
                {
                    runPhase += Time.deltaTime * Mathf.Lerp(9f, 15f, Mathf.Clamp01(vx / 6f));
                    visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(runPhase)) * 0.07f, 0f);
                }
                else ResetBob();
            }
        }

        void ResetBob() { if (visual != null && visual.localPosition != Vector3.zero) visual.localPosition = Vector3.zero; }

        void UpdateShoulderGada()
        {
            if (sr == null) return;
            bool show = !player.Climbing && !player.Rolling && player.spriteRenderer != null && player.spriteRenderer.enabled;
            sr.enabled = show;
            if (!show) return;

            // swing: wind up a little, slam down in front, hold, then come back to the shoulder
            float a = carryAngle;
            bool front = false;
            if (swingT >= 0f)
            {
                swingT += Time.deltaTime;
                float t = swingT;
                if (t < 0.08f) a = Mathf.Lerp(carryAngle, carryAngle + 25f, t / 0.08f);
                else if (t < 0.16f) a = Mathf.Lerp(carryAngle + 25f, strikeAngle, (t - 0.08f) / 0.08f);
                else if (t < 0.5f) a = strikeAngle;
                else if (t < 0.8f) a = Mathf.Lerp(strikeAngle, carryAngle, (t - 0.5f) / 0.3f);
                else swingT = -1f;
                front = swingT >= 0f && t >= 0.08f;
            }
            angle = a;

            bool right = player.FacingRight;
            pivot.localPosition = new Vector3(right ? grip.x : -grip.x, grip.y, 0f);
            pivot.localRotation = Quaternion.Euler(0f, 0f, right ? angle : -angle);
            sr.flipX = !right;
            sr.sortingOrder = player.spriteRenderer.sortingOrder + (front ? 1 : -1);
        }
    }
}
