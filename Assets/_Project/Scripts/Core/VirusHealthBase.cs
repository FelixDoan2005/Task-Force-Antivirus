using UnityEngine;

public abstract class VirusHealthBase : MonoBehaviour
{
    public float maxHealth = 10f;
    protected float currentHealth;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;

        if (currentHealth <= 0f)
            Die();
    }

    protected virtual void Die()
    {
        Destroy(gameObject);
    }
}
