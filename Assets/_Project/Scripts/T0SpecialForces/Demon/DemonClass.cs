using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

//crowd-control class. everything he does is wide rather than deep - a sustained cone instead of
//a swing, area bombs instead of one hard hit, and beams that pierce a whole line of targets.
//every point of damage he deals heals him a little, so he stays alive by staying in the fight.
public class DemonClass : MonoBehaviour
{
    [Header("Setup")]
    public Camera playerCamera;
    public Transform firePoint;               // where the flame stream and bombs come from
    public CharacterController playerController;
    public PlayerController playerMovement;
    public PlayerHealth playerHealth;
    public WeaponSwitcher weaponSwitcher;
    public CameraShake cameraShake;

    [Header("Perk - Lifesteal")]
    [Tooltip("Fraction of all damage dealt that comes back as health. 0.02 = 2%.")]
    public float lifestealFraction = 0.02f;

    [Tooltip("Prints every heal, so the perk can be verified.")]
    public bool logLifesteal;

    //---------------------------------------------------------------- auto attack: flame cone
    [Header("Auto Attack - Flame Cone")]
    [Tooltip("Damage per second to each virus standing in the cone.")]
    public float flameDamagePerSecond = 40f;

    [Tooltip("How often damage is applied. Smaller is smoother but costs more.")]
    public float flameTickInterval = 0.1f;

    public float flameRange = 14f;

    [Tooltip("Full width of the cone in degrees.")]
    public float flameAngle = 60f;

    [Tooltip("Held while the stream is running, destroyed on release.")]
    public GameObject flameVfx;
    public Vector3 flameVfxPositionOffset;
    public Vector3 flameVfxRotationOffset;
    public float flameVfxScale = 1f;

    float flameTickTimer;
    GameObject activeFlameVfx;
    bool isFiring;
    public bool IsFiring => isFiring;

    //---------------------------------------------------------------- Q: bomb mode
    [Header("Q - Bomb Mode")]
    public float qCooldown = 8f;

    [Tooltip("How long the auto attack stays converted to bombs.")]
    public float bombModeDuration = 6f;

    [Tooltip("Seconds between throws while holding fire.")]
    public float bombInterval = 0.25f;

    public float bombDamage = 60f;
    public float bombRadius = 5f;

    [Tooltip("Furthest he can lob one. Aiming past this drops it short rather than throwing harder.")]
    public float bombMaxRange = 32f;

    [Tooltip("Time in the air. Longer gives a lazier, higher arc.")]
    public float bombFlightTime = 0.9f;

    [Tooltip("Downward pull on the throw. Higher makes the arc sharper and drops it faster.")]
    public float bombGravity = 30f;

    [Tooltip("Leave empty and a plain sphere is used, so it plays before you have art.")]
    public GameObject bombPrefab;
    public GameObject bombExplosionVfx;
    public float bombExplosionVfxScale = 1f;

    float qReadyTime;
    float bombModeUntil;
    float bombTimer;
    public bool IsBombMode => Time.time < bombModeUntil;

    //---------------------------------------------------------------- E: takeover
    [Header("E - Takeover")]
    public float eCooldown = 24f;
    public float takeoverRadius = 20f;
    public float takeoverDuration = 10f;

    [Tooltip("Tank-class viruses are too strong to convert. Off lets him take anything.")]
    public bool lowClassOnly = true;

    public GameObject takeoverVfx;
    public float takeoverVfxScale = 1f;
    public float takeoverVfxLifetime = 2f;

    float eReadyTime;

    //---------------------------------------------------------------- X: laser eyes
    [Header("X - Laser Eyes")]
    public float ultimateCooldown = 70f;

    [Tooltip("Charge time before the beams fire.")]
    public float ultimateWindup = 5f;

    [Tooltip("How long the beams stay on.")]
    public float ultimateBeamDuration = 8f;

    [Tooltip("Damage per second from EACH beam, so double this is the combined rate.")]
    public float beamDamagePerSecond = 80f;

    public float beamRange = 120f;

    [Tooltip("Beams pierce, so a line of enemies all take it.")]
    public float beamThickness = 0.6f;

    [Tooltip("Eye positions relative to the player: X = right, Y = up, Z = forward.")]
    public Vector3 leftEyeOffset = new Vector3(-0.35f, 1.7f, 0.45f);
    public Vector3 rightEyeOffset = new Vector3(0.35f, 1.7f, 0.45f);

    [Tooltip("Swing the camera round to face him during the windup.")]
    public bool frontViewDuringWindup = true;

    [Tooltip("Keep facing him while the beams fire too. Off returns control so you can aim.")]
    public bool frontViewDuringBeams;

    public float ultimateMoveSpeedMultiplier = 0.35f;

    [Tooltip("How fast the camera whips round to his face. Much higher than the normal orbit speed.")]
    public float ultimateCameraSpinSpeed = 18f;

    public float windupShake = 0.35f;
    public float beamShake = 0.15f;

    public GameObject windupVfx;
    public float windupVfxScale = 1f;
    public Color beamColour = new Color(1f, 0.15f, 0.05f);
    public float beamTickInterval = 0.1f;

    float ultimateReadyTime;
    bool isUltimateActive;
    bool beamsFiring;
    public bool IsUltimateActive => isUltimateActive;
    public bool BeamsFiring => beamsFiring;

    LineRenderer beamLeft, beamRight;

    //the camera's normal orbit speed, stashed while the ultimate borrows it. -1 = not borrowed.
    float storedYawLerpSpeed = -1f;

    //---------------------------------------------------------------- flight
    [Header("Flight")]
    [Tooltip("Two space presses inside this window toggle flight.")]
    public float doubleTapWindow = 0.3f;
    public float flightVerticalSpeed = 9f;

    float lastSpaceTime = -99f;
    bool isFlying;
    public bool IsFlying => isFlying;

    //---------------------------------------------------------------- events for the animator
    public event System.Action OnFireStarted;
    public event System.Action OnFireStopped;
    public event System.Action OnBombThrown;
    public event System.Action OnTakeover;
    public event System.Action OnUltimateStarted;
    public event System.Action OnUltimateEnded;
    public event System.Action<bool> OnFlightChanged;

    Vector3 Origin => firePoint != null ? firePoint.position : transform.position;
    Vector3 Forward => playerCamera != null ? playerCamera.transform.forward : transform.forward;

    void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
    }

    //coroutines die when this is disabled, so anything they were going to clean up has to be
    //cleared here - otherwise an interrupted ultimate silently locks the class out, the same
    //trap the BladeMaster hit
    void OnDisable()
    {
        isUltimateActive = false;
        beamsFiring = false;
        isFiring = false;
        bombModeUntil = 0f;

        StopFlameVfx();
        SetBeamsVisible(false);

        RestoreCameraOrbit();

        if (playerMovement != null)
        {
            playerMovement.speedMultiplier = 1f;
            playerMovement.extraCameraDistance = 0f;
            playerMovement.flightMode = false;
        }

        isFlying = false;
    }

    void Update()
    {
        bool isDemonClass = weaponSwitcher == null ||
                            weaponSwitcher.CurrentClass == WeaponSwitcher.PlayerClass.Demon;

        if (!isDemonClass)
        {
            if (isFiring) StopFiring();
            return;
        }

        HandleFlightInput();

        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame) TryActivateQ();
            if (Keyboard.current.eKey.wasPressedThisFrame) TryActivateE();
            if (Keyboard.current.xKey.wasPressedThisFrame) TryActivateUltimate();
        }

        HandleAttackInput();

        //driven from state every frame rather than set once, so pausing mid-ultimate cannot
        //leave the camera or movement stuck
        if (playerMovement != null)
        {
            //no camera pullback during the ultimate - it stays tight on him
            playerMovement.speedMultiplier = isUltimateActive ? ultimateMoveSpeedMultiplier : 1f;
            playerMovement.flightMode = isFlying;
        }
    }

    //---------------------------------------------------------------- flight
    void HandleFlightInput()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current.spaceKey.wasPressedThisFrame) return;

        if (Time.time - lastSpaceTime <= doubleTapWindow)
        {
            isFlying = !isFlying;
            lastSpaceTime = -99f;   // consumed, so a third tap does not immediately toggle back

            if (playerMovement != null)
            {
                playerMovement.flightMode = isFlying;
                playerMovement.flightVerticalSpeed = flightVerticalSpeed;
            }

            OnFlightChanged?.Invoke(isFlying);
        }
        else
        {
            lastSpaceTime = Time.time;
        }
    }

    //---------------------------------------------------------------- auto attack
    void HandleAttackInput()
    {
        bool held = Mouse.current != null && Mouse.current.leftButton.isPressed;

        if (held && !isUltimateActive)
        {
            if (!isFiring) StartFiring();

            if (IsBombMode) TickBombs();
            else TickFlame();
        }
        else if (isFiring)
        {
            StopFiring();
        }
    }

    void StartFiring()
    {
        isFiring = true;
        flameTickTimer = 0f;
        bombTimer = bombInterval; // first bomb goes out immediately

        if (!IsBombMode) StartFlameVfx();
        OnFireStarted?.Invoke();
    }

    void StopFiring()
    {
        isFiring = false;
        StopFlameVfx();
        OnFireStopped?.Invoke();
    }

    void TickFlame()
    {
        if (activeFlameVfx == null) StartFlameVfx();

        flameTickTimer += Time.deltaTime;
        if (flameTickTimer < flameTickInterval) return;

        float damage = flameDamagePerSecond * flameTickTimer;
        flameTickTimer = 0f;

        Vector3 origin = Origin;
        Vector3 forward = Forward;
        List<VirusHealthBase> hitThisTick = new List<VirusHealthBase>();

        foreach (Collider c in Physics.OverlapSphere(origin, flameRange))
        {
            VirusHealthBase vh = c.GetComponentInParent<VirusHealthBase>();
            if (vh == null || hitThisTick.Contains(vh)) continue;
            if (VirusAllegiance.IsConverted(vh)) continue;

            Vector3 toTarget = vh.transform.position - origin;
            if (Vector3.Angle(forward, toTarget) > flameAngle * 0.5f) continue;

            hitThisTick.Add(vh);
            DealDamage(vh, damage);
            Debug.DrawLine(origin, vh.transform.position, new Color(1f, 0.5f, 0f), flameTickInterval);
        }

        DrawCone(origin, forward);
    }

    void TickBombs()
    {
        StopFlameVfx(); // the stream has no business playing while he is lobbing bombs

        bombTimer += Time.deltaTime;
        if (bombTimer < bombInterval) return;
        bombTimer = 0f;

        Vector3 origin = Origin;
        Vector3 target = ThrowTarget(origin);
        Vector3 dir = (target - origin).normalized;

        GameObject go;
        if (bombPrefab != null)
        {
            go = Instantiate(bombPrefab, origin, Quaternion.LookRotation(dir));
        }
        else
        {
            //placeholder so the ability is playable before there is art for it
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "DemonBomb";
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * 0.6f;
            Destroy(go.GetComponent<Collider>());
        }

        DemonBomb bomb = go.GetComponent<DemonBomb>();
        if (bomb == null) bomb = go.AddComponent<DemonBomb>();

        bomb.damage = bombDamage;
        bomb.radius = bombRadius;
        bomb.explosionVfx = bombExplosionVfx;
        bomb.explosionVfxScale = bombExplosionVfxScale;
        bomb.Launch(this, target, bombGravity, bombFlightTime);

        OnBombThrown?.Invoke();
    }

    //where the bomb should land. takes whatever the crosshair is over, then pulls it back to
    //Bomb Max Range if that's further - so aiming at the horizon lobs it as far as he can throw
    //rather than firing it into the distance.
    Vector3 ThrowTarget(Vector3 from)
    {
        if (playerCamera == null) return from + transform.forward * bombMaxRange;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        Vector3 aimPoint = Physics.Raycast(ray, out RaycastHit hit, bombMaxRange * 3f, ~0, QueryTriggerInteraction.Ignore)
            ? hit.point
            : ray.origin + ray.direction * bombMaxRange;

        Vector3 flat = aimPoint - from;
        flat.y = 0f;

        if (flat.magnitude > bombMaxRange)
        {
            //too far - throw as far as he can in that direction, and let it land on the ground
            Vector3 capped = from + flat.normalized * bombMaxRange;

            if (Physics.Raycast(capped + Vector3.up * 50f, Vector3.down, out RaycastHit ground,
                                200f, ~0, QueryTriggerInteraction.Ignore))
                return ground.point;

            capped.y = from.y;
            return capped;
        }

        return aimPoint;
    }

    //aim at whatever the crosshair is over, so bombs converge on the reticle rather than
    //flying parallel to the camera
    Vector3 AimDirection(Vector3 from)
    {
        if (playerCamera == null) return transform.forward;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 aimPoint = Physics.Raycast(ray, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore)
            ? hit.point
            : ray.origin + ray.direction * 300f;

        return (aimPoint - from).normalized;
    }

    void DrawCone(Vector3 origin, Vector3 forward)
    {
        const int rays = 9;
        for (int i = 0; i < rays; i++)
        {
            float t = i / (float)(rays - 1);
            float ang = Mathf.Lerp(-flameAngle * 0.5f, flameAngle * 0.5f, t);
            Vector3 dir = Quaternion.AngleAxis(ang, Vector3.up) * forward;
            Debug.DrawRay(origin, dir * flameRange, new Color(1f, 0.35f, 0f), flameTickInterval);
        }
    }

    void StartFlameVfx()
    {
        if (flameVfx == null || activeFlameVfx != null) return;

        Transform parent = firePoint != null ? firePoint : transform;
        activeFlameVfx = Instantiate(flameVfx, parent);
        activeFlameVfx.transform.localPosition = flameVfxPositionOffset;
        activeFlameVfx.transform.localRotation = Quaternion.Euler(flameVfxRotationOffset);
        if (!Mathf.Approximately(flameVfxScale, 1f))
            activeFlameVfx.transform.localScale *= flameVfxScale;

        foreach (ParticleSystem ps in activeFlameVfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Clear(true);
            ps.Play(true);
        }
    }

    void StopFlameVfx()
    {
        if (activeFlameVfx == null) return;
        Destroy(activeFlameVfx);
        activeFlameVfx = null;
    }

    //---------------------------------------------------------------- Q
    void TryActivateQ()
    {
        if (Time.time < qReadyTime) return;
        if (isUltimateActive) return;

        qReadyTime = Time.time + qCooldown;
        bombModeUntil = Time.time + bombModeDuration;
        bombTimer = bombInterval;

        StopFlameVfx();
    }

    //---------------------------------------------------------------- E
    void TryActivateE()
    {
        if (Time.time < eReadyTime) return;
        if (isUltimateActive) return;

        eReadyTime = Time.time + eCooldown;

        Vector3 centre = playerController != null ? playerController.transform.position : transform.position;
        int converted = 0;

        List<VirusHealthBase> seen = new List<VirusHealthBase>();
        foreach (Collider c in Physics.OverlapSphere(centre, takeoverRadius))
        {
            VirusHealthBase vh = c.GetComponentInParent<VirusHealthBase>();
            if (vh == null || seen.Contains(vh)) continue;
            seen.Add(vh);

            if (vh.GetComponent<VirusAllegiance>() != null) continue;   // already ours
            if (lowClassOnly && vh is TankVirusHealth) continue;        // tanks are too strong to turn

            //hand the allegiance component the scripts it switched off, so it can put them back
            List<MonoBehaviour> toSuspend = new List<MonoBehaviour>();
            foreach (MonoBehaviour mb in vh.GetComponents<MonoBehaviour>())
                if (mb != null && mb != vh && mb.enabled) toSuspend.Add(mb);

            VirusAllegiance allegiance = vh.gameObject.AddComponent<VirusAllegiance>();
            allegiance.Begin(takeoverDuration, toSuspend.ToArray());

            if (takeoverVfx != null)
            {
                GameObject fx = Instantiate(takeoverVfx, vh.transform.position, Quaternion.identity);
                foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Clear(true);
                    ps.Play(true);
                }
                if (!Mathf.Approximately(takeoverVfxScale, 1f))
                    fx.transform.localScale *= takeoverVfxScale;
                Destroy(fx, takeoverVfxLifetime);
            }

            Debug.DrawLine(centre, vh.transform.position, Color.green, 1f);
            converted++;
        }

        OnTakeover?.Invoke();
        Debug.Log($"Demon takeover: converted {converted} virus(es) for {takeoverDuration}s");
    }

    //---------------------------------------------------------------- X
    void TryActivateUltimate()
    {
        if (Time.time < ultimateReadyTime) return;
        if (isUltimateActive) return;

        ultimateReadyTime = Time.time + ultimateCooldown;
        StartCoroutine(LaserEyes());
    }

    IEnumerator LaserEyes()
    {
        isUltimateActive = true;
        StopFiring();
        OnUltimateStarted?.Invoke();

        GameObject charge = null;
        if (windupVfx != null)
        {
            charge = Instantiate(windupVfx, EyeCentre(), Quaternion.identity, transform);
            foreach (ParticleSystem ps in charge.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Clear(true);
                ps.Play(true);
            }
            if (!Mathf.Approximately(windupVfxScale, 1f))
                charge.transform.localScale *= windupVfxScale;
        }

        // ---- windup: the camera whips round to look him in the face
        //the target is set once rather than ramped over the windup - the camera's own easing
        //does the movement, and it's temporarily sped right up so the whip happens in a moment
        //instead of crawling round over five seconds
        if (playerMovement != null && frontViewDuringWindup)
        {
            storedYawLerpSpeed = playerMovement.cameraYawLerpSpeed;
            playerMovement.cameraYawLerpSpeed = ultimateCameraSpinSpeed;
            playerMovement.cameraYawOffset = 180f;
        }

        float t = 0f;
        while (t < ultimateWindup)
        {
            t += Time.deltaTime;

            if (cameraShake != null && windupShake > 0f)
                cameraShake.SetTraumaFloor(Mathf.Lerp(0f, windupShake, t / ultimateWindup));

            yield return null;
        }

        if (charge != null) Destroy(charge);

        // ---- beams
        beamsFiring = true;
        SetBeamsVisible(true);

        if (playerMovement != null && !frontViewDuringBeams)
            playerMovement.cameraYawOffset = 0f;   // hand aiming back to the player

        float tick = 0f;
        float endAt = Time.time + ultimateBeamDuration;

        while (Time.time < endAt)
        {
            tick += Time.deltaTime;
            bool damageThisFrame = tick >= beamTickInterval;

            Vector3 aim = AimPoint();
            FireBeam(beamLeft, EyeWorld(leftEyeOffset), aim, damageThisFrame);
            FireBeam(beamRight, EyeWorld(rightEyeOffset), aim, damageThisFrame);

            if (damageThisFrame) tick = 0f;

            if (cameraShake != null && beamShake > 0f)
                cameraShake.SetTraumaFloor(beamShake);

            yield return null;
        }

        beamsFiring = false;
        SetBeamsVisible(false);

        RestoreCameraOrbit();
        if (cameraShake != null) cameraShake.SetTraumaFloor(0f);

        isUltimateActive = false;
        OnUltimateEnded?.Invoke();
    }

    //puts the camera back where it was: facing forward again, at its normal orbit speed
    void RestoreCameraOrbit()
    {
        if (playerMovement == null) return;

        playerMovement.cameraYawOffset = 0f;

        if (storedYawLerpSpeed >= 0f)
        {
            playerMovement.cameraYawLerpSpeed = storedYawLerpSpeed;
            storedYawLerpSpeed = -1f;
        }
    }

    Vector3 EyeWorld(Vector3 offset)
    {
        Transform t = playerController != null ? playerController.transform : transform;
        return t.position + t.right * offset.x + Vector3.up * offset.y + t.forward * offset.z;
    }

    Vector3 EyeCentre()
    {
        return EyeWorld((leftEyeOffset + rightEyeOffset) * 0.5f);
    }

    Vector3 AimPoint()
    {
        if (playerCamera == null) return transform.position + transform.forward * beamRange;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        return Physics.Raycast(ray, out RaycastHit hit, beamRange, ~0, QueryTriggerInteraction.Ignore)
            ? hit.point
            : ray.origin + ray.direction * beamRange;
    }

    //beams pierce - everything on the line takes damage, which is what makes this crowd control
    //rather than a sniper shot
    void FireBeam(LineRenderer lr, Vector3 from, Vector3 aim, bool applyDamage)
    {
        Vector3 dir = (aim - from).normalized;
        Vector3 end = from + dir * beamRange;

        if (lr != null)
        {
            lr.SetPosition(0, from);
            lr.SetPosition(1, end);
        }

        if (!applyDamage) return;

        float damage = beamDamagePerSecond * beamTickInterval;

        List<VirusHealthBase> seen = new List<VirusHealthBase>();
        foreach (RaycastHit h in Physics.SphereCastAll(from, beamThickness, dir, beamRange, ~0, QueryTriggerInteraction.Ignore))
        {
            VirusHealthBase vh = h.collider.GetComponentInParent<VirusHealthBase>();
            if (vh == null || seen.Contains(vh)) continue;
            if (VirusAllegiance.IsConverted(vh)) continue;
            seen.Add(vh);

            DealDamage(vh, damage);
        }
    }

    void SetBeamsVisible(bool on)
    {
        if (on && beamLeft == null)
        {
            beamLeft = MakeBeam("Beam_L");
            beamRight = MakeBeam("Beam_R");
        }

        if (beamLeft != null) beamLeft.enabled = on;
        if (beamRight != null) beamRight.enabled = on;
    }

    LineRenderer MakeBeam(string beamName)
    {
        GameObject go = new GameObject(beamName);
        go.transform.SetParent(transform, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.startWidth = beamThickness * 1.4f;
        lr.endWidth = beamThickness * 0.7f;

        //unlit so it reads as light rather than a lit tube, and needs no scene lighting
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", beamColour);
        lr.material = mat;
        lr.startColor = beamColour;
        lr.endColor = beamColour;

        return lr;
    }

    //---------------------------------------------------------------- damage + lifesteal
    //every damage source routes through here, so the perk can never be forgotten on a new ability
    public void DealDamage(VirusHealthBase target, float amount)
    {
        if (target == null || amount <= 0f) return;

        //don't credit lifesteal for overkill - heal on what was actually taken off
        float dealt = Mathf.Min(amount, Mathf.Max(0f, target.CurrentHealth));
        target.TakeDamage(amount);

        if (playerHealth == null || lifestealFraction <= 0f || dealt <= 0f) return;

        float heal = dealt * lifestealFraction;
        playerHealth.Heal(heal);

        if (logLifesteal)
            Debug.Log($"Demon lifesteal: +{heal:F1} from {dealt:F1} damage");
    }
}
