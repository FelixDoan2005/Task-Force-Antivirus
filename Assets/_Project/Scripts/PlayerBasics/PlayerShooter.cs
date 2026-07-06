using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShooter : MonoBehaviour
{
    [Header("Setup")]
    public Camera playerCamera;
    public Transform firePoint;

    [Header("Weapon")]
    public float range = 100f;
    public float damage = 5f;

    int hitMask;

    void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }
        else if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        hitMask = ~LayerMask.GetMask("Player");
    }

    void Update()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
            Shoot();
    }

    void Shoot()
    {
        if (playerCamera == null || firePoint == null)
        {
            Debug.LogError("PlayerShooter: Assign playerCamera and firePoint.");
            return;
        }

        // CAMERA AIM
        Ray aimRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 aimPoint;

        if (Physics.Raycast(aimRay, out RaycastHit aimHit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            aimPoint = aimHit.point;
            Debug.DrawLine(aimRay.origin, aimHit.point, Color.green, 0.2f);
        }
        else
        {
            aimPoint = aimRay.origin + aimRay.direction * range;
            Debug.DrawRay(aimRay.origin, aimRay.direction * range, Color.green, 0.2f);
        }

        // WEAPON FIRE
        Vector3 shootDir = (aimPoint - firePoint.position).normalized;
        Ray shotRay = new(firePoint.position, shootDir);

        if (Physics.Raycast(shotRay, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            Debug.DrawLine(shotRay.origin, hit.point, Color.red, 0.2f);

            BasicVirusHealth zh = hit.collider.GetComponentInParent<BasicVirusHealth>();
            if (zh != null)
                zh.TakeDamage(damage);
        }
        else
        {
            Debug.DrawRay(shotRay.origin, shotRay.direction * range, Color.red, 0.2f);
        }
    }
}