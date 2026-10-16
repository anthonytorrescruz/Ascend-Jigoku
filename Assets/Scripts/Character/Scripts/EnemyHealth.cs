using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Enemy Health")]
    [SerializeField] private int maxHealth = 3;

    private int currentHealth;

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

    private void Awake()
    {
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
        if (amount <= 0 || currentHealth <= 0)
        {
            return;
        }

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
    }

    public bool TakeCombatDamage(int amount, Vector2 awayFromAttacker)
    {
        if (amount <= 0 || currentHealth <= 0) return false;
        TakeDamage(amount);
        if (currentHealth > 0) GetComponent<EnemyMotor>()?.TakeHit(awayFromAttacker);
        return true;
    }

    private void Die()
    {
        Debug.Log(gameObject.name + " died.");

        GetComponent<EnemyCombat>()?.CancelIntent();
        GetComponent<CombatAttack>()?.CancelAll();
        Destroy(gameObject);
    }
}
