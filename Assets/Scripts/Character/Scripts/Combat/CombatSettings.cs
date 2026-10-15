using UnityEngine;

// Shared tuning for CombatAttack components. Duplicate the asset for a different enemy type.
[CreateAssetMenu(menuName = "Ascend Jigoku/Combat Settings")]
public sealed class CombatSettings : ScriptableObject
{
    [Header("Player")]
    public Vector2 playerAttackSize = new Vector2(2, 2);
    [Min(1)] public int playerActiveFrames = 6;
    [Min(0)] public int playerRecoveryFrames = 8;
    [Min(0)] public float inputBufferSeconds = 0.12f;
    [Header("Enemy")]
    public Vector2 enemyAttackSize = new Vector2(1.25f, 1.25f);
    [Min(0)] public int enemyWindupFrames = 18;
    [Min(1)] public int enemyActiveFrames = 6;
    [Min(0)] public float enemyCooldownSeconds = 1;
    [Header("Placement and Walls")]
    [Min(0)] public float bodyClearance = 0.02f;
    public LayerMask terrainLayers = 1 << 3;
}
