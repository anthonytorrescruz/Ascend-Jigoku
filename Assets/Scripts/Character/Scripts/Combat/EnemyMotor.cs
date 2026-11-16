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
    private bool isFlying;
    private float nextOverlapCheckAt, detourUntil;
    private int steerSide, detourSide = 1;
    private readonly List<Collider2D> overlaps = new List<Collider2D>();
    public bool IsFlying => isFlying;
    public float JumpRise => Definition == null ? 0 : Definition.jumpSpeed * Definition.jumpSpeed / (2 * Mathf.Max(0.01f, Mathf.Abs(Physics2D.gravity.y) * Definition.gravityScale));
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
        body.mass = Mathf.Max(1, Definition.bodyMass);
        SetFlying(Definition.movement == EnemyMovement.Flying);
    }
    public void SetFlying(bool value)
    {
        isFlying = value;
        body.gravityScale = value ? 0 : Definition.gravityScale;
    }
    public void Move(Vector2 velocity) => desiredVelocity = velocity;
    public void Stop() => desiredVelocity = Vector2.zero;
    private void FixedUpdate() => Tick(Time.time, Time.fixedDeltaTime);
    public void Tick(float now, float dt)
    {
        if (Definition == null || health.CurrentHealth <= 0) return;
        if (now >= nextOverlapCheckAt) { nextOverlapCheckAt = now + 0.2f; ResolveOverlap(); }
        if (now < knockbackUntil)
        {
            // Hits shove enemies, but never off the edge of the ledge they stand on.
            Vector2 pushed = body.linearVelocity;
            if (!isFlying && Definition.ledgeGuard && Mathf.Abs(pushed.x) > 0.1f && IsGrounded && !HasFloorAhead(pushed.x))
                body.linearVelocity = new Vector2(0, pushed.y);
            return;
        }
        bool flying = isFlying;
        Vector2 wanted = combat.IsStunned ? Vector2.zero : desiredVelocity;
        // Keep a jump's horizontal momentum until landing, except when parried.
        if (!flying && (!IsGrounded || body.linearVelocity.y > 0.1f) && !combat.IsStunned) return;
        if (!flying && Mathf.Abs(wanted.x) > 0.01f) TryStepUp(Mathf.Sign(wanted.x));
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
        if (isFlying && awayFromAttacker.sqrMagnitude > 0.01f)
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
        return GroundAt(from, 0.55f, out _);
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
    // Blocked by something a walker cannot just step onto.
    public bool IsWallAhead(float direction) => IsBlocked(Vector2.right * Mathf.Sign(direction)) && !CanStepUp(Mathf.Sign(direction));
    private bool CanStepUp(float direction)
    {
        float step = Definition != null ? Definition.stepHeight : 0;
        if (step <= 0.01f || isFlying) return false;
        Bounds bounds = shape.bounds;
        Vector2 size = (Vector2)bounds.size * 0.96f;
        var filter = TerrainFilter();
        hits.Clear();
        Physics2D.BoxCast(bounds.center, size, 0, Vector2.up, filter, hits, step + 0.03f);
        foreach (var hit in hits) if (!hit.transform.IsChildOf(transform)) return false;
        hits.Clear();
        Physics2D.BoxCast(bounds.center + Vector3.up * (step + 0.03f), size, 0, Vector2.right * direction, filter, hits, 0.3f);
        foreach (var hit in hits) if (!hit.transform.IsChildOf(transform)) return false;
        return true;
    }
    private void TryStepUp(float direction)
    {
        if (!IsGrounded || !IsBlocked(Vector2.right * direction) || !CanStepUp(direction)) return;
        body.position += new Vector2(direction * 0.06f, Definition.stepHeight + 0.03f);
        body.linearVelocity = new Vector2(body.linearVelocity.x, 0);
    }
    public bool GroundBelow(float distance)
    {
        Bounds bounds = shape.bounds;
        foreach (float x in new[] { -bounds.extents.x * 0.8f, 0, bounds.extents.x * 0.8f })
            if (!GroundAt(new Vector2(bounds.center.x + x, bounds.min.y + 0.05f), distance, out _)) return false;
        return true;
    }
    // Walks the floor between here and the target: gaps and walls a walker cannot cross make it unreachable.
    public bool CanWalkTo(Vector2 target)
    {
        if (Definition == null) return false;
        bool jumper = Definition.movement == EnemyMovement.Jumper;
        float step = Definition.stepHeight + 0.05f;
        float maxClimb = jumper ? Mathf.Max(step, JumpRise * 0.85f) : step;
        float maxGap = jumper ? Definition.jumpSearchDistance * 0.9f : 0;
        Bounds bounds = shape.bounds;
        float direction = Mathf.Sign(target.x - bounds.center.x);
        float stop = Mathf.Max(Definition.stoppingDistance, 0.3f);
        float limit = Mathf.Abs(target.x - bounds.center.x) - stop;
        float feet = bounds.min.y, gap = 0, previousX = bounds.center.x;
        var filter = TerrainFilter();
        for (float d = 0.4f; d <= limit + 0.4f; d += 0.4f)
        {
            float x = bounds.center.x + direction * Mathf.Min(d, Mathf.Max(0, limit));
            hits.Clear();
            Physics2D.Raycast(new Vector2(x, feet + maxClimb + 0.3f), Vector2.down, filter, hits, maxClimb + 0.3f + Definition.maximumDrop + 0.3f);
            bool found = false; float top = 0;
            foreach (var hit in hits)
                if (!hit.transform.IsChildOf(transform) && hit.normal.y > 0.65f) { found = true; top = hit.point.y; break; }
            if (!found) { gap += x == previousX ? 0 : Mathf.Abs(x - previousX); if (gap > maxGap) return false; previousX = x; continue; }
            if (top - feet > maxClimb || feet - top > Definition.maximumDrop + 0.6f) return false;
            // A ceiling or wall between the last sample and this one at body height.
            hits.Clear();
            Physics2D.Linecast(new Vector2(previousX, Mathf.Max(feet, top) + bounds.size.y * 0.6f), new Vector2(x, Mathf.Max(feet, top) + bounds.size.y * 0.6f), filter, hits);
            foreach (var hit in hits) if (!hit.transform.IsChildOf(transform)) return false;
            gap = 0; feet = top; previousX = x;
            if (d >= limit) break;
        }
        return gap <= maxGap;
    }
    public void Detour(int side, float until) { detourSide = side >= 0 ? 1 : -1; detourUntil = until; }
    // Flyers: straight ahead if clear, else the nearest clear direction in a fan, sticking to one side so they do not dither.
    public Vector2 Steer(Vector2 wanted)
    {
        float speed = wanted.magnitude;
        if (speed < 0.01f) return wanted;
        Vector2 direction = wanted / speed;
        float look = Mathf.Max(0.3f, Definition.steerLookAhead);
        if (Time.time < detourUntil)
        {
            Vector2 sideways = Rotate(direction, 90 * detourSide);
            if (!IsBlocked(sideways, look)) return sideways * speed * 0.8f;
        }
        else if (!IsBlocked(direction, look)) { steerSide = 0; return wanted; }
        int first = steerSide != 0 ? steerSide : (direction.x >= 0 ? 1 : -1);
        for (int angle = 30; angle <= 150; angle += 30)
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? first : -first;
                Vector2 candidate = Rotate(direction, angle * side);
                if (IsBlocked(candidate, look)) continue;
                steerSide = side;
                return candidate * speed * (angle >= 90 ? 0.6f : 0.85f);
            }
        return Vector2.zero;
    }
    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
    // Pops the body out of solid terrain it ended up inside (spawned or squeezed into it).
    private void ResolveOverlap()
    {
        if (shape == null || !shape.enabled) return;
        overlaps.Clear();
        shape.Overlap(TerrainFilter(), overlaps);
        foreach (var other in overlaps)
        {
            if (other == null || other.transform.IsChildOf(transform)) continue;
            var distance = shape.Distance(other);
            if (!distance.isOverlapped) continue;
            Vector2 centre = shape.bounds.center, away = centre - other.ClosestPoint(centre);
            if (away.sqrMagnitude < 0.0001f) away = Vector2.up;
            body.position += away.normalized * (Mathf.Abs(distance.distance) + 0.02f);
        }
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
