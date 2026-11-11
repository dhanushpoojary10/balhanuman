using UnityEngine;

namespace BalHanuman
{
    /// <summary>Slow sideways drift and gentle swell for a strip of water art, so the surface never looks frozen.</summary>
    public class WaterDrift : MonoBehaviour
    {
        public float driftX = 0.15f;
        public float driftSpeed = 0.35f;
        public float swellY = 0.025f;
        public float swellSpeed = 0.9f;
        Vector3 origin;

        void Start() { origin = transform.position; }

        void Update()
        {
            float t = Time.time;
            transform.position = origin + new Vector3(Mathf.Sin(t * driftSpeed) * driftX, Mathf.Sin(t * swellSpeed) * swellY, 0f);
        }
    }
}
