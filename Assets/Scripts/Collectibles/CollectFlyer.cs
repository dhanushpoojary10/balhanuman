using UnityEngine;

namespace BalHanuman
{
    /// <summary>The picked-up item pops up, then flies to the mango counter on the HUD (or just floats away and fades for other items).</summary>
    public class CollectFlyer : MonoBehaviour
    {
        const float PopTime = 0.14f, FlyTime = 0.5f, FadeTime = 0.3f;
        bool toHud;
        Vector3 baseScale, popPos, flightStart;
        float age;
        SpriteRenderer sr;
        bool flying;

        public static void Spawn(SpriteRenderer src, bool toHud)
        {
            if (src == null || src.sprite == null) return;
            var go = new GameObject("CollectFlyer");
            go.transform.position = src.transform.position;
            go.transform.rotation = src.transform.rotation;
            go.transform.localScale = src.transform.lossyScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = src.sprite; sr.color = src.color; sr.flipX = src.flipX;
            sr.sortingLayerID = src.sortingLayerID; sr.sortingOrder = 40;
            var f = go.AddComponent<CollectFlyer>();
            f.toHud = toHud; f.baseScale = go.transform.localScale; f.popPos = go.transform.position; f.sr = sr;
        }

        bool TargetWorld(out Vector3 w)
        {
            w = Vector3.zero;
            var hud = GameSession.Current != null ? GameSession.Current.hud : null;
            var cam = SideScrollCamera.Current != null ? SideScrollCamera.Current.GetComponent<Camera>() : Camera.main;
            if (hud == null || cam == null) return false;
            Vector2 sp = hud.MangoIconScreenPos;
            w = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, Mathf.Abs(cam.transform.position.z)));
            w.z = 0f;
            return true;
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            if (age < PopTime)
            {
                float k = age / PopTime;
                transform.position = popPos + Vector3.up * 0.35f * Mathf.Sin(k * Mathf.PI * 0.5f);
                transform.localScale = baseScale * (1f + 0.4f * Mathf.Sin(k * Mathf.PI * 0.5f));
                return;
            }

            if (toHud && TargetWorld(out var target))
            {
                if (!flying) { flying = true; flightStart = transform.position; }
                float k = Mathf.Clamp01((age - PopTime) / FlyTime);
                float e = k * k;                                              // accelerate towards the counter
                Vector3 pos = Vector3.Lerp(flightStart, target, e);
                pos += Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.8f;           // a gentle arc
                transform.position = pos;
                transform.localScale = baseScale * Mathf.Lerp(1.4f, 0.5f, k);
                if (k >= 1f)
                {
                    if (GameSession.Current != null && GameSession.Current.hud != null) GameSession.Current.hud.PulseMango();
                    Destroy(gameObject);
                }
                return;
            }

            // no counter to fly to: float up and fade
            float f = Mathf.Clamp01((age - PopTime) / FadeTime);
            transform.position = popPos + Vector3.up * (0.35f + 0.5f * f);
            transform.localScale = baseScale * (1.4f - 0.2f * f);
            var c = sr.color; c.a = 1f - f; sr.color = c;
            if (f >= 1f) Destroy(gameObject);
        }
    }
}
