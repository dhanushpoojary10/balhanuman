using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BalHanuman
{    /// <summary>Raised bridge that swings down into place when the lever is used.</summary>
    public class BridgeController : MonoBehaviour
    {
        public Transform pivot;
        public Collider2D deck;
        public float raisedAngle = 78f;
        public float duration = 2.6f;
        public Sprite dust;
        public Vector2 freeEndOffset = new Vector2(12f, 0f);
        public Transform ropeAnchor;
        public bool Lowered { get; private set; }
        LineRenderer[] ropes;
        bool moving;

        void Start()
        {
            if (ropeAnchor != null)
            {
                ropes = new LineRenderer[2];
                for (int i = 0; i < 2; i++)
                {
                    var go = new GameObject("Rope" + i); go.transform.SetParent(transform, false);
                    var lr = go.AddComponent<LineRenderer>();
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                    lr.startColor = lr.endColor = new Color(0.42f, 0.26f, 0.11f);
                    lr.startWidth = lr.endWidth = 0.07f;
                    lr.positionCount = 3; lr.sortingOrder = 6; lr.useWorldSpace = true;
                    ropes[i] = lr;
                }
            }
            if (pivot != null) pivot.localRotation = Quaternion.Euler(0, 0, raisedAngle);
            if (deck != null) deck.enabled = false;
        }

        void LateUpdate()
        {
            if (ropes == null || pivot == null) return;
            for (int i = 0; i < 2; i++)
            {
                Vector3 tip = pivot.TransformPoint(new Vector3(freeEndOffset.x, 0.35f + i * 0.55f, 0f));
                Vector3 a = ropeAnchor.position + new Vector3(0f, i * 0.45f, 0f);
                Vector3 mid = (tip + a) * 0.5f + Vector3.down * (Lowered ? 0.05f : 0.45f);
                ropes[i].SetPosition(0, tip); ropes[i].SetPosition(1, mid); ropes[i].SetPosition(2, a);
            }
        }

        public void Lower(Action done)
        {
            if (Lowered || moving) return;
            StartCoroutine(LowerRoutine(done));
        }

        IEnumerator LowerRoutine(Action done)
        {
            moving = true;
            float t = 0, dustT = 0;
            while (t < duration)
            {
                t += Time.deltaTime; dustT -= Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                pivot.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(raisedAngle, 0f, k));
                if (dustT <= 0f)
                {
                    dustT = 0.3f;
                    Fx.Burst(dust, pivot.position + pivot.right * freeEndOffset.x, 2, 1.2f, 0.7f, -0.3f, 0.5f, new Color(0.85f, 0.8f, 0.7f, 0.5f), 0.3f, 9);
                }
                yield return null;
            }
            pivot.localRotation = Quaternion.identity;
            if (deck != null) deck.enabled = true;
            Lowered = true; moving = false;
            Sfx.Play(Sfx.Id.Lever, 0.7f);
            Fx.Burst(dust, pivot.position + pivot.right * freeEndOffset.x, 14, 3f, 1.1f, -0.4f, 0.8f, new Color(0.85f, 0.8f, 0.7f, 0.6f), 0.5f, 9);
            done?.Invoke();
        }
    }
}
