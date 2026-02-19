using UnityEngine;

public class WallHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        Debug.Log("Wall HP: " + currentHealth);

        if (currentHealth <= 0f)
        {
            Debug.Log("GAME OVER");
            Time.timeScale = 0f;
        }
    }
}
