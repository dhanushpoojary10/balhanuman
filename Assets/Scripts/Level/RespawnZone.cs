using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BalHanuman
{    /// <summary>Water or chasm. Touching it fades out and returns the player to the last checkpoint (no damage).</summary>
    public class RespawnZone : MonoBehaviour
    {
        public bool isWater;
        public Sprite splashSprite;

        void OnTriggerEnter2D(Collider2D other)
        {
            var p = other.GetComponentInParent<PlayerController2D>();
            if (p == null) return;
            if (isWater)
            {
                Fx.Burst(splashSprite, new Vector3(p.transform.position.x, transform.position.y + transform.localScale.y / 2f * 0f + GetComponent<Collider2D>().bounds.max.y, 0), 14, 4f, 0.8f, 9f, 0.5f, new Color(0.8f, 0.95f, 1f, 0.9f), 3f, 22);
                Sfx.Play(Sfx.Id.Splash);
            }
            GameSession.Current.Respawn();
        }
    }
}
