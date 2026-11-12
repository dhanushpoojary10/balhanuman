using UnityEngine;

namespace BalHanuman.Fight
{
    /// <summary>Fighting-game camera: frames both fighters, zooms out for giants and flight, shakes, punches in on big moments and can focus for cinematics.</summary>
    [RequireComponent(typeof(Camera))]
    public class FightCamera : MonoBehaviour
    {
        public Fighter a, b;
        public float baseSize = 3.6f, maxSize = 8.6f, arenaHalf = 14f, floorView = -1.25f;
        Camera cam;
        Vector3 basePos; float size;
        float shakeAmp, shakeT, shakeDur;
        float punch, punchT, punchDur;
        Vector2 focus; float focusSize, focusW, focusTarget;

        void Awake() { cam = GetComponent<Camera>(); size = baseSize; basePos = transform.position; }

        public void Shake(float amp, float dur)
        {
            if (shakeT > 0f && shakeAmp * (shakeT / shakeDur) > amp) return;
            shakeAmp = amp; shakeT = shakeDur = Mathf.Max(0.05f, dur);
        }

        /// <summary>Quick zoom-in kick (negative size change), e.g. on a heavy hit.</summary>
        public void Punch(float amount, float dur) { punch = amount; punchT = punchDur = dur; }

        /// <summary>Blend the view towards a point at a given size (weight 0..1). Call ClearFocus to release.</summary>
        public void Focus(Vector2 p, float s, float weight) { focus = p; focusSize = s; focusTarget = weight; }
        public void ClearFocus() { focusTarget = 0f; }

        public void Snap() { if (cam == null) cam = GetComponent<Camera>(); size = Target(out var p); basePos = p; transform.position = p; cam.orthographicSize = size; }

        float Target(out Vector3 pos)
        {
            Vector2 pa = a != null ? (Vector2)a.Center : Vector2.zero, pb = b != null && b.gameObject.activeInHierarchy ? (Vector2)b.Center : pa;
            float dist = Mathf.Abs(pa.x - pb.x);
            float big = Mathf.Max(a != null ? a.scale : 1f, b != null ? b.scale : 1f);
            float high = Mathf.Max(pa.y, pb.y);
            float s = Mathf.Clamp(Mathf.Max(baseSize + dist * 0.16f, baseSize * (0.55f + 0.45f * big), 2.2f + high * 0.75f), baseSize, maxSize);
            float aspect = cam.aspect;
            float x = (pa.x + pb.x) * 0.5f;
            float halfW = s * aspect;
            x = halfW >= arenaHalf ? 0f : Mathf.Clamp(x, -arenaHalf + halfW, arenaHalf - halfW);
            float y = floorView + s;            // the floor sits a fixed distance above the bottom of the screen
            pos = new Vector3(x, y, -10f);
            return s;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            float dt = Time.unscaledDeltaTime;
            float s = Target(out var goal);
            focusW = Mathf.MoveTowards(focusW, focusTarget, dt * 2.5f);
            if (focusW > 0f)
            {
                float fs = Mathf.Lerp(s, focusSize, focusW);
                goal = Vector3.Lerp(goal, new Vector3(focus.x, Mathf.Max(focus.y, floorView + fs * 0.75f), -10f), focusW);
                s = fs;
            }
            float k = 1f - Mathf.Exp(-6f * dt);
            size = Mathf.Lerp(size, s, 1f - Mathf.Exp(-3.5f * dt));
            basePos = Vector3.Lerp(basePos, goal, k);
            float ps = 0f;
            if (punchT > 0f) { punchT -= dt; float u = Mathf.Clamp01(punchT / punchDur); ps = punch * Mathf.Sin(u * Mathf.PI); }
            cam.orthographicSize = Mathf.Max(1.5f, size - ps);
            Vector3 sh = Vector3.zero;
            if (shakeT > 0f)
            {
                shakeT -= dt;
                float u = Mathf.Clamp01(shakeT / shakeDur);
                sh = new Vector3(Mathf.PerlinNoise(Time.unscaledTime * 40f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, Time.unscaledTime * 40f) - 0.5f, 0) * 2f * shakeAmp * u;
            }
            transform.position = basePos + sh;
        }
    }
}
