using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BalHanuman
{    /// <summary>Ripples drifting across the stream surface.</summary>
    public class WaterSurface : MonoBehaviour
    {
        public Sprite ripple;
        public float width = 20f;
        float timer;

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f && ripple != null)
            {
                timer = UnityEngine.Random.Range(0.25f, 0.6f);
                var pos = transform.position + new Vector3(UnityEngine.Random.Range(-width / 2f, width / 2f), 0f, 0f);
                Fx.Burst(ripple, pos, 1, 0f, 1.6f, 0f, 0.35f, new Color(0.9f, 1f, 1f, 0.8f), 0f, 9);
            }
        }
    }
}
