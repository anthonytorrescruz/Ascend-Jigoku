using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Functionality")]
    [SerializeField] private float moveSpeed = 20f;
    
    private PlayerActions controller;
    private Vector2 moveInput;
    private Vector2 targetVelocity;
    private Vector2 velocityRef;

    [SerializeField] private float smoothTime = 0.01f;
    [SerializeField] private bool facingRight = true;

    public bool IsFacingRight => facingRight;

    [Header("Damage Knockback")]
    [Min(0), SerializeField] private float knockbackSpeed = 8f;
    [Min(0), SerializeField] private float knockbackDuration = 0.15f;
    private float knockbackEndsAt = float.NegativeInfinity;
    private float knockbackVelocity;
    public bool IsKnockedBack => Time.time < knockbackEndsAt;

    [Header("Jump Functionality")]

    [SerializeField] private bool isGrounded;
    // Combat queries the current physics state instead of depending on which
    // component's Update happened first this frame.
    public bool IsGrounded => CheckGrounded();
    [SerializeField] private bool wasGrounded;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private int maxJumpCount = 2;
    private int jumpCount;
    private readonly List<Collider2D> groundContacts = new List<Collider2D>();
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Space(10)]
    [SerializeField] private Rigidbody2D rb;
    void Awake()
    {
        // initializes the players movement actions
        controller = new PlayerActions();
    }

    void OnEnable()
    {
        // enables the keybindings for inputs built into unity
        controller.Enable();
        controller.PlayerInput.Jump.performed += OnJump;
    }

    void OnDisable()
    {
        controller.Disable();
        controller.PlayerInput.Jump.performed -= OnJump;
    }

    private void OnDestroy() => controller.Dispose();

    void Update()
    {
        // updates the value depending on if the player moves left or right
        //example left is -1 and right is 1
        moveInput = controller.PlayerInput.Move.ReadValue<Vector2>();
        // decides if the sprite should flip
        HandleFlip();

        // checks to see if the player is on the ground
        isGrounded = CheckGrounded();

        if(!wasGrounded && isGrounded)
        {
            jumpCount = 0;
        }

        wasGrounded = isGrounded;
    }

    private bool CheckGrounded()
    {
        // A jump/pogo has already left the ground even if the feet probe still
        // touches the floor for the first frame of upward movement.
        if (rb == null || groundCheck == null || rb.linearVelocityY > 0.01f)
        {
            return false;
        }

        ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(groundLayer);
        groundContacts.Clear();
        Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, filter, groundContacts);

        foreach (Collider2D contact in groundContacts)
        {
            if (!contact.isTrigger && contact.attachedRigidbody != rb
                && !contact.transform.IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }

    private void HandleFlip()
    {
        // checks if the player is moving
        if(Mathf.Abs(rb.linearVelocityX) > 0.1f)
        {
            bool moveRight = rb.linearVelocityX > 0;
            // if player is moving one direction but facing the wrong direction
            if(moveRight != facingRight)
            {
                Flip();
            }
        }
    }

    private void Flip()
    {
        // changes value to show if the player is facing left or right
        facingRight = !facingRight;

        // changes the scale to -1 which just flips the sprite
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }
    void FixedUpdate()
    {
        if (IsKnockedBack)
        {
            rb.linearVelocityX = knockbackVelocity;
            velocityRef.x = 0;
            return;
        }
        // the targetVelocity is what the speed we want to get to is
        targetVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocityY);
        // slowly builds up to the targetVelocity making the movement feel smoother
        rb.linearVelocity = Vector2.SmoothDamp(rb.linearVelocity, targetVelocity, ref velocityRef, smoothTime);
    }

    public void ApplyHorizontalKnockback(float direction)
    {
        if (rb == null || direction == 0 || knockbackSpeed <= 0) return;
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

    private void OnJump(InputAction.CallbackContext context)
    {
        if (context.ReadValue<float>() <= 0f) return;
        // if the sprite is grounded than apply the jumpforce to the y position.
        if(jumpCount < maxJumpCount)
        {
            // immediatly changes the isGrounded variable to false so you can't jump again before the OverlapCircle
            isGrounded = false;
            jumpCount++;
            rb.linearVelocityY = 0;
            rb.linearVelocityY = jumpForce;
        }
    }

    public void PogoBounce()
    {
        isGrounded = false;

        // Pogo is an external impulse. Do not spend or refill jumpCount.

        // Preserve horizontal movement, but completely replace
        // vertical movement with the normal jump velocity.
        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x,
            jumpForce
        );
    }
}
