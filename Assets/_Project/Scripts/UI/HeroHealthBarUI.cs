using UnityEngine;
using UnityEngine.UI;

public class HeroHealthBarUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth heroHealth;
    [SerializeField] private Image fillImage;

    private void OnEnable()
    {
        if (heroHealth != null)
            heroHealth.OnHealthChanged += UpdateBar;
    }

    private void OnDisable()
    {
        if (heroHealth != null)
            heroHealth.OnHealthChanged -= UpdateBar;
    }

    private void Start()
    {
        if (heroHealth != null)
            UpdateBar(heroHealth.currentHealth, heroHealth.maxHealth);
    }

    private void UpdateBar(float current, float max)
    {
        Debug.Log("HERO UI UPDATE: " + current + "/" + max);
        if (fillImage == null) return;
        fillImage.fillAmount = (max <= 0f) ? 0f : current / max;
    }
}