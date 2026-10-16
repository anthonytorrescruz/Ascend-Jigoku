using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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
    private readonly List<ContactPoint2D> groundContacts = new List<ContactPoint2D>();
    public bool IsFacingRight => facingRight;
    public float MoveSpeed => moveSpeed;
    public float JumpSpeed => jumpForce;
    public int MaxJumps => maxJumpCount;
    public bool IsKnockedBack => Time.time < knockbackEndsAt;
    public bool IsGrounded => CheckGrounded();

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        bodyShape = GetComponent<Collider2D>();
        controls = new PlayerActions();
    }
    private void OnEnable()
    {
        controls.Enable();
        controls.PlayerInput.Jump.performed += OnJump;
    }
    private void OnDisable()
    {
        controls.PlayerInput.Jump.performed -= OnJump;
        controls.Disable();
    }
    private void OnDestroy() => controls.Dispose();
    private void Update()
    {
        moveInput = controls.PlayerInput.Move.ReadValue<Vector2>();
        if (!IsKnockedBack && Mathf.Abs(moveInput.x) > 0.1f && (moveInput.x > 0) != facingRight)
        {
            facingRight = moveInput.x > 0;
            Vector3 scale = transform.localScale;
            scale.x *= -1;
            transform.localScale = scale;
        }
        bool grounded = CheckGrounded();
        if (grounded && !wasGrounded) jumpCount = 0;
        wasGrounded = grounded;
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
        var wanted = new Vector2(moveInput.x * moveSpeed, rb.linearVelocityY);
        rb.linearVelocity = Vector2.SmoothDamp(rb.linearVelocity, wanted, ref velocityRef, smoothTime);
    }
    private void OnJump(InputAction.CallbackContext context)
    {
        if (context.ReadValue<float>() <= 0) return;
        if (CheckGrounded()) jumpCount = 0;
        if (jumpCount >= maxJumpCount) return;
        jumpCount++;
        wasGrounded = false;
        rb.linearVelocityY = jumpForce;
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
        CancelKnockback();
        jumpCount = 0;
        wasGrounded = false;
        moveInput = velocityRef = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0;
    }
}
