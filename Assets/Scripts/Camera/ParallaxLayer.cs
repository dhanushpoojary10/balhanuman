using UnityEngine;

namespace BalHanuman
{
    /// <summary>
    /// Visual-only parallax strip. The art is not seamless, so neighbouring copies are mirrored, which always joins cleanly.
    /// factor 0 = glued to the camera, 1 = fixed in the world (like the terrain).
    /// </summary>
    public class ParallaxLayer : MonoBehaviour
    {
        public float factor = 0.5f;
        public float width = 40f;            // world width of one tile
        public bool followCameraY = true;
        public bool mirror = false;          // true only for art that is not seamless
        public float yOffset;                // relative to camera Y (or world Y when followCameraY is false)
        Transform cam;

        void Start()
        {
            cam = Camera.main.transform;
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) return;
            float localStep = width / transform.localScale.x;
            for (int i = -2; i <= 2; i++)
            {
                if (i == 0) continue;
                var c = new GameObject(name + "_mirror" + i);
                c.transform.SetParent(transform, false);
                c.transform.localPosition = new Vector3(i * localStep, 0, 0);
                var csr = c.AddComponent<SpriteRenderer>();
                csr.sprite = sr.sprite; csr.sortingOrder = sr.sortingOrder; csr.color = sr.color;
                csr.flipX = mirror && Mathf.Abs(i) % 2 == 1;
            }
        }

        void LateUpdate()
        {
            if (cam == null) return;
            float cx = cam.position.x;
            float period = mirror ? width * 2f : width;
            float wrap = Mathf.Repeat(cx * factor + period * 0.5f, period) - period * 0.5f;
            float y = followCameraY ? cam.position.y + yOffset : yOffset;
            transform.position = new Vector3(cx - wrap, y, 0f);
        }
    }
}
