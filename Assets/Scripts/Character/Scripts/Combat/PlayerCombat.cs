using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController), typeof(CombatAttack), typeof(PlayerParry))]
public class PlayerCombat : MonoBehaviour
{
    [Min(0), SerializeField] private int attackDamage = 1;
    [Tooltip("Downward attacks can pogo Pogoable objects on any layer.")]
    [SerializeField] private LayerMask enemyLayers = ~0;
    private PlayerController playerController;
    private PlayerHealth health;
    private PlayerActions controls;
    private CombatAttack attack;
    private PlayerParry parry;
    private float nextAttackAt;
    private float bufferedUntil = float.NegativeInfinity;
    private CombatDirection bufferedDirection;
    public CombatAttack Attack => attack;
    public float RecoveryRemaining => Mathf.Max(0, nextAttackAt - Time.time);
    public bool HasBufferedAttack => Time.time <= bufferedUntil;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        health = GetComponent<PlayerHealth>();
        attack = GetComponent<CombatAttack>();
        parry = GetComponent<PlayerParry>();
        controls = new PlayerActions();
    }
    private void OnEnable() => controls.Enable();
    private void OnDisable()
    {
        controls.Disable();
        Cancel();
    }
    private void OnDestroy() => controls.Dispose();
    private void Update()
    {
        if (controls.PlayerInput.Attack.WasPressedThisFrame()) QueueAttack(ReadDirection());
        Tick(Time.time);
    }
    public void QueueAttack(CombatDirection direction)
    {
        bufferedDirection = direction;
        bufferedUntil = Time.time + Mathf.Max(0, attack.Settings.inputBufferSeconds);
    }
    public void Tick(float now)
    {
        if (now <= bufferedUntil && TryAttack(bufferedDirection, now))
            bufferedUntil = float.NegativeInfinity;
    }
    public bool TryAttack() => TryAttack(ReadDirection(), Time.time);
    private bool TryAttack(CombatDirection direction, float now)
    {
        if (!isActiveAndEnabled || (health != null && health.CurrentHealth <= 0)
            || now < nextAttackAt || (attack.CurrentSwing != null && attack.CurrentSwing.IsActive(now))
            || parry.PhaseAt(now) != ParryPhase.Idle) return false;
        if (direction == CombatDirection.Down && playerController.IsGrounded) return false;
        GetAttackGeometry(direction, out Vector2 offset, out Vector2 size);
        var swing = attack.BeginAttack(direction, offset, size, attackDamage,
            attack.Settings.playerActiveFrames, enemyLayers, now);
        if (swing == null) return false;
        nextAttackAt = swing.EndsAt + Mathf.Max(0, attack.Settings.playerRecoveryFrames) / CombatAttack.ReferenceFramesPerSecond;
        return true;
    }
    private CombatDirection ReadDirection()
    {
        Vector2 movement = controls.PlayerInput.Move.ReadValue<Vector2>();
        return movement.y < -0.5f && !playerController.IsGrounded ? CombatDirection.Down
            : movement.y > 0.5f ? CombatDirection.Up
            : playerController.IsFacingRight ? CombatDirection.Right : CombatDirection.Left;
    }
    public void Cancel()
    {
        bufferedUntil = float.NegativeInfinity;
        nextAttackAt = 0;
        attack?.CancelAll();
    }
    public void GetAttackGeometry(CombatDirection direction, out Vector2 offset, out Vector2 size)
    {
        var source = attack != null ? attack : GetComponent<CombatAttack>();
        size = source.Settings.playerAttackSize;
        size = new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
        offset = source.GetAttackOffset(direction, size);
    }
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying && !AscendJigoku.Debugging.DebugMenu.IsOpen) return;
        foreach (CombatDirection direction in System.Enum.GetValues(typeof(CombatDirection)))
        {
            GetAttackGeometry(direction, out Vector2 offset, out Vector2 size);
            Gizmos.color = direction == CombatDirection.Down ? Color.blue : Color.cyan;
            Gizmos.DrawWireCube((Vector2)transform.position + offset, size);
        }
    }
}
