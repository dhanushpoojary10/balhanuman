using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BalHanuman
{    /// <summary>Falling leaves, drifting fireflies and distant bird calls around the camera.</summary>
    public class AmbientFx : MonoBehaviour
    {
        public Sprite[] leaves;
        public Sprite sparkle;
        public Vector2 verticalRange = new Vector2(-2f, 9f);
        float leafTimer, sparkTimer, birdTimer = 3f;

        void Update()
        {
            var cam = Camera.main;
            if (cam == null || Time.timeScale == 0f) return;
            Vector3 c = cam.transform.position;
            float halfW = cam.orthographicSize * cam.aspect;

            leafTimer -= Time.deltaTime;
            if (leafTimer <= 0f && leaves != null && leaves.Length > 0)
            {
                leafTimer = UnityEngine.Random.Range(0.8f, 1.8f);
                var pos = new Vector3(c.x + UnityEngine.Random.Range(-halfW, halfW), c.y + cam.orthographicSize + 0.5f, 0);
                Fx.Burst(leaves[UnityEngine.Random.Range(0, leaves.Length)], pos, 1, 0.3f, 7f, 0.05f, 0.28f, new Color(1, 1, 1, 0.95f), -0.7f, 28, 0.5f);
            }
            sparkTimer -= Time.deltaTime;
            if (sparkTimer <= 0f && sparkle != null)
            {
                sparkTimer = UnityEngine.Random.Range(0.5f, 1.1f);
                var pos = new Vector3(c.x + UnityEngine.Random.Range(-halfW, halfW), c.y + UnityEngine.Random.Range(-1f, 3f), 0);
                Fx.Burst(sparkle, pos, 1, 0.35f, 3.5f, 0f, 0.22f, new Color(1f, 0.92f, 0.5f, 0.9f), 0f, 27);
            }
            birdTimer -= Time.deltaTime;
            if (birdTimer <= 0f)
            {
                birdTimer = UnityEngine.Random.Range(4f, 9f);
                Sfx.Play(Sfx.Id.Chirp, UnityEngine.Random.Range(0.85f, 1.25f), 0.35f);
            }
        }
    }
}
