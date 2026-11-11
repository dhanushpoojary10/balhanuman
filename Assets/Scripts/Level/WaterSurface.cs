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
                FxRipple.Spawn(ripple, pos, UnityEngine.Random.Range(0.9f, 1.7f), UnityEngine.Random.Range(1.4f, 2.2f), new Color(1f, 1f, 1f, 0.75f), 9);
            }
        }
    }
}
