using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum FireWormState { Idle, Chase, FireballWindup, SlamWindup, SlamActive, Recovery, Stunned }
public enum FireWormAttack { Fireball, Slam }

// Reuses EnemyHealth, EnemyMotor, EnemyContactDamage and the combat resolver.
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
    private Transform projectileRoot;
    private readonly List<BossFireball> projectiles = new List<BossFireball>();
    private readonly Stack<BossFireball> pool = new Stack<BossFireball>();
    private float phaseEndsAt, nextAttackAt, nextTargetSearch, tailFlashUntil;
    private Vector2 slamOffset, lockedAim;
    private Color tailColor;
    private int facing = -1, bodyFacing = 1, attackFacing = -1, previousHealth;
    public FireWormState State { get; private set; }
    public FireWormBossSettings Settings => settings;
    public Collider2D Arena => arena;
    public EnemyCombat Combat => combat;
    public bool IsFacingRight => bodyFacing > 0;
    public int FireballsLaunched { get; private set; }
    public int SlamsPerformed { get; private set; }
    public Bounds BodyBounds
    {
        get
        {
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            bool first = true;
            foreach (var shape in shapes)
                if (shape != null && shape.enabled)
                { if (first) { bounds = shape.bounds; first = false; } else bounds.Encapsulate(shape.bounds); }
            return bounds;
        }
    }
    private void Awake()
    {
        combat = GetComponent<EnemyCombat>(); motor = GetComponent<EnemyMotor>();
        health = GetComponent<EnemyHealth>(); attack = GetComponent<CombatAttack>(); body = GetComponent<Rigidbody2D>();
        headVisual = GetComponent<SpriteRenderer>();
        headShape = GetComponent<Collider2D>();
        if (tailVisual != null)
        {
            tailOffsetX = Mathf.Abs(tailVisual.transform.localPosition.x);
            bodyFacing = tailVisual.transform.localPosition.x > 0 ? -1 : 1;
        }
        facing = bodyFacing;
        var all = GetComponentsInChildren<Collider2D>();
        var solids = new List<Collider2D>();
        foreach (var shape in all) if (!shape.isTrigger && shape.attachedRigidbody == body) solids.Add(shape);
        shapes = solids.ToArray();
        previousHealth = health.CurrentHealth;
        if (tailVisual != null) tailColor = tailVisual.color;
        var visual = new GameObject("Slam Warning and Impact"); visual.transform.SetParent(transform, false);
        slamVisual = visual.AddComponent<SpriteRenderer>(); slamVisual.sprite = headVisual.sprite;
        slamVisual.sortingLayerID = headVisual.sortingLayerID; slamVisual.sortingOrder = headVisual.sortingOrder + 1;
        visual.SetActive(false);
    }
    private void OnEnable() => health.HealthChanged += OnHealthChanged;
    private void Start()
    {
        // This component selects the two boss attacks; EnemyCombat still supplies targeting and parry stun.
        combat.enabled = false;
        combat.SetTarget(FindAnyObjectByType<PlayerHealth>());
        previousHealth = health.CurrentHealth;
    }
    private void OnHealthChanged(int current, int maximum)
    {
        if (current < previousHealth) tailFlashUntil = Time.time + 0.1f;
        previousHealth = current;
    }
    private void FixedUpdate() => Tick(Time.time);
    public void Tick(float now)
    {
        if (settings == null || combat.Definition == null || health.CurrentHealth <= 0 || Time.timeScale <= 0) { motor.Stop(); return; }
        if (combat.Target == null && now >= nextTargetSearch)
        { combat.SetTarget(FindAnyObjectByType<PlayerHealth>()); nextTargetSearch = now + 0.5f; }
        var target = combat.Target;
        if (target == null || !target.isActiveAndEnabled || target.CurrentHealth <= 0)
        { CancelAttacks(true); return; }
        bool canFacePlayer = FacePlayer(target, now);
        if (combat.IsStunned)
        {
            motor.Stop(); body.linearVelocityX = 0; attack.CancelAll(); slamVisual.gameObject.SetActive(false);
            State = FireWormState.Stunned;
            nextAttackAt = Mathf.Max(nextAttackAt, combat.StunnedUntil + settings.recoverySeconds);
            return;
        }
        if (State == FireWormState.Stunned) State = FireWormState.Idle;
        if (State == FireWormState.FireballWindup || State == FireWormState.SlamWindup)
        {
            motor.Stop();
            if (now < phaseEndsAt) return;
            attack.Feedback?.CancelWindup();
            if (State == FireWormState.FireballWindup) { LaunchFireball(); Recover(now); }
            else
            {
                attack.BeginAttack(attackFacing < 0 ? CombatDirection.Left : CombatDirection.Right, slamOffset,
                    settings.slamSize, settings.slamDamage, settings.slamActiveFrames, ~0, now);
                State = FireWormState.SlamActive; SlamsPerformed++;
                phaseEndsAt = now + Mathf.Max(1, settings.slamActiveFrames) / CombatAttack.ReferenceFramesPerSecond;
            }
            return;
        }
        if (State == FireWormState.SlamActive)
        {
            motor.Stop();
            if (attack.CurrentSwing == null || attack.CurrentSwing.Cancelled || now >= phaseEndsAt) Recover(now);
            return;
        }
        if (State == FireWormState.Recovery)
        { motor.Stop(); if (now < phaseEndsAt) return; State = FireWormState.Idle; }
        Bounds bounds = BodyBounds;
        var targetShape = target.GetComponent<Collider2D>();
        Bounds player = targetShape != null ? targetShape.bounds : new Bounds(target.transform.position, Vector3.one);
        if ((player.center - bounds.center).sqrMagnitude > combat.Definition.aggroRadius * combat.Definition.aggroRadius
            || (arena != null && (player.center.x < arena.bounds.min.x || player.center.x > arena.bounds.max.x)))
        { State = FireWormState.Idle; motor.Stop(); return; }
        if (!canFacePlayer) { State = FireWormState.Idle; motor.Stop(); body.linearVelocityX = 0; return; }
        float gap = facing < 0 ? bounds.min.x - player.max.x : player.min.x - bounds.max.x;
        bool lowEnoughForSlam = player.center.y <= bounds.min.y + settings.slamSize.y + 0.5f;
        if (settings.automaticAttacks && now >= nextAttackAt && motor.IsGrounded && combat.HasLineOfSight())
        {
            if (gap <= settings.slamRange && lowEnoughForSlam && TryBeginAttack(FireWormAttack.Slam, now)) return;
            if (gap <= settings.fireballRange && TryBeginAttack(FireWormAttack.Fireball, now)) return;
        }
        State = FireWormState.Chase;
        float desired = gap > combat.Definition.stoppingDistance ? facing * combat.Definition.moveSpeed : 0;
        if (arena != null)
        {
            float travel = desired * Time.fixedDeltaTime;
            if ((desired < 0 && bounds.min.x + travel <= arena.bounds.min.x + settings.edgePadding)
                || (desired > 0 && bounds.max.x + travel >= arena.bounds.max.x - settings.edgePadding)) desired = 0;
        }
        if (desired != 0 && (!motor.HasFloorAhead(facing) || motor.IsBlocked(Vector2.right * facing))) desired = 0;
        motor.Move(new Vector2(desired, 0));
        if (desired == 0) body.linearVelocityX = 0;
    }
    private bool FacePlayer(PlayerHealth target, float now)
    {
        if (tailVisual == null || tailOffsetX <= 0.001f) return true;
        float delta = target.transform.position.x - BodyBounds.center.x;
        int desired = Mathf.Abs(delta) < 0.05f ? bodyFacing : delta < 0 ? -1 : 1;
        if (desired == bodyFacing) return true;

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
            if (!obstacle.transform.IsChildOf(transform)) return false;

        local.x = newTailX; tail.localPosition = local;
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
        if (State == FireWormState.SlamWindup) PrepareSlam(now);
        else if (State == FireWormState.FireballWindup) lockedAim = combat.PredictPlayerPosition();
        return true;
    }
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
    public bool TryBeginAttack(FireWormAttack kind, float now)
    {
        if (!isActiveAndEnabled || settings == null || combat.Definition == null || health.CurrentHealth <= 0 || combat.Target == null
            || combat.Target.CurrentHealth <= 0 || combat.IsStunned || Time.timeScale <= 0 || now < nextAttackAt
            || (State != FireWormState.Idle && State != FireWormState.Chase)) return false;
        if (!FacePlayer(combat.Target, now)) return false;
        motor.Stop(); body.linearVelocityX = 0;
        attackFacing = facing;
        int frames = kind == FireWormAttack.Fireball ? settings.fireballWindupFrames : settings.slamWindupFrames;
        phaseEndsAt = now + Mathf.Max(1, frames) / CombatAttack.ReferenceFramesPerSecond;
        attack.Feedback?.Windup(phaseEndsAt - now);
        if (kind == FireWormAttack.Fireball)
        { State = FireWormState.FireballWindup; lockedAim = combat.PredictPlayerPosition(); }
        else
        {
            State = FireWormState.SlamWindup;
            PrepareSlam(now);
        }
        return true;
    }
    private void Recover(float now)
    {
        State = FireWormState.Recovery;
        phaseEndsAt = now + Mathf.Max(0, settings.recoverySeconds);
        nextAttackAt = now + Mathf.Max(0, settings.attackCooldownSeconds);
        attack.CancelAll(); slamVisual.gameObject.SetActive(false);
    }
    private void LaunchFireball()
    {
        if (pool.Count == 0 && projectiles.Count >= Mathf.Max(1, settings.maximumFireballs)) return;
        if (projectileRoot == null)
        {
            projectileRoot = new GameObject("Boss Fireballs").transform;
            SceneManager.MoveGameObjectToScene(projectileRoot.gameObject, gameObject.scene);
        }
        BossFireball shot;
        if (pool.Count > 0) shot = pool.Pop();
        else
        {
            shot = new GameObject("Boss Fireball").AddComponent<BossFireball>();
            shot.transform.SetParent(projectileRoot, false);
            shot.Configure(this, headVisual.sprite, headVisual.sortingLayerID, headVisual.sortingOrder + 2, Recycle);
            projectiles.Add(shot);
        }
        Bounds head = headShape.bounds;
        Vector2 origin = new Vector2(head.center.x + facing * (head.extents.x + settings.fireballSize.x * 0.5f + 0.05f),
            head.center.y + 0.3f);
        Vector2 heading = lockedAim - origin;
        if (heading.sqrMagnitude < 0.001f) heading = Vector2.right * facing;
        shot.Launch(origin, heading.normalized); FireballsLaunched++;
    }
    private void Recycle(BossFireball shot) => pool.Push(shot);
    public void CancelAttacks(bool clearFireballs = false)
    {
        State = FireWormState.Idle; attack.CancelAll(); motor.Stop();
        if (slamVisual != null) slamVisual.gameObject.SetActive(false);
        if (clearFireballs)
            foreach (var shot in projectiles) if (shot != null && shot.gameObject.activeSelf) shot.Despawn();
    }
    private void LateUpdate()
    {
        bool visible = State == FireWormState.SlamWindup || State == FireWormState.SlamActive;
        slamVisual.gameObject.SetActive(visible);
        if (visible)
        {
            slamVisual.transform.position = (Vector2)transform.position + slamOffset;
            slamVisual.transform.localScale = new Vector3(settings.slamSize.x, settings.slamSize.y, 1);
            slamVisual.color = State == FireWormState.SlamWindup ? settings.warningColor : settings.slamColor;
        }
        if (tailVisual != null)
            tailVisual.color = Time.time < tailFlashUntil ? Color.white
                : State == FireWormState.FireballWindup || State == FireWormState.SlamWindup
                ? Color.Lerp(tailColor, Color.yellow, 0.6f) : tailColor;
    }
    private void OnDisable()
    {
        if (health != null) health.HealthChanged -= OnHealthChanged;
        if (attack != null && motor != null) CancelAttacks(true);
    }
    private void OnDestroy() { if (projectileRoot != null) Destroy(projectileRoot.gameObject); }
}
