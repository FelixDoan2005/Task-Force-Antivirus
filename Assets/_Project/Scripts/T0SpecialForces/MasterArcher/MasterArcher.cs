using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class MasterArcher : MonoBehaviour

{
    public GameObject arrowPrefab;
    [Header("Setup")]
    public Camera playerCamera;
    public Transform firePoint;

    [Tooltip("Needed for the over-the-shoulder camera. Assign the Player's PlayerController.")]
    public PlayerController playerMovement;

    [Tooltip("Optional. Kicks the camera on a melee hit, like the other classes do.")]
    public CameraShake cameraShake;

    [Header("Camera - Over the Shoulder")]
    [Tooltip("He aims down a crosshair, so the camera sits off to one side instead of dead " +
             "behind him. X = right of the player, Y = up. Negative X for the left shoulder.")]
    public Vector2 shoulderOffset = new Vector2(0.7f, 0.25f);

    [Tooltip("How much closer than the default third-person distance. Negative pulls in.")]
    public float cameraCloseness = -0.9f;

    [Header("Weapon")]
    public float arrowRange = 500f;
    public float arrowDamage = 50f;
    public float fireCooldown = 0.5f;
    public int arrowCount = 5;
    public float spreadAngle = 20f; // total degrees the arrows fan across
    public float circleRadius = 0.4f; // ring size for ADS focused-fire mode
    private float nextFireTime = 0f;
    private bool isDrawing = false;
    private float drawStartTime;
    public float minArrowSpeed = 10f;
    public float maxArrowSpeed = 200f;
    public float maxDrawTime = 0.5f;
    public float aimAssistRange = 200f;

    [Header("ADS Zoom")]
    public float zoomFOV = 35f;
    public float zoomSpeed = 10f;
    float defaultFOV;


    [Header("Crosshair")]
    public RectTransform crosshairRect;
    public float crosshairStartScale = 1.6f;   // bigger at rest
    public float crosshairDrawScale  = 0.8f;   // smaller while drawing
    public float crosshairLerpSpeed  = 12f;
    public Vector2 crosshairStartSize = new(16, 16);
    public Vector2 crosshairDrawSize  = new(12, 12);

    [Header("Q - Explosive Arrows")]
    public float explosiveCooldown = 8f;
    public float explosiveArrowRadius = 5f;
    public float explosiveArrowDamage = 100f;
    float explosiveReadyTime;
    bool nextShotExplosive;

    [Header("E - Rapid Fire")]
    public float rapidFireCooldown = 12f;
    public float rapidFireDuration = 5f;
    public float rapidFireActiveCooldown = 0.1f;
    public float rapidFireSpeedMultiplier = 5f;
    float rapidFireReadyTime;
    float rapidFireEndTime;
    bool isRapidFireActive;

    [Header("V - Melee")]
    [Tooltip("Damage of the melee hit. It only ever lands on one enemy.")]
    public float meleeDamage = 100f;

    [Tooltip("How far in front of her the hit reaches.")]
    public float meleeRange = 2.5f;

    [Tooltip("Width of the arc in front of her, in degrees. 90 means 45 either side of straight ahead.")]
    public float meleeArc = 90f;

    public float meleeCooldown = 1.5f;

    [Tooltip("Delay from the keypress to the damage landing - line this up with the swing.")]
    public float meleeHitDelay = 0.3f;

    [Tooltip("How long she is locked out of shooting - line this up with the animation.")]
    public float meleeLockDuration = 0.9f;

    public GameObject meleeHitVfx;
    public float meleeShake = 0.25f;

    float meleeReadyTime;
    bool isMeleeing;
    public bool IsMeleeing => isMeleeing;
    public event System.Action OnMelee;

    [Header("X - The Heavens Open (Ultimate)")]
    public float ultimateCooldown = 60f;
    public int rainArrowCount = 500;
    public float rainSpawnHeight = 25f;
    public float rainSpreadRadius = 4f;
    public float rainSpawnDuration = 1.5f;
    public float rainArrowDamage = 50f;
    public float rainArrowSpeed = 40f;
    float ultimateReadyTime;
    bool nextShotUltimate;

    int hitMask;
    bool isAiming;

    public bool IsAiming => isAiming;

    //how far through the draw she is: 0 the instant the string starts moving, 1 at full power.
    //this is the same curve the arrow's speed uses, so the pose and the shot always agree
    public float DrawProgress01 => isDrawing && maxDrawTime > 0f
        ? Mathf.Clamp01((Time.time - drawStartTime) / maxDrawTime)
        : 0f;
    float EffectiveFireCooldown => isRapidFireActive ? rapidFireActiveCooldown : fireCooldown;

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        hitMask = ~LayerMask.GetMask("Player");
        defaultFOV = playerCamera.fieldOfView;

        if (crosshairRect != null)
        {
            crosshairRect.localScale = Vector3.one;
            crosshairRect.sizeDelta = crosshairStartSize;
        }
    }

    void Update()
    {
        if (Mouse.current == null) return;

        isAiming = Mouse.current.rightButton.isPressed;

        //asserted every frame rather than set once on selection, so a pause or anything else
        //that resets the camera rig can't leave him shooting from the default third-person view
        if (playerMovement != null)
        {
            playerMovement.cameraShoulderOffset = shoulderOffset;
            playerMovement.extraCameraDistance = cameraCloseness;

            //she has crouch animations again, so she opts into the crouch the Cyborg uses
            playerMovement.allowCrouch = true;

            //no sprinting with an arrow on the string - drawing drops her to a walk, which is
            //also the pace the aim-walk animations are built for
            playerMovement.suspendSprint = isDrawing;
        }

        if (isRapidFireActive && Time.time >= rapidFireEndTime)
            isRapidFireActive = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame) TryActivateExplosive();
            if (Keyboard.current.eKey.wasPressedThisFrame) TryActivateRapidFire();
            if (Keyboard.current.xKey.wasPressedThisFrame) TryActivateUltimate();
            if (Keyboard.current.vKey.wasPressedThisFrame) TryMelee();
        }

        if (playerCamera != null)
        {
            float targetFOV = isAiming ? zoomFOV : defaultFOV;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, Time.deltaTime * zoomSpeed);
        }

        // if (Mouse.current.leftButton.wasPressedThisFrame)
        // {
        //     isDrawing = true;
        //     drawStartTime = Time.time;
        //     CombatDebugHUD.Instance.SetLine("Bow: Drawing");
        // }

        if (isRapidFireActive && !isMeleeing && Mouse.current.leftButton.isPressed && Time.time >= nextFireTime)
        {
            Shoot(maxDrawTime);
            nextFireTime = Time.time + EffectiveFireCooldown;
        }

        if (!isRapidFireActive && Mouse.current.leftButton.wasReleasedThisFrame && isDrawing && Time.time >= nextFireTime)
        {
            float drawTime = Time.time - drawStartTime;
            Shoot(drawTime);
            nextFireTime = Time.time + EffectiveFireCooldown;
            isDrawing = false;
        }

        if (!isRapidFireActive && Mouse.current.leftButton.wasReleasedThisFrame && isDrawing && Time.time >= nextFireTime)
        {
            float drawTime = Time.time - drawStartTime;
            Shoot(drawTime);
            nextFireTime = Time.time + EffectiveFireCooldown;
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
            Debug.LogError("MasterArcher: Assign playerCamera and firePoint.");
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
        Vector3 baseDir = (aimPoint - firePoint.position).normalized;

        float t = Mathf.Clamp01(drawTime / maxDrawTime);
        float speed = Mathf.Lerp(minArrowSpeed, maxArrowSpeed, t);
        if (isRapidFireActive)
            speed *= rapidFireSpeedMultiplier;

        if (nextShotUltimate)
        {
            FireUltimate(baseDir, speed);
            nextShotUltimate = false;
        }
        else if (isAiming)
        {
            FireCircle(baseDir, speed);
        }
        else
        {
            FireSpread(baseDir, speed);
        }

        nextShotExplosive = false;
    }

    void FireSpread(Vector3 baseDir, float speed)
    {   
        float startAngle = -spreadAngle / 2f;
        float step = arrowCount > 1 ? spreadAngle / (arrowCount - 1) : 0f;

        for (int i = 0; i < arrowCount; i++)
        {
            float angle = startAngle + step * i;
            Vector3 shootDir = Quaternion.AngleAxis(angle, Vector3.up) * baseDir;

            SpawnArrow(firePoint.position, shootDir, speed);
        }
    }

    void FireCircle(Vector3 baseDir, float speed)
    {
        Vector3 camRight = playerCamera.transform.right;
        Vector3 camUp = playerCamera.transform.up;

        for (int i = 0; i < arrowCount; i++)
        {
            float angle = i * (360f / arrowCount) * Mathf.Deg2Rad;
            Vector3 offset = (Mathf.Cos(angle) * camRight + Mathf.Sin(angle) * camUp) * circleRadius;

            Vector3 spawnPos = firePoint.position + offset;

            SpawnArrow(spawnPos, baseDir, speed);
        }
    }

    void FireUltimate(Vector3 baseDir, float speed)
    {
        ArrowProjectile ap = SpawnArrow(firePoint.position, baseDir, speed);
        if (ap != null)
            ap.OnImpact += HandleUltimateImpact;
    }

    void HandleUltimateImpact(Vector3 landingPoint)
    {
        StartCoroutine(RainArrows(landingPoint));
    }

    IEnumerator RainArrows(Vector3 center)
    {
        float interval = rainArrowCount > 0 ? rainSpawnDuration / rainArrowCount : 0f;

        for (int i = 0; i < rainArrowCount; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * rainSpreadRadius;
            Vector3 spawnPos = center + new Vector3(randomOffset.x, rainSpawnHeight, randomOffset.y);

            SpawnArrow(spawnPos, Vector3.down, rainArrowSpeed, rainArrowDamage);

            yield return new WaitForSeconds(interval);
        }
    }

    ArrowProjectile SpawnArrow(Vector3 spawnPos, Vector3 shootDir, float speed, float damageOverride = -1f)
    {
        GameObject arrow = Instantiate(arrowPrefab, spawnPos, Quaternion.LookRotation(shootDir, Vector3.up));
        ArrowProjectile ap = arrow.GetComponent<ArrowProjectile>();
        if (ap != null)
        {
            ap.damage = damageOverride >= 0f ? damageOverride : arrowDamage;
            ap.speed = speed;

            if (nextShotExplosive)
            {
                ap.isExplosive = true;
                ap.explosionRadius = explosiveArrowRadius;
                ap.explosionDamage = explosiveArrowDamage;
            }
        }

        Debug.DrawRay(spawnPos, shootDir * arrowRange, Color.red, 0.2f);
        return ap;
    }

    void OnDisable()
    {
        isDrawing = false;
        isMeleeing = false;

        //hand the camera rig back, or his shoulder view follows the next class around
        if (playerMovement != null)
        {
            playerMovement.cameraShoulderOffset = Vector2.zero;
            playerMovement.extraCameraDistance = 0f;

            playerMovement.allowCrouch = false;
            playerMovement.suspendSprint = false;
        }

        if (playerCamera != null)
            playerCamera.fieldOfView = defaultFOV;

        if (crosshairRect != null)
        {
            crosshairRect.localScale = Vector3.one * crosshairStartScale;
            crosshairRect.sizeDelta = crosshairStartSize;
        }
    }

    public void StartDraw()
    {
        //no nocking an arrow mid-swing - the melee owns her arms until it finishes
        if (isMeleeing) return;

        isDrawing = true;
        drawStartTime = Time.time;
    }

    public void ReleaseDraw()
    {
        if (isRapidFireActive)
        {
            isDrawing = false;
            return;
        }

        if (!isDrawing || Time.time < nextFireTime) return;

        float drawTime = Time.time - drawStartTime;
        Shoot(drawTime);
        nextFireTime = Time.time + EffectiveFireCooldown;
        isDrawing = false;
    }

    public bool IsDrawing()
    {
        return isDrawing;
    }

    //a quick shove for anything that gets too close. one target only, so it never turns into a
    //crowd clear - that is what the ultimate is for
    void TryMelee()
    {
        if (Time.time < meleeReadyTime || isMeleeing) return;

        meleeReadyTime = Time.time + meleeCooldown;
        StartCoroutine(MeleeSwing());
    }

    IEnumerator MeleeSwing()
    {
        isMeleeing = true;
        isDrawing = false;          // cancels any draw in progress
        OnMelee?.Invoke();

        //the hit lands partway through the swing, not on the keypress
        if (meleeHitDelay > 0f) yield return new WaitForSeconds(meleeHitDelay);

        VirusHealthBase target = FindMeleeTarget();
        if (target != null)
        {
            target.TakeDamage(meleeDamage);

            if (meleeHitVfx != null)
            {
                Collider col = target.GetComponentInChildren<Collider>();
                Vector3 at = col != null ? col.bounds.center : target.transform.position + Vector3.up;
                GameObject fx = Instantiate(meleeHitVfx, at, Quaternion.identity);
                Destroy(fx, 2f);
            }

            if (cameraShake != null && meleeShake > 0f) cameraShake.AddTrauma(meleeShake);
        }

        float rest = meleeLockDuration - meleeHitDelay;
        if (rest > 0f) yield return new WaitForSeconds(rest);

        isMeleeing = false;
    }

    //nearest thing inside the arc in front of her - one target, never a sweep
    VirusHealthBase FindMeleeTarget()
    {
        Transform body = playerMovement != null ? playerMovement.transform : transform;
        Vector3 centre = body.position + Vector3.up;

        VirusHealthBase best = null;
        float bestDistance = float.MaxValue;

        foreach (Collider c in Physics.OverlapSphere(centre, meleeRange))
        {
            VirusHealthBase v = c.GetComponentInParent<VirusHealthBase>();
            if (v == null || v.CurrentHealth <= 0f) continue;

            Vector3 to = v.transform.position - centre;
            to.y = 0f;
            if (Vector3.Angle(body.forward, to) > meleeArc * 0.5f) continue;

            float d = to.sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = v; }
        }

        return best;
    }

    void TryActivateExplosive()
    {
        if (Time.time < explosiveReadyTime) return;

        explosiveReadyTime = Time.time + explosiveCooldown;
        nextShotExplosive = true;
    }

    void TryActivateRapidFire()
    {
        if (Time.time < rapidFireReadyTime) return;

        rapidFireReadyTime = Time.time + rapidFireCooldown;
        isRapidFireActive = true;
        rapidFireEndTime = Time.time + rapidFireDuration;
    }

    void TryActivateUltimate()
    {
        if (Time.time < ultimateReadyTime) return;

        ultimateReadyTime = Time.time + ultimateCooldown;
        nextShotUltimate = true;
    }
}