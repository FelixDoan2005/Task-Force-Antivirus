using UnityEngine;
using UnityEngine.InputSystem;

public class PistolWeapon : MonoBehaviour
{
    [Header("Setup")]
    public Camera playerCamera;
    public Transform firePoint;

    [Header("Weapon")]
    public float damage = 12f;
    public float range = 100f;
    public float fireRate = 4f; // max taps per second
    public float pullOutTime = 0.5f;

    [Header("Ammo")]
    public int magazineSize = 8;
    public float reloadTime = 1.5f;
    public int totalAmmo = 24;
    public int amoNeeded;

    [Header("Tracer")]
    public Material tracerMaterial;
    public Color tracerColor = Color.yellow;
    public float tracerWidth = 0.02f;
    public float tracerLifetime = 0.03f;

    float nextFireTime;
    int hitMask;
    Material runtimeTracerMaterial;

    int currentAmmo;
    bool isReloading;
    float reloadEndTime;
    float readyTime;

    public bool IsFiring { get; private set; }
    public int CurrentAmmo => currentAmmo;
    public bool IsReloading => isReloading;

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        hitMask = ~LayerMask.GetMask("Player");
        currentAmmo = magazineSize;
    }

    void OnEnable()
    {
        readyTime = Time.time + pullOutTime;
    }

    void Update()
    {
        if (isReloading && Time.time >= reloadEndTime)
        {
            isReloading = false;
        }

        if (currentAmmo == 0)
            TryReload();

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            TryReload();
    }

    public void TryFire(bool firePressedThisFrame)
    {
        IsFiring = false;

        if (Time.time < readyTime) return;

        if (isReloading) return;

        if (currentAmmo <= 0) return;

        if (!firePressedThisFrame) return;
        if (Time.time < nextFireTime) return;
        if (playerCamera == null || firePoint == null) return;

        nextFireTime = Time.time + 1f / fireRate;
        currentAmmo--;
        IsFiring = true;
        Shoot();
    }

    public void TryReload()
    {
        if (isReloading || currentAmmo >= magazineSize || totalAmmo == 0) return;

        if (totalAmmo + currentAmmo <= magazineSize)
        {
            currentAmmo += totalAmmo;
            totalAmmo = 0;
        }
        else
        {
            amoNeeded = magazineSize - currentAmmo;
            totalAmmo -= amoNeeded;
            currentAmmo = magazineSize;
        }

        isReloading = true;
        reloadEndTime = Time.time + reloadTime;
    }

    void Shoot()
    {
        Ray aimRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 aimPoint;

        if (Physics.Raycast(aimRay, out RaycastHit aimHit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            aimPoint = aimHit.point;
            Debug.DrawLine(aimRay.origin, aimHit.point, Color.green, 0.1f);
        }
        else
        {
            aimPoint = aimRay.origin + aimRay.direction * range;
            Debug.DrawRay(aimRay.origin, aimRay.direction * range, Color.green, 0.1f);
        }

        Vector3 shootDir = (aimPoint - firePoint.position).normalized;
        Ray shotRay = new(firePoint.position, shootDir);

        if (Physics.Raycast(shotRay, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            Debug.DrawLine(shotRay.origin, hit.point, Color.yellow, 0.1f);
            SpawnTracer(shotRay.origin, hit.point);

            VirusHealthBase vh = hit.collider.GetComponentInParent<VirusHealthBase>();
            if (vh != null)
                vh.TakeDamage(damage);
        }
        else
        {
            Debug.DrawRay(shotRay.origin, shotRay.direction * range, Color.yellow, 0.1f);
            SpawnTracer(shotRay.origin, shotRay.origin + shotRay.direction * range);
        }
    }

    void SpawnTracer(Vector3 start, Vector3 end)
    {
        if (runtimeTracerMaterial == null)
            runtimeTracerMaterial = tracerMaterial != null ? tracerMaterial : new Material(Shader.Find("Sprites/Default"));

        GameObject tracer = new("BulletTracer");
        LineRenderer lr = tracer.AddComponent<LineRenderer>();
        lr.material = runtimeTracerMaterial;
        lr.startColor = tracerColor;
        lr.endColor = tracerColor;
        lr.startWidth = tracerWidth;
        lr.endWidth = tracerWidth;
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        Destroy(tracer, tracerLifetime);
    }
}
