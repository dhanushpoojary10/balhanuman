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
            ApplyCollider();
            Sfx.Play(Sfx.Id.Jump, 0.65f, 0.8f);
            Fx.Burst(dustSprite, transform.position + Vector3.up * 0.1f, 5, 1.5f, 0.4f, -0.4f, 0.35f, new Color(1, 1, 1, 0.5f), 0.3f, 11);
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
            if (rollBufferTimer > 0f && grounded && !rolling && !climbing && rollCooldownTimer <= 0f && !InputLocked) StartRoll();

            // ---- bend (crouch): hold down while on the ground and not next to a vine
            bool wantCrouch = !InputLocked && grounded && !rolling && !climbing && vine == null && vertInput < -0.3f;
            if (wantCrouch) { if (!crouching) { crouching = true; ApplyCollider(); } }
            else if (crouching && CanStand()) { crouching = false; ApplyCollider(); }

            if (!grounded && !climbing) airTime += Time.deltaTime;
            if (grounded && !wasGrounded)
            {
                if (airTime > 0.3f)
                {
                    Sfx.Play(Sfx.Id.Land);
                    Fx.Burst(dustSprite, transform.position + Vector3.up * 0.1f, 6, 1.6f, 0.45f, -0.5f, 0.35f, new Color(1, 1, 1, 0.55f), 0.4f, 11);
                }
                airTime = 0f;
            }
            wasGrounded = grounded;

            if (!rolling)
            {
                if (moveInput > 0.1f) facingRight = true; else if (moveInput < -0.1f) facingRight = false;
            }
            if (spriteRenderer != null) spriteRenderer.flipX = !facingRight;

            if (grounded && !crouching && Mathf.Abs(rb.linearVelocity.x) > 2f && !rolling)
            {
                stepTimer -= Time.deltaTime;
                if (stepTimer <= 0f) { Sfx.Play(Sfx.Id.Step, Random.Range(0.9f, 1.2f), 0.7f); stepTimer = 0.28f; }
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
            }
            if (jumpCutPending) { if (v.y > 0f) v.y *= jumpCutMultiplier; jumpCutPending = false; }

            rb.gravityScale = v.y < -0.1f ? gravityScale * fallGravityMultiplier : gravityScale;
            rb.linearVelocity = v;
        }

        void StartClimb()
        {
            climbing = true; grounded = false;
            crouching = false; ApplyCollider();
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
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
            rolling = false; crouching = false; ApplyCollider();
            rb.position = pos;
            transform.position = pos;
            rb.linearVelocity = Vector2.zero;
            jumpBufferTimer = 0f; airTime = 0f;
        }

        /// <summary>Called by moving platforms so the player is carried along.</summary>
        public void Carry(Vector2 delta) { rb.position += delta; }
    }
}
