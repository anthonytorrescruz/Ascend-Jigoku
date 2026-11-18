using UnityEngine;
using UnityEngine.InputSystem;

// Orthographic follow camera for the player: dead zone and smoothing, look-ahead/look-down, screen shake and boss framing.
// Sits on the main camera; finds the PlayerHealth object itself if `player` isn't assigned.
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
    [Header("Boss Framing")]
    [Min(1)] public float bossZoomMultiplier = 1.18f;
    [Min(1)] public float bossMaxZoomMultiplier = 1.35f;
    private Transform combatTarget;
    private float explorationSize;
    private RectTransform directionCanvas;
    private UnityEngine.UI.Text directionLabel;
    public Transform CombatTarget => combatTarget;
    private Vector2 look;
    private float aheadDirection, downHeld;
    private Rigidbody2D playerBody;
    private PlayerController playerMotion;
    private Camera view;
    private Transform trackedPlayer;
    private Collider2D playerShape;

    private void Awake()
    {
        view = GetComponent<Camera>();
        explorationSize = view.orthographicSize;
    }

    private void Start() => SnapToTarget();

    // The shake offset added last frame, removed again before following so shake never feeds back into the follow.
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
        UpdateDirectionCue();
    }

    // A boss (or similar) the camera should keep in frame. Remembers the normal zoom to return to afterwards.
    public void SetCombatTarget(Transform target)
    {
        if (combatTarget == null && target != null) explorationSize = view != null ? view.orthographicSize : explorationSize;
        combatTarget = target;
    }

    public void ClearCombatTarget(Transform target)
    {
        if (combatTarget == target) combatTarget = null;
    }

    // Shows a "BOSS >" style label at the screen edge while the combat target is off screen. Built lazily on first use.
    private void UpdateDirectionCue()
    {
        if (combatTarget == null || !combatTarget.gameObject.activeInHierarchy || view == null || Time.timeScale <= 0)
        {
            if (directionCanvas != null) directionCanvas.gameObject.SetActive(false);
            return;
        }
        var position = view.WorldToViewportPoint(combatTarget.position + Vector3.up);
        bool outside = position.x < .04f || position.x > .96f || position.y < .04f || position.y > .96f;
        if (outside && directionCanvas == null)
        {
            directionCanvas = AscendJigoku.UI.MenuStyle.CreateCanvas("Boss direction cue", 7, transform);
            var group = directionCanvas.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            directionLabel = AscendJigoku.UI.MenuStyle.Label(
                directionCanvas, "Boss position", "", Vector2.zero, new Vector2(190, 40), 22);
            directionLabel.raycastTarget = false;
            directionLabel.color = new Color(1, .78f, .4f);
        }
        if (directionCanvas == null) return;
        directionCanvas.gameObject.SetActive(outside);
        if (!outside) return;
        bool horizontal = position.x < .04f || position.x > .96f;
        var rect = directionLabel.rectTransform;
        // Pin the label to whichever edge the target left through, inset a little.
        rect.anchorMin = rect.anchorMax = horizontal
            ? new Vector2(position.x < .5f ? 0 : 1, .58f)
            : new Vector2(.5f, position.y < .5f ? 0 : 1);
        rect.pivot = horizontal
            ? new Vector2(position.x < .5f ? 0 : 1, .5f)
            : new Vector2(.5f, position.y < .5f ? 0 : 1);
        rect.anchoredPosition = horizontal
            ? new Vector2(position.x < .5f ? 16 : -16, 0)
            : new Vector2(0, position.y < .5f ? 52 : -52);
        directionLabel.text = horizontal
            ? (position.x < .5f ? "< BOSS" : "BOSS >")
            : (position.y < .5f ? "BOSS BELOW" : "BOSS ABOVE");
    }

    // Finds the player lazily and caches its collider, body and controller (re-cached if `player` is reassigned).
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

    // Jumps straight to the player with no smoothing (scene start, respawn, long teleports).
    public void SnapToTarget()
    {
        if (!FindTarget()) return;
        appliedShake = Vector3.zero;
        look = Vector2.zero;
        downHeld = 0;
        Vector3 focus = PlayerBounds.center + (Vector3)lookOffset;
        transform.position = new Vector3(focus.x, focus.y, transform.position.z);
        Constrain(PlayerBounds);
    }

    // One step of following. Order: look offset -> boss zoom/lead -> dead zone -> smoothing -> keep player inside safe region.
    public void Follow(float dt)
    {
        if (!FindTarget()) return;
        Bounds bounds = PlayerBounds;
        UpdateLook(dt);
        Vector2 position = transform.position;
        Vector2 focus = (Vector2)bounds.center + lookOffset + look;
        bool framing = combatTarget != null && combatTarget.gameObject.activeInHierarchy;
        if (view.orthographic)
        {
            float zoom = explorationSize;
            if (framing)
            {
                // Zoom out far enough to fit the boss horizontally, within the configured multipliers.
                float needed = (Mathf.Abs(combatTarget.position.x - bounds.center.x) + 5) / (2 * Mathf.Max(.4f, view.aspect) * .8f);
                zoom = Mathf.Clamp(
                    needed,
                    explorationSize * bossZoomMultiplier,
                    explorationSize * Mathf.Max(bossZoomMultiplier, bossMaxZoomMultiplier));
                // Shift the focus part of the way toward the boss.
                float lead = Mathf.Clamp(
                    (combatTarget.position.x - bounds.center.x) * .5f,
                    -zoom * view.aspect * .35f,
                    zoom * view.aspect * .35f);
                focus.x = bounds.center.x + lead;
            }
            // 1 - exp(-k*dt) is a frame-rate independent smoothing factor (same pattern used below).
            view.orthographicSize = Mathf.Lerp(view.orthographicSize, zoom, 1 - Mathf.Exp(-3 * Mathf.Max(0, dt)));
        }
        Vector2 delta = focus - position;
        if (teleportDistance > 0 && delta.sqrMagnitude > teleportDistance * teleportDistance)
        {
            SnapToTarget();
            return;
        }
        // Only the part of the offset beyond the dead zone moves the camera.
        Vector2 desired = position + new Vector2(Outside(delta.x, deadZone.x), Outside(delta.y, deadZone.y));
        float blend = smoothSpeed <= 0 ? 1 : 1 - Mathf.Exp(-smoothSpeed * Mathf.Max(0, dt));
        Vector2 followed = Vector2.Lerp(position, desired, blend);
        transform.position = new Vector3(followed.x, followed.y, transform.position.z);
        Constrain(bounds);
    }

    // Look-ahead leans toward the facing/travel direction; holding down while idle on the ground peeks below the ledge.
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

    // How far `value` lies beyond +/- zone (0 inside the dead zone), keeping its sign.
    private static float Outside(float value, float zone) => Mathf.Sign(value) * Mathf.Max(0, Mathf.Abs(value) - Mathf.Max(0, zone));

    // Final clamp so the player's whole body stays inside the safe region (wider while framing a boss) and within maxDistance.
    private void Constrain(Bounds bounds)
    {
        bool framing = combatTarget != null && combatTarget.gameObject.activeInHierarchy;
        Vector2 region = framing ? new Vector2(.72f, .6f) : safeRegion;
        Vector2 anchor = (Vector2)bounds.center + (framing ? Vector2.zero : look);
        Vector2 offset = (Vector2)transform.position - anchor;
        if (view.orthographic)
        {
            // Account for the whole body, including on a narrow viewport.
            Vector2 limit = new Vector2(
                Mathf.Max(0, view.orthographicSize * view.aspect * Mathf.Clamp(region.x, 0.05f, 1) - bounds.extents.x),
                Mathf.Max(0, view.orthographicSize * Mathf.Clamp(region.y, 0.05f, 1) - bounds.extents.y));
            offset.x = Mathf.Clamp(offset.x, -limit.x, limit.x);
            offset.y = Mathf.Clamp(offset.y, -limit.y, limit.y);
        }
        offset = Vector2.ClampMagnitude(offset, Mathf.Max(0, framing ? view.orthographicSize * .7f : maxDistance));
        Vector2 center = anchor + offset;
        transform.position = new Vector3(center.x, center.y, transform.position.z);
    }
}
