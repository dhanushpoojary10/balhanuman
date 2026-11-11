using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BalHanuman
{    /// <summary>Trigger that stores a respawn point for the player.</summary>
    public class Checkpoint : MonoBehaviour
    {
        public SpriteRenderer flag;
        public bool silent;
        public Vector2 respawnOffset = new Vector2(0f, 0.1f);
        public Sprite sparkle;
        bool active;

        void Start() { if (flag != null) flag.color = new Color(0.6f, 0.6f, 0.6f, 1f); }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (active || other.GetComponentInParent<PlayerController2D>() == null) return;
            active = true;
            GameSession.Current.SetCheckpoint((Vector2)transform.position + respawnOffset);
            if (flag != null) flag.color = Color.white;
            Fx.Burst(sparkle, transform.position + Vector3.up * 1.5f, 10, 2f, 0.9f, -0.5f, 0.3f, new Color(1f, 0.9f, 0.5f), 1f);
            if (!silent) { GameSession.Current.Toast("Checkpoint reached!", 1.8f); Sfx.Play(Sfx.Id.Pickup, 1.3f); }
        }
    }
}
