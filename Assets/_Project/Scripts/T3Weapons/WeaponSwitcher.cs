using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponSwitcher : MonoBehaviour
{
    [Header("Assign your weapon GameObjects here")]
    [SerializeField] private GameObject bowObject;
    [SerializeField] private GameObject swordObject;
    [SerializeField] private GameObject rifleObject;
    [SerializeField] private GameObject pistolObject;
    [SerializeField] private SwordWeapon swordWeapon;
    [SerializeField] private Archer archer;
    [SerializeField] private RifleWeapon rifle;
    [SerializeField] private PistolWeapon pistol;
    [SerializeField] private CrosshairTarget crosshairTarget;

    private int activeIndex = 0; // 0 = bow, 1 = sword, 2 = rifle, 3 = pistol
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
        if (Keyboard.current.digit3Key.wasPressedThisFrame) SetActive(2);
        if (Keyboard.current.digit4Key.wasPressedThisFrame) SetActive(3);

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

        //rifle
        if (activeIndex == 2 && rifle != null)
        {
            bool triggerHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
            rifle.TryFire(triggerHeld);
            if (rifle.IsFiring)
                attackUntilTime = Time.time + 0.15f;
        }

        //pistol (tap fire only)
        if (activeIndex == 3 && pistol != null)
        {
            bool firePressedThisFrame = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            pistol.TryFire(firePressedThisFrame);
            if (pistol.IsFiring)
                attackUntilTime = Time.time + 0.15f;
        }

        if (CombatDebugHUD.Instance != null)
        {
            string line = WeaponName();
            if (activeIndex == 2 && rifle != null)
                line += rifle.IsReloading ? " Reloading" : $" {rifle.CurrentAmmo}/{rifle.magazineSize} {rifle.totalAmmo}";
            if (activeIndex == 3 && pistol != null)
                line += pistol.IsReloading ? " Reloading" : $" {pistol.CurrentAmmo}/{pistol.magazineSize} {pistol.totalAmmo}";

            CombatDebugHUD.Instance.SetLine(line);
        }
    }

    private string WeaponName()
    {
        return activeIndex switch
        {
            0 => "Bow",
            1 => "Sword",
            2 => "Rifle",
            3 => "Pistol",
            _ => "Unknown"
        };
    }

    private void SetActive(int index)
    {
        activeIndex = index;

        if (bowObject != null) bowObject.SetActive(activeIndex == 0);
        if (swordObject != null) swordObject.SetActive(activeIndex == 1);
        if (rifleObject != null) rifleObject.SetActive(activeIndex == 2);
        if (pistolObject != null) pistolObject.SetActive(activeIndex == 3);

        if (crosshairTarget != null) crosshairTarget.SetWeaponMode(activeIndex);

        if (CombatDebugHUD.Instance != null)
        {
            CombatDebugHUD.Instance.SetLine($"Weapon: {WeaponName()}");
        }
    }
}