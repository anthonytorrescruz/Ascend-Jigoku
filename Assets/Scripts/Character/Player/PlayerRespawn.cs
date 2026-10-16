using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    private Vector3 respawnPosition;
    private Rigidbody2D rb;
    public event System.Action<Vector3> BeforeRespawn;

    private void Awake()
    {
        // The player's starting position is the first respawn point.
        respawnPosition = transform.position;

        rb = GetComponent<Rigidbody2D>();
    }

    public void SetRespawnPoint(Vector3 newPosition)
    {
        respawnPosition = newPosition;

        Debug.Log("Respawn point updated to: " + respawnPosition);
    }

    public void Respawn()
    {
        BeforeRespawn?.Invoke(respawnPosition);
        GetComponent<PlayerCombat>()?.Cancel();
        GetComponent<CombatAttack>()?.CancelAll();
        GetComponent<CombatFeedback>()?.ResetFeedback();
        GetComponent<PlayerController>()?.ResetMovement();
        GetComponent<PlayerParry>()?.Cancel();
        // Stop existing movement before teleporting.
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position = respawnPosition;
        }

        transform.position = respawnPosition;

        Debug.Log("Player respawned.");
    }
}
