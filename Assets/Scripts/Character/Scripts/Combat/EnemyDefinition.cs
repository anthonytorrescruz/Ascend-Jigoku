using UnityEngine;

public enum EnemyMovement { Stationary, Ground, Jumper, Flying }

// Create > Ascend Jigoku > Enemy Definition. Assign it on EnemyCombat; duplicate to make a variant.
[CreateAssetMenu(menuName = "Ascend Jigoku/Enemy Definition")]
public sealed class EnemyDefinition : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Enemy";
    public EnemyMovement movement = EnemyMovement.Ground;
    [Min(1)] public int maxHealth = 3;

    [Header("Movement")]
    [Min(0)] public float moveSpeed = 2;
    [Min(0.1f)] public float acceleration = 18;
    [Min(0.1f)] public float gravityScale = 3;
    [Tooltip("Resistance to body pushing. Attack knockback is controlled separately below.")]
    [Min(1)] public float bodyMass = 100;
    [Min(0)] public float patrolRadius = 2;
    [Min(0)] public float stoppingDistance = 0.7f;
    [Min(0.1f)] public float aggroRadius = 7;
    [Min(0.1f)] public float leashRadius = 12;
    [Min(0)] public float loseSightSeconds = 1.5f;
    public LayerMask terrainLayers = 1 << 3;

    [Header("Jumping / Flying")]
    [Min(0)] public float jumpSpeed = 10;
    [Min(0)] public float jumpForwardSpeed = 4;
    [Min(0)] public float jumpCooldown = 1;
    [Min(0.1f)] public float jumpSearchDistance = 4;
    [Min(0)] public float maximumDrop = 2;
    [Tooltip("Flyers approach this height above the player before striking down.")]
    public float hoverHeight = 1.4f;

    [Header("Attack - Frames at 60 FPS")]
    [Min(0)] public int damage = 1;
    [Min(1)] public int windupFrames = 24;
    [Min(1)] public int activeFrames = 6;
    [Min(0)] public float recoverySeconds = 0.8f;
    public Vector2 attackSize = new Vector2(1.25f, 1.25f);
    [Min(0)] public int predictionFrames = 4;
    [Min(0)] public float maximumPredictionDistance = 1;
    [Tooltip("Flyers may attack down. Other variants can still strike above them.")]
    public bool allowDownAttack;
    [Min(0)] public int contactDamage = 1;
    [Min(0)] public float contactCooldown = 0.5f;

    [Header("Reaction to Visible Player Attacks")]
    public bool evadeAttacks;
    [Min(0)] public float reactionSeconds = 0.06f;
    [Min(0)] public float evadeSeconds = 0.2f;
    [Min(0.01f)] public float evadeCooldown = 1.5f;
    [Min(0)] public float evadeSpeed = 3.5f;

    [Header("Response to Being Hit")]
    [Tooltip("0 ignores knockback; 1 takes the full impulse. Parries always stun.")]
    [Range(0, 2)] public float knockbackMultiplier = 1;
    public Vector2 knockbackVelocity = new Vector2(6, 2);
    [Min(0)] public float hitStunSeconds = 0.18f;
}
