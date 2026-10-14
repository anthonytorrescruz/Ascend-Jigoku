using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController), typeof(CombatAttack), typeof(PlayerParry))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Attack Damage")]
    [Min(0), SerializeField] private int attackDamage = 1;
    [Header("Attack Timing - Frames at 60 FPS")]
    [Min(1), SerializeField] private int attackActiveFrames = 4;
    [Min(0), SerializeField] private int attackCooldownFrames = 15;
    [Header("Side Attack")]
    [SerializeField] private Vector2 sideAttackSize = new Vector2(1.4f, 1f);
    [SerializeField] private Vector2 sideAttackOffset = new Vector2(0.9f, 0f);
    [Header("Up Attack - Ground or Air")]
    [SerializeField] private Vector2 upAttackSize = new Vector2(1.5f, 1.3f);
    [SerializeField] private Vector2 upAttackOffset = new Vector2(0f, 1f);
    [Header("Down Attack - Air Only")]
    [SerializeField] private Vector2 downAttackSize = new Vector2(1f, 1.2f);
    [SerializeField] private Vector2 downAttackOffset = new Vector2(0f, -0.9f);
    [Header("Damage Collision")]
    [Tooltip("Damage layers; downward attacks can pogo Pogoable objects on any layer.")]
    [SerializeField] private LayerMask enemyLayers = ~0;
    private PlayerController playerController;
    private PlayerActions controls;
    private CombatAttack attack;
    private PlayerParry parry;
    private float nextAttackAt;
    public CombatAttack Attack => attack;
    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        attack = GetComponent<CombatAttack>();
        parry = GetComponent<PlayerParry>();
        controls = new PlayerActions();
    }
    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();
    private void OnDestroy() => controls.Dispose();
    private void Update()
    {
        if (controls.PlayerInput.Attack.WasPressedThisFrame()) TryAttack();
    }
    public bool TryAttack()
    {
        float now = Time.time;
        if (now < nextAttackAt || attack.IsAttacking || parry.IsBusy) return false;
        Vector2 movement = controls.PlayerInput.Move.ReadValue<Vector2>();
        Keyboard keyboard = Keyboard.current;
        bool down = movement.y < -0.5f || (keyboard != null
            && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed));
        bool up = movement.y > 0.5f || (keyboard != null
            && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed));
        CombatDirection direction = down && !playerController.IsGrounded ? CombatDirection.Down
            : up ? CombatDirection.Up
            : playerController.IsFacingRight ? CombatDirection.Right : CombatDirection.Left;
        GetAttackGeometry(direction, out Vector2 offset, out Vector2 size);
        attack.BeginAttack(direction, offset, size, attackDamage, attackActiveFrames, enemyLayers, now);
        nextAttackAt = now + attackCooldownFrames / CombatAttack.ReferenceFramesPerSecond;
        return true;
    }
    public void GetAttackGeometry(CombatDirection direction, out Vector2 offset, out Vector2 size)
    {
        switch (direction)
        {
            case CombatDirection.Up: offset = upAttackOffset; size = upAttackSize; break;
            case CombatDirection.Down: offset = downAttackOffset; size = downAttackSize; break;
            default:
                offset = new Vector2(Mathf.Abs(sideAttackOffset.x)
                    * (direction == CombatDirection.Left ? -1 : 1), sideAttackOffset.y);
                size = sideAttackSize;
                break;
        }
    }
    private void OnDrawGizmosSelected()
    {
        Vector2 position = transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(position + new Vector2(Mathf.Abs(sideAttackOffset.x), sideAttackOffset.y), sideAttackSize);
        Gizmos.DrawWireCube(position + new Vector2(-Mathf.Abs(sideAttackOffset.x), sideAttackOffset.y), sideAttackSize);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(position + upAttackOffset, upAttackSize);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(position + downAttackOffset, downAttackSize);
    }
}
