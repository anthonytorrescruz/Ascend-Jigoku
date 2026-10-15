using System.Collections.Generic;
using UnityEngine;

// Physics only. The brain supplies movement; hit reactions temporarily take priority.
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(EnemyCombat))]
[DisallowMultipleComponent]
public sealed class EnemyMotor : MonoBehaviour
{
    private Rigidbody2D body;
    private Collider2D shape;
    private EnemyCombat combat;
    private EnemyHealth health;
    private Vector2 desiredVelocity;
    private float knockbackUntil = float.NegativeInfinity;
    private float nextJumpAt;
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
    public bool IsKnockedBack => Time.time < knockbackUntil;
    public bool IsGrounded => ProbeGround(out _);
    public EnemyDefinition Definition => combat.Definition;
    public Vector2 Velocity => body.linearVelocity;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        shape = GetComponent<Collider2D>();
        combat = GetComponent<EnemyCombat>();
        health = GetComponent<EnemyHealth>();
    }
    private void Start()
    {
        if (Definition == null) return;
        body.gravityScale = Definition.movement == EnemyMovement.Flying ? 0 : Definition.gravityScale;
    }
    public void Move(Vector2 velocity) => desiredVelocity = velocity;
    public void Stop() => desiredVelocity = Vector2.zero;
    private void FixedUpdate() => Tick(Time.time, Time.fixedDeltaTime);
    public void Tick(float now, float dt)
    {
        if (Definition == null || health.CurrentHealth <= 0) return;
        if (now < knockbackUntil) return;
        bool flying = Definition.movement == EnemyMovement.Flying;
        Vector2 wanted = combat.IsStunned ? Vector2.zero : desiredVelocity;
        // Keep a jump's horizontal momentum until landing, except when parried.
        if (!flying && (!IsGrounded || body.linearVelocity.y > 0.1f) && !combat.IsStunned) return;
        Vector2 velocity = body.linearVelocity;
        velocity.x = Mathf.MoveTowards(velocity.x, wanted.x, Definition.acceleration * dt);
        if (flying) velocity.y = Mathf.MoveTowards(velocity.y, wanted.y, Definition.acceleration * dt);
        body.linearVelocity = velocity;
    }
    public void TakeHit(Vector2 awayFromAttacker)
    {
        if (Definition == null || health.CurrentHealth <= 0) return;
        float duration = Definition.hitStunSeconds;
        if (duration > 0) combat.Stun(Time.time, duration);
        if (Definition.knockbackMultiplier <= 0) return;
        float sign = Mathf.Abs(awayFromAttacker.x) > 0.001f ? Mathf.Sign(awayFromAttacker.x) : 1;
        Vector2 impulse = new Vector2(sign * Definition.knockbackVelocity.x, Definition.knockbackVelocity.y);
        if (Mathf.Abs(awayFromAttacker.y) > Mathf.Abs(awayFromAttacker.x))
            impulse = new Vector2(0, Mathf.Sign(awayFromAttacker.y) * Definition.knockbackVelocity.y);
        if (Definition.movement == EnemyMovement.Flying && awayFromAttacker.sqrMagnitude > 0.01f)
            impulse = awayFromAttacker.normalized * Definition.knockbackVelocity.magnitude;
        body.linearVelocity = impulse * Definition.knockbackMultiplier;
        knockbackUntil = Time.time + Mathf.Max(0.06f, duration);
        Stop();
    }
    private ContactFilter2D TerrainFilter()
    {
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(Definition != null ? Definition.terrainLayers : (LayerMask)(1 << 3));
        return filter;
    }
    private bool ProbeGround(out RaycastHit2D ground)
    {
        ground = default;
        if (shape == null || !shape.enabled) return false;
        hits.Clear();
        shape.Cast(Vector2.down, TerrainFilter(), hits, 0.09f);
        foreach (var hit in hits)
            if (!hit.transform.IsChildOf(transform) && hit.normal.y > 0.55f)
            { ground = hit; return true; }
        return false;
    }
    public bool HasFloorAhead(float direction)
    {
        Bounds bounds = shape.bounds;
        Vector2 from = new Vector2(bounds.center.x + Mathf.Sign(direction) * (bounds.extents.x + 0.25f), bounds.min.y + 0.15f);
        return GroundAt(from, 0.5f, out _);
    }
    private bool GroundAt(Vector2 from, float distance, out RaycastHit2D ground)
    {
        hits.Clear();
        Physics2D.Raycast(from, Vector2.down, TerrainFilter(), hits, distance);
        ground = default;
        foreach (var hit in hits)
            if (!hit.transform.IsChildOf(transform) && hit.normal.y > 0.65f)
            { ground = hit; return true; }
        return false;
    }
    public bool IsBlocked(Vector2 direction, float distance = 0.3f)
    {
        if (direction.sqrMagnitude < 0.001f) return false;
        hits.Clear();
        shape.Cast(direction.normalized, TerrainFilter(), hits, distance);
        foreach (var hit in hits)
            if (!hit.transform.IsChildOf(transform) && Vector2.Dot(hit.normal, direction.normalized) < -0.2f) return true;
        return false;
    }
    public Vector2 AvoidTerrain(Vector2 wanted)
    {
        if (!IsBlocked(wanted, Mathf.Max(0.3f, wanted.magnitude * Time.fixedDeltaTime + 0.1f))) return wanted;
        // Slide along a wall or ceiling; this is local avoidance, not maze pathfinding.
        var vertical = new Vector2(0, wanted.y);
        if (Mathf.Abs(vertical.y) > 0.1f && !IsBlocked(vertical)) return vertical;
        var horizontal = new Vector2(wanted.x, 0);
        if (Mathf.Abs(horizontal.x) > 0.1f && !IsBlocked(horizontal)) return horizontal;
        return Vector2.zero;
    }
    public bool TryJump(float direction, Vector2 target, float now)
    {
        if (Definition == null || Definition.movement != EnemyMovement.Jumper || now < nextJumpAt
            || !IsGrounded || IsKnockedBack || combat.IsStunned) return false;
        float gravity = Mathf.Abs(Physics2D.gravity.y * body.gravityScale);
        if (gravity < 0.01f || Definition.jumpSpeed <= 0 || Definition.jumpForwardSpeed <= 0) return false;
        Bounds bounds = shape.bounds;
        float rise = Definition.jumpSpeed * Definition.jumpSpeed / (2 * gravity);
        float maxDistance = Mathf.Min(Definition.jumpSearchDistance, Mathf.Max(1, Mathf.Abs(target.x - bounds.center.x)));
        for (float distance = maxDistance; distance >= 0.6f; distance -= 0.35f)
        {
            float x = bounds.center.x + Mathf.Sign(direction) * distance;
            if (!GroundAt(new Vector2(x, bounds.min.y + rise), rise + Definition.maximumDrop, out var landing)) continue;
            // A landing needs support across the body, not just a point on a corner.
            bool supported = GroundAt(landing.point + new Vector2(-bounds.extents.x * 0.75f, 0.15f), 0.3f, out _)
                && GroundAt(landing.point + new Vector2(bounds.extents.x * 0.75f, 0.15f), 0.3f, out _);
            float dy = landing.point.y + 0.04f - bounds.min.y;
            float discriminant = Definition.jumpSpeed * Definition.jumpSpeed - 2 * gravity * dy;
            if (!supported || discriminant < 0) continue;
            float flight = (Definition.jumpSpeed + Mathf.Sqrt(discriminant)) / gravity;
            float vx = (x - bounds.center.x) / flight;
            if (Mathf.Abs(vx) > Definition.jumpForwardSpeed || !ClearArc(bounds, vx, gravity, flight)) continue;
            body.linearVelocity = new Vector2(vx, Definition.jumpSpeed);
            nextJumpAt = now + Definition.jumpCooldown;
            return true;
        }
        return false;
    }
    private bool ClearArc(Bounds bounds, float vx, float gravity, float flight)
    {
        Vector2 previous = bounds.center;
        for (int i = 1; i <= 12; i++)
        {
            float t = flight * i / 12;
            Vector2 point = (Vector2)bounds.center + new Vector2(vx * t, Definition.jumpSpeed * t - gravity * t * t * 0.5f);
            Vector2 delta = point - previous;
            hits.Clear();
            Physics2D.BoxCast(previous, (Vector2)bounds.size * 0.9f, 0, delta.normalized, TerrainFilter(), hits, delta.magnitude);
            foreach (var hit in hits) if (!hit.transform.IsChildOf(transform)) return false;
            previous = point;
        }
        return true;
    }
    private void OnDisable() => Stop();
}
