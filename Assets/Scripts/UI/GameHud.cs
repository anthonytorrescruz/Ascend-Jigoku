using UnityEngine;

[DisallowMultipleComponent]
public class GameHud : MonoBehaviour
{
    [SerializeField] private HealthBarUI healthBarPrefab;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Camera hudCamera;

    private void Start()
    {
        if (healthBarPrefab == null || playerHealth == null || hudCamera == null)
        {
            Debug.LogError("Game HUD needs a health bar prefab, player health, and camera.", this);
            return;
        }

        // The scene stores only this small bootstrap object. UI geometry exists
        // during gameplay, and camera rendering keeps it at the camera's scale.
        HealthBarUI healthBar = Instantiate(healthBarPrefab, transform, false);
        healthBar.name = "Health Bar";
        Canvas canvas = healthBar.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = hudCamera;
        canvas.planeDistance = Mathf.Max(0.5f, hudCamera.nearClipPlane + 0.05f);
        healthBar.Bind(playerHealth);
    }
}
