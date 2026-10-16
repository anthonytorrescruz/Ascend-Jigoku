using UnityEngine;

public enum EnemyState { Idle, Patrol, Chase, Evade, Attack, Return, Stunned, Knockback }

[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(EnemyMotor), typeof(EnemyCombat))]
[DisallowMultipleComponent]
public sealed class EnemyBrain : MonoBehaviour
{
    private EnemyCombat combat;
    private EnemyMotor motor;
    private EnemyHealth health;
    private Vector2 home;
    private float patrolDirection = 1;
    private float lastSeenAt, observedAt, evadeUntil, nextEvadeAt, nextTargetSearch;
    private Vector2 evadeVelocity;
    private CombatAttack.Swing observedSwing;
    private bool engaged, returning;
    public EnemyState State { get; private set; }
    public bool CanAttack => IsEngaged && State != EnemyState.Evade;
    public bool IsEngaged => engaged && !returning;
    public EnemyDefinition Definition => combat.Definition;

    private void Awake()
    {
        combat = GetComponent<EnemyCombat>();
        motor = GetComponent<EnemyMotor>();
        health = GetComponent<EnemyHealth>();
        home = transform.position;
    }
    public void SetHome(Vector2 position) => home = position;
    private void FixedUpdate() => Tick(Time.time);
    public void Tick(float now)
    {
        var profile = Definition;
        if (profile == null || health.CurrentHealth <= 0) { motor.Stop(); return; }
        if (motor.IsKnockedBack) { State = EnemyState.Knockback; return; }
        if (combat.IsStunned) { State = EnemyState.Stunned; motor.Stop(); return; }
        if (combat.Target == null && now >= nextTargetSearch)
        {
            combat.SetTarget(FindAnyObjectByType<PlayerHealth>());
            nextTargetSearch = now + 0.5f;
        }
        var target = combat.Target;
        bool valid = target != null && target.isActiveAndEnabled && target.CurrentHealth > 0;
        Vector2 position = transform.position;
        if (valid)
        {
            float distance = Vector2.Distance(position, target.transform.position);
            if (!returning && distance <= profile.aggroRadius && combat.HasLineOfSight())
            { engaged = true; lastSeenAt = now; }
            if (engaged && (Vector2.Distance(position, home) > profile.leashRadius
                || distance > profile.leashRadius || now - lastSeenAt > profile.loseSightSeconds))
            { engaged = false; returning = true; combat.CancelIntent(); }
        }
        else if (engaged) { engaged = false; returning = true; combat.CancelIntent(); }
        if (!IsEngaged)
        {
            State = returning ? EnemyState.Return : profile.patrolRadius > 0 ? EnemyState.Patrol : EnemyState.Idle;
            float dx = home.x - position.x;
            if (returning && Vector2.Distance(position, home) < 0.4f) returning = false;
            if (!returning)
            {
                if (Mathf.Abs(position.x - home.x) >= profile.patrolRadius) patrolDirection = Mathf.Sign(dx);
                dx = patrolDirection * profile.patrolRadius;
            }
            Vector2 destination = returning ? home : position + Vector2.right * dx;
            Navigate(destination, returning ? profile.moveSpeed : profile.moveSpeed * 0.5f, now);
            return;
        }
        if (combat.IsWindingUp || combat.Attack.IsAttacking)
        { State = EnemyState.Attack; motor.Stop(); return; }
        ObserveAttack(now);
        if (now < evadeUntil)
        {
            State = EnemyState.Evade;
            Navigate(position + evadeVelocity, evadeVelocity.magnitude, now);
            return;
        }
        State = EnemyState.Chase;
        Vector2 goal = target.transform.position;
        if (profile.movement == EnemyMovement.Flying) goal.y += profile.hoverHeight;
        Navigate(goal, profile.moveSpeed, now);
    }
    private void ObserveAttack(float now)
    {
        if (!Definition.evadeAttacks || !combat.HasLineOfSight()) return;
        var swing = combat.Target.GetComponent<CombatAttack>()?.CurrentSwing;
        if (swing == null || !swing.IsActive(now)) return;
        if (swing != observedSwing) { observedSwing = swing; observedAt = now; }
        if (now - observedAt < Definition.reactionSeconds || now < nextEvadeAt) return;
        Rect threat = swing.Box;
        threat.min -= Vector2.one * 0.35f;
        threat.max += Vector2.one * 0.35f;
        Bounds body = GetComponent<Collider2D>().bounds;
        if (!threat.Overlaps(new Rect((Vector2)body.min, body.size))) return;
        Vector2 away = (Vector2)(transform.position - combat.Target.transform.position);
        if (Definition.movement != EnemyMovement.Flying) away = new Vector2(Mathf.Abs(away.x) < 0.01f ? 1 : Mathf.Sign(away.x), 0);
        if (away.sqrMagnitude < 0.01f) away = Vector2.up;
        evadeVelocity = away.normalized * Definition.evadeSpeed;
        evadeUntil = now + Definition.evadeSeconds;
        nextEvadeAt = now + Definition.evadeCooldown;
    }
    private void Navigate(Vector2 destination, float speed, float now)
    {
        Vector2 delta = destination - (Vector2)transform.position;
        if (Definition.movement == EnemyMovement.Stationary) { motor.Stop(); return; }
        if (Definition.movement == EnemyMovement.Flying)
        {
            motor.Move(delta.magnitude <= Definition.stoppingDistance ? Vector2.zero : motor.AvoidTerrain(delta.normalized * speed));
            return;
        }
        float direction = Mathf.Sign(delta.x);
        bool blocked = motor.IsBlocked(Vector2.right * direction);
        bool floor = motor.HasFloorAhead(direction);
        bool needsJump = blocked || !floor || (delta.y > 0.65f && Mathf.Abs(delta.x) < Definition.jumpSearchDistance);
        if (needsJump && motor.TryJump(direction, destination, now)) return;
        if (Mathf.Abs(delta.x) <= Definition.stoppingDistance || blocked || !floor)
        {
            motor.Stop();
            if (!IsEngaged) patrolDirection = -patrolDirection;
            return;
        }
        motor.Move(new Vector2(direction * speed, 0));
    }
    private void OnDisable() { engaged = false; motor?.Stop(); }
}
