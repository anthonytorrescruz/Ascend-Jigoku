using UnityEngine;

// Hurts the player on body contact (collision enter/stay). The hit is queued on CombatResolver so it resolves with
// the rest of the frame's combat. Uses the EnemyDefinition's values when present, else the fields below.
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

    // Ignores attack hitboxes (those are handled by swings, not body contact).
    private void CheckContact(Collider2D other)
    {
        if (CombatAttack.IsHitbox(other)) return;
        var player = other.GetComponentInParent<PlayerHealth>();
        if (player != null) TryContact(player, Time.time);
    }

    // Queues contact damage unless dead, stunned, on cooldown, or suppressed. Returns whether damage was queued.
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
    // Briefly blocks contact damage to one player, e.g. right after they pogo off this enemy.
    public void Suppress(PlayerHealth player, float until)
    {
        protectedPlayer = player;
        suppressedUntil = until;
    }

    public bool IsSuppressed(PlayerHealth player, float now) => player == protectedPlayer && now <= suppressedUntil;
}
