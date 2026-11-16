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
    private InputAction abilityInput, swapInput;
    private PlayerWeapons weapons;
    private CombatAttack attack;
    private PlayerParry parry;
    private float nextAttackAt;
    private float bufferedUntil = float.NegativeInfinity;
    private CombatDirection bufferedDirection;
    public CombatAttack Attack => attack;
    public PlayerWeapons Weapons => weapons;
    public int AttackDamage => Mathf.Max(0, attackDamage);
    public void IncreaseAttackDamage(int amount) => attackDamage = Mathf.Clamp(attackDamage + Mathf.Max(0, amount), 0, 99);
    public LayerMask EnemyLayers => enemyLayers;
    public float RecoveryRemaining => Mathf.Max(0, nextAttackAt - Time.time);
    public bool HasBufferedAttack => Time.time <= bufferedUntil;
    public AudioClip attackClip;
    [Range(0, 1), SerializeField] private float attackVolume = 0.12f;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        health = GetComponent<PlayerHealth>();
        attack = GetComponent<CombatAttack>();
        parry = GetComponent<PlayerParry>();
        weapons = GetComponent<PlayerWeapons>();
        if (weapons == null) weapons = gameObject.AddComponent<PlayerWeapons>();
        abilityInput = AscendJigoku.Settings.KeybindSettings.CreateExtraAction("ability");
        swapInput = AscendJigoku.Settings.KeybindSettings.CreateExtraAction("swap");
        controls = new PlayerActions();
        AscendJigoku.Settings.KeybindSettings.Register(controls.asset); // apply saved keybinds
    }
    private void OnEnable() { controls.Enable(); abilityInput.Enable(); swapInput.Enable(); }
    private void OnDisable()
    {
        controls.Disable(); abilityInput.Disable(); swapInput.Disable();
        Cancel();
    }
    private void OnDestroy()
    {
        AscendJigoku.Settings.KeybindSettings.ReleaseExtraAction("ability", abilityInput);
        AscendJigoku.Settings.KeybindSettings.ReleaseExtraAction("swap", swapInput);
        abilityInput.Dispose(); swapInput.Dispose(); controls.Dispose();
    }
    private void Update()
    {
        if (swapInput.WasPressedThisFrame()) weapons.Swap();
        if (abilityInput.WasPressedThisFrame()) weapons.TryAbility();
        if (controls.PlayerInput.Attack.WasPressedThisFrame()){
            QueueAttack(ReadDirection());
            return;
        }
        Tick(Time.time);
    }
    public void QueueAttack(CombatDirection direction)
    {
        GetComponent<PlayerSpell>()?.Cancel();
        bufferedDirection = direction;
        bufferedUntil = Time.time + Mathf.Max(0, attack.Settings.inputBufferSeconds);
    }
    public void Tick(float now)
    {
        if (now <= bufferedUntil && TryAttack(bufferedDirection, now))
            bufferedUntil = float.NegativeInfinity;
    }
    public bool TryAttack() => TryAttack(ReadDirection(), Time.time);
    // True when nothing blocks starting a new attack, ability or weapon swap.
    public bool CanAct(float now) => isActiveAndEnabled && (health == null || health.CurrentHealth > 0)
        && now >= nextAttackAt && !(attack.CurrentSwing != null && attack.CurrentSwing.IsActive(now))
        && parry.PhaseAt(now) == ParryPhase.Idle;
    public void HoldAttacks(float until) => nextAttackAt = Mathf.Max(nextAttackAt, until);
    private bool TryAttack(CombatDirection direction, float now)
    {
        if (!CanAct(now)) return false;

        if (direction == CombatDirection.Down && playerController.IsGrounded) return false;
        GetAttackGeometry(direction, out Vector2 offset, out Vector2 size);

        var weapon = weapons.Current;
        var swing = attack.BeginAttack(direction, offset, size, attackDamage + weapon.BonusDamage,
            attack.Settings.playerActiveFrames + weapon.ExtraActiveFrames, enemyLayers, now, weapon.ExtraStartupFrames);

        if (swing == null) return false;
        swing.Knockback = weapon.Knockback;

        //if (direction == CombatDirection.Left || direction == CombatDirection.Right)
        if (direction != CombatDirection.Up)
            playerController.PlayAttackAnimation();
        CombatFeedback.PlayClip(attackClip, attackVolume);
        nextAttackAt = swing.EndsAt + Mathf.Max(0, attack.Settings.playerRecoveryFrames) / CombatAttack.ReferenceFramesPerSecond
            + weapon.ExtraRecoveryFrames / CombatAttack.ReferenceFramesPerSecond;
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
        var scale = (weapons != null ? weapons.Current : Weapon.Katana).SizeScale;
        size = new Vector2(Mathf.Max(0.01f, size.x * scale.x), Mathf.Max(0.01f, size.y * scale.y));
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

