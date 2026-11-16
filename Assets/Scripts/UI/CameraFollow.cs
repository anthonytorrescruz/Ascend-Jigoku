using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform player;
    [Min(0)] public float smoothSpeed = 14f; // Zero follows instantly.
    [Tooltip("Small movement allowed before following, in world units.")]
    public Vector2 deadZone = new Vector2(0.15f, 0.12f);
    [Tooltip("Width/height of the centered safe region, as fractions of the screen.")]
    public Vector2 safeRegion = new Vector2(0.36f, 0.4f);
    [Min(0)] public float maxDistance = 1.5f;
    public Vector2 lookOffset = new Vector2(0, 0.35f);
    [Min(0)] public float teleportDistance = 8;
    [Header("Look")]
    [Tooltip("Shift toward the direction of travel, in world units. 0 = off.")]
    [Min(0)] public float lookAhead = 1.2f;
    [Min(0)] public float lookAheadSpeed = 4f;
    [Tooltip("Hold Down (S / arrow / stick) while standing still to see below the ledge. 0 = off.")]
    [Min(0)] public float lookDown = 3f;
    [Min(0)] public float lookDownDelay = 0.35f;
    private Vector2 look;
    private float aheadDirection, downHeld;
    private Rigidbody2D playerBody;
    private PlayerController playerMotion;
    private Camera view;
    private Transform trackedPlayer;
    private Collider2D playerShape;

    private void Awake() => view = GetComponent<Camera>();
    private void Start() => SnapToTarget();
    private Vector3 appliedShake;
    [Tooltip("Stop following, e.g. while the player drops off screen after a fatal fall.")]
    public bool Hold;
    private void LateUpdate()
    {
        if (Hold) return;
        // Follow from the unshaken position, then layer the current shake on top.
        transform.position -= appliedShake;
        Follow(Time.deltaTime);
        appliedShake = AscendJigoku.Gameplay.EncounterFeedback.CameraOffset;
        transform.position += appliedShake;
    }
    private bool FindTarget()
    {
        if (player == null)
        {
            var health = FindAnyObjectByType<PlayerHealth>();
            if (health == null) return false;
            player = health.transform;
        }
        if (trackedPlayer != player)
        {
            trackedPlayer = player;
            playerShape = player.GetComponent<Collider2D>();
            playerBody = player.GetComponent<Rigidbody2D>();
            playerMotion = player.GetComponent<PlayerController>();
        }
        if (view == null) view = GetComponent<Camera>();
        return true;
    }
    private Bounds PlayerBounds => playerShape != null && playerShape.enabled
        ? playerShape.bounds : new Bounds(player.position, Vector3.zero);
    public void SnapToTarget()
    {
        if (!FindTarget()) return;
        appliedShake = Vector3.zero;
        look = Vector2.zero; downHeld = 0;
        Vector3 focus = PlayerBounds.center + (Vector3)lookOffset;
        transform.position = new Vector3(focus.x, focus.y, transform.position.z);
        Constrain(PlayerBounds);
    }
    public void Follow(float dt)
    {
        if (!FindTarget()) return;
        Bounds bounds = PlayerBounds;
        UpdateLook(dt);
        Vector2 position = transform.position;
        Vector2 focus = (Vector2)bounds.center + lookOffset + look;
        Vector2 delta = focus - position;
        if (teleportDistance > 0 && delta.sqrMagnitude > teleportDistance * teleportDistance)
        { SnapToTarget(); return; }
        Vector2 desired = position + new Vector2(Outside(delta.x, deadZone.x), Outside(delta.y, deadZone.y));
        float blend = smoothSpeed <= 0 ? 1 : 1 - Mathf.Exp(-smoothSpeed * Mathf.Max(0, dt));
        Vector2 followed = Vector2.Lerp(position, desired, blend);
        transform.position = new Vector3(followed.x, followed.y, transform.position.z);
        Constrain(bounds);
    }
    private void UpdateLook(float dt)
    {
        float vx = playerBody != null ? playerBody.linearVelocity.x : 0;
        if (Mathf.Abs(vx) > 1f) aheadDirection = Mathf.Sign(vx);
        else if (aheadDirection == 0 && playerMotion != null) aheadDirection = playerMotion.IsFacingRight ? 1 : -1;
        bool still = playerMotion != null && playerMotion.IsGrounded && Mathf.Abs(vx) < 0.5f;
        downHeld = lookDown > 0 && still && Time.timeScale > 0 && DownHeld() ? downHeld + dt : 0;
        float maxDown = view != null && view.orthographic ? view.orthographicSize * 0.4f : lookDown;
        Vector2 target = new Vector2(aheadDirection * lookAhead, downHeld >= lookDownDelay ? -Mathf.Min(lookDown, maxDown) : 0);
        look = Vector2.Lerp(look, target, 1 - Mathf.Exp(-lookAheadSpeed * Mathf.Max(0, dt)));
    }
    private static bool DownHeld()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed)) return true;
        var pad = Gamepad.current;
        return pad != null && (pad.leftStick.y.ReadValue() < -0.6f || pad.dpad.down.isPressed);
    }
    private static float Outside(float value, float zone) => Mathf.Sign(value) * Mathf.Max(0, Mathf.Abs(value) - Mathf.Max(0, zone));
    private void Constrain(Bounds bounds)
    {
        Vector2 anchor = (Vector2)bounds.center + look;
        Vector2 offset = (Vector2)transform.position - anchor;
        if (view.orthographic)
        {
            // Account for the whole body, including on a narrow viewport.
            Vector2 limit = new Vector2(
                Mathf.Max(0, view.orthographicSize * view.aspect * Mathf.Clamp(safeRegion.x, 0.05f, 1) - bounds.extents.x),
                Mathf.Max(0, view.orthographicSize * Mathf.Clamp(safeRegion.y, 0.05f, 1) - bounds.extents.y));
            offset.x = Mathf.Clamp(offset.x, -limit.x, limit.x);
            offset.y = Mathf.Clamp(offset.y, -limit.y, limit.y);
        }
        offset = Vector2.ClampMagnitude(offset, Mathf.Max(0, maxDistance));
        Vector2 center = anchor + offset;
        transform.position = new Vector3(center.x, center.y, transform.position.z);
    }
}
