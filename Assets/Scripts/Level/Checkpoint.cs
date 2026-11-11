using UnityEngine;

namespace BalHanuman
{
    /// <summary>Trigger that stores a respawn point for the player. Unlit it is a grey flag with a faint hint of light; reaching it lights it up.</summary>
    public class Checkpoint : MonoBehaviour
    {
        public SpriteRenderer flag;
        public bool silent;
        public Vector2 respawnOffset = new Vector2(0f, 0.1f);
        public Sprite sparkle;
        bool active;
        GameObject glow;
        FxGlowPulse pulse;
        Vector3 flagBase;
        float popT = -1f;

        void Start()
        {
            if (flag != null)
            {
                flag.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                flagBase = flag.transform.localScale;
                // a faint cool glow so the player notices the flag from a distance
                float h = Mathf.Max(1.5f, flag.bounds.size.y);
                glow = FxGlow.Pulse(transform, flag.bounds.center, h * 1.5f, new Color(0.85f, 0.95f, 1f), 0.08f, 0.2f, 1.8f, flag.sortingOrder - 1);
                pulse = glow.GetComponent<FxGlowPulse>();
            }
        }

        void Update()
        {
            if (popT >= 0f && flag != null)
            {
                popT += Time.deltaTime;
                float k = Mathf.Clamp01(popT / 0.45f);
                float bounce = 1f + 0.35f * Mathf.Sin(k * Mathf.PI) * (1f - k);
                flag.transform.localScale = flagBase * bounce;
                if (k >= 1f) { flag.transform.localScale = flagBase; popT = -1f; }
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (active || other.GetComponentInParent<PlayerController2D>() == null) return;
            active = true;
            GameSession.Current.SetCheckpoint((Vector2)transform.position + respawnOffset);
            if (flag != null) flag.color = Color.white;

            // light up: warm steady glow, expanding rings, rising sparkles, a pop of the flag
            Vector3 c = flag != null ? flag.bounds.center : transform.position + Vector3.up * 1.5f;
            if (pulse != null) { pulse.color = new Color(1f, 0.85f, 0.4f); pulse.minAlpha = 0.28f; pulse.maxAlpha = 0.55f; pulse.size *= 1.25f; }
            FxGlow.Ring(c, 0.5f, 4.5f, 0.7f, new Color(1f, 0.88f, 0.45f, 0.7f), 22);
            FxGlow.Ring(c, 0.3f, 3.0f, 0.5f, new Color(1f, 1f, 0.8f, 0.6f), 23);
            Fx.Burst(sparkle, transform.position + Vector3.up * 1.5f, 16, 2.4f, 1.1f, -0.5f, 0.3f, new Color(1f, 0.9f, 0.5f), 1.4f, 24);
            popT = 0f;
            Haptics.Medium();
            if (!silent) { GameSession.Current.Toast("Checkpoint reached!", 1.8f); Sfx.Play(Sfx.Id.Checkpoint); }
        }
    }
}
