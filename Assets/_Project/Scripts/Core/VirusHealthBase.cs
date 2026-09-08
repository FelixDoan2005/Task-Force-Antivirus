using UnityEngine;

public abstract class VirusHealthBase : MonoBehaviour
{
    public float maxHealth = 10f;
    protected float currentHealth;

    public float CurrentHealth => currentHealth;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
    }

    //set while something else is protecting this virus - currently the Demon's takeover, which
    //turns it to the player's side and shouldn't have it dying to the fight it was sent into
    public bool invulnerable;

    public void TakeDamage(float amount)
    {
        if (invulnerable) return;

        currentHealth -= amount;

        if (currentHealth <= 0f)
            Die();
    }

    protected virtual void Die()
    {
        Destroy(gameObject);
    }
}
