using UnityEngine;

[RequireComponent(typeof(EnemyHealth), typeof(Pogoable), typeof(CombatAttack))]
[DisallowMultipleComponent]
public class EnemyCombat : MonoBehaviour
{
    [Tooltip("Optional. Leave empty to keep the original stationary enemy settings.")]
    [SerializeField] private EnemyDefinition definition;
    [Header("Player Detection")]
    [SerializeField] private PlayerHealth targetPlayer;
    [Min(0.1f), SerializeField] private float detectionRadius = 3.5f;
    [Min(1), SerializeField] private int detectionIntervalFrames = 2;
    [Min(0), SerializeField] private int predictionFrames = 8;
    [Min(0), SerializeField] private float maximumPredictionDistance = 2f;
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask obstacleLayers = 1 << 3;
    [Min(0), SerializeField] private float aboveThreshold = 0.6f;
    [Min(0), SerializeField] private int attackDamage = 1;
    [SerializeField] private LayerMask playerLayers = ~0;
    private CombatAttack attack;
    private EnemyBrain brain;
    private SpriteRenderer sprite;
    private Rigidbody2D targetBody;
    private Collider2D targetCollider;
    private PlayerController targetController;
    private EnemyHealth health;
    private float nextDetectionAt, nextAttackAt, windupEndsAt;
    private bool windingUp;
    private float stunnedUntil = float.NegativeInfinity;
    private CombatDirection direction;
    public EnemyDefinition Definition => definition;
    public PlayerHealth Target => targetPlayer;
    public CombatAttack Attack => attack;
    public bool IsWindingUp => windingUp;
    public float NextAttackAt => nextAttackAt;
    public float WindupRemaining => windingUp ? Mathf.Max(0, windupEndsAt - Time.time) : 0;
    public bool IsStunned => Time.time < stunnedUntil;
    public float StunnedUntil => stunnedUntil;
    public void Stun(float now, float duration)
    {
        stunnedUntil = Mathf.Max(stunnedUntil, now + Mathf.Max(0, duration));
        nextAttackAt = Mathf.Max(nextAttackAt, stunnedUntil);
        windingUp = false;
        attack.CancelTelegraph();
        attack.Feedback?.CancelWindup();
        if (attack.CurrentSwing != null) attack.Cancel(attack.CurrentSwing);
    }

    private void Awake()
    {
        attack = GetComponent<CombatAttack>();
        brain = GetComponent<EnemyBrain>();
        sprite = GetComponent<SpriteRenderer>();
        attack.SetTeam(CombatTeam.Enemy);
        health = GetComponent<EnemyHealth>();
    }
    private void Start()
    {
        if (targetPlayer == null) targetPlayer = FindAnyObjectByType<PlayerHealth>();
        SetTarget(targetPlayer);
    }
    public void SetTarget(PlayerHealth player)
    {
        targetPlayer = player;
        targetBody = player != null ? player.GetComponent<Rigidbody2D>() : null;
        targetCollider = player != null ? player.GetComponent<Collider2D>() : null;
        targetController = player != null ? player.GetComponent<PlayerController>() : null;
    }
    private void Update() => Tick(Time.time);
    public void Tick(float now)
    {
        if (health.CurrentHealth <= 0 || targetPlayer == null || !targetPlayer.isActiveAndEnabled || targetPlayer.CurrentHealth <= 0)
        { CancelIntent(); return; }
        if (now < stunnedUntil || Time.timeScale <= 0) return;
        if (brain != null && brain.enabled && !brain.CanAttack) return;
        if (windingUp)
        {
            if (now < windupEndsAt) return;
            windingUp = false;
            attack.Feedback?.CancelWindup();
            GetAttackGeometry(direction, out Vector2 offset, out Vector2 size);
            var swing = attack.BeginAttack(direction, offset, size, definition != null ? definition.damage : attackDamage,
                definition != null ? definition.activeFrames : attack.Settings.enemyActiveFrames, playerLayers, now);
            if (swing != null) nextAttackAt = swing.EndsAt + (definition != null ? definition.recoverySeconds : attack.Settings.enemyCooldownSeconds);
            return;
        }
        if ((attack.CurrentSwing != null && attack.CurrentSwing.IsActive(now))
            || now < nextAttackAt || now < nextDetectionAt) return;
        nextDetectionAt = now + detectionIntervalFrames / CombatAttack.ReferenceFramesPerSecond;
        if (!DetectPlayer(out direction)) return;
        GetAttackGeometry(direction, out Vector2 windupOffset, out Vector2 windupSize);
        windingUp = true;
        windupEndsAt = now + (definition != null ? definition.windupFrames : attack.Settings.enemyWindupFrames) / CombatAttack.ReferenceFramesPerSecond;
        if (sprite != null && (direction == CombatDirection.Left || direction == CombatDirection.Right))
            sprite.flipX = direction == CombatDirection.Left;
        attack.Feedback?.Windup(windupEndsAt - now);
        attack.ShowTelegraph(windupOffset, windupSize);
    }
    public Vector2 PredictPlayerPosition()
    {
        Vector2 position = targetPlayer.transform.position;
        if (targetBody == null) return position;
        float time = (definition != null ? definition.predictionFrames : predictionFrames) / CombatAttack.ReferenceFramesPerSecond;
        Vector2 displacement = targetBody.linearVelocity * time;
        if (targetController == null || !targetController.IsGrounded)
            displacement += 0.5f * Physics2D.gravity * targetBody.gravityScale * time * time;
        return position + Vector2.ClampMagnitude(displacement, definition != null ? definition.maximumPredictionDistance : maximumPredictionDistance);
    }
    public bool DetectPlayer(out CombatDirection attackDirection)
    {
        attackDirection = CombatDirection.Right;
        if (targetPlayer == null || !targetPlayer.isActiveAndEnabled) return false;
        Vector2 position = transform.position;
        Vector2 current = targetPlayer.transform.position;
        Vector2 predicted = PredictPlayerPosition();
        float radius = definition != null ? definition.aggroRadius : detectionRadius;
        float radiusSquared = radius * radius;
        if ((current - position).sqrMagnitude > radiusSquared && (predicted - position).sqrMagnitude > radiusSquared) return false;
        if (requireLineOfSight && !HasLineOfSight()) return false;
        Vector2 delta = predicted - position;
        if (delta.y < -aboveThreshold && -delta.y > Mathf.Abs(delta.x) && (definition == null || !definition.allowDownAttack)) return false;
        attackDirection = delta.y > aboveThreshold && delta.y > Mathf.Abs(delta.x) ? CombatDirection.Up
            : delta.y < -aboveThreshold && -delta.y > Mathf.Abs(delta.x) && definition != null && definition.allowDownAttack
            ? CombatDirection.Down : delta.x < 0 ? CombatDirection.Left : CombatDirection.Right;
        GetAttackGeometry(attackDirection, out Vector2 offset, out Vector2 size);
        Rect box = attack.GetBox(offset, size);
        Vector2 targetSize = targetCollider != null ? (Vector2)targetCollider.bounds.size : Vector2.one;
        return box.Overlaps(new Rect(predicted - targetSize * 0.5f, targetSize));
    }
    public void GetAttackGeometry(CombatDirection attackDirection, out Vector2 offset, out Vector2 size)
    {
        var source = attack != null ? attack : GetComponent<CombatAttack>();
        size = definition != null ? definition.attackSize : source.Settings.enemyAttackSize;
        size = new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
        offset = source.GetAttackOffset(attackDirection, size);
    }
    private readonly System.Collections.Generic.List<RaycastHit2D> sightHits = new System.Collections.Generic.List<RaycastHit2D>();
    public bool HasLineOfSight()
    {
        if (targetPlayer == null) return false;
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(definition != null ? definition.terrainLayers : obstacleLayers);
        sightHits.Clear();
        Physics2D.Linecast(transform.position, targetPlayer.transform.position, filter, sightHits);
        foreach (var hit in sightHits)
            if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(targetPlayer.transform)) return false;
        return true;
    }
    public void CancelIntent()
    {
        windingUp = false;
        attack?.CancelAll();
    }
    private void OnDisable() => CancelIntent();
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying && !AscendJigoku.Debugging.DebugMenu.IsOpen) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, definition != null ? definition.aggroRadius : detectionRadius);
        foreach (var direction in new[] { CombatDirection.Left, CombatDirection.Right, CombatDirection.Up, CombatDirection.Down })
        {
            GetAttackGeometry(direction, out Vector2 offset, out Vector2 size);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube((Vector2)transform.position + offset, size);
        }
    }
}
