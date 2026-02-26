using TMPro;
using UnityEngine;

public class CombatDebugHUD : MonoBehaviour
{
    public static CombatDebugHUD Instance;

    [SerializeField] private TMP_Text debugText;

    private void Awake()
    {
        Instance = this;
    }

    public void SetLine(string line)
    {
        if (debugText != null) debugText.text = line;
    }
}