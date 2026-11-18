using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum FireWormState { Idle, Chase, FireballWindup, SlamWindup, SlamActive, Recovery, Stunned, VolleyWindup, Transition }
public enum FireWormAttack { Fireball, Slam, Volley }

// Reuses EnemyHealth, EnemyMotor, EnemyContactDamage and the combat resolver.
// Fire Worm boss. BossFightGate spawns it inactive and activates it when the player enters the arena.
// It chases the player along the arena floor and attacks when the cooldown allows: a close-range slam, or a
// ranged aimed fireball. Health phases (thresholds in FireWormBossSettings) make it harder:
//   phase 2: slams also send ground shockwaves and fireballs alternate with 3-shot fan volleys
//   phase 3: bigger volleys and shorter cooldowns
// Each phase change interrupts it with a short Transition state. Every attack is a windup (telegraph),
// the attack itself, then Recovery. A parry stuns it (handled by EnemyCombat).
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyCombat), typeof(EnemyMotor))]
public sealed class FireWormBoss : MonoBehaviour
{
    [SerializeField] private FireWormBossSettings settings;
    [Tooltip("The boss stays on this platform and only engages players in this arena.")]
    [SerializeField] private Collider2D arena;
    [SerializeField] private SpriteRenderer tailVisual;
    private EnemyCombat combat;
    private EnemyMotor motor;
    private EnemyHealth health;
    private CombatAttack attack;
    private Rigidbody2D body;
    private Collider2D[] shapes;
    private Collider2D headShape;
    private readonly List<Collider2D> turnObstacles = new List<Collider2D>();
    private float tailOffsetX;
    private SpriteRenderer headVisual, slamVisual;
    private FireWormPresentation presentation;
    private Transform projectileRoot;
    private readonly List<BossFireball> projectiles = new List<BossFireball>();
    private readonly Stack<BossFireball> pool = new Stack<BossFireball>();
    // Absolute Time.time values: phaseEndsAt = end of the current windup / active / recovery / transition state,
    // nextAttackAt = earliest start of the next attack.
    private float phaseEndsAt, nextAttackAt, nextTargetSearch, tailFlashUntil;
    // slamOffset = slam hitbox position relative to the boss; lockedAim = player position captured at windup start.
    private Vector2 slamOffset, lockedAim;
    private Color tailColor;
    // facing = direction the head points (-1 left, 1 right); bodyFacing = which way the body is turned.
    private int facing = -1, bodyFacing = 1, attackFacing = -1, previousHealth;
    public FireWormState State { get; private set; }
    public FireWormBossSettings Settings => settings;
    public Collider2D Arena => arena;
    public EnemyCombat Combat => combat;
    public bool IsFacingRight => bodyFacing > 0;
    public int FireballsLaunched { get; private set; }
    public int SlamsPerformed { get; private set; }
    public int VolleysPerformed { get; private set; }
    // 1 to 3; only ever increases.
    public int HealthPhase { get; private set; } = 1;
    // Alternates between a plain fireball and a volley while in phase 2 or later.
    private bool volleyNext;
    public bool EncounterActive { get; private set; } = true;
    public int ShockwavesLaunched { get; private set; }

    public void BindArena(Collider2D value) => arena = value;

    public void SetEncounterActive(bool value)
    {
        EncounterActive = value;
        if (!value && attack != null)
            CancelAttacks(true);
    }

    // Union of the bounds of all enabled solid colliders (head and tail segments).
    public Bounds BodyBounds
    {
        get
        {
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            bool first = true;
            foreach (var shape in shapes)
                if (shape != null && shape.enabled)
                {
                    if (first)
                    {
                        bounds = shape.bounds;
                        first = false;
                    }
                    else
                        bounds.Encapsulate(shape.bounds);
                }
            return bounds;
        }
    }

    private void Awake()
    {
        combat = GetComponent<EnemyCombat>();
        motor = GetComponent<EnemyMotor>();
        health = GetComponent<EnemyHealth>();
        attack = GetComponent<CombatAttack>();
        body = GetComponent<Rigidbody2D>();
        headVisual = GetComponent<SpriteRenderer>();
        presentation = GetComponent<FireWormPresentation>();
        headShape = GetComponent<Collider2D>();
        if (tailVisual != null)
        {
            tailOffsetX = Mathf.Abs(tailVisual.transform.localPosition.x);
            bodyFacing = tailVisual.transform.localPosition.x > 0 ? -1 : 1;
        }
        facing = bodyFacing;
        var all = GetComponentsInChildren<Collider2D>();
        var solids = new List<Collider2D>();
        foreach (var shape in all)
            if (!shape.isTrigger && shape.attachedRigidbody == body)
                solids.Add(shape);
        shapes = solids.ToArray();
        previousHealth = health.CurrentHealth;
        if (tailVisual != null)
            tailColor = tailVisual.color;
        var visual = new GameObject("Slam Warning and Impact");
        visual.transform.SetParent(transform, false);
        slamVisual = visual.AddComponent<SpriteRenderer>();
        slamVisual.sprite = headVisual.sprite;
        slamVisual.sortingLayerID = headVisual.sortingLayerID;
        slamVisual.sortingOrder = headVisual.sortingOrder + 1;
        visual.SetActive(false);
    }

    private void OnEnable() => health.HealthChanged += OnHealthChanged;

    private void Start()
    {
        // EnemyCombat supplies targeting/parry stun; this component selects the boss patterns.
        combat.enabled = false;
        combat.SetTarget(FindAnyObjectByType<PlayerHealth>());
        previousHealth = health.CurrentHealth;
    }

    // Flashes the tail on damage and enters the next health phase when a threshold (fraction of max health) is crossed.
    private void OnHealthChanged(int current, int maximum)
    {
        if (current < previousHealth)
            tailFlashUntil = Time.time + 0.1f;
        previousHealth = current;
        if (settings == null || !settings.healthPhases || current <= 0)
            return;
        int phase = current <= maximum * settings.phaseThreeHealth ? 3
            : current <= maximum * settings.phaseTwoHealth ? 2 : 1;
        if (phase <= HealthPhase)
            return;
        HealthPhase = phase;
        CancelAttacks(true);
        State = FireWormState.Transition;
        phaseEndsAt = Time.time + settings.phaseTransitionSeconds;
        nextAttackAt = phaseEndsAt;
    }

    private void FixedUpdate() => Tick(Time.time);

    // The boss brain, run every physics step (public so tests can drive it with a fake clock).
    // Order of checks: inactive or dead -> find target -> Transition -> stun -> windup states (fire the attack
    // when the windup ends) -> active slam -> recovery -> out-of-range check -> start an attack -> otherwise chase.
    public void Tick(float now)
    {
        if (settings == null || combat.Definition == null || health.CurrentHealth <= 0 || Time.timeScale <= 0)
        {
            motor.Stop();
            return;
        }
        if (!EncounterActive)
        {
            motor.Stop();
            return;
        }
        if (combat.Target == null && now >= nextTargetSearch)
        {
            combat.SetTarget(FindAnyObjectByType<PlayerHealth>());
            nextTargetSearch = now + 0.5f;
        }
        var target = combat.Target;
        if (target == null || !target.isActiveAndEnabled || target.CurrentHealth <= 0)
        {
            CancelAttacks(true);
            return;
        }
        bool canFacePlayer = FacePlayer(target, now);
        if (State == FireWormState.Transition)
        {
            motor.Stop();
            body.linearVelocityX = 0;
            if (now < phaseEndsAt)
                return;
            State = FireWormState.Idle;
        }
        if (combat.IsStunned)
        {
            motor.Stop();
            body.linearVelocityX = 0;
            attack.CancelAll();
            slamVisual.gameObject.SetActive(false);
            State = FireWormState.Stunned;
            nextAttackAt = Mathf.Max(nextAttackAt, combat.StunnedUntil + settings.recoverySeconds);
            return;
        }
        if (State == FireWormState.Stunned)
            State = FireWormState.Idle;
        // Windup finished: release the attack (fireball, fan volley or slam).
        if (State == FireWormState.FireballWindup || State == FireWormState.SlamWindup || State == FireWormState.VolleyWindup)
        {
            motor.Stop();
            if (now < phaseEndsAt)
                return;
            attack.Feedback?.CancelWindup();
            if (State == FireWormState.FireballWindup)
            {
                LaunchFireball();
                Recover(now);
            }
            else if (State == FireWormState.VolleyWindup)
            {
                // Fan of 3 fireballs, or up to 7 in phase 3, spread evenly around the aim line.
                int count = HealthPhase >= 3 ? Mathf.Clamp(settings.phaseThreeVolleyCount, 3, 7) : 3;
                for (int i = 0; i < count; i++)
                    LaunchFireball((i - (count - 1) * 0.5f) * settings.volleySpreadDegrees);
                VolleysPerformed++;
                Recover(now);
            }
            else
            {
                attack.BeginAttack(
                    attackFacing < 0 ? CombatDirection.Left : CombatDirection.Right,
                    slamOffset,
                    settings.slamSize,
                    settings.slamDamage,
                    settings.slamActiveFrames,
                    ~0,
                    now);
                State = FireWormState.SlamActive;
                SlamsPerformed++;
                // From phase 2 the slam also sends a ground wave out to each side.
                if (settings.slamShockwaves && HealthPhase >= 2)
                {
                    LaunchGroundWave(-1);
                    LaunchGroundWave(1);
                }
                phaseEndsAt = now + Mathf.Max(1, settings.slamActiveFrames) / CombatAttack.ReferenceFramesPerSecond;
            }
            return;
        }
        if (State == FireWormState.SlamActive)
        {
            motor.Stop();
            if (attack.CurrentSwing == null || attack.CurrentSwing.Cancelled || now >= phaseEndsAt)
                Recover(now);
            return;
        }
        if (State == FireWormState.Recovery)
        {
            motor.Stop();
            if (now < phaseEndsAt)
                return;
            State = FireWormState.Idle;
        }
        Bounds bounds = BodyBounds;
        var targetShape = target.GetComponent<Collider2D>();
        Bounds player = targetShape != null ? targetShape.bounds : new Bounds(target.transform.position, Vector3.one);
        // Player out of aggro range or outside the arena's horizontal extent: stand still.
        if ((player.center - bounds.center).sqrMagnitude > combat.Definition.aggroRadius * combat.Definition.aggroRadius
            || (arena != null && (player.center.x < arena.bounds.min.x || player.center.x > arena.bounds.max.x)))
        {
            State = FireWormState.Idle;
            motor.Stop();
            return;
        }
        if (!canFacePlayer)
        {
            // A player standing on the tail can obstruct a safe body turn. Keep the solids in place,
            // but still threaten that position with an aimed shot rather than becoming defenseless.
            State = FireWormState.Idle;
            motor.Stop();
            body.linearVelocityX = 0;
            if (settings.automaticAttacks && now >= nextAttackAt && motor.IsGrounded && combat.HasLineOfSight())
                TryBeginAttack(FireWormAttack.Fireball, now);
            return;
        }
        // gap = horizontal distance between the head and the player. Slam only when close and the player is low
        // enough for the slam box to reach; otherwise fireball (or volley) within fireballRange.
        float gap = facing < 0 ? bounds.min.x - player.max.x : player.min.x - bounds.max.x;
        bool lowEnoughForSlam = player.center.y <= bounds.min.y + settings.slamSize.y + 0.5f;
        if (settings.automaticAttacks && now >= nextAttackAt && motor.IsGrounded && combat.HasLineOfSight())
        {
            if (gap <= settings.slamRange && lowEnoughForSlam && TryBeginAttack(FireWormAttack.Slam, now))
                return;
            if (gap <= settings.fireballRange)
            {
                var kind = settings.healthPhases && HealthPhase >= 2 && volleyNext ? FireWormAttack.Volley : FireWormAttack.Fireball;
                if (TryBeginAttack(kind, now))
                {
                    volleyNext = !volleyNext;
                    return;
                }
            }
        }
        // No attack started: walk toward the player, but stop at the arena edges, ledges and walls.
        State = FireWormState.Chase;
        float desired = gap > combat.Definition.stoppingDistance ? facing * combat.Definition.moveSpeed : 0;
        if (arena != null)
        {
            float travel = desired * Time.fixedDeltaTime;
            if ((desired < 0 && bounds.min.x + travel <= arena.bounds.min.x + settings.edgePadding)
                || (desired > 0 && bounds.max.x + travel >= arena.bounds.max.x - settings.edgePadding))
                desired = 0;
        }
        if (desired != 0 && (!motor.HasFloorAhead(facing) || motor.IsBlocked(Vector2.right * facing)))
            desired = 0;
        motor.Move(new Vector2(desired, 0));
        if (desired == 0)
            body.linearVelocityX = 0;
    }

    // Turns the body (the tail sits on the side opposite the head) toward the player by moving the tail
    // and shifting the whole body. Returns false if the turn is blocked by another actor or terrain.
    private bool FacePlayer(PlayerHealth target, float now)
    {
        if (tailVisual == null || tailOffsetX <= 0.001f)
            return true;
        float delta = target.transform.position.x - BodyBounds.center.x;
        int desired = Mathf.Abs(delta) < 0.05f ? bodyFacing : delta < 0 ? -1 : 1;
        if (desired == bodyFacing)
            return true;

        Transform tail = tailVisual.transform;
        Vector3 local = tail.localPosition;
        float newTailX = -desired * tailOffsetX;
        float shift = transform.TransformVector(new Vector3(local.x - newTailX, 0, 0)).x;
        Bounds nextHead = headShape.bounds;
        nextHead.center += Vector3.right * shift;
        // The tail remains in place; only the head occupies a different part of the body footprint.
        // Defer the turn instead of moving a solid collider through another actor or terrain.
        turnObstacles.Clear();
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(~0);
        Vector2 size = (Vector2)nextHead.size - Vector2.one * 0.04f;
        size = new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
        Physics2D.OverlapBox(nextHead.center, size, 0, filter, turnObstacles);
        foreach (var obstacle in turnObstacles)
            if (!obstacle.transform.IsChildOf(transform))
                return false;

        local.x = newTailX;
        tail.localPosition = local;
        body.position += Vector2.right * shift;
        transform.position = new Vector3(body.position.x, body.position.y, transform.position.z);
        Physics2D.SyncTransforms();
        facing = bodyFacing = desired;
        // Keep an already-active ground impact in the place that was telegraphed.
        slamOffset.x -= shift;
        if (attack.CurrentSwing != null)
        {
            attack.CurrentSwing.Offset.x -= shift;
            attack.CurrentSwing.RefreshBox();
        }
        if (State == FireWormState.SlamWindup)
            PrepareSlam(now);
        else if (State == FireWormState.FireballWindup || State == FireWormState.VolleyWindup)
            lockedAim = combat.PredictPlayerPosition();
        return true;
    }

    // Places the slam hitbox in front of the head (past slamClearance) on the ground and shows its telegraph.
    private void PrepareSlam(float now)
    {
        Bounds bounds = BodyBounds;
        attackFacing = facing;
        phaseEndsAt = now + Mathf.Max(1, settings.slamWindupFrames) / CombatAttack.ReferenceFramesPerSecond;
        float x = facing < 0 ? bounds.min.x - settings.slamClearance - settings.slamSize.x * 0.5f
            : bounds.max.x + settings.slamClearance + settings.slamSize.x * 0.5f;
        slamOffset = new Vector2(x, bounds.min.y + settings.slamSize.y * 0.5f) - (Vector2)transform.position;
        attack.ShowTelegraph(slamOffset, settings.slamSize);
        attack.Feedback?.Windup(phaseEndsAt - now);
    }

    // Begins the windup for an attack if the boss is free (Idle or Chase) and the cooldown has passed.
    // Public so tests and debug tools can force an attack. Windup lengths are in frames at the reference frame rate.
    public bool TryBeginAttack(FireWormAttack kind, float now)
    {
        if (!EncounterActive || !isActiveAndEnabled || settings == null || combat.Definition == null || health.CurrentHealth <= 0 || combat.Target == null
            || combat.Target.CurrentHealth <= 0 || combat.IsStunned || Time.timeScale <= 0 || now < nextAttackAt
            || (State != FireWormState.Idle && State != FireWormState.Chase))
            return false;
        if (!FacePlayer(combat.Target, now) && kind == FireWormAttack.Slam)
            return false;
        if (kind == FireWormAttack.Volley && (!settings.healthPhases || HealthPhase < 2))
            return false;
        motor.Stop();
        body.linearVelocityX = 0;
        attackFacing = facing;
        int frames = kind == FireWormAttack.Volley ? settings.volleyWindupFrames
            : kind == FireWormAttack.Fireball ? settings.fireballWindupFrames : settings.slamWindupFrames;
        phaseEndsAt = now + Mathf.Max(1, frames) / CombatAttack.ReferenceFramesPerSecond;
        attack.Feedback?.Windup(phaseEndsAt - now);
        if (kind == FireWormAttack.Fireball || kind == FireWormAttack.Volley)
        {
            State = kind == FireWormAttack.Volley ? FireWormState.VolleyWindup : FireWormState.FireballWindup;
            lockedAim = combat.PredictPlayerPosition();
        }
        else
        {
            State = FireWormState.SlamWindup;
            PrepareSlam(now);
        }
        return true;
    }

    // Enters Recovery (an opening for the player) and schedules the next attack; later phases use shorter cooldowns.
    private void Recover(float now)
    {
        State = FireWormState.Recovery;
        phaseEndsAt = now + Mathf.Max(0, settings.recoverySeconds);
        float multiplier = !settings.healthPhases ? 1 : HealthPhase >= 3 ? settings.phaseThreeCooldownMultiplier
            : HealthPhase == 2 ? settings.phaseTwoCooldownMultiplier : 1;
        nextAttackAt = now + Mathf.Max(0, settings.attackCooldownSeconds * multiplier);
        attack.CancelAll();
        slamVisual.gameObject.SetActive(false);
    }

    // Fires one fireball from just in front of the head toward the aim point locked at windup start,
    // rotated by `angle` degrees (used to fan out volleys).
    private void LaunchFireball(float angle = 0)
    {
        var shot = GetProjectile();
        if (shot == null)
            return;
        Bounds head = headShape.bounds;
        Vector2 origin = new Vector2(
            head.center.x + facing * (head.extents.x + settings.fireballSize.x * 0.5f + 0.05f),
            head.center.y + 0.3f);
        Vector2 heading = lockedAim - origin;
        if (heading.sqrMagnitude < 0.001f)
            heading = Vector2.right * facing;
        heading = Quaternion.Euler(0, 0, angle) * heading.normalized;
        shot.Launch(origin, heading);
        FireballsLaunched++;
    }

    // Sends a low shockwave along the floor from the left (-1) or right (1) edge of the body.
    private void LaunchGroundWave(int side)
    {
        var shot = GetProjectile();
        if (shot == null)
            return;
        Bounds bounds = BodyBounds;
        float x = side < 0 ? bounds.min.x - settings.shockwaveSize.x * 0.5f : bounds.max.x + settings.shockwaveSize.x * 0.5f;
        shot.Launch(new Vector2(x, bounds.min.y + settings.shockwaveSize.y * 0.5f + 0.03f), Vector2.right * side, true);
        ShockwavesLaunched++;
    }

    // Pooled projectiles, capped at settings.maximumFireballs; returns null when the cap is reached.
    private BossFireball GetProjectile()
    {
        if (pool.Count == 0 && projectiles.Count >= Mathf.Max(1, settings.maximumFireballs))
            return null;
        if (projectileRoot == null)
        {
            projectileRoot = new GameObject("Boss Fireballs").transform;
            SceneManager.MoveGameObjectToScene(projectileRoot.gameObject, gameObject.scene);
        }
        BossFireball shot;
        if (pool.Count > 0)
            shot = pool.Pop();
        else
        {
            shot = new GameObject("Boss Fireball").AddComponent<BossFireball>();
            shot.transform.SetParent(projectileRoot, false);
            shot.Configure(this, headVisual.sprite, headVisual.sortingLayerID, headVisual.sortingOrder + 2, Recycle);
            projectiles.Add(shot);
        }
        return shot;
    }

    private void Recycle(BossFireball shot) => pool.Push(shot);

    public void CancelAttacks(bool clearFireballs = false)
    {
        State = FireWormState.Idle;
        attack.CancelAll();
        motor.Stop();
        if (slamVisual != null)
            slamVisual.gameObject.SetActive(false);
        if (clearFireballs)
            foreach (var shot in projectiles)
                if (shot != null && shot.gameObject.activeSelf)
                    shot.Despawn();
    }

    // Shows the slam telegraph / impact box (when no presentation art draws it) and tints the tail:
    // white on hit, magenta before a volley, yellow before a fireball or slam.
    private void LateUpdate()
    {
        bool visible = State == FireWormState.SlamWindup || (State == FireWormState.SlamActive
            && (presentation == null || presentation.presentation == null));
        slamVisual.gameObject.SetActive(visible);
        if (visible)
        {
            slamVisual.transform.position = (Vector2)transform.position + slamOffset;
            slamVisual.transform.localScale = new Vector3(settings.slamSize.x, settings.slamSize.y, 1);
            slamVisual.color = State == FireWormState.SlamWindup ? settings.warningColor : settings.slamColor;
        }
        if (tailVisual != null)
            tailVisual.color = Time.time < tailFlashUntil ? Color.white
                : State == FireWormState.VolleyWindup ? Color.Lerp(tailColor, Color.magenta, 0.7f)
                : State == FireWormState.FireballWindup || State == FireWormState.SlamWindup
                ? Color.Lerp(tailColor, Color.yellow, 0.6f) : tailColor;
    }

    private void OnDisable()
    {
        if (health != null)
            health.HealthChanged -= OnHealthChanged;
        if (attack != null && motor != null)
            CancelAttacks(true);
    }

    private void OnDestroy()
    {
        if (projectileRoot != null)
            Destroy(projectileRoot.gameObject);
    }
}
