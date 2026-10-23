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

    [Tooltip("How long the body stays after it dies, so a death animation can play out. 0 " +
             "removes it immediately, which is what every virus did before this existed.")]
    public float deathDelay = 0f;

    //raised the moment it dies, before the body is removed
    public event System.Action OnDied;

    public bool IsDead { get; private set; }

    //how many shield totems are holding this one right now. a count, not the flag above: the
    //Demon's takeover owns that flag and switches it on and off itself, so a totem letting go
    //would strip the Demon's protection - and two totems holding the same virus would clear
    //each other. a count lets any number of holders come and go without stepping on anyone.
    int shieldedBy;

    public bool IsShielded => shieldedBy > 0;

    public void AddShield() => shieldedBy++;

    public void RemoveShield()
    {
        if (shieldedBy > 0) shieldedBy--;
    }

    public void TakeDamage(float amount)
    {
        if (invulnerable || shieldedBy > 0) return;

        currentHealth -= amount;

        if (currentHealth <= 0f)
            Die();
    }

    protected virtual void Die()
    {
        if (IsDead) return;   //a volley can land several hits in the same frame
        IsDead = true;

        OnDied?.Invoke();

        if (deathDelay <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        //it is a corpse for the length of the animation: it stops chasing, stops hitting, and
        //stops being something arrows can collide with or abilities can count as a target
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;

        foreach (MonoBehaviour script in GetComponents<MonoBehaviour>())
            if (script != this) script.enabled = false;

        Destroy(gameObject, deathDelay);
    }
}
