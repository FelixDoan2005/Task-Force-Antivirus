using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[RequireComponent(typeof(CharacterController))]
[DefaultExecutionOrder(-100)]
public class DashAbility : MonoBehaviour
{
    [Header("Dash")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 1f;
    public float doubleTapWindow = 0.3f;

    [Header("Weapon Restriction")]
    public WeaponSwitcher weaponSwitcher;
    public int[] allowedWeaponIndices = { 0 }; // 0 = bow, for now

    CharacterController controller;
    Vector3 dashDirection;
    float dashEndTime;
    float nextDashReadyTime;
    bool isDashing;

    float lastWPressTime = -999f;
    float lastAPressTime = -999f;
    float lastSPressTime = -999f;
    float lastDPressTime = -999f;

    public bool IsDashing => isDashing;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (Keyboard.current != null)
        {
            CheckDoubleTap(Keyboard.current.wKey, ref lastWPressTime, transform.forward);
            CheckDoubleTap(Keyboard.current.sKey, ref lastSPressTime, -transform.forward);
            CheckDoubleTap(Keyboard.current.aKey, ref lastAPressTime, -transform.right);
            CheckDoubleTap(Keyboard.current.dKey, ref lastDPressTime, transform.right);
        }

        if (isDashing)
        {
            controller.Move(dashSpeed * Time.deltaTime * dashDirection);

            if (Time.time >= dashEndTime)
                isDashing = false;
        }
    }

    void CheckDoubleTap(KeyControl key, ref float lastPressTime, Vector3 direction)
    {
        if (!key.wasPressedThisFrame) return;

        if (Time.time - lastPressTime <= doubleTapWindow)
            TryDash(direction);

        lastPressTime = Time.time;
    }

    void TryDash(Vector3 direction)
    {
        if (isDashing) return;
        if (Time.time < nextDashReadyTime) return;
        if (!IsCurrentWeaponAllowed()) return;

        dashDirection = direction.normalized;
        isDashing = true;
        dashEndTime = Time.time + dashDuration;
        nextDashReadyTime = Time.time + dashCooldown;
    }

    bool IsCurrentWeaponAllowed()
    {
        if (weaponSwitcher == null) return true;

        int current = weaponSwitcher.ActiveWeaponIndex;
        foreach (int allowed in allowedWeaponIndices)
        {
            if (allowed == current) return true;
        }

        return false;
    }
}
