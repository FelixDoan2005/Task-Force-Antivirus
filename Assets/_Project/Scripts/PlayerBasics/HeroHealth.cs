using UnityEngine;
using System;

public class HeroHealth : MonoBehaviour, IDamageable
{
    public float maxHealth = 100.0f;
    public float currentHealth;

    public event Action<float, float> OnHealthChanged; // (current, max)

    void Awake()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth); // notify UI at start
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0.0f, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth); // notify UI

        if (currentHealth <= 0.0f)
        {
            Debug.Log("HERO DEAD");
            Time.timeScale = 0.0f;
        }
    }
}