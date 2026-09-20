using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MasterArcher : MonoBehaviour

{
    public GameObject arrowPrefab;

    [Tooltip("The arrow the E volley rains down. Falls back to arrowPrefab if left empty.")]
    public GameObject supportArrowPrefab;

    [Tooltip("The blue-trailed arrow the C ability fires. Falls back to arrowPrefab if empty.")]
    public GameObject focusArrowPrefab;

    [Tooltip("The arrow the ultimate rains down. Falls back to arrowPrefab if left empty.")]
    public GameObject rainArrowPrefab;
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

    [Tooltip("Plays on the bow while the shot is armed, and goes away when she looses it.")]
    public GameObject explosiveArmedVfx;

    [Tooltip("Rides along with the armed arrow in flight.")]
    public GameObject explosiveArrowVfx;

    [Tooltip("Damage the armed arrow deals to anything it flies past on the way, once each. " +
             "This is on top of the blast when it finally lands.")]
    public float explosiveFlyByDamage = 40f;

    [Tooltip("How close it has to pass to catch them.")]
    public float explosiveFlyByRadius = 1.5f;

    [Tooltip("Plays where the arrow detonates.")]
    public GameObject explosiveExplosionVfx;

    [Tooltip("How long the detonation effect is given before it is cleaned up.")]
    public float explosiveVfxLifetime = 3f;

    float explosiveReadyTime;
    bool nextShotExplosive;
    GameObject armedVfxInstance;

    [Header("E - Covering Fire")]
    public float coveringFireCooldown = 20f;

    [Tooltip("How long she keeps the archer line behind her.")]
    public float coveringFireDuration = 8f;

    [Tooltip("How many arrows come down on each shot she lands while this is up.")]
    public int supportArrowCount = 50;

    [Tooltip("How far back the imaginary archers stand. Bigger values flatten the incoming arc.")]
    public float supportOriginDistance = 22f;

    [Tooltip("How high above the target they are firing from - the cliff.")]
    public float supportOriginHeight = 18f;

    [Tooltip("How wide the line of archers is, side to side.")]
    public float supportOriginSpread = 8f;

    [Tooltip("How far the volley scatters around the spot her arrow landed.")]
    public float supportSpreadRadius = 4f;

    [Tooltip("Seconds the arrows take to arrive, so they land as a wave rather than all at once.")]
    public float supportSpawnDuration = 1.2f;

    public float supportArrowDamage = 25f;
    public float supportArrowSpeed = 45f;

    float coveringFireReadyTime;
    float coveringFireEndTime;
    bool isCoveringFireActive;
    public bool IsCoveringFireActive => isCoveringFireActive;

    //---------------------------------------------------------------- C - Focus Fire
    //she has been putting arrows into things; this calls in everything she has marked at once.
    [Header("C - Focus Fire")]
    [Tooltip("How many ordinary shots it takes to charge this. One per shot, not per arrow.")]
    public int autoAttacksToCharge = 10;

    [Tooltip("How long a virus counts as carrying one of her arrows. The arrow itself is cleaned " +
             "up about a second after it lands, so this is what actually decides who gets hit - " +
             "treat it as how long the mark lasts rather than how long the arrow is visible.")]
    public float markDuration = 8f;

    [Tooltip("Arrows rained on each marked target, on top of the single aimed shot.")]
    public int focusVolleyArrowsPerTarget = 5;

    public float focusArrowDamage = 60f;
    public float focusArrowSpeed = 55f;

    [Tooltip("How wide the rain lands around each target.")]
    public float focusVolleySpread = 1.5f;

    [Tooltip("Seconds the volley on each target takes to arrive.")]
    public float focusVolleyDuration = 0.6f;

    [Tooltip("How high above the target its volley is loosed from.")]
    public float focusVolleyHeight = 14f;

    [Tooltip("How far back along her own facing the volley comes from. 0 drops it straight down.")]
    public float focusVolleyDistance = 6f;

    //shared by all the arrows of a single shot, so the first one to land banks the charge and
    //the rest of that volley cannot bank it again
    class AutoShot { public bool counted; }
    AutoShot pendingAutoShot;

    int autoAttackCharge;
    public int AutoAttackCharge => autoAttackCharge;
    public bool IsFocusFireReady => autoAttackCharge >= autoAttacksToCharge;

    //every virus currently carrying one of her arrows, and when that stops being true
    readonly Dictionary<VirusHealthBase, float> markedUntil = new Dictionary<VirusHealthBase, float>();

    //reused rather than allocated, because this is pruned every frame for the HUD
    readonly List<VirusHealthBase> markBuffer = new List<VirusHealthBase>();
    readonly List<VirusHealthBase> markExpired = new List<VirusHealthBase>();

    public int MarkedCount => markBuffer.Count;

    public event System.Action OnFocusFire;

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

    [Tooltip("Volleys like the E one, but ringed all the way around the target and firing inward " +
             "while the rain comes straight down. 0 turns the ring off.")]
    public int ultVolleyDirections = 8;

    public int ultVolleyArrowsPerDirection = 25;

    [Tooltip("How far out the ring of archers stands. This is the massive circle.")]
    public float ultVolleyRingRadius = 45f;

    public float ultVolleyHeight = 25f;

    [Tooltip("How wide each of the ring's firing lines is.")]
    public float ultVolleySpread = 12f;

    public float ultVolleyArrowDamage = 50f;
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
    float EffectiveFireCooldown => fireCooldown;

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

        //pruned every frame so the marked count on the HUD is what C would actually hit
        CollectMarked();

        if (isCoveringFireActive && Time.time >= coveringFireEndTime)
            isCoveringFireActive = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame) TryActivateExplosive();
            if (Keyboard.current.eKey.wasPressedThisFrame) TryActivateCoveringFire();
            if (Keyboard.current.xKey.wasPressedThisFrame) TryActivateUltimate();
            if (Keyboard.current.cKey.wasPressedThisFrame) TryActivateFocusFire();
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

        if (Mouse.current.leftButton.wasReleasedThisFrame && isDrawing && Time.time >= nextFireTime)
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

        //a shot only earns its charge once one of its arrows actually lands in something, so
        //missing costs her the stack. the token below is shared by every arrow in this one shot,
        //which is what stops a five-arrow volley banking five charges off a single enemy.
        pendingAutoShot = nextShotUltimate ? null : new AutoShot();

        if (nextShotUltimate)
        {
            FireUltimate(baseDir, speed);
            nextShotUltimate = false;
        }
        else if (isCoveringFireActive)
        {
            FireCoveringShot(baseDir, speed);
        }
        else if (isAiming)
        {
            FireCircle(baseDir, speed);
        }
        else
        {
            FireSpread(baseDir, speed);
        }

        if (nextShotExplosive) ClearArmedVfx();
        nextShotExplosive = false;

        //arrows already carry a reference to it; this just stops later volleys picking it up
        pendingAutoShot = null;
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

    //one arrow out, like the ultimate's opener - wherever it lands is the spot the line behind
    //her is told to hit. the flat direction of the shot is captured here rather than read at
    //impact, so turning around mid-flight doesn't move the archers.
    void FireCoveringShot(Vector3 baseDir, float speed)
    {
        ArrowProjectile ap = SpawnArrow(firePoint.position, baseDir, speed);
        if (ap == null) return;

        Vector3 flat = new Vector3(baseDir.x, 0f, baseDir.z);
        Vector3 shotDir = flat.sqrMagnitude > 0.0001f ? flat.normalized : transform.forward;

        ap.OnImpact += landingPoint => StartCoroutine(SupportVolley(landingPoint, shotDir));
    }

    //E's volley: one line of archers on the cliff behind her, all aiming at the one spot
    IEnumerator SupportVolley(Vector3 center, Vector3 shotDir)
    {
        return VolleyFrom(center, shotDir, supportArrowCount, supportOriginDistance,
                          supportOriginHeight, supportOriginSpread, supportSpawnDuration,
                          supportArrowDamage, supportSpreadRadius, -1f);
    }

    //a line of archers standing back along 'fromDir', loosing at the one spot. arrows arrive on a
    //steep diagonal and converge, which is what makes it read as massed fire rather than rain.
    //targetRadius is the footprint on the ground they are covering. phase below zero scatters
    //at random inside it; a phase of zero or more lays the hits out evenly instead, offset by
    //that much, so several lines firing together carpet the circle rather than overlap.
    IEnumerator VolleyFrom(Vector3 center, Vector3 fromDir, int count, float distance,
                           float height, float lineSpread, float duration, float damage,
                           float targetRadius, float phase, GameObject prefabOverride = null)
    {
        //back along the shot, up on the cliff
        Vector3 origin = center - fromDir * distance + Vector3.up * height;

        //the line of archers runs across their firing direction
        Vector3 across = Vector3.Cross(Vector3.up, fromDir).normalized;

        return SpawnOverTime(count, duration, i =>
        {
            Vector3 spawnPos = origin
                             + across * Random.Range(-lineSpread, lineSpread)
                             + Vector3.up * Random.Range(-1f, 1f);

            Vector2 scatter;
            if (phase < 0f)
            {
                scatter = Random.insideUnitCircle * targetRadius;
            }
            else
            {
                //sunflower spacing - the golden angle keeps successive points from lining up, and
                //the square root spreads them by equal area instead of bunching at the middle
                float a = phase + i * 2.39996323f;
                float r = targetRadius * Mathf.Sqrt((i + 0.5f) / Mathf.Max(1, count));
                scatter = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }

            Vector3 target = center + new Vector3(scatter.x, 0f, scatter.y);

            //the arrows have gravity on, so firing straight at the spot lands them short and the
            //volley misses. aiming high by the drop over the flight puts them back on it, and
            //the sag on the way in is what gives the shot its arc.
            float flight = Vector3.Distance(spawnPos, target) / Mathf.Max(0.01f, supportArrowSpeed);
            float drop = 0.5f * Mathf.Abs(Physics.gravity.y) * flight * flight;

            SpawnArrow(spawnPos, (target + Vector3.up * drop - spawnPos).normalized,
                       supportArrowSpeed, damage,
                       prefabOverride != null ? prefabOverride : supportArrowPrefab);
        });
    }

    void HandleUltimateImpact(Vector3 landingPoint)
    {
        //the vertical rain is the centrepiece; the ring closes in on it from every side at once
        StartCoroutine(RainArrows(landingPoint));

        for (int i = 0; i < ultVolleyDirections; i++)
        {
            float angle = i * Mathf.PI * 2f / ultVolleyDirections;

            //the direction this line is firing ALONG, so the maths puts them out on the ring
            Vector3 fromDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            //every line covers the whole rain footprint, each starting its pattern at a different
            //angle so eight lines interleave into one even carpet instead of eight copies.
            //the window is the rain's, so the ring runs for exactly as long as the ultimate does.
            StartCoroutine(VolleyFrom(landingPoint, fromDir, ultVolleyArrowsPerDirection,
                                      ultVolleyRingRadius, ultVolleyHeight, ultVolleySpread,
                                      rainSpawnDuration, ultVolleyArrowDamage,
                                      rainSpreadRadius, angle));
        }
    }

    IEnumerator RainArrows(Vector3 center)
    {
        return SpawnOverTime(rainArrowCount, rainSpawnDuration, i =>
        {
            Vector2 randomOffset = Random.insideUnitCircle * rainSpreadRadius;
            Vector3 spawnPos = center + new Vector3(randomOffset.x, rainSpawnHeight, randomOffset.y);

            SpawnArrow(spawnPos, Vector3.down, rainArrowSpeed, rainArrowDamage, rainArrowPrefab);
        });
    }

    //spreads 'count' spawns across 'duration' by the clock rather than by waiting between each
    //one. WaitForSeconds can never wait less than a frame, so asking for 1000 arrows in 2s used
    //to hand out one per frame and quietly stretch to 17s at 60fps - and to half that on a 144Hz
    //machine. going by elapsed time makes the length the same everywhere.
    IEnumerator SpawnOverTime(int count, float duration, System.Action<int> spawn)
    {
        if (count <= 0) yield break;

        if (duration <= 0f)
        {
            for (int i = 0; i < count; i++) spawn(i);
            yield break;
        }

        int spawned = 0;
        float elapsed = 0f;

        while (spawned < count)
        {
            elapsed += Time.deltaTime;

            //however many the schedule says are due by now, even if that is several this frame
            int due = Mathf.Min(count, Mathf.CeilToInt(count * (elapsed / duration)));
            while (spawned < due) spawn(spawned++);

            if (spawned < count) yield return null;
        }
    }

    ArrowProjectile SpawnArrow(Vector3 spawnPos, Vector3 shootDir, float speed, float damageOverride = -1f,
                               GameObject prefabOverride = null)
    {
        GameObject prefab = prefabOverride != null ? prefabOverride : arrowPrefab;
        GameObject arrow = Instantiate(prefab, spawnPos, Quaternion.LookRotation(shootDir, Vector3.up));
        ArrowProjectile ap = arrow.GetComponent<ArrowProjectile>();
        if (ap != null)
        {
            ap.damage = damageOverride >= 0f ? damageOverride : arrowDamage;
            ap.speed = speed;

            //whatever this arrow lodges in counts as marked from then on
            ap.OnStuckIn += MarkTarget;

            //and if it came from one of her ordinary shots, landing it earns the charge
            if (pendingAutoShot != null)
            {
                AutoShot shot = pendingAutoShot;
                ap.OnStuckIn += _ => CountAutoHit(shot);
            }

            if (nextShotExplosive)
            {
                ap.isExplosive = true;
                ap.explosionRadius = explosiveArrowRadius;
                ap.explosionDamage = explosiveArrowDamage;
                ap.explosionVfx = explosiveExplosionVfx;
                ap.explosionVfxLifetime = explosiveVfxLifetime;

                //it now cuts through whatever it passes, not just what it lands on
                ap.passThroughDamage = explosiveFlyByDamage;
                ap.passThroughRadius = explosiveFlyByRadius;

                //parented, so it follows the arrow and dies with it
                VfxSpawner.Spawn(explosiveArrowVfx, arrow.transform.position,
                                 arrow.transform.rotation, arrow.transform);
            }
        }

        Debug.DrawRay(spawnPos, shootDir * arrowRange, Color.red, 0.2f);
        return ap;
    }

    //---------------------------------------------------------------- HUD
    [Header("HUD")]
    public bool showArcherHud = true;
    public int hudFontSize = 22;
    public float hudTopMargin = 24f;

    GUIStyle archerHudStyle;

    //only runs while she is the selected class - the object is switched off otherwise
    void OnGUI()
    {
        if (!showArcherHud) return;

        archerHudStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontStyle = FontStyle.Bold
        };
        archerHudStyle.fontSize = hudFontSize;

        bool ready = IsFocusFireReady;

        string label = ready
            ? $"C READY   ·   {MarkedCount} marked"
            : $"AUTOS  {autoAttackCharge} / {autoAttacksToCharge}";

        if (ready && MarkedCount == 0) label += "   (nothing marked)";

        Rect rect = new Rect(0f, hudTopMargin, Screen.width, hudFontSize * 2f);

        //drawn twice, dark underneath, so it stays readable against a bright floor
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), label, archerHudStyle);

        GUI.color = ready ? new Color(0.35f, 0.7f, 1f) : Color.white;
        GUI.Label(rect, label, archerHudStyle);

        GUI.color = Color.white;
    }

    void OnDisable()
    {
        isDrawing = false;
        isMeleeing = false;
        isCoveringFireActive = false;
        autoAttackCharge = 0;
        pendingAutoShot = null;
        markedUntil.Clear();
        ClearArmedVfx();

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

        //parented to the bow so it tracks her while she lines the shot up
        if (firePoint != null)
        {
            ClearArmedVfx();
            armedVfxInstance = VfxSpawner.Spawn(explosiveArmedVfx, firePoint.position,
                                                firePoint.rotation, firePoint);
        }
    }

    void ClearArmedVfx()
    {
        if (armedVfxInstance != null) Destroy(armedVfxInstance);
        armedVfxInstance = null;
    }

    void TryActivateCoveringFire()
    {
        if (Time.time < coveringFireReadyTime) return;

        coveringFireReadyTime = Time.time + coveringFireCooldown;
        isCoveringFireActive = true;
        coveringFireEndTime = Time.time + coveringFireDuration;
    }

    void CountAutoHit(AutoShot shot)
    {
        if (shot == null || shot.counted) return;

        shot.counted = true;
        autoAttackCharge = Mathf.Min(autoAttackCharge + 1, autoAttacksToCharge);
    }

    void MarkTarget(VirusHealthBase virus)
    {
        if (virus == null) return;
        markedUntil[virus] = Time.time + markDuration;
    }

    //drops anything that died or whose mark ran out, and hands back what is left
    List<VirusHealthBase> CollectMarked()
    {
        markBuffer.Clear();
        markExpired.Clear();

        foreach (var pair in markedUntil)
        {
            if (pair.Key == null || pair.Key.CurrentHealth <= 0f || Time.time > pair.Value)
                markExpired.Add(pair.Key);
            else
                markBuffer.Add(pair.Key);
        }

        foreach (var dead in markExpired) markedUntil.Remove(dead);
        return markBuffer;
    }

    //fires one aimed arrow at every marked virus and rains a small volley on each at the same
    //time. the charge is only spent when there is actually something marked to spend it on.
    void TryActivateFocusFire()
    {
        if (!IsFocusFireReady) return;

        List<VirusHealthBase> targets = CollectMarked();
        if (targets.Count == 0) return;

        autoAttackCharge = 0;
        OnFocusFire?.Invoke();

        Vector3 facing = playerMovement != null ? playerMovement.transform.forward : transform.forward;

        foreach (VirusHealthBase target in targets)
        {
            Vector3 aimAt = target.transform.position + Vector3.up;

            //the straight shot, led for the drop over the distance it has to cover
            Vector3 from = firePoint != null ? firePoint.position : transform.position;
            float flight = Vector3.Distance(from, aimAt) / Mathf.Max(0.01f, focusArrowSpeed);
            float drop = 0.5f * Mathf.Abs(Physics.gravity.y) * flight * flight;

            SpawnArrow(from, (aimAt + Vector3.up * drop - from).normalized,
                       focusArrowSpeed, focusArrowDamage, focusArrowPrefab);

            //and the rain, tight around that same target
            StartCoroutine(VolleyFrom(target.transform.position, facing,
                                      focusVolleyArrowsPerTarget, focusVolleyDistance,
                                      focusVolleyHeight, focusVolleySpread * 2f,
                                      focusVolleyDuration, focusArrowDamage,
                                      focusVolleySpread, -1f, focusArrowPrefab));
        }
    }

    void TryActivateUltimate()
    {
        if (Time.time < ultimateReadyTime) return;

        ultimateReadyTime = Time.time + ultimateCooldown;
        nextShotUltimate = true;
    }
}