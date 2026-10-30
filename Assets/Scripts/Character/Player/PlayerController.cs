using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 20;
    [SerializeField] private float smoothTime = 0.01f;
    [SerializeField] private bool facingRight = true;
    [SerializeField] private Rigidbody2D rb;
    [Header("Jump")]
    [SerializeField] private float jumpForce = 10;
    [SerializeField] private int maxJumpCount = 2;
    [SerializeField] private float coyoteTime = 0.2f;
    [SerializeField] private float jumpBufferTime = 0.2f;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    [SerializeField] private LayerMask groundLayer;
    [Header("Damage Knockback")]
    [Min(0), SerializeField] private float knockbackSpeed = 8;
    [Min(0), SerializeField] private float knockbackDuration = 0.15f;
    private PlayerActions controls;
    private Vector2 moveInput, velocityRef;
    private int jumpCount;
    private bool wasGrounded;
    private float knockbackEndsAt = float.NegativeInfinity;
    private float knockbackVelocity;
    private Collider2D bodyShape;
    private PlayerSpell playerSpell;
    private readonly List<ContactPoint2D> groundContacts = new List<ContactPoint2D>();
    public bool IsFacingRight => facingRight;
    public float MoveSpeed => moveSpeed;
    public float JumpSpeed => jumpForce;
    public int MaxJumps => maxJumpCount;
    public bool IsKnockedBack => Time.time < knockbackEndsAt;
    public bool IsGrounded => CheckGrounded();
    [Header("Dash Functionality")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    private bool canDash = true;
    private bool isDashing;
    private bool hasDashedInAir;

    [Header("Wall Jump Functionality")]
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float wallCheckDistance = 0.6f;
    [SerializeField] private LayerMask wallLayer;

    [SerializeField] private float wallJumpForceX = 8f;
    [SerializeField] private float wallJumpForceY = 12f;
    [SerializeField] private float wallJumpTime = 0.2f;
    [SerializeField] private float wallSlideSpeed = 2f;

    [SerializeField] private bool isTouchingWall;
    private bool isWallSliding;
    private bool isWallJumping;
    private float facingDirection = 1;

    private Animator animator;
    [Range(0, 1), SerializeField] private float jumpVolume = 0.12f;
    [Range(0, 1), SerializeField] private float dashVolume = 0.12f;
    public AudioClip jumpClip;
    public AudioClip dashClip;

    

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        foreach (var c in GetComponents<Collider2D>())
            {
                if (c.enabled && !c.isTrigger) { bodyShape = c; break; }
            }
        playerSpell = GetComponent<PlayerSpell>();
        controls = new PlayerActions();
    }
    private void OnEnable()
    {
        if(controls == null) controls = new PlayerActions();
        controls.Enable();
        controls.PlayerInput.Jump.performed += OnJump;
        controls.PlayerInput.Dash.performed += OnDash;
    }
    private void OnDisable()
    {
        controls.PlayerInput.Jump.performed -= OnJump;
        controls.PlayerInput.Dash.performed -= OnDash;
        controls.Disable();
    }
    private void OnDestroy() => controls.Dispose();
    private void Update()
    {
        moveInput = controls.PlayerInput.Move.ReadValue<Vector2>();
        /*
        if (!IsKnockedBack && Mathf.Abs(moveInput.x) > 0.1f && (moveInput.x > 0) != facingRight)
        {
            facingRight = moveInput.x > 0;
            Vector3 scale = transform.localScale;
            scale.x *= -1;
            transform.localScale = scale;
        }
        */
        bool moving = Mathf.Abs(moveInput.x) > 0.1f && Mathf.Abs(rb.linearVelocityX) > 0.1f;
        animator.SetFloat("Speed", moving ? 1f : 0f);

        
        HandleFlip();
        bool grounded = CheckGrounded();
        

        CalculateCoyoteAndJumpBufferTime(grounded);

        if (grounded && !wasGrounded)
        {
            jumpCount = 0;
            hasDashedInAir = false;
        }

        wasGrounded = grounded;

        CheckWall();
        HandleJump();
        HandleWallSlide();
        
    }
    private void CheckWall()
    {
        if (wallCheck == null) { isTouchingWall = false; return; }
        isTouchingWall = Physics2D.Raycast(wallCheck.position, Vector2.right * facingDirection, wallCheckDistance, wallLayer);
    }

    private void HandleWallSlide()
    {
        bool pressingIntoWall = moveInput.x * facingDirection > 0.1f;

        if(isTouchingWall && !IsGrounded && rb.linearVelocityY < 0 && pressingIntoWall)
        {
            isWallSliding = true;
            rb.linearVelocityY = Mathf.Max(rb.linearVelocityY, -wallSlideSpeed);
        }
        else
        {
            isWallSliding = false;
        }
        
    }

    private void CalculateCoyoteAndJumpBufferTime(bool grounded)
    {
        if (grounded) coyoteTimeCounter = coyoteTime;
        else coyoteTimeCounter -= Time.deltaTime;

        if (jumpBufferCounter > 0) jumpBufferCounter -= Time.deltaTime;
    }

    private void HandleFlip()
    {
        if (IsKnockedBack || isWallJumping) return;

         if (Mathf.Abs(moveInput.x) > 0.1f)
        {
            bool moveRight = moveInput.x > 0;

            if (moveRight != facingRight)
            {
                Flip();
            }
        }

        if (animator != null)
            animator.SetFloat("Speed", Mathf.Abs(rb.linearVelocityX));
    }

    private void Flip()
    {
        facingRight = !facingRight;
        facingDirection = facingRight ? 1 : -1;

        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    private void HandleJump()
    {
        if(jumpBufferCounter <= 0) return;

        if(isTouchingWall && !IsGrounded)
        {
            PerformWallJump();
            return;
        }

        //First Jump
        if(coyoteTimeCounter > 0 && jumpCount == 0)
        {
            PerformJump();
            return;
        }
        //Second Jump
        if(jumpCount > 0 && jumpCount < maxJumpCount)
        {
            PerformJump();
        }
    }

    private void PerformWallJump()
    {
        /*
        isWallJumping = true;

        animator.SetTrigger("Jump");

        jumpBufferCounter = 0;
        coyoteTimeCounter = 0;

        float jumpDir = facingDirection; 

        rb.linearVelocity = new Vector2(jumpDir * wallJumpForceX, wallJumpForceY);
        */
         jumpBufferCounter = 0;
        coyoteTimeCounter = 0;
        jumpCount = 1;
        wasGrounded = false;
        isWallSliding = false;

        float jumpDir = -facingDirection;
        if ((jumpDir > 0) != facingRight) Flip();

        velocityRef = Vector2.zero;
        rb.linearVelocity = new Vector2(jumpDir * wallJumpForceX, wallJumpForceY);

        if (animator != null) animator.SetTrigger("Jump");

        StartCoroutine(WallJumpLock());
    }

    private IEnumerator WallJumpLock()
    {
        isWallJumping = true;
        yield return new WaitForSeconds(wallJumpTime);
        isWallJumping = false;
    }

    private void PerformJump()
    {
        jumpCount++;
        jumpBufferCounter = 0;
        coyoteTimeCounter = 0;   
        wasGrounded = false;
        rb.linearVelocityY = jumpForce;
    }

    private bool CheckGrounded()
    {
        if (rb == null || bodyShape == null || !bodyShape.enabled || !rb.simulated || rb.linearVelocityY > 0.1f) return false;
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(groundLayer);
        groundContacts.Clear();
        bodyShape.GetContacts(filter, groundContacts);
        foreach (var contact in groundContacts)
            if (contact.normal.y > 0.65f && contact.point.y <= bodyShape.bounds.min.y + 0.08f) return true;
        return false;
    }
    
    private void FixedUpdate()
    {
        if (IsKnockedBack)
        { rb.linearVelocityX = knockbackVelocity; velocityRef.x = 0; return; }

        if(isDashing) return;
        if(isWallJumping) return;

        float inputX = moveInput.x;
        if (playerSpell != null && playerSpell.IsFocusing) inputX *= playerSpell.FocusMovementMultiplier;

        if(isTouchingWall && !IsGrounded && inputX * facingDirection > 0)
        {
            inputX = 0;
        }

        var wanted = new Vector2(inputX * moveSpeed, rb.linearVelocityY);
        rb.linearVelocity = Vector2.SmoothDamp(rb.linearVelocity, wanted, ref velocityRef, smoothTime);
    }
    private void OnJump(InputAction.CallbackContext context)
    {
        playerSpell?.Cancel();

        /*
        if (context.ReadValue<float>() <= 0) return;
        if (CheckGrounded()) jumpCount = 0;
        if (jumpCount >= maxJumpCount) return;
        jumpCount++;
        wasGrounded = false;
        rb.linearVelocityY = jumpForce;
        */
        jumpBufferCounter = jumpBufferTime;
        CombatFeedback.PlayClip(jumpClip, jumpVolume);
    }

    private void OnDash(InputAction.CallbackContext context)
    {
        playerSpell?.Cancel();
        if(canDash && !isDashing)
        {
            if(CheckGrounded() || !hasDashedInAir)
            {
               StartCoroutine(Dash());
            }
        }
        CombatFeedback.PlayClip(dashClip, dashVolume);
    }
    IEnumerator Dash()
    {
        canDash = false;
        isDashing = true;

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        float dashDirection = facingRight ? 1f : -1f;
        rb.linearVelocityX = dashDirection * dashSpeed;

        if(!CheckGrounded())
        {
            hasDashedInAir = true;
        }

        yield return new WaitForSeconds(dashDuration);
        rb.gravityScale = originalGravity;
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }
    public void PogoBounce()
    {
        // Pogo neither spends nor restores a jump.
        wasGrounded = false;
        rb.linearVelocityY = jumpForce;
    }
    public void FallBounce(float speed)
    {
        CancelKnockback();
        jumpCount = 0;
        wasGrounded = false;
        rb.linearVelocityY = Mathf.Max(jumpForce, speed);
    }
    public void ApplyHorizontalKnockback(float direction)
    {
        if (direction == 0 || knockbackSpeed <= 0) return;
        knockbackVelocity = Mathf.Sign(direction) * knockbackSpeed;
        knockbackEndsAt = Time.time + knockbackDuration;
        velocityRef.x = 0;
        rb.linearVelocityX = knockbackVelocity;
    }
    public void CancelKnockback()
    {
        knockbackEndsAt = float.NegativeInfinity;
        velocityRef.x = 0;
    }
    public void ResetMovement()
    {
        playerSpell?.Cancel(true);
        CancelKnockback();
        jumpCount = 0;
        wasGrounded = false;
        moveInput = velocityRef = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0;
    }

    private void OnDrawGizmos()
    {
        if (wallCheck == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(wallCheck.position, Vector2.right * (facingRight ? 1f : -1f) * wallCheckDistance);
    }
}
