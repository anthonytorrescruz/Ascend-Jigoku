using UnityEngine;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameObject[] healthSegments;
    private bool isBound;

    private void Start()
    {
        if (!isBound)
        {
            Bind(playerHealth != null ? playerHealth : FindAnyObjectByType<PlayerHealth>());
        }
    }

    public void Bind(PlayerHealth health)
    {
        if (isBound && playerHealth != null)
        {
            playerHealth.HealthChanged -= UpdateHealthBar;
        }

        playerHealth = health;
        isBound = playerHealth != null;

        if (isBound && playerHealth != null)
        {
            playerHealth.HealthChanged += UpdateHealthBar;

            UpdateHealthBar(
                playerHealth.CurrentHealth,
                playerHealth.MaxHealth
            );
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.HealthChanged -= UpdateHealthBar;
        }
    }

    private void UpdateHealthBar(int currentHealth, int maxHealth)
    {
        for (int i = 0; i < healthSegments.Length; i++)
        {
            bool shouldBeVisible = i < currentHealth;
            if (healthSegments[i].activeSelf != shouldBeVisible)
            {
                healthSegments[i].SetActive(shouldBeVisible);
            }
        }
    }
}
