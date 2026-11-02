using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using AscendJigoku.UI;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 3;
    [Tooltip("Protection after damage so body contact and an attack do not charge twice.")]
    [Min(0), SerializeField] private float damageCooldownSeconds = 0.35f;
    [Header("Player Damage Audio")]
    [SerializeField] private AudioClip damageSound;
    [Range(0, 1), SerializeField] private float damageSoundVolume = 0.65f;
    [SerializeField] private AudioClip deathSound;
    [Range(0, 1), SerializeField] private float deathSoundVolume = 0.65f;
    [Header("Death Screen")]
    [Tooltip("Load the Death scene with Retry and Give Up. Disable to restart immediately everywhere.")]
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
    public event Action<int> Damaged;
    public bool IncreaseMaxHealth(int amount)
    {
        if (amount <= 0 || currentHealth <= 0 || maxHealth >= 99) return false;
        int gained = Mathf.Min(amount, 99 - maxHealth); maxHealth += gained; currentHealth += gained;
        HealthChanged?.Invoke(currentHealth, maxHealth); return true;
    }

    public bool Heal(int amount)
    {
        if (amount <= 0 || currentHealth <= 0 || currentHealth >= maxHealth) return false;
        currentHealth += Mathf.Min(amount, maxHealth - currentHealth);
        HealthChanged?.Invoke(currentHealth, maxHealth);
        return true;
    }

    private void Awake()
    {
        currentHealth = maxHealth;
        playerController = GetComponent<PlayerController>();
    }

    private void Start()
    {
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public bool TakeDamage(int amount) => ApplyDamage(amount, false);
    public bool TakeFallDamage(int amount) => ApplyDamage(amount, true);
    public void ProtectFor(float seconds) => invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + Mathf.Max(0, seconds));
    private bool ApplyDamage(int amount, bool ignoreProtection)
    {
        if (amount <= 0 || currentHealth <= 0 || (!ignoreProtection && Time.time < invulnerableUntil))
            return false;

        invulnerableUntil = Time.time + damageCooldownSeconds;

        int previousHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        Damaged?.Invoke(previousHealth - currentHealth);
        GetComponent<CombatFeedback>()?.Hit();

        Debug.Log("Player health: " + currentHealth + "/" + maxHealth);

        HealthChanged?.Invoke(currentHealth, maxHealth);

        // Surviving hits never leave gameplay; only zero health enters the death flow.
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            CombatFeedback.PlayClip(damageSound, damageSoundVolume);
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
        GetComponent<PlayerSpell>()?.Cancel(true);
        GetComponent<PlayerCombat>()?.Cancel();
        GetComponent<PlayerParry>()?.Cancel();
        GetComponent<CombatAttack>()?.CancelAll();
        var deathHandler = GetComponent<PlayerDeathHandler>();
        // Keep the death cue for both the original Death scene and an opt-in test-scene flow.
        if (deathHandler != null || ShouldShowDeathScreen)
            CombatFeedback.PlayClip(deathSound, deathSoundVolume);
        if (deathHandler != null) { deathHandler.OnPlayerDied(); return; }
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
