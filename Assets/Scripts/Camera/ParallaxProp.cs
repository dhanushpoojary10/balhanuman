using UnityEngine;

namespace BalHanuman
{
    /// <summary>
    /// A single (non-tiling) background piece such as a distant temple. It sits at baseX in world space and drifts
    /// slower than the camera: factor 1 = fixed in the world, 0 = glued to the camera. Vertically it follows the camera.
    /// </summary>
    public class ParallaxProp : MonoBehaviour
    {
        public float baseX;
        public float factor = 0.08f;
        public float yOffset;   // relative to the camera
        Transform cam;

        void Start() { cam = Camera.main.transform; }

        void LateUpdate()
        {
            if (cam == null) return;
            float x = cam.position.x + (baseX - cam.position.x) * factor;
            transform.position = new Vector3(x, cam.position.y + yOffset, 0f);
        }
    }
}
