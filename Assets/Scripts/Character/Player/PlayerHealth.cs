using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using AscendJigoku.UI;
using System.Collections;

// Player hit points, post-hit protection (invulnerability) and the death flow. Damage comes from enemies/hazards via TakeDamage*;
// UI listens to the events.
[DefaultExecutionOrder(50)] // after CombatFeedback, so the protection blink wins over its tint
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

    [Header("Death Animation")]
    [Min(0), SerializeField] private float deathAnimationSeconds = 0.8f;
    [Tooltip("Upper limit on the pause between dying and the death screen / retry.")]
    [Min(0), SerializeField] private float maxDeathDelaySeconds = 0.55f;
    private SpriteRenderer blinkSprite;
    // Time.time until which normal damage is ignored (fall damage bypasses it).
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
        int gained = Mathf.Min(amount, 99 - maxHealth);
        maxHealth += gained;
        currentHealth += gained;
        HealthChanged?.Invoke(currentHealth, maxHealth);
        return true;
    }

    public bool Heal(int amount)
    {
        if (amount <= 0 || currentHealth <= 0 || currentHealth >= maxHealth) return false;
        currentHealth += Mathf.Min(amount, maxHealth - currentHealth);
        HealthChanged?.Invoke(currentHealth, maxHealth);
        return true;
    }

    // Back to full health after a scripted death (tutorial); scene-level death handlers call this.
    public void Revive()
    {
        currentHealth = maxHealth;
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Awake()
    {
        currentHealth = maxHealth;
        playerController = GetComponent<PlayerController>();
        blinkSprite = GetComponent<SpriteRenderer>();
    }

    // Readable protection blink: a clear on/off flicker instead of a colour wash. Steady dim with Reduce Flashing.
    private void LateUpdate()
    {
        if (blinkSprite == null || currentHealth <= 0 || ProtectionRemaining <= 0) return;
        float alpha = AscendJigoku.Settings.GameSettings.ReduceFlashing
            ? 0.6f
            : (Mathf.FloorToInt(Time.time * 12) % 2 == 0 ? 1f : 0.35f);
        var color = blinkSprite.color;
        color.a = alpha;
        blinkSprite.color = color;
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
        if (AscendJigoku.Gameplay.CheckpointSanctuary.Protects(this) || amount <= 0 || currentHealth <= 0
            || (!ignoreProtection && Time.time < invulnerableUntil))
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
        QolRunHud.NoteDeath();
        GetComponent<PlayerSpell>()?.Cancel(true);
        GetComponent<PlayerCombat>()?.Cancel();
        GetComponent<PlayerParry>()?.Cancel();
        GetComponent<CombatAttack>()?.CancelAll();

        if (playerController != null) playerController.PlayDeathAnimation();

        var deathHandler = GetComponent<PlayerDeathHandler>();
        if (deathHandler != null || ShouldShowDeathScreen)
            CombatFeedback.PlayClip(deathSound, deathSoundVolume);

        StartCoroutine(FinishDeath(deathHandler));
    }

    // After the death animation: a scene's PlayerDeathHandler takes over if present, otherwise show the death screen or reload the scene.
    private IEnumerator FinishDeath(PlayerDeathHandler deathHandler)
    {
        yield return new WaitForSecondsRealtime(Mathf.Min(deathAnimationSeconds, maxDeathDelaySeconds));

        if (deathHandler != null)
        {
            deathHandler.OnPlayerDied();
            yield break;
        }
        if (ShouldShowDeathScreen && !AscendJigoku.Settings.GameSettings.InstantRespawn)
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
