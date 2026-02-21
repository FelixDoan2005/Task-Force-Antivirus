using UnityEngine;
using UnityEngine.UI;

public class WallHealthBarUI : MonoBehaviour
{
    [SerializeField] private WallHealth wallHealth;
    [SerializeField] private Image fillImage;

    private void OnEnable()
    {
        if (wallHealth != null)
            wallHealth.OnHealthChanged += UpdateBar;
    }

    private void OnDisable()
    {
        if (wallHealth != null)
            wallHealth.OnHealthChanged -= UpdateBar;
    }

    private void Start()
    {
        if (wallHealth != null)
            UpdateBar(wallHealth.currentHealth, wallHealth.maxHealth);
    }

    private void UpdateBar(float current, float max)
    {
        if (fillImage == null) return;

        fillImage.fillAmount = (max <= 0f) ? 0f : current / max;
    }
}