using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponSwitcher : MonoBehaviour
{
    public enum PlayerClass { Bow, Sword, Riflemen }

    [Header("Assign your weapon GameObjects here")]
    [SerializeField] private GameObject bowObject;
    [SerializeField] private GameObject swordObject;
    [SerializeField] private GameObject rifleObject;
    [SerializeField] private GameObject pistolObject;
    [SerializeField] private GameObject grenadeObject;
    [SerializeField] private SwordWeapon swordWeapon;
    [SerializeField] private MasterArcher archer;
    [SerializeField] private RifleWeapon rifle;
    [SerializeField] private PistolWeapon pistol;
    [SerializeField] private GrenadeWeapon grenade;
    [SerializeField] private CrosshairTarget crosshairTarget;

    private PlayerClass currentClass = PlayerClass.Bow;
    private int riflemenWeaponIndex; // 0 = rifle, 1 = pistol, 2 = grenade - only meaningful within Riflemen class
    private float attackUntilTime;

    //animations
    public bool IsAttacking { get; private set; }
    public bool IsDrawingBow { get; private set; }

    public PlayerClass CurrentClass => currentClass;
    public int ActiveWeaponIndex => (int)currentClass; // kept for DashAbility/GrapplingHook's "Bow only" (index 0) checks

    private void Start()
    {
        SetActive(0);
    }

    private void Update()
    {
        IsAttacking = Time.time < attackUntilTime;
        IsDrawingBow = (currentClass == PlayerClass.Bow && archer != null && archer.IsDrawing());

        if (Keyboard.current == null) return;

        //switching sub-weapons only works within the Riflemen class - classes themselves only change via the pause menu selector
        if (currentClass == PlayerClass.Riflemen)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) SwitchRiflemenWeapon(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) SwitchRiflemenWeapon(1);
            if (Keyboard.current.qKey.wasPressedThisFrame) SwitchRiflemenWeapon(2);
        }

        //sword
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && currentClass == PlayerClass.Sword)
        {
            swordWeapon.Attack();
            attackUntilTime = Time.time + 0.15f;
        }

        //bow
        if (Mouse.current != null && currentClass == PlayerClass.Bow)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
                archer.StartDraw();

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                archer.ReleaseDraw();
                attackUntilTime = Time.time + 0.15f;
            }
        }

        //rifle
        if (currentClass == PlayerClass.Riflemen && riflemenWeaponIndex == 0 && rifle != null)
        {
            bool triggerHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
            rifle.TryFire(triggerHeld);
            if (rifle.IsFiring)
                attackUntilTime = Time.time + 0.15f;
        }

        //pistol (tap fire only)
        if (currentClass == PlayerClass.Riflemen && riflemenWeaponIndex == 1 && pistol != null)
        {
            bool firePressedThisFrame = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            pistol.TryFire(firePressedThisFrame);
            if (pistol.IsFiring)
                attackUntilTime = Time.time + 0.15f;
        }

        //grenade (charge + release with left mouse, like the bow)
        if (currentClass == PlayerClass.Riflemen && riflemenWeaponIndex == 2 && grenade != null && Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
                grenade.StartCharge();

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                grenade.ReleaseCharge();
                if (grenade.IsThrowing)
                    attackUntilTime = Time.time + 0.15f;
            }
        }

        if (CombatDebugHUD.Instance != null)
        {
            string line = ClassName();

            if (currentClass == PlayerClass.Riflemen)
            {
                if (riflemenWeaponIndex == 0 && rifle != null)
                    line += rifle.IsReloading ? " Reloading" : $" {rifle.CurrentAmmo}/{rifle.magazineSize} {rifle.totalAmmo}";
                if (riflemenWeaponIndex == 1 && pistol != null)
                    line += pistol.IsReloading ? " Reloading" : $" {pistol.CurrentAmmo}/{pistol.magazineSize} {pistol.totalAmmo}";
                if (riflemenWeaponIndex == 2 && grenade != null)
                    line += grenade.IsCharging ? " Charging" : $" {grenade.CarriedGrenades}/{grenade.maxGrenades}";
            }

            CombatDebugHUD.Instance.SetLine(line);
        }
    }

    private string ClassName()
    {
        return currentClass switch
        {
            PlayerClass.Bow => "Bow",
            PlayerClass.Sword => "Sword",
            PlayerClass.Riflemen => riflemenWeaponIndex switch
            {
                0 => "Riflemen (Rifle)",
                1 => "Riflemen (Pistol)",
                2 => "Riflemen (Grenade)",
                _ => "Riflemen"
            },
            _ => "Unknown"
        };
    }

    void SwitchRiflemenWeapon(int index)
    {
        riflemenWeaponIndex = index;
        UpdateActiveObjects();
    }

    public void SetActive(int classIndex)
    {
        currentClass = (PlayerClass)classIndex;
        riflemenWeaponIndex = 0;
        UpdateActiveObjects();
    }

    public void SetWeaponInputEnabled(bool value)
    {
        enabled = value; // stops this script's own Update() (sword/rifle/pistol/grenade/bow-draw calls)

        if (archer != null)
            archer.enabled = value; // MasterArcher self-polls mouse input independently, so disable it directly too
    }

    void UpdateActiveObjects()
    {
        bool isBow = currentClass == PlayerClass.Bow;
        bool isSword = currentClass == PlayerClass.Sword;
        bool isRiflemen = currentClass == PlayerClass.Riflemen;
        bool rifleActive = isRiflemen && riflemenWeaponIndex == 0;
        bool pistolActive = isRiflemen && riflemenWeaponIndex == 1;
        bool grenadeActive = isRiflemen && riflemenWeaponIndex == 2;

        if (bowObject != null) bowObject.SetActive(isBow);
        if (swordObject != null) swordObject.SetActive(isSword);
        if (rifleObject != null) rifleObject.SetActive(rifleActive);
        if (pistolObject != null) pistolObject.SetActive(pistolActive);
        if (grenadeObject != null) grenadeObject.SetActive(grenadeActive);

        if (crosshairTarget != null)
            crosshairTarget.SetWeaponMode(isSword ? 1 : (rifleActive ? 2 : 0));

        if (CombatDebugHUD.Instance != null)
        {
            CombatDebugHUD.Instance.SetLine($"Class: {ClassName()}");
        }
    }
}
