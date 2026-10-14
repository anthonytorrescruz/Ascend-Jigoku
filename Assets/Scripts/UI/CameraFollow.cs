using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player;
    [Min(0)] public float smoothSpeed = 5f; // Zero follows instantly.

    private void LateUpdate()
    {
        if (player == null)
        {
            var target = FindAnyObjectByType<PlayerHealth>();
            if (target == null) return;
            player = target.transform;
        }
        Vector3 desired = new Vector3(player.position.x, player.position.y, transform.position.z);
        float blend = smoothSpeed <= 0 ? 1 : 1 - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, blend);
    }
}
