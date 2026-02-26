using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponSwitcher : MonoBehaviour
{
    [Header("Assign your weapon GameObjects here")]
    [SerializeField] private GameObject bowObject;
    [SerializeField] private GameObject swordObject;
    [SerializeField] private SwordWeapon swordWeapon;
    [SerializeField] private Archer archer;

    private int activeIndex = 0; // 0 = bow, 1 = sword
    private float attackUntilTime;

    //animations
    public bool IsAttacking { get; private set; }
    public bool IsDrawingBow { get; private set; }

    private void Start()
    {
        SetActive(0);
    }

    private void Update()
    {
        IsAttacking = Time.time < attackUntilTime;
        IsDrawingBow = (activeIndex == 0 && archer != null && archer.IsDrawing());

        if (Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) SetActive(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SetActive(1);

        //sword
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && activeIndex == 1)
        {
            swordWeapon.Attack();
            attackUntilTime = Time.time + 0.15f;
        }

        //bow
        if (Mouse.current != null)
        {
            if (activeIndex == 0)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                    archer.StartDraw();

                if (Mouse.current.leftButton.wasReleasedThisFrame)
                {
                    archer.ReleaseDraw();
                    attackUntilTime = Time.time + 0.15f;
                }
            }
        }
        if (CombatDebugHUD.Instance != null)
            CombatDebugHUD.Instance.SetLine($"W:{(activeIndex==0?"Bow":"Sword")} A:{IsAttacking} D:{IsDrawingBow}");
    }

    private void SetActive(int index)
    {
        activeIndex = index;

        if (bowObject != null) bowObject.SetActive(activeIndex == 0);
        if (swordObject != null) swordObject.SetActive(activeIndex == 1);

        if (CombatDebugHUD.Instance != null)
        {
            CombatDebugHUD.Instance.SetLine(activeIndex == 0 ? "Weapon: Bow" : "Weapon: Sword");
        }
    }
}