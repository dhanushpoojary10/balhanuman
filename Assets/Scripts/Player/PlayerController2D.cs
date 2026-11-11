using UnityEngine;

namespace BalHanuman
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController2D : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 6f;
        public float acceleration = 45f;
        public float deceleration = 55f;
        [Range(0f, 1f)] public float airControl = 0.75f;

        [Header("Jump")]
        public float jumpVelocity = 13.5f;
        public float gravityScale = 3.2f;
        public float fallGravityMultiplier = 1.25f;
        public float coyoteTime = 0.12f;
        public float jumpBufferTime = 0.12f;
        [Range(0.1f, 1f)] public float jumpCutMultiplier = 0.5f;

        [Header("Climb")]
        public float climbSpeed = 3.5f;

        [Header("Roll and bend")]
        public float rollSpeed = 9.5f;
        public float rollDuration = 0.5f;
        public float rollCooldown = 0.35f;
        public float rollCost = 0.45f;         // roll stamina (the HUD energy bar)
        public float energyRegen = 0.12f;
        public float energyRegenDelay = 0.6f;
        [Range(0f, 1f)] public float crouchSpeedFactor = 0.3f;
        public Vector2 standSize = new Vector2(0.7f, 1.5f);
        public Vector2 standOffset = new Vector2(0f, 0.75f);
        public Vector2 smallSize = new Vector2(0.7f, 0.85f);
        public Vector2 smallOffset = new Vector2(0f, 0.425f);

        [Header("References")]
        public Transform groundCheck;
        public Vector2 groundCheckSize = new Vector2(0.5f, 0.14f);
        public LayerMask groundMask;
        public Animator animator;
        public SpriteRenderer spriteRenderer;
        public Sprite dustSprite;

        Rigidbody2D rb;
        CapsuleCollider2D capsule;
        bool grounded, wasGrounded, climbing, facingRight = true, jumpHeldLast, jumpCutPending;
        bool rolling, crouching;
        float coyoteTimer, jumpBufferTimer, airTime, stepTimer, moveInput, vertInput;
        float noClimbUntil, airLockUntil, rollTimer, rollCooldownTimer, rollBufferTimer, rollDir = 1f;
        ClimbableVine vine;
        float energy = 1f, lastRollTime = -10f;
        // juice: squash and stretch, landing strength, skid dust
        Transform squashTf; Vector3 baseScale = Vector3.one; Vector2 squash = Vector2.one;
        float maxFall, skidCooldown;

        public float Energy01 => energy;
        public event System.Action RollDenied;
        public bool InputLocked { get; set; }
        public bool Grounded => grounded;
        public bool Climbing => climbing;
        public bool Rolling => rolling;
        public bool Crouching => crouching;
        public bool InVine => vine != null;
        public Rigidbody2D Body => rb;
        public bool FacingRight => facingRight;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            capsule = GetComponent<CapsuleCollider2D>();
            rb.gravityScale = gravityScale;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            // squash and stretch only on the visual child (never scale the root: it carries the colliders)
            if (spriteRenderer != null && spriteRenderer.transform != transform) { squashTf = spriteRenderer.transform; baseScale = squashTf.localScale; }
        }

        Vector3 Feet => transform.position + Vector3.up * 0.08f;

        void Dust(int n, float speed, float size, float alpha, float drift = 0f)
        {
            Fx.Burst(dustSprite, Feet, n, speed, 0.4f, -0.4f, size, new Color(1f, 1f, 1f, alpha), 0.3f, 11, drift);
        }

        bool CanStand()
        {
            return Physics2D.OverlapBox((Vector2)transform.position + new Vector2(0f, 1.15f), new Vector2(0.55f, 0.55f), 0f, groundMask) == null;
        }

        void ApplyCollider()
        {
            if (capsule == null) return;
            bool small = rolling || crouching;
            capsule.size = small ? smallSize : standSize;
            capsule.offset = small ? smallOffset : standOffset;
        }

        void StartRoll()
        {
            rolling = true; crouching = false;
            rollTimer = rollDuration;
            rollCooldownTimer = rollDuration + rollCooldown;
            rollDir = facingRight ? 1f : -1f;
            rollBufferTimer = 0f;
            energy = Mathf.Max(0f, energy - rollCost); lastRollTime = Time.time;
            ApplyCollider();
            Sfx.Play(Sfx.Id.Jump, 0.65f, 0.8f);
            Fx.Burst(dustSprite, transform.position + Vector3.up * 0.1f, 5, 1.5f, 0.4f, -0.4f, 0.35f, new Color(1, 1, 1, 0.5f), 0.3f, 11);
            Haptics.Tick();
        }

        void EndRoll()
        {
            rolling = false;
            ApplyCollider();
        }

        void Update()
        {
            if (Time.timeScale == 0f) return;

            moveInput = InputLocked ? 0f : GameInput.Horizontal;
            vertInput = InputLocked ? 0f : GameInput.Vertical;
            bool upIsJump = vine == null && !climbing;
            if (!InputLocked && GameInput.ConsumeJump(upIsJump)) jumpBufferTimer = jumpBufferTime;
            if (!InputLocked && GameInput.ConsumeRoll()) rollBufferTimer = 0.18f;
            jumpBufferTimer -= Time.deltaTime;
            rollBufferTimer -= Time.deltaTime;
            rollCooldownTimer -= Time.deltaTime;

            bool jumpHeld = !InputLocked && GameInput.JumpHeldFor(upIsJump);
            if (jumpHeldLast && !jumpHeld && rb.linearVelocity.y > 1f) jumpCutPending = true;
            jumpHeldLast = jumpHeld;

            Vector2 gp = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position + Vector2.down * 0.06f;
            grounded = !climbing && rb.linearVelocity.y <= 0.2f && Physics2D.OverlapBox(gp, groundCheckSize, 0f, groundMask) != null;
            if (grounded) coyoteTimer = coyoteTime; else coyoteTimer -= Time.deltaTime;

            // ---- roll
            if (!rolling && Time.time - lastRollTime > energyRegenDelay) energy = Mathf.Min(1f, energy + energyRegen * Time.deltaTime);
            if (rollBufferTimer > 0f && grounded && !rolling && !climbing && rollCooldownTimer <= 0f && !InputLocked)
            {
                if (energy >= rollCost) StartRoll();
                else { rollBufferTimer = 0f; RollDenied?.Invoke(); }
            }

            // ---- bend (crouch): hold down while on the ground and not next to a vine
            bool wantCrouch = !InputLocked && grounded && !rolling && !climbing && vine == null && vertInput < -0.3f;
            if (wantCrouch) { if (!crouching) { crouching = true; ApplyCollider(); } }
            else if (crouching && CanStand()) { crouching = false; ApplyCollider(); }

            if (!grounded && !climbing)
            {
                airTime += Time.deltaTime;
                maxFall = Mathf.Min(maxFall, rb.linearVelocity.y);
            }
            else if (climbing) maxFall = 0f;
            if (grounded && !wasGrounded)
            {
                float impact = -maxFall;                       // how fast he was falling when he touched down
                if (airTime > 0.3f || impact > 6f)
                {
                    float k = Mathf.Clamp01((impact - 6f) / 14f);
                    Sfx.Play(Sfx.Id.Land, 1f - 0.15f * k, 0.7f + 0.3f * k);
                    Dust(6 + Mathf.RoundToInt(6 * k), 1.6f + 1.2f * k, 0.35f + 0.15f * k, 0.55f);
                    squash = new Vector2(1f + 0.10f + 0.20f * k, 1f - 0.10f - 0.22f * k);
                    if (impact > 11f) SideScrollCamera.Shake(0.04f + 0.10f * k, 0.18f + 0.12f * k);
                    if (impact > 18f) Haptics.Medium(); else if (impact > 11f) Haptics.Light();
                }
                airTime = 0f; maxFall = 0f;
            }
            wasGrounded = grounded;

            // skid: turning around at speed kicks up a little dust
            if (grounded && !rolling && !crouching && Time.time > skidCooldown && Mathf.Abs(rb.linearVelocity.x) > 4.2f && moveInput * rb.linearVelocity.x < -0.1f)
            {
                skidCooldown = Time.time + 0.25f;
                Dust(4, 1.4f, 0.28f, 0.5f, -Mathf.Sign(rb.linearVelocity.x) * 0.6f);
            }

            if (!rolling)
            {
                if (moveInput > 0.1f) facingRight = true; else if (moveInput < -0.1f) facingRight = false;
            }
            if (spriteRenderer != null) spriteRenderer.flipX = !facingRight;

            if (grounded && !crouching && Mathf.Abs(rb.linearVelocity.x) > 2f && !rolling)
            {
                stepTimer -= Time.deltaTime;
                if (stepTimer <= 0f)
                {
                    Sfx.Play(Sfx.Id.Step, Random.Range(0.9f, 1.2f), 0.7f); stepTimer = 0.28f;
                    Dust(1, 0.7f, 0.2f, 0.4f, -Mathf.Sign(rb.linearVelocity.x) * 0.5f);   // tiny puff behind each footstep
                }
            }

            // squash and stretch settles back to normal
            if (squashTf != null)
            {
                squash = Vector2.Lerp(squash, Vector2.one, 1f - Mathf.Exp(-13f * Time.deltaTime));
                squashTf.localScale = new Vector3(baseScale.x * squash.x, baseScale.y * squash.y, baseScale.z);
            }

            if (animator != null)
            {
                animator.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
                animator.SetBool("Grounded", grounded);
                animator.SetFloat("VelY", rb.linearVelocity.y);
                animator.SetBool("Climbing", climbing);
                animator.SetBool("Rolling", rolling);
                animator.SetBool("Crouching", crouching);
                animator.speed = climbing && Mathf.Abs(rb.linearVelocity.y) < 0.1f ? 0f : 1f;
            }
        }

        void FixedUpdate()
        {
            var v = rb.linearVelocity;

            // ---------------- climbing ----------------
            if (!climbing && vine != null && !InputLocked && Time.time >= noClimbUntil && !rolling)
            {
                if (vertInput > 0.3f || (vertInput < -0.3f && !grounded)) StartClimb();
            }
            if (climbing)
            {
                if (vine == null) { StopClimb(); return; }
                if (jumpBufferTimer > 0f)
                {
                    float dir = Mathf.Abs(moveInput) > 0.1f ? Mathf.Sign(moveInput) : (facingRight ? 1f : -1f);
                    jumpBufferTimer = 0f;
                    StopClimb();
                    rb.linearVelocity = new Vector2(dir * moveSpeed * 0.8f, jumpVelocity * 0.8f);
                    airLockUntil = Time.time + 0.35f;
                    Sfx.Play(Sfx.Id.Jump);
                    squash = new Vector2(0.88f, 1.14f);
                    return;
                }
                if (vertInput > 0.1f && transform.position.y >= vine.TopY)
                {
                    StopClimb();
                    rb.linearVelocity = new Vector2(vine.exitDirection * 4.6f, 8f);
                    airLockUntil = Time.time + 0.55f;
                    return;
                }
                if (vertInput < -0.1f && Physics2D.OverlapBox((Vector2)transform.position + Vector2.down * 0.06f, groundCheckSize, 0f, groundMask) != null)
                {
                    StopClimb();
                    return;
                }
                float nx = Mathf.MoveTowards(rb.position.x, vine.transform.position.x, 6f * Time.fixedDeltaTime);
                rb.position = new Vector2(nx, rb.position.y);
                rb.linearVelocity = new Vector2(0f, vertInput * climbSpeed);
                return;
            }

            // ---------------- roll ----------------
            if (rolling)
            {
                rollTimer -= Time.fixedDeltaTime;
                v.x = rollDir * rollSpeed;
                rb.linearVelocity = v;
                if (jumpBufferTimer > 0f && CanStand()) { EndRoll(); }
                else
                {
                    if (rollTimer <= 0f && CanStand()) EndRoll();
                    return;
                }
            }

            // ---------------- ground / air movement ----------------
            float speedNow = crouching ? moveSpeed * crouchSpeedFactor : moveSpeed;
            float target = moveInput * speedNow;
            float rate = Mathf.Abs(target) > 0.01f ? acceleration : deceleration;
            if (!grounded) rate *= airControl;
            if (Time.time >= airLockUntil) v.x = Mathf.MoveTowards(v.x, target, rate * Time.fixedDeltaTime);

            if (jumpBufferTimer > 0f && coyoteTimer > 0f && !(crouching && !CanStand()))
            {
                v.y = jumpVelocity;
                jumpBufferTimer = 0f; coyoteTimer = 0f; grounded = false; jumpCutPending = false;
                Sfx.Play(Sfx.Id.Jump);
                squash = new Vector2(0.86f, 1.16f);                 // stretch up on take-off
                Dust(4, 1.3f, 0.3f, 0.5f);
            }
            if (jumpCutPending) { if (v.y > 0f) v.y *= jumpCutMultiplier; jumpCutPending = false; }

            float g = v.y < -0.1f ? gravityScale * fallGravityMultiplier : gravityScale;
            // a moment of float at the top of a held jump
            if (!grounded && jumpHeldLast && Mathf.Abs(v.y) < 1.6f) g *= 0.7f;
            rb.gravityScale = g;
            rb.linearVelocity = v;
        }

        void StartClimb()
        {
            climbing = true; grounded = false;
            crouching = false; ApplyCollider();
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            if (vine != null) vine.Nudge(facingRight ? 1f : -1f);
            Haptics.Tick();
        }

        void StopClimb()
        {
            if (!climbing) return;
            climbing = false;
            noClimbUntil = Time.time + 0.5f;
            rb.gravityScale = gravityScale;
            if (animator != null) animator.speed = 1f;
        }

        public void EnterVine(ClimbableVine v) { vine = v; }

        public void ExitVine(ClimbableVine v)
        {
            if (vine != v) return;
            vine = null;
            if (climbing) StopClimb();
        }

        public void PlayInteract() { if (animator != null) animator.SetTrigger("Interact"); }

        public void Face(bool right) { facingRight = right; if (spriteRenderer != null) spriteRenderer.flipX = !right; }

        public void TeleportTo(Vector2 pos)
        {
            StopClimb();
            vine = null;
            rolling = false; crouching = false; energy = 1f; ApplyCollider();
            rb.position = pos;
            transform.position = pos;
            rb.linearVelocity = Vector2.zero;
            jumpBufferTimer = 0f; airTime = 0f; maxFall = 0f; squash = Vector2.one;
        }

        /// <summary>Called by moving platforms so the player is carried along.</summary>
        public void Carry(Vector2 delta) { rb.position += delta; }
    }
}
