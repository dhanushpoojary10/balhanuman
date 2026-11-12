using UnityEngine;

namespace BalHanuman
{
    public enum CollectibleKind { Mango, GoldCoin, Lotus }

    public class Collectible : MonoBehaviour
    {
        public CollectibleKind kind = CollectibleKind.Mango;
        public int value = 1;
        public bool bob = true;
        public Sprite sparkle;
        bool taken;
        Vector3 start;
        float phase;
        SpriteRenderer art;

        // picking several in a row raises the pitch of the chime
        static int chain;
        static readonly float[] BhoopRatios = { 1f, 1.1225f, 1.2599f, 1.4983f, 1.6818f, 2f };
        static float lastPickup = -10f;

        void Start()
        {
            start = transform.position; phase = Random.value * 6.28f;
            art = GetComponentInChildren<SpriteRenderer>();
            AddGlow();
        }

        /// <summary>A soft warm halo behind the item so it stands out against the greens.</summary>
        void AddGlow()
        {
            if (art == null) return;
            Color c = kind == CollectibleKind.Lotus ? new Color(1f, 0.72f, 0.88f) : kind == CollectibleKind.GoldCoin ? new Color(1f, 0.9f, 0.45f) : new Color(1f, 0.82f, 0.32f);
            float size = Mathf.Max(art.bounds.size.x, art.bounds.size.y) * 2.1f;
            var g = FxGlow.Pulse(transform, art.bounds.center, size, c, 0.22f, 0.5f, 2.4f, art.sortingOrder - 1);
            g.name = "Halo";
        }

        void Update()
        {
            if (bob) transform.position = start + new Vector3(0f, Mathf.Sin(Time.time * 3f + phase) * 0.1f, 0f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (taken || other.GetComponentInParent<PlayerController2D>() == null) return;
            taken = true;
            GameSession.Current.Collect(kind, value);

            if (Time.time - lastPickup > 1.3f) chain = 0;
            chain++; lastPickup = Time.time;
            // each pickup in a quick run climbs raga Bhoop (D E F# A B D'), so a row of mangoes plays a little tune
            float pitch = BhoopRatios[Mathf.Min(chain - 1, BhoopRatios.Length - 1)] * (kind == CollectibleKind.Mango ? 1f : 1.5f);
            Sfx.Play(Sfx.Id.Pickup, pitch);
            Haptics.Tick();

            Fx.Burst(sparkle, transform.position, 8, 2.5f, 0.6f, 0f, 0.28f, new Color(1f, 0.92f, 0.5f), 0.6f, 25);
            FxGlow.Ring(transform.position, 0.3f, 1.5f, 0.35f, new Color(1f, 0.9f, 0.5f, 0.6f), 24);
            CollectFlyer.Spawn(art, kind == CollectibleKind.Mango);
            if (kind == CollectibleKind.Lotus) GameSession.Current.Toast("A hidden lotus flower! Bonus!", 2f);
            Destroy(gameObject);
        }
    }
}
