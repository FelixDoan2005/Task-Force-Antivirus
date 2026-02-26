using UnityEngine;
using UnityEngine.InputSystem;

public class Archer : MonoBehaviour

{
    public GameObject arrowPrefab;
    [Header("Setup")]
    public Camera playerCamera;
    public Transform firePoint;

    [Header("Weapon")]
    public float arrowRange = 60f;
    public float arrowDamage = 3f;
    public float fireCooldown = 0.5f;
    private float nextFireTime = 0f;
    private bool isDrawing = false;
    private float drawStartTime;
    public float minArrowSpeed = 10f;
    public float maxArrowSpeed = 100f;
    public float maxDrawTime = 1.25f;
    public float aimAssistRange = 200f;


    [Header("Crosshair")]
    public RectTransform crosshairRect;
    public float crosshairStartScale = 1.6f;   // bigger at rest
    public float crosshairDrawScale  = 0.4f;   // smaller while drawing
    public float crosshairLerpSpeed  = 12f;
    public Vector2 crosshairStartSize = new(16, 16);
    public Vector2 crosshairDrawSize  = new(10, 10);

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

        if (crosshairRect != null)
        {
            crosshairRect.localScale = Vector3.one;
            crosshairRect.sizeDelta = crosshairStartSize;
        }
    }

    void Update()
    {
        if (Mouse.current == null) return;

        // if (Mouse.current.leftButton.wasPressedThisFrame)
        // {
        //     isDrawing = true;
        //     drawStartTime = Time.time;
        //     CombatDebugHUD.Instance.SetLine("Bow: Drawing");
        // }

        if (Mouse.current.leftButton.wasReleasedThisFrame && isDrawing && Time.time >= nextFireTime)
        {
            float drawTime = Time.time - drawStartTime;
            Shoot(drawTime);
            nextFireTime = Time.time + fireCooldown;
            isDrawing = false;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && isDrawing && Time.time >= nextFireTime)
        {
            float drawTime = Time.time - drawStartTime;
            Shoot(drawTime);
            nextFireTime = Time.time + fireCooldown;
            isDrawing = false;
            CombatDebugHUD.Instance.SetLine("Weapon: Bow");
        }

        if (crosshairRect != null)
        {
            float draw01 = isDrawing ? Mathf.Clamp01((Time.time - drawStartTime) / maxDrawTime) : 0f;

            Vector2 targetSize = Vector2.Lerp(crosshairStartSize, crosshairDrawSize, draw01);
            crosshairRect.sizeDelta = targetSize;

            float targetScale = Mathf.Lerp(crosshairStartScale, crosshairDrawScale, draw01);
            crosshairRect.localScale = Vector3.one * targetScale;
        }
    }

    void Shoot(float drawTime)
    {
        if (playerCamera == null || firePoint == null)
        {
            Debug.LogError("PlayerShooter: Assign playerCamera and firePoint.");
            return;
        }

        // CAMERA AIM
        Ray aimRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 aimPoint;

        if (Physics.Raycast(aimRay, out RaycastHit aimHit, aimAssistRange, hitMask, QueryTriggerInteraction.Ignore))
        {
            aimPoint = aimHit.point;
            Debug.DrawLine(aimRay.origin, aimHit.point, Color.green, 0.2f);
        }
        else
        {
            aimPoint = aimRay.origin + aimRay.direction * arrowRange;
            Debug.DrawRay(aimRay.origin, aimRay.direction * arrowRange, Color.green, 0.2f);
        }
        Debug.DrawLine(firePoint.position, aimPoint, Color.yellow, 0.1f);
        

        // WEAPON FIRE
        Vector3 shootDir = (aimPoint - firePoint.position).normalized;
        GameObject arrow = Instantiate(arrowPrefab, firePoint.position, Quaternion.LookRotation(shootDir, Vector3.up));
        ArrowProjectile ap = arrow.GetComponent<ArrowProjectile>();

        float t = Mathf.Clamp01(drawTime / maxDrawTime);
        float speed = Mathf.Lerp(minArrowSpeed, maxArrowSpeed, t);
        if (ap != null)
        {
            ap.damage = arrowDamage;
            ap.speed = speed;
        }

        Ray shotRay = new(firePoint.position, shootDir);
        Debug.DrawRay(shotRay.origin, shotRay.direction * arrowRange, Color.red, 0.2f);
    }

    void OnDisable()
    {
        isDrawing = false;

        if (crosshairRect != null)
        {
            crosshairRect.localScale = Vector3.one * crosshairStartScale;
            crosshairRect.sizeDelta = crosshairStartSize;
        }
    }

    public void StartDraw()
    {
        isDrawing = true;
        drawStartTime = Time.time;
    }

    public void ReleaseDraw()
    {
        if (!isDrawing || Time.time < nextFireTime) return;

        float drawTime = Time.time - drawStartTime;
        Shoot(drawTime);
        nextFireTime = Time.time + fireCooldown;
        isDrawing = false;
    }

    public bool IsDrawing()
    {
        return isDrawing;
    }
}