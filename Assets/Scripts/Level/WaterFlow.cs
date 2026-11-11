using UnityEngine;

namespace BalHanuman
{
    /// <summary>Drifting highlight streaks that make the stream look like it is flowing.</summary>
    public class WaterFlow : MonoBehaviour
    {
        public Sprite streak;
        public float width = 23f;
        public float height = 3.2f;
        public int count = 14;
        public float speed = 0.9f;

        class Band { public Transform t; public SpriteRenderer sr; public float x, y, len, life, age; }
        Band[] bands;

        void Start()
        {
            bands = new Band[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("flow");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = streak; sr.sortingOrder = 9;
                bands[i] = new Band { t = go.transform, sr = sr };
                Respawn(bands[i]);
                bands[i].age = Random.value * bands[i].life;
            }
        }

        void Respawn(Band b)
        {
            b.x = Random.Range(-width / 2f, width / 2f);
            b.y = -Random.Range(0.15f, height);
            b.len = Random.Range(0.8f, 3.2f);
            b.life = Random.Range(2.5f, 5f);
            b.age = 0f;
        }

        void Update()
        {
            float u = streak != null ? streak.bounds.size.x : 0.08f;
            float v = streak != null ? streak.bounds.size.y : 0.08f;
            foreach (var b in bands)
            {
                b.age += Time.deltaTime;
                if (b.age >= b.life) Respawn(b);
                float k = b.age / b.life;
                float x = b.x + b.age * speed;
                if (x > width / 2f) x -= width;
                b.t.localPosition = new Vector3(x, b.y - k * 0.15f, 0f);
                b.t.localScale = new Vector3(b.len / u, 0.05f / v, 1f);
                b.sr.color = new Color(0.85f, 0.97f, 1f, Mathf.Sin(k * Mathf.PI) * 0.45f);
            }
        }
    }
}
