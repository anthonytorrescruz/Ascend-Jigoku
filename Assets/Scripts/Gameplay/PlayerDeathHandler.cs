using UnityEngine;

// Optional scene-specific death flow. Without one, PlayerHealth uses the existing Death scene.
public abstract class PlayerDeathHandler : MonoBehaviour
{
    public abstract void OnPlayerDied();
}
