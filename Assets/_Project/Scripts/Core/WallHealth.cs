using UnityEngine;
using System;

public class WallHealth : MonoBehaviour, IDamageable
{
    public float maxHealth = 100f;
    public float currentHealth;

    bool isDestroyed;
    public bool IsTargetable => !isDestroyed;

    public event Action<float, float> OnHealthChanged;
    public event Action OnWallDestroyed;

    void Awake()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (isDestroyed) return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
        {
            isDestroyed = true;
            OnWallDestroyed?.Invoke();
        }
    }
}
