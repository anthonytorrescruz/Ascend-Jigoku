using UnityEngine;

public enum EnemyState { Idle, Patrol, Chase, Evade, Attack, Return, Stunned, Knockback }

// Decides what to do; EnemyMotor moves. Engage / give up / return use timers so the state never flip-flops.
// Flow each FixedUpdate (Tick): bail out if dead, knocked back or stunned; find the player; Watch() updates the
// engaged flag with hysteresis; ChooseFlight() toggles flying; then one of
//   not engaged -> Unengaged(): Idle / Patrol around home, or Return to it
//   mid-windup or swing -> Attack (stand still)
//   engaged -> Engaged(): Evade a visible player swing, else Chase the last known player position.
// CheckStuck() last turns "tried to move but did not get anywhere" into turn-around / detour / give-up.
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(EnemyMotor), typeof(EnemyCombat))]
[DisallowMultipleComponent]
public sealed class EnemyBrain : MonoBehaviour
{
    private EnemyCombat combat;
    private EnemyMotor motor;
    private EnemyHealth health;
    // home is the patrol anchor (moved by Rehome); lastKnown is the player's last position seen with line of sight.
    private Vector2 home, lastKnown;
    private float patrolDirection = 1;
    // Timers below are all absolute Time.time values ("...At" / "...Until") unless named "...For" (accumulated seconds).
    private float lastSeenAt, observedAt, evadeUntil, nextEvadeAt, nextTargetSearch;
    private Vector2 evadeVelocity;
    private CombatAttack.Swing observedSwing;
    // engaged: chasing the player. returning: walking/flying back to home after giving up. Never both (IsEngaged).
    private bool engaged, returning;
    private float patrolPauseUntil;
    private float nextFlightChangeAt;
    private float nextFlightRollAt, flyUntil;
    private float reaggroAt, unreachableFor, blockedFor, nextReachAt;
    // reachable/walkable: cached CheckReach result. holding/hoverArrived: inside stopping distance (gives hysteresis
    // so the enemy does not jitter at the edge). moveIntent: this tick asked the motor to move (used by CheckStuck).
    private bool reachable = true, walkable = true, holding, hoverArrived, moveIntent;
    // Stuck detection: position at stuckSince; strikes escalate the response; detourSide flips for flyers.
    private Vector2 stuckAnchor;
    private float stuckSince, lastStuckAt;
    private int stuckStrikes, detourSide = 1;
    private Collider2D body;
    private CombatAttack watchedAttack;
    private PlayerHealth watchedOwner;
    private readonly System.Collections.Generic.List<RaycastHit2D> rayHits =
        new System.Collections.Generic.List<RaycastHit2D>();
    private readonly System.Collections.Generic.List<Collider2D> overlapHits =
        new System.Collections.Generic.List<Collider2D>();
    private const float PatrolPauseSeconds = 1.2f;

    public EnemyState State { get; private set; }

    // EnemyCombat checks this before starting a windup.
    public bool CanAttack => IsEngaged && State != EnemyState.Evade;
    public bool IsEngaged => engaged && !returning;
    public EnemyDefinition Definition => combat.Definition;

    private void Awake()
    {
        combat = GetComponent<EnemyCombat>();
        motor = GetComponent<EnemyMotor>();
        health = GetComponent<EnemyHealth>();
        body = GetComponent<Collider2D>();
        home = transform.position;
    }

    public void SetHome(Vector2 position) => home = position;

    private void FixedUpdate() => Tick(Time.time);

    public void Tick(float now)
    {
        var profile = Definition;
        if (profile == null || health.CurrentHealth <= 0)
        {
            motor.Stop();
            return;
        }
        moveIntent = false;
        if (motor.IsKnockedBack)
        {
            State = EnemyState.Knockback;
            ResetStuck(now);
            return;
        }
        if (combat.IsStunned)
        {
            State = EnemyState.Stunned;
            motor.Stop();
            ResetStuck(now);
            return;
        }
        // Re-search twice a second while there is no live target; this also covers a respawned player.
        if ((combat.Target == null || !combat.Target.isActiveAndEnabled) && now >= nextTargetSearch)
        {
            combat.SetTarget(FindAnyObjectByType<PlayerHealth>());
            nextTargetSearch = now + 0.5f;
        }
        var target = combat.Target;
        bool valid = target != null && target.isActiveAndEnabled && target.CurrentHealth > 0;
        bool busy = combat.IsWindingUp || combat.Attack.IsAttacking;
        Vector2 position = transform.position;
        if (valid) Watch(now, position, target, profile, busy);
        else if (engaged) Disengage(now, 0);
        ChooseFlight(now, target, valid);
        if (!IsEngaged)
            Unengaged(now, position, profile);
        else if (busy)
        {
            State = EnemyState.Attack;
            motor.Stop();
            ResetStuck(now);
        }
        else
            Engaged(now, position, target, profile);
        CheckStuck(now, transform.position);
    }

    // Aggro, line of sight, reachability and leash, all with hysteresis.
    // Engaging needs sight + reachable + inside aggroRadius (and no re-aggro lockout). Once engaged, the enemy keeps
    // chasing until one of: too far from home or player (leash), no sight for loseSightSeconds (sight only refreshes
    // inside 1.25x aggroRadius, a wider "keep" band than the engage radius), or unreachable for giveUpSeconds.
    // A started attack is never interrupted.
    private void Watch(float now, Vector2 position, PlayerHealth target, EnemyDefinition profile, bool busy)
    {
        Vector2 targetPosition = target.transform.position;
        float distance = Vector2.Distance(position, targetPosition);
        bool sight = combat.HasLineOfSight();
        if (sight) lastKnown = targetPosition;
        bool reach = CheckReach(now, targetPosition, profile);
        if (!engaged)
        {
            if (!returning && now >= reaggroAt && sight && reach && distance <= profile.aggroRadius)
            {
                engaged = true;
                lastSeenAt = now;
                unreachableFor = blockedFor = 0;
            }
            return;
        }
        if (sight && distance <= profile.aggroRadius * 1.25f) lastSeenAt = now;
        unreachableFor = reach ? 0 : unreachableFor + Time.fixedDeltaTime;
        // Let a started attack finish.
        if (busy) return;
        if (Vector2.Distance(position, home) > profile.leashRadius || distance > profile.leashRadius
            || now - lastSeenAt > profile.loseSightSeconds || unreachableFor > profile.giveUpSeconds)
            Disengage(now, profile.reaggroDelay);
    }

    // Can the player be reached on foot (or by flying)? Re-evaluated at most every 0.2 s and cached in between.
    // Walkable = target is not too far above (attack height + jump rise for hoppers) or more than 2 below,
    // and the motor's ground probe finds a walkable route. Flyers/stationary enemies are always "reachable".
    private bool CheckReach(float now, Vector2 targetPosition, EnemyDefinition profile)
    {
        if (now < nextReachAt) return reachable;
        nextReachAt = now + 0.2f;
        if (profile.movement == EnemyMovement.Flying || profile.movement == EnemyMovement.Stationary)
            return reachable = walkable = true;
        if (motor.IsGrounded || motor.IsFlying)
        {
            float dy = targetPosition.y - transform.position.y;
            float up = profile.attackSize.y * 1.2f + 0.5f + (profile.movement == EnemyMovement.Jumper ? motor.JumpRise : 0);
            walkable = dy <= up && dy >= -2 && motor.CanWalkTo(targetPosition);
        }
        return reachable = walkable || profile.canFly;
    }

    // Stops chasing. lockout is how long (seconds) the enemy refuses to re-engage. Ground enemies that cannot walk
    // back to their old home just adopt the current spot as the new home.
    private void Disengage(float now, float lockout)
    {
        engaged = false;
        reaggroAt = now + lockout;
        combat.CancelIntent();
        returning = true;
        if (!motor.IsFlying && !CanReturn()) Rehome();
    }
    // Home must be walkable and not more than 2.5 units above/below.
    private bool CanReturn() => motor.CanWalkTo(home) && Mathf.Abs(transform.position.y - home.y) < 2.5f;
    private void Rehome()
    {
        home = transform.position;
        returning = false;
        holding = false;
        stuckStrikes = 0;
    }

    // Not chasing: flyers hover above home; walkers return home if they strayed beyond patrolRadius + 1.5, otherwise
    // patrol between home +/- patrolRadius, pausing at each end.
    private void Unengaged(float now, Vector2 position, EnemyDefinition profile)
    {
        if (motor.IsFlying)
        {
            Vector2 goal = FlightGoal();
            // FlyTo stops at the goal, which sits above home, so arrival is judged against the goal.
            if (returning && Vector2.Distance(position, goal) <= profile.stoppingDistance + 0.05f) returning = false;
            // Reports Patrol while hovering so the animator shows movement.
            State = returning ? EnemyState.Return : EnemyState.Patrol;
            FlyTo(goal, returning ? profile.moveSpeed : profile.moveSpeed * 0.5f);
            return;
        }
        float homeDx = home.x - position.x;
        if (!returning && Mathf.Abs(homeDx) > profile.patrolRadius + 1.5f)
        {
            returning = true;
            if (!CanReturn())
            {
                Rehome();
                return;
            }
        }
        if (returning)
        {
            if (Mathf.Abs(homeDx) < 0.5f)
            {
                returning = false;
                State = EnemyState.Idle;
                motor.Stop();
                return;
            }
            State = EnemyState.Return;
            // Blocked for over a second on the way back: give up and make this spot the new home.
            if (!WalkTo(home, profile.moveSpeed, now))
            {
                blockedFor += Time.fixedDeltaTime;
                if (blockedFor > 1)
                {
                    blockedFor = 0;
                    Rehome();
                }
            }
            else
                blockedFor = 0;
            return;
        }
        if (profile.patrolRadius <= 0.01f || profile.movement == EnemyMovement.Stationary || now < patrolPauseUntil)
        {
            State = EnemyState.Idle;
            motor.Stop();
            return;
        }
        State = EnemyState.Patrol;
        float goalX = home.x + patrolDirection * profile.patrolRadius;
        if (Mathf.Abs(goalX - position.x) <= Mathf.Max(0.2f, profile.stoppingDistance) + 0.05f)
        {
            TurnPatrol(now, PatrolPauseSeconds);
            return;
        }
        if (!WalkTo(new Vector2(goalX, position.y), profile.moveSpeed * 0.5f, now)) TurnPatrol(now, 0.4f);
    }
    // Reverses patrol direction and stands still for pause seconds.
    private void TurnPatrol(float now, float pause)
    {
        patrolDirection = -patrolDirection;
        patrolPauseUntil = now + pause;
        State = EnemyState.Idle;
        motor.Stop();
    }

    // Engaged and not mid-attack: dodge a visible player swing if one is being evaded, else chase.
    // Flyers aim for hoverHeight above the player (if that spot is not inside terrain).
    private void Engaged(float now, Vector2 position, PlayerHealth target, EnemyDefinition profile)
    {
        ObserveAttack(now);
        if (now < evadeUntil)
        {
            State = EnemyState.Evade;
            if (motor.IsFlying) FlyTo(position + evadeVelocity, evadeVelocity.magnitude);
            else WalkTo(position + evadeVelocity, evadeVelocity.magnitude, now);
            return;
        }
        State = EnemyState.Chase;
        if (motor.IsFlying)
        {
            Vector2 goal = lastKnown;
            if (profile.hoverHeight > 0 && !Blocked(goal + Vector2.up * profile.hoverHeight)) goal.y += profile.hoverHeight;
            FlyTo(goal, profile.moveSpeed);
            return;
        }
        if (WalkTo(lastKnown, profile.moveSpeed, now)) blockedFor = 0;
        else if ((blockedFor += Time.fixedDeltaTime) > profile.giveUpSeconds + 0.4f)
        {
            blockedFor = 0;
            Disengage(now, profile.reaggroDelay);
        }
    }

    // Idle hover point above home, lowered if terrain (ceiling) is within the hover height plus 0.6 margin.
    private Vector2 FlightGoal()
    {
        float height = Definition.idleFlightHeight;
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(Definition.terrainLayers);
        Physics2D.Raycast(home, Vector2.up, filter, rayHits, height + 0.6f);
        foreach (var hit in rayHits)
            if (!hit.transform.IsChildOf(transform))
            {
                height = Mathf.Max(0, hit.distance - 0.6f);
                break;
            }
        return home + Vector2.up * height;
    }
    // True if terrain overlaps a 0.4-radius circle at the point.
    private bool Blocked(Vector2 point)
    {
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(Definition.terrainLayers);
        return Physics2D.OverlapCircle(point, 0.4f, filter, overlapHits) > 0;
    }

    // Flight is needed to get home when walking is impossible, or to reach a target that is far above, behind a wall,
    // or not walkable.
    private bool WantsFlight(PlayerHealth target, bool valid)
    {
        if (returning && !motor.CanWalkTo(home)) return true;
        if (!IsEngaged || !valid) return false;
        Vector2 toTarget = target.transform.position - transform.position;
        return toTarget.y > Definition.flyWhenTargetAbove
            || motor.IsWallAhead(Mathf.Sign(toTarget.x)) || !walkable;
    }
    // Takes off when flight is needed or on a random roll (flyChance, rolled every flightCooldown seconds); lands once
    // the flight timer expires, nothing needs it, and there is ground within 3 units below.
    private void ChooseFlight(float now, PlayerHealth target, bool valid)
    {
        var profile = Definition;
        if (!profile.canFly) return;
        bool needed = WantsFlight(target, valid);
        if (!motor.IsFlying)
        {
            // Short rest after landing.
            if (now < nextFlightChangeAt) return;
            bool takeOff = needed;
            if (!takeOff && now >= nextFlightRollAt)
            {
                nextFlightRollAt = now + profile.flightCooldown;
                takeOff = Random.value < profile.flyChance;
            }
            if (takeOff)
            {
                motor.SetFlying(true);
                flyUntil = now + Random.Range(profile.flightSecondsMin, profile.flightSecondsMax);
            }
        }
        else if (now >= flyUntil && !needed && !returning)
        {
            if (motor.GroundBelow(3))
            {
                motor.SetFlying(false);
                nextFlightChangeAt = now + profile.flightCooldown;
            }
            else
                flyUntil = now + 1;   // never drop into a pit
        }
    }
    // Evasive enemies watch the player's current swing. After reactionSeconds, if the swing box (padded by 0.35)
    // overlaps this body, start an evade away from the player for evadeSeconds, then wait evadeCooldown.
    private void ObserveAttack(float now)
    {
        if (!Definition.evadeAttacks || !combat.HasLineOfSight()) return;
        if (watchedOwner != combat.Target)
        {
            watchedOwner = combat.Target;
            watchedAttack = watchedOwner.GetComponent<CombatAttack>();
        }
        var swing = watchedAttack != null ? watchedAttack.CurrentSwing : null;
        if (swing == null || !swing.IsActive(now)) return;
        if (swing != observedSwing)
        {
            observedSwing = swing;
            observedAt = now;
        }
        if (now - observedAt < Definition.reactionSeconds || now < nextEvadeAt) return;
        Rect threat = swing.Box;
        threat.min -= Vector2.one * 0.35f;
        threat.max += Vector2.one * 0.35f;
        Bounds bounds = body.bounds;
        if (!threat.Overlaps(new Rect((Vector2)bounds.min, bounds.size))) return;
        Vector2 away = (Vector2)(transform.position - combat.Target.transform.position);
        if (!motor.IsFlying) away = new Vector2(Mathf.Abs(away.x) < 0.01f ? 1 : Mathf.Sign(away.x), 0);
        if (away.sqrMagnitude < 0.01f) away = Vector2.up;
        evadeVelocity = away.normalized * Definition.evadeSpeed;
        evadeUntil = now + Definition.evadeSeconds;
        nextEvadeAt = now + Definition.evadeCooldown;
    }

    // Ground movement. False when a wall or ledge stops it (and a hopper cannot clear it).
    // Within stoppingDistance horizontally it just stops and counts as arrived (holding widens the band by 0.2 to
    // avoid jitter). Walls, missing floor, or a target well above (0.65) within jumpSearchDistance request a jump.
    private bool WalkTo(Vector2 destination, float speed, float now)
    {
        Vector2 delta = destination - (Vector2)transform.position;
        if (Definition.movement == EnemyMovement.Stationary)
        {
            motor.Stop();
            return true;
        }
        float arrive = Definition.stoppingDistance + (holding ? 0.2f : 0);
        if (Mathf.Abs(delta.x) <= arrive)
        {
            holding = true;
            motor.Stop();
            return true;
        }
        holding = false;
        float direction = Mathf.Sign(delta.x);
        bool wall = motor.IsWallAhead(direction), floor = motor.HasFloorAhead(direction);
        bool needsJump = wall || !floor || (delta.y > 0.65f && Mathf.Abs(delta.x) < Definition.jumpSearchDistance);
        if (needsJump && motor.TryJump(direction, destination, now))
        {
            moveIntent = true;
            return true;
        }
        if (wall || !floor)
        {
            motor.Stop();
            return false;
        }
        motor.Move(new Vector2(direction * speed, 0));
        moveIntent = true;
        return true;
    }
    // Flight movement: steers toward the goal around terrain; hoverArrived widens the stop band by 0.3 once inside.
    private void FlyTo(Vector2 goal, float speed)
    {
        Vector2 delta = goal - (Vector2)transform.position;
        float arrive = Definition.stoppingDistance + (hoverArrived ? 0.3f : 0);
        if (delta.magnitude <= arrive)
        {
            hoverArrived = true;
            motor.Stop();
            return;
        }
        hoverArrived = false;
        motor.Move(motor.Steer(delta.normalized * speed));
        moveIntent = true;
    }

    private void ResetStuck(float now)
    {
        stuckAnchor = transform.position;
        stuckSince = now;
    }

    // Trying to move but going nowhere (less than ~0.25 units from the anchor for stuckSeconds): turn around, detour,
    // or give up. Strikes accumulate unless 5 s pass between stuck events; a second strike also gives up a flying chase,
    // and flyers flip their detour side and a third strike while unengaged rehomes them.
    private void CheckStuck(float now, Vector2 position)
    {
        if (!moveIntent || (position - stuckAnchor).sqrMagnitude > 0.06f)
        {
            ResetStuck(now);
            return;
        }
        if (now - stuckSince < Definition.stuckSeconds) return;
        stuckSince = now;
        stuckStrikes = now - lastStuckAt > 5 ? 1 : stuckStrikes + 1;
        lastStuckAt = now;
        if (IsEngaged)
        {
            if (stuckStrikes >= 2 || !motor.IsFlying) Disengage(now, Definition.reaggroDelay);
        }
        else if (returning && !motor.IsFlying) Rehome();
        else if (!motor.IsFlying) TurnPatrol(now, 0.4f);
        if (!motor.IsFlying) return;
        detourSide = -detourSide;
        motor.Detour(detourSide, now + 1);
        if (stuckStrikes >= 3 && !IsEngaged)
        {
            Rehome();
        }
    }

    private void OnDisable()
    {
        engaged = false;
        motor?.Stop();
    }
}
