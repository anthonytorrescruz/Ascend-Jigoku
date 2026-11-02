using UnityEngine;

namespace AscendJigoku.Gameplay
{
    // Opt-in rest point; existing checkpoint scenes retain their original rules.
    public sealed class HealingCheckpoint : MonoBehaviour
    {
        public Transform respawnPoint;
        private PlayerHealth occupant;
        public bool Activate(PlayerHealth player)
        {
            if (player == null || player.CurrentHealth <= 0) return false;
            player.GetComponent<PlayerRespawn>()?.SetRespawnPoint(respawnPoint != null ? respawnPoint.position : transform.position);
            player.Heal(player.MaxHealth);
            return true;
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (CombatAttack.IsHitbox(other)) return;
            var player = other.GetComponentInParent<PlayerHealth>();
            if (player != null && occupant != player && Activate(player)) occupant = player;
        }
        private void OnTriggerExit2D(Collider2D other)
        { if (!CombatAttack.IsHitbox(other) && other.GetComponentInParent<PlayerHealth>() == occupant) occupant = null; }
    }
}
