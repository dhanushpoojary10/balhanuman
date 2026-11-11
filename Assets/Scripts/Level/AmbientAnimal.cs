using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BalHanuman
{    /// <summary>Bobbing / hopping idle motion for friendly animals; they all hop wildly when the level is completed.</summary>
    public class AmbientAnimal : MonoBehaviour
    {
        public float bobAmount = 0.04f;
        public float bobSpeed = 2f;
        public bool hops = true;
        public float hopHeight = 0.4f;
        public bool flies;
        public float flyRadius = 0.5f;
        Vector3 basePos, baseScale;
        float phase, hopT = -1f, nextHop, celebrate;

        void Start()
        {
            basePos = transform.localPosition; baseScale = transform.localScale;
            phase = UnityEngine.Random.value * 6f;
            nextHop = Time.time + UnityEngine.Random.Range(2f, 6f);
        }

        public void Celebrate() { celebrate = 6f; }

        void Update()
        {
            float t = Time.time + phase;
            float y = Mathf.Sin(t * bobSpeed) * bobAmount;
            float x = 0f;
            if (flies) { x = Mathf.Cos(t * bobSpeed * 0.5f) * flyRadius; y = Mathf.Sin(t * bobSpeed) * flyRadius * 0.5f; }
            if (celebrate > 0f)
            {
                celebrate -= Time.deltaTime;
                y += Mathf.Abs(Mathf.Sin(t * 6f)) * hopHeight * 1.4f;
            }
            else if (hops)
            {
                if (hopT < 0f && Time.time > nextHop) { hopT = 0f; nextHop = Time.time + UnityEngine.Random.Range(3f, 7f); }
                if (hopT >= 0f)
                {
                    hopT += Time.deltaTime * 2.2f;
                    y += Mathf.Sin(Mathf.Min(hopT, 1f) * Mathf.PI) * hopHeight;
                    if (hopT >= 1f) hopT = -1f;
                }
            }
            transform.localPosition = basePos + new Vector3(x, y, 0);
            float sq = 1f + Mathf.Sin(t * bobSpeed * 2f) * 0.012f;
            transform.localScale = new Vector3(baseScale.x * sq, baseScale.y / sq, baseScale.z);
        }
    }
}
