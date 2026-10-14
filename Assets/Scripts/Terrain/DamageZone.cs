using UnityEngine;

public class DamageZone : MonoBehaviour
{
    [SerializeField] private int damageAmount = 1;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<CombatAttackHitbox>() != null) return;
        PlayerHealth playerHealth =
            other.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damageAmount);
            // Falling out of the level always returns a survivor, even during damage protection.
            if (playerHealth.CurrentHealth > 0)
                playerHealth.GetComponent<PlayerRespawn>()?.Respawn();
        }
    }
}
