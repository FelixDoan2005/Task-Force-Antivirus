using UnityEngine;
using System;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    public float maxHealth = 100.0f;
    public float currentHealth;
    public float damageTakenMultiplier = 1f; // e.g. 0.25f while blocking = takes 25% of incoming damage

    public bool isDead { get; private set; }
    public bool IsTargetable => !isDead;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;

    void Awake()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        amount *= damageTakenMultiplier;
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0.0f, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0.0f)
        {
            isDead = true;
            OnDied?.Invoke();
        }
    }

    public void Respawn()
    {
        isDead = false;
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
