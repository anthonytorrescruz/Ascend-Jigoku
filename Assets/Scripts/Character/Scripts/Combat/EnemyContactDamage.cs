using UnityEngine;

[DisallowMultipleComponent]
public class EnemyContactDamage : MonoBehaviour
{
    [Min(0), SerializeField] private int damageAmount = 1;
    [Min(0), SerializeField] private float contactCooldownSeconds = 0.5f;
    private float nextDamageAt;
    private float suppressedUntil = float.NegativeInfinity;
    private PlayerHealth protectedPlayer;
    public CombatAttack Attack { get; private set; }
    private void Awake() => Attack = GetComponent<CombatAttack>();
    private void OnCollisionEnter2D(Collision2D collision) => CheckContact(collision.collider);
    private void OnCollisionStay2D(Collision2D collision) => CheckContact(collision.collider);
    private void CheckContact(Collider2D other)
    {
        if (other.GetComponentInParent<CombatAttackHitbox>() != null) return;
        var player = other.GetComponentInParent<PlayerHealth>();
        if (player != null) TryContact(player, Time.time);
    }
    public bool TryContact(PlayerHealth player, float now)
    {
        var health = GetComponent<EnemyHealth>();
        var combat = GetComponent<EnemyCombat>();
        var profile = combat != null ? combat.Definition : null;
        int damage = profile != null ? profile.contactDamage : damageAmount;
        if (!isActiveAndEnabled || player == null || player.CurrentHealth <= 0 || health == null || health.CurrentHealth <= 0
            || (combat != null && combat.IsStunned) || damage <= 0 || now < nextDamageAt || IsSuppressed(player, now)) return false;
        nextDamageAt = now + (profile != null ? profile.contactCooldown : contactCooldownSeconds);
        CombatResolver.Instance.QueueContact(this, player, damage, now);
        return true;
    }
    public void Suppress(PlayerHealth player, float until) { protectedPlayer = player; suppressedUntil = until; }
    public bool IsSuppressed(PlayerHealth player, float now) => player == protectedPlayer && now <= suppressedUntil;
}
