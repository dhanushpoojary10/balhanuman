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

        void Start() { start = transform.position; phase = Random.value * 6.28f; }

        void Update()
        {
            if (bob) transform.position = start + new Vector3(0f, Mathf.Sin(Time.time * 3f + phase) * 0.1f, 0f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (taken || other.GetComponentInParent<PlayerController2D>() == null) return;
            taken = true;
            GameSession.Current.Collect(kind, value);
            Sfx.Play(Sfx.Id.Pickup, kind == CollectibleKind.Mango ? 1f : 1.25f);
            Fx.Burst(sparkle, transform.position, 8, 2.5f, 0.6f, 0f, 0.28f, new Color(1f, 0.92f, 0.5f), 0.6f, 25);
            if (kind == CollectibleKind.Lotus) GameSession.Current.Toast("A hidden lotus flower! Bonus!", 2f);
            Destroy(gameObject);
        }
    }
}
