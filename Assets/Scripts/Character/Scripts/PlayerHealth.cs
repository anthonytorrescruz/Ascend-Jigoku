using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 3;
    [Tooltip("Protection after damage so body contact and an attack do not charge twice.")]
    [Min(0), SerializeField] private float damageCooldownSeconds = 0.35f;
    private float invulnerableUntil;

    private int currentHealth;
    private PlayerController playerController;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    public event Action<int, int> HealthChanged;

    private void Awake()
    {
        currentHealth = maxHealth;
        playerController = GetComponent<PlayerController>();
    }

    private void Start()
    {
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public bool TakeDamage(int amount)
    {
        if (amount <= 0 || currentHealth <= 0 || Time.time < invulnerableUntil)
            return false;

        invulnerableUntil = Time.time + damageCooldownSeconds;

        currentHealth = Mathf.Max(0, currentHealth - amount);

        Debug.Log("Player health: " + currentHealth + "/" + maxHealth);

        HealthChanged?.Invoke(currentHealth, maxHealth);

        // If the player has no health left, restart the current scene.
        if (currentHealth <= 0)
        {
            Die();
        }
        return true;
    }

    public bool TakeCombatDamage(int amount, float knockbackDirection)
    {
        if (!TakeDamage(amount)) return false;
        if (currentHealth > 0 && playerController != null)
            playerController.ApplyHorizontalKnockback(knockbackDirection);
        return true;
    }

    private void Die()
    {
        Debug.Log("Player died. Restarting scene.");

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }
}
