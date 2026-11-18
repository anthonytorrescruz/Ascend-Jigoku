using System;
using UnityEngine;

// Health for enemies and bosses. TakeCombatDamage (from CombatResolver) applies damage, knockback via EnemyMotor,
// and death; HealthChanged drives health bars. Max health comes from the EnemyDefinition when one is assigned.
public class EnemyHealth : MonoBehaviour
{
    [Header("Enemy Health")]
    [SerializeField] private int maxHealth = 3;
    [Min(0), SerializeField] private float despawnDelay;
    [Tooltip("0 keeps normal weapon damage. Bosses can cap damage per hit without changing player upgrades.")]
    [Min(0), SerializeField] private int damagePerHitCap;
    // Set false to make the enemy invulnerable (e.g. during a boss phase change).
    public bool DamageEnabled { get; set; } = true;

    private int currentHealth;
    private EnemySounds sounds;

    public int CurrentHealth => currentHealth;
    public int MaxHealth
    {
        get
        {
            var definition = GetComponent<EnemyCombat>()?.Definition;
            return definition != null ? Mathf.Max(1, definition.maxHealth) : maxHealth;
        }
    }

    public event Action<int, int> HealthChanged;

    // Called once by encounter prefabs in Awake, before their first HealthChanged event.
    public void InitializeEncounterHealth(int maximum, float deathDelay, int perHitCap = 0)
    {
        maxHealth = Mathf.Max(1, maximum);
        currentHealth = maxHealth;
        despawnDelay = Mathf.Max(0, deathDelay);
        damagePerHitCap = Mathf.Max(0, perHitCap);
    }

    private void Awake()
    {
        sounds = GetComponent<EnemySounds>();
        currentHealth = MaxHealth;
    }

    private void Start()
    {
        HealthChanged?.Invoke(currentHealth, MaxHealth);
    }

    public void RestoreHealth(int amount)
    {
        currentHealth = Mathf.Clamp(amount, 1, MaxHealth);
        HealthChanged?.Invoke(currentHealth, MaxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (!DamageEnabled || amount <= 0 || currentHealth <= 0)
        {
            return;
        }

        if (damagePerHitCap > 0) amount = Mathf.Min(amount, damagePerHitCap);
        currentHealth = Mathf.Max(
            0,
            currentHealth - amount
        );

        GetComponent<CombatFeedback>()?.Hit();
        Debug.Log(
            gameObject.name +
            " health: " +
            currentHealth +
            "/" +
            MaxHealth
        );

        HealthChanged?.Invoke(
            currentHealth,
            MaxHealth
        );

        if (currentHealth <= 0)
        {
            Die();
        }
        else if (sounds != null)
        {
            sounds.PlayHurt();
        }
    }

    // Damage from a combat hit: returns false if it was ignored, and knocks the enemy back if it survived.
    public bool TakeCombatDamage(int amount, Vector2 awayFromAttacker)
    {
        if (!DamageEnabled || amount <= 0 || currentHealth <= 0) return false;
        TakeDamage(amount);
        if (currentHealth > 0) GetComponent<EnemyMotor>()?.TakeHit(awayFromAttacker);
        return true;
    }

    // With a despawn delay the body stays (colliders off, physics frozen) so a death animation can play.
    private void Die()
    {
        Debug.Log(gameObject.name + " died.");
        if (sounds != null) sounds.PlayDeath();

        GetComponent<EnemyCombat>()?.CancelIntent();
        GetComponent<CombatAttack>()?.CancelAll();
        if (despawnDelay > 0)
        {
            foreach (var shape in GetComponentsInChildren<Collider2D>()) shape.enabled = false;
            var body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.simulated = false;
            }
        }
        Destroy(gameObject, despawnDelay);
    }
}
