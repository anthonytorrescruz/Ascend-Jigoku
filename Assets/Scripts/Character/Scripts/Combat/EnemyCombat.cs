using UnityEngine;

[RequireComponent(typeof(EnemyHealth), typeof(Pogoable), typeof(CombatAttack))]
[DisallowMultipleComponent]
public class EnemyCombat : MonoBehaviour
{
    [Header("Player Detection")]
    [SerializeField] private PlayerHealth targetPlayer;
    [Min(0.1f), SerializeField] private float detectionRadius = 3.5f;
    [Min(1), SerializeField] private int detectionIntervalFrames = 2;
    [Min(0), SerializeField] private int predictionFrames = 8;
    [Min(0), SerializeField] private float maximumPredictionDistance = 2f;
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask obstacleLayers = 1 << 3;
    [Min(0), SerializeField] private float aboveThreshold = 0.6f;
    [Header("Attack Timing - Frames at 60 FPS")]
    [Min(0), SerializeField] private int attackWindupFrames = 8;
    [Min(1), SerializeField] private int attackActiveFrames = 6;
    [Tooltip("Seconds after the attack finishes before another windup can start.")]
    [Min(0), SerializeField] private float attackCooldownSeconds = 1f;
    [Min(0), SerializeField] private int attackDamage = 1;
    [Header("Side Attack")]
    [SerializeField] private Vector2 sideAttackSize = new Vector2(1.6f, 1.25f);
    [SerializeField] private Vector2 sideAttackOffset = new Vector2(0.9f, 0f);
    [Header("Up Attack")]
    [SerializeField] private Vector2 upAttackSize = new Vector2(1.5f, 1.7f);
    [SerializeField] private Vector2 upAttackOffset = new Vector2(0f, 1f);
    [SerializeField] private LayerMask playerLayers = ~0;
    private CombatAttack attack;
    private Rigidbody2D targetBody;
    private Collider2D targetCollider;
    private PlayerController targetController;
    private EnemyHealth health;
    private float nextDetectionAt, nextAttackAt, windupEndsAt;
    private bool windingUp;
    private float stunnedUntil = float.NegativeInfinity;
    private CombatDirection direction;
    public bool IsWindingUp => windingUp;
    public float NextAttackAt => nextAttackAt;
    public bool IsStunned => Time.time < stunnedUntil;
    public float StunnedUntil => stunnedUntil;
    public void Stun(float now, float duration)
    {
        stunnedUntil = Mathf.Max(stunnedUntil, now + Mathf.Max(0, duration));
        nextAttackAt = Mathf.Max(nextAttackAt, stunnedUntil);
        windingUp = false;
        attack.CancelTelegraph();
        if (attack.CurrentSwing != null) attack.Cancel(attack.CurrentSwing);
    }

    private void Awake()
    {
        attack = GetComponent<CombatAttack>();
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
        if (now < stunnedUntil || health.CurrentHealth <= 0 || targetPlayer == null || targetPlayer.CurrentHealth <= 0) return;
        if (windingUp)
        {
            if (now < windupEndsAt) return;
            windingUp = false;
            GetAttackGeometry(direction, out Vector2 offset, out Vector2 size);
            var swing = attack.BeginAttack(direction, offset, size, attackDamage, attackActiveFrames, playerLayers, now);
            if (swing != null) nextAttackAt = swing.EndsAt + attackCooldownSeconds;
            return;
        }
        if ((attack.CurrentSwing != null && attack.CurrentSwing.IsActive(now))
            || now < nextAttackAt || now < nextDetectionAt) return;
        nextDetectionAt = now + detectionIntervalFrames / CombatAttack.ReferenceFramesPerSecond;
        if (!DetectPlayer(out direction)) return;
        GetAttackGeometry(direction, out Vector2 windupOffset, out Vector2 windupSize);
        windingUp = true;
        windupEndsAt = now + attackWindupFrames / CombatAttack.ReferenceFramesPerSecond;
        attack.ShowTelegraph(windupOffset, windupSize);
    }
    public Vector2 PredictPlayerPosition()
    {
        Vector2 position = targetPlayer.transform.position;
        if (targetBody == null) return position;
        float time = predictionFrames / CombatAttack.ReferenceFramesPerSecond;
        Vector2 displacement = targetBody.linearVelocity * time;
        if (targetController == null || !targetController.IsGrounded)
            displacement += 0.5f * Physics2D.gravity * targetBody.gravityScale * time * time;
        return position + Vector2.ClampMagnitude(displacement, maximumPredictionDistance);
    }
    public bool DetectPlayer(out CombatDirection attackDirection)
    {
        attackDirection = CombatDirection.Right;
        if (targetPlayer == null || !targetPlayer.isActiveAndEnabled) return false;
        Vector2 position = transform.position;
        Vector2 current = targetPlayer.transform.position;
        Vector2 predicted = PredictPlayerPosition();
        float radiusSquared = detectionRadius * detectionRadius;
        if ((current - position).sqrMagnitude > radiusSquared && (predicted - position).sqrMagnitude > radiusSquared) return false;
        if (requireLineOfSight && Physics2D.Linecast(position, current, obstacleLayers).collider != null) return false;
        Vector2 delta = predicted - position;
        if (delta.y < -aboveThreshold && -delta.y > Mathf.Abs(delta.x)) return false;
        attackDirection = delta.y > aboveThreshold && delta.y > Mathf.Abs(delta.x)
            ? CombatDirection.Up : delta.x < 0 ? CombatDirection.Left : CombatDirection.Right;
        GetAttackGeometry(attackDirection, out Vector2 offset, out Vector2 size);
        Rect box = attack.GetBox(offset, size);
        Vector2 targetSize = targetCollider != null ? (Vector2)targetCollider.bounds.size : Vector2.one;
        return box.Overlaps(new Rect(predicted - targetSize * 0.5f, targetSize));
    }
    public void GetAttackGeometry(CombatDirection attackDirection, out Vector2 offset, out Vector2 size)
    {
        if (attackDirection == CombatDirection.Up) { offset = upAttackOffset; size = upAttackSize; }
        else
        {
            offset = new Vector2(Mathf.Abs(sideAttackOffset.x) * (attackDirection == CombatDirection.Left ? -1 : 1), sideAttackOffset.y);
            size = sideAttackSize;
        }
    }
    private void OnDisable()
    {
        windingUp = false;
        if (attack != null) attack.CancelTelegraph();
    }
    private void OnDrawGizmosSelected()
    {
        Vector2 position = transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(position + sideAttackOffset, sideAttackSize);
        Gizmos.DrawWireCube(position + new Vector2(-Mathf.Abs(sideAttackOffset.x), sideAttackOffset.y), sideAttackSize);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(position + upAttackOffset, upAttackSize);
    }
}
