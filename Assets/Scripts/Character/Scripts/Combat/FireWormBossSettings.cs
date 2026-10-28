using UnityEngine;

[CreateAssetMenu(menuName = "Ascend Jigoku/Fire Worm Boss Settings")]
public sealed class FireWormBossSettings : ScriptableObject
{
    [Header("Arena")]
    [Min(0)] public float edgePadding = 0.25f;
    [Header("Attack Selection")]
    public bool automaticAttacks = true;
    [Min(0)] public float fireballRange = 13;
    [Min(0)] public float slamRange = 3;
    [Min(0)] public float attackCooldownSeconds = 1;
    [Min(0)] public float recoverySeconds = 0.4f;
    [Header("Fireball - Frames at 60 FPS")]
    [Min(1)] public int fireballWindupFrames = 33;
    [Min(0)] public int fireballDamage = 1;
    [Min(0.1f)] public float fireballSpeed = 9;
    [Min(0.1f)] public float fireballLifetime = 3;
    public Vector2 fireballSize = new Vector2(0.8f, 0.55f);
    [Min(1)] public int maximumFireballs = 6;
    [Min(0)] public float fireballParryStunSeconds = 0.6f;
    [Header("Slam - Frames at 60 FPS")]
    [Min(1)] public int slamWindupFrames = 42;
    [Min(1)] public int slamActiveFrames = 8;
    [Min(0)] public int slamDamage = 1;
    public Vector2 slamSize = new Vector2(3.8f, 1.4f);
    [Min(0)] public float slamClearance = 0.05f;
    [Header("Placeholder Feedback")]
    public Color fireballColor = new Color(1, 0.3f, 0.05f);
    public Color warningColor = new Color(1, 0.7f, 0.1f, 0.3f);
    public Color slamColor = new Color(1, 0.2f, 0.05f, 0.75f);
}
