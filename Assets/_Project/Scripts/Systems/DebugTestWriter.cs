using UnityEngine;

public class DebugTestWriter : MonoBehaviour
{
    private void Start()
    {
        CombatDebugHUD.Instance.SetLine("HUD Connected");
    }
}