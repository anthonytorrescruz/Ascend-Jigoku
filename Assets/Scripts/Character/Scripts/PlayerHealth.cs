using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using AscendJigoku.UI;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 3;
    [Tooltip("Protection after damage so body contact and an attack do not charge twice.")]
    [Min(0), SerializeField] private float damageCooldownSeconds = 0.35f;
    [Header("Death Screen")]
    [Tooltip("Show YOU DIED with Retry and Give Up. Disable to restart immediately everywhere.")]
    [SerializeField] private bool showDeathScreen = true;
    [Tooltip("Restart immediately during Unity playtesting. Turn this off to preview the death screen. Builds still show it.")]
    [SerializeField] private bool skipDeathScreenInEditor = true;
    private float invulnerableUntil;

    private int currentHealth;
    private PlayerController playerController;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public float ProtectionRemaining => Mathf.Max(0, invulnerableUntil - Time.time);
    public bool ShouldShowDeathScreen => showDeathScreen && !(Application.isEditor && skipDeathScreenInEditor);

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
        GetComponent<CombatFeedback>()?.Hit();

        Debug.Log("Player health: " + currentHealth + "/" + maxHealth);

        HealthChanged?.Invoke(currentHealth, maxHealth);

        // Surviving hits never leave gameplay; only zero health enters the death flow.
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
        GetComponent<PlayerCombat>()?.Cancel();
        GetComponent<PlayerParry>()?.Cancel();
        GetComponent<CombatAttack>()?.CancelAll();
        if (ShouldShowDeathScreen)
        {
            if (playerController != null) playerController.enabled = false;
            var combat = GetComponent<PlayerCombat>();
            if (combat != null) combat.enabled = false;
            var parry = GetComponent<PlayerParry>();
            if (parry != null) parry.enabled = false;
            DeathScreen.Show(gameObject.scene);
        }
        else SceneManager.LoadScene(gameObject.scene.name);
    }
}
