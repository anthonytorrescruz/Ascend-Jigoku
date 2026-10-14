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
        if (damageAmount <= 0 || now < nextDamageAt || IsSuppressed(player, now)) return false;
        nextDamageAt = now + contactCooldownSeconds;
        CombatResolver.Instance.QueueContact(this, player, damageAmount, now);
        return true;
    }
    public void Suppress(PlayerHealth player, float until) { protectedPlayer = player; suppressedUntil = until; }
    public bool IsSuppressed(PlayerHealth player, float now) => player == protectedPlayer && now <= suppressedUntil;
}
