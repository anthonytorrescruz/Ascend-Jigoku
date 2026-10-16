using UnityEngine;

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
    private Camera view;
    private Transform trackedPlayer;
    private Collider2D playerShape;

    private void Awake() => view = GetComponent<Camera>();
    private void Start() => SnapToTarget();
    private void LateUpdate() => Follow(Time.deltaTime);
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
        }
        if (view == null) view = GetComponent<Camera>();
        return true;
    }
    private Bounds PlayerBounds => playerShape != null && playerShape.enabled
        ? playerShape.bounds : new Bounds(player.position, Vector3.zero);
    public void SnapToTarget()
    {
        if (!FindTarget()) return;
        Vector3 focus = PlayerBounds.center + (Vector3)lookOffset;
        transform.position = new Vector3(focus.x, focus.y, transform.position.z);
        Constrain(PlayerBounds);
    }
    public void Follow(float dt)
    {
        if (!FindTarget()) return;
        Bounds bounds = PlayerBounds;
        Vector2 position = transform.position;
        Vector2 focus = (Vector2)bounds.center + lookOffset;
        Vector2 delta = focus - position;
        if (teleportDistance > 0 && delta.sqrMagnitude > teleportDistance * teleportDistance)
        { SnapToTarget(); return; }
        Vector2 desired = position + new Vector2(Outside(delta.x, deadZone.x), Outside(delta.y, deadZone.y));
        float blend = smoothSpeed <= 0 ? 1 : 1 - Mathf.Exp(-smoothSpeed * Mathf.Max(0, dt));
        Vector2 followed = Vector2.Lerp(position, desired, blend);
        transform.position = new Vector3(followed.x, followed.y, transform.position.z);
        Constrain(bounds);
    }
    private static float Outside(float value, float zone) => Mathf.Sign(value) * Mathf.Max(0, Mathf.Abs(value) - Mathf.Max(0, zone));
    private void Constrain(Bounds bounds)
    {
        Vector2 offset = transform.position - bounds.center;
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
        Vector2 center = (Vector2)bounds.center + offset;
        transform.position = new Vector3(center.x, center.y, transform.position.z);
    }
}
