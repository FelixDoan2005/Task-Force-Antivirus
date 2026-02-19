using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShooter : MonoBehaviour
{
    public Camera playerCamera;
    public float range = 100f;
    public float damage = 5f;

    void Awake()
    {
        // Auto-find a camera if not assigned
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    void Update()
    {
        if (Mouse.current == null) return; //

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Shoot();
        }
    }

    void Shoot()
    {
        if (playerCamera == null)
        {
            Debug.LogError("PlayerShooter: No camera found/assigned.");
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            Debug.Log("Ray hit: " + hit.collider.name);

            ZombieHealth zh = hit.collider.GetComponentInParent<ZombieHealth>();
            if (zh != null)
            {
                zh.TakeDamage(damage);
                Debug.Log("Hit zombie for " + damage);
            }
        }
        else
        {
            Debug.Log("Ray hit nothing");
        }
    }
}
