using UnityEngine;
using System;

//a friendly soldier's health. IDamageable, so viruses treat it as a person to fight
public class SoldierHealth : MonoBehaviour, IDamageable
{
    public float maxHealth = 150f;
    public float currentHealth;

    [Tooltip("Seconds the body stays for the death animation before it's removed.")]
    public float deathDelay = 3f;

    public bool IsDead { get; private set; }
    public bool IsTargetable => !IsDead;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f) Die();
    }

    void Die()
    {
        IsDead = true;
        OnDied?.Invoke();

        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (MonoBehaviour script in GetComponents<MonoBehaviour>())
            if (script != this) script.enabled = false;

        Destroy(gameObject, deathDelay);
    }
}
