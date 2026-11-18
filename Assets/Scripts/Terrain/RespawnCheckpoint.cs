using UnityEngine;

// Trigger that sets the player's respawn point to respawnPoint (or to this object if none is assigned) when they touch it.
public class RespawnCheckpoint : MonoBehaviour
{
    [SerializeField] private Transform respawnPoint;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (CombatAttack.IsHitbox(other)) return; // a sword swing reaching the trigger is not the player arriving

        PlayerRespawn playerRespawn =
            other.GetComponentInParent<PlayerRespawn>();

        if (playerRespawn == null)
            return;

        Vector3 newRespawnPosition;

        if (respawnPoint != null)
            newRespawnPosition = respawnPoint.position;
        else
            newRespawnPosition = transform.position;

        playerRespawn.SetRespawnPoint(newRespawnPosition);

        Debug.Log("Checkpoint activated: " + gameObject.name);
    }
}