using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering;

[RequireComponent(typeof(CharacterController))] //safety net
public class GrapplingHook : MonoBehaviour
{
    [Header("Grapple")]
    public float grappleSpeed = 30f;
    public float grappleCooldown = 2f;
    public float grappleRange = 40f;

    [Header("Weapon Restriction")]
    public WeaponSwitcher weaponSwitcher;
    public int[] allowedWeaponIndices = { 0 }; // 0 = bow, for now

    CharacterController controller;
    bool isGrapple;
    float nextGrappleReadyTime;
    
    public PlayerController playerController;
    public Camera playerCamera;
    Vector3 grapplePoint;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            TryGrapple();

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && isGrapple)
        {
            isGrapple = false;
            playerController.suspendGravity = false;
        }

        if (isGrapple)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TryGrapple();
            }

            Vector3 dir = (grapplePoint - transform.position).normalized;
            Vector3 beforeMove = transform.position;
            controller.Move(grappleSpeed * Time.deltaTime * dir);

            // if we barely moved despite trying to, something (the floor, a wall) blocked us - treat as arrived
            float actualMoved = Vector3.Distance(beforeMove, transform.position);
            float intendedMove = grappleSpeed * Time.deltaTime;
            bool blocked = actualMoved < intendedMove * 0.1f;
            bool closeEnough = Vector3.Distance(transform.position, grapplePoint) < 0.3f;

            if (blocked || closeEnough)
                playerController.suspendGravity = true;
        }
    }

    void TryGrapple()
    {
        if (Time.time < nextGrappleReadyTime) return;
        if (!IsCurrentWeaponAllowed()) return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, grappleRange)) return; //if doesnt hit any object in range

        grapplePoint = hit.point;
        isGrapple = true;
        playerController.suspendGravity = true;
        nextGrappleReadyTime = Time.time + grappleCooldown;
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