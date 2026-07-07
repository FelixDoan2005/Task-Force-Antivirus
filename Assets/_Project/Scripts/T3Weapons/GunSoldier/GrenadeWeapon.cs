using UnityEngine;

public class GrenadeWeapon : MonoBehaviour
{
    [Header("Setup")]
    public Camera playerCamera;
    public Transform throwPoint;
    public GameObject grenadePrefab;

    [Header("Weapon")]
    public float damage = 40f;
    public float minThrowSpeed = 8f;
    public float maxThrowSpeed = 25f;
    public float maxChargeTime = 1.25f;
    public float arcAmount = 0.25f; // upward tilt for a lobbed arc
    public float cooldown = 1.5f;

    [Header("Supply")]
    public int maxGrenades = 3;

    [Header("Trajectory Preview")]
    public LineRenderer trajectoryLine;
    public int trajectorySteps = 30;
    public float trajectoryTimeStep = 0.05f;

    int carriedGrenades;
    float nextThrowTime;
    bool isCharging;
    float chargeStartTime;
    int hitMask;

    public bool IsThrowing { get; private set; }
    public bool IsCharging => isCharging;
    public int CarriedGrenades => carriedGrenades;

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        carriedGrenades = maxGrenades;
        hitMask = ~LayerMask.GetMask("Player");

        if (trajectoryLine == null)
        {
            GameObject lineObj = new("TrajectoryPreview");
            lineObj.transform.SetParent(transform);
            trajectoryLine = lineObj.AddComponent<LineRenderer>();
            trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
            trajectoryLine.startWidth = 0.05f;
            trajectoryLine.endWidth = 0.05f;
            trajectoryLine.useWorldSpace = true;
        }

        trajectoryLine.enabled = false;
    }

    void Update()
    {
        if (isCharging)
            UpdateTrajectoryPreview();
        else
            trajectoryLine.enabled = false;
    }

    void OnDisable()
    {
        if (trajectoryLine != null)
            trajectoryLine.enabled = false;
    }

    void UpdateTrajectoryPreview()
    {
        if (playerCamera == null || throwPoint == null) return;

        Vector3 velocity = GetThrowVelocity(Time.time - chargeStartTime);
        Vector3 gravity = Physics.gravity;
        Vector3 point = throwPoint.position;

        trajectoryLine.enabled = true;
        trajectoryLine.positionCount = trajectorySteps;
        trajectoryLine.SetPosition(0, point);

        float halfStepSq = 0.5f * trajectoryTimeStep * trajectoryTimeStep;

        for (int i = 1; i < trajectorySteps; i++)
        {
            Vector3 nextPoint = point + velocity * trajectoryTimeStep + gravity * halfStepSq;
            velocity += gravity * trajectoryTimeStep;

            if (Physics.Linecast(point, nextPoint, out RaycastHit hit, hitMask))
            {
                trajectoryLine.positionCount = i + 1;
                trajectoryLine.SetPosition(i, hit.point);
                return;
            }

            trajectoryLine.SetPosition(i, nextPoint);
            point = nextPoint;
        }
    }

    Vector3 GetThrowVelocity(float chargeTime)
    {
        float t = Mathf.Clamp01(chargeTime / maxChargeTime);
        float speed = Mathf.Lerp(minThrowSpeed, maxThrowSpeed, t);
        Vector3 throwDir = (playerCamera.transform.forward + Vector3.up * arcAmount).normalized;
        return throwDir * speed;
    }

    public void StartCharge()
    {
        if (carriedGrenades <= 0) return;
        if (Time.time < nextThrowTime) return;

        isCharging = true;
        chargeStartTime = Time.time;
    }

    public void ReleaseCharge()
    {
        IsThrowing = false;

        if (!isCharging) return;
        isCharging = false;

        if (carriedGrenades <= 0) return;
        if (Time.time < nextThrowTime) return;
        if (playerCamera == null || throwPoint == null || grenadePrefab == null) return;

        float chargeTime = Time.time - chargeStartTime;

        nextThrowTime = Time.time + cooldown;
        carriedGrenades--;
        IsThrowing = true;
        Throw(chargeTime);
    }

    void Throw(float chargeTime)
    {
        Vector3 velocity = GetThrowVelocity(chargeTime);
        GameObject grenade = Instantiate(grenadePrefab, throwPoint.position, Quaternion.LookRotation(velocity.normalized, Vector3.up));

        GrenadeProjectile gp = grenade.GetComponent<GrenadeProjectile>();
        if (gp != null)
        {
            gp.throwSpeed = velocity.magnitude;
            gp.damage = damage;
        }
    }
}
