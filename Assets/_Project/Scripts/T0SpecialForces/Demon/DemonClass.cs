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

    //---------------------------------------------------------------- auto attack: damage waves
    [Header("Auto Attack - Damage Wave")]
    [Tooltip("Damage each virus takes from a wave. A wave hits any one target only once.")]
    public float waveDamage = 45f;

    [Tooltip("How long a wave lives before it fades. One click, one wave.")]
    public float waveDuration = 3f;

    [Tooltip("How fast the wave travels away from him.")]
    public float waveSpeed = 18f;

    [Tooltip("How wide the wave is - this is the crowd-control part.")]
    public float waveWidth = 9f;

    [Tooltip("Vertical thickness, so it still catches things on slopes and steps.")]
    public float waveHeight = 3f;

    [Tooltip("Minimum gap between clicks. Waves can overlap if this is shorter than Wave Duration.")]
    public float waveCooldown = 0.4f;

    [Tooltip("Travels along with the wave. Destroyed when the wave expires.")]
    public GameObject waveVfx;

    [Tooltip("Relative to the wave: X = right, Y = up, Z = along its travel direction.")]
    public Vector3 waveVfxPositionOffset;
    public Vector3 waveVfxRotationOffset;
    public float waveVfxScale = 1f;

    [Tooltip("Draws the wave's box each frame so its size is readable while tuning.")]
    public bool drawWaveBounds = true;

    float nextWaveTime;
    bool isFiring;   // true while at least one wave is still travelling
    int liveWaves;
    public bool IsFiring => isFiring;

    //---------------------------------------------------------------- Q: explosive orb
    [Header("Q - Explosive Orb")]
    public float qCooldown = 8f;

    [Tooltip("Damage on impact, the moment it lands.")]
    public float bombDamage = 200f;

    [Tooltip("Blast radius on impact.")]
    public float bombRadius = 12f;

    [Tooltip("Seconds after the impact before the second, bigger blast.")]
    public float bombSecondaryDelay = 1f;

    [Tooltip("Damage of the delayed blast. Standing in the crater after the first hit is a mistake.")]
    public float bombSecondaryDamage = 320f;

    [Tooltip("Radius of the delayed blast - wider than the impact.")]
    public float bombSecondaryRadius = 18f;

    [Tooltip("Furthest he can throw it. Aiming past this drops it short rather than throwing harder.")]
    public float bombMaxRange = 40f;

    [Tooltip("Time in the air. Longer gives a lazier, higher arc.")]
    public float bombFlightTime = 1.1f;

    [Tooltip("Downward pull on the throw. Higher makes the arc sharper and drops it faster.")]
    public float bombGravity = 30f;

    [Tooltip("Leave empty and a plain sphere is used, so it plays before you have art.")]
    public GameObject bombPrefab;

    [Tooltip("Size of the placeholder sphere, and of your prefab if you want it scaled up.")]
    public float bombScale = 1.6f;

    [Tooltip("Played on impact.")]
    public GameObject bombExplosionVfx;
    public float bombExplosionVfxScale = 2f;

    [Tooltip("Played on the delayed blast. Leave empty for no effect on that stage.")]
    public GameObject bombSecondaryVfx;
    public float bombSecondaryVfxScale = 3f;

    [Tooltip("Camera trauma on impact.")]
    public float bombShake = 0.35f;

    [Tooltip("Camera trauma on the delayed blast.")]
    public float bombSecondaryShake = 0.7f;

    [Tooltip("Wait between pressing Q and the orb leaving his hand, so the throw animation " +
             "gets to play first. The cooldown starts on the press, not on the launch.")]
    public float qLaunchDelay = 1f;

    float qReadyTime;

    //---------------------------------------------------------------- E: takeover
    [Header("E - Takeover")]
    public float eCooldown = 24f;
    public float takeoverRadius = 20f;
    public float takeoverDuration = 10f;

    [Tooltip("How long they stand frozen after the takeover ends, before turning hostile again. " +
             "A window to kill them while they can't fight back.")]
    public float takeoverFreezeDuration = 3f;

    [Tooltip("How long E roots him in place - line this up with the animation length.")]
    public float eLockDuration = 3.53f;

    [Tooltip("Delay from pressing E to the viruses actually turning - line this up with the " +
             "moment in the animation where the takeover lands.")]
    public float takeoverDelay = 1.2f;

    [Tooltip("Tank-class viruses are too strong to convert. Off lets him take anything.")]
    public bool lowClassOnly = true;

    public GameObject takeoverVfx;
    public float takeoverVfxScale = 1f;
    public float takeoverVfxLifetime = 2f;

    float eReadyTime;
    bool isEAbilityActive;
    public bool IsEAbilityActive => isEAbilityActive;

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

    [Tooltip("Where each beam starts, relative to the player: X = right, Y = up, Z = forward. " +
             "Pushed out in front of his face so the effect isn't spawning inside his head - " +
             "he renders 2.89 units tall, so eye level is around 2.4, not 1.7.")]
    public Vector3 leftEyeOffset = new Vector3(-0.28f, 2.45f, 0.9f);
    public Vector3 rightEyeOffset = new Vector3(0.28f, 2.45f, 0.9f);

    [Tooltip("Swing the camera round to face him during the windup.")]
    public bool frontViewDuringWindup = true;

    [Tooltip("Keep facing him while the beams fire too. Off returns control so you can aim.")]
    public bool frontViewDuringBeams;

    public float ultimateMoveSpeedMultiplier = 0.35f;

    [Tooltip("How fast the camera whips round to his face. Much higher than the normal orbit speed.")]
    public float ultimateCameraSpinSpeed = 18f;

    public float windupShake = 0.35f;
    public float beamShake = 0.15f;

    [Tooltip("Spawned at his eyes for the charge-up and destroyed when the beams fire.")]
    public GameObject windupVfx;

    [Tooltip("Relative to the player: X = right, Y = up, Z = forward. " +
             "Leave at zero to sit exactly between the two eye points.")]
    public Vector3 windupVfxPositionOffset;

    [Tooltip("Turns the effect if it doesn't face the way you want out of the box.")]
    public Vector3 windupVfxRotationOffset;

    public float windupVfxScale = 1f;

    [Tooltip("Parent it to the player so it follows him as he turns. Off leaves it where it spawned.")]
    public bool windupVfxFollowsPlayer = true;

    [Tooltip("One is spawned per eye and follows the beam, pointing down it. Leave empty and " +
             "you get the debug lines only.")]
    public GameObject beamVfx;

    [Tooltip("Nudges the effect off the beam origin. X = right of the player, Y = up, " +
             "Z = along the beam. Use it to bring the effect down toward his hands.")]
    public Vector3 beamVfxPositionOffset;

    [Tooltip("Turns the effect if its own forward axis isn't down the beam.")]
    public Vector3 beamVfxRotationOffset;
    public float beamVfxScale = 1f;

    [Tooltip("Forces every particle system in the beam effect to loop, so a one-shot prefab " +
             "holds for the whole 8 seconds instead of burning out after its own duration.")]
    public bool loopBeamVfx = true;

    [Tooltip("Draws the damage line and a marker on each target that is being burned.")]
    public bool drawBeamDebug = true;

    public float beamTickInterval = 0.1f;

    float ultimateReadyTime;
    bool isUltimateActive;
    bool beamsFiring;
    public bool IsUltimateActive => isUltimateActive;
    public bool BeamsFiring => beamsFiring;

    GameObject beamFxLeft, beamFxRight;

    //the camera's normal orbit speed, stashed while the ultimate borrows it. -1 = not borrowed.
    float storedYawLerpSpeed = -1f;

    //---------------------------------------------------------------- camera
    [Header("Camera - Over the Shoulder")]
    [Tooltip("He aims down a crosshair, so the camera sits off to one side instead of dead " +
             "behind him. X = right of the player, Y = up. Negative X puts it over the left shoulder.")]
    public Vector2 shoulderOffset = new Vector2(0.7f, 0.25f);

    [Tooltip("How much closer than the default third-person distance. Negative pulls in.")]
    public float cameraCloseness = -0.9f;

    //---------------------------------------------------------------- C: soul harvest
    [Header("C - Soul Harvest")]
    [Tooltip("Kills needed to gain one tier. Five tiers means 50 kills for a full-power blast.")]
    public int killsPerTier = 10;

    [Tooltip("How many tiers there are. The arrays below are indexed by tier, so changing this resizes them.")]
    public int cMaxTiers = 5;

    public float cCooldown = 10f;

    [Tooltip("How long C locks out other actions - line this up with the animation length.")]
    public float cLockDuration = 1f;

    [Tooltip("Delay from pressing C to the blast landing - line this up with the animation.")]
    public float cDamageDelay = 0.4f;

    [Tooltip("Off lets C be cast with no souls banked, using the tier 1 values.")]
    public bool cRequiresSouls = true;

    [Tooltip("Damage at each tier: element 0 = tier 1, element 4 = tier 5. Same as the BladeMaster.")]
    public float[] cDamagePerTier = { 40f, 70f, 110f, 165f, 240f };

    [Tooltip("Blast radius at each tier. Doubled from the BladeMaster's - he is the crowd-control class.")]
    public float[] cRadiusPerTier = { 8f, 10f, 12f, 15f, 18f };

    [Tooltip("Camera trauma at each tier.")]
    public float[] cShakePerTier = { 0.2f, 0.3f, 0.4f, 0.55f, 0.75f };

    [Tooltip("Spawned on the player when the blast goes off.")]
    public GameObject cVfx;
    public Vector3 cVfxPositionOffset;
    public Vector3 cVfxRotationOffset;

    [Tooltip("Size of the cast effect at each tier. 0 falls back to 1.")]
    public float[] cVfxScalePerTier = { 1f, 1.4f, 1.8f, 2.3f, 3f };
    public float cVfxLifetime = 2.5f;

    [Tooltip("Spawned on each virus caught in the blast.")]
    public GameObject cHitVfx;
    public float cHitVfxScale = 1f;
    public float cHitVfxLifetime = 2f;

    [Tooltip("TEMPORARY: on-screen soul counter.")]
    public bool showSoulHud = true;
    public int soulHudFontSize = 26;
    public float soulHudTopMargin = 24f;

    [Tooltip("Prints every soul gained and every blast.")]
    public bool logSouls;

    [Tooltip("TESTING: start with a full soul bar so C can be tried without farming 50 kills. " +
             "Turn off before shipping.")]
    public bool startWithMaxSouls = true;

    int souls;
    float cReadyTime;
    bool isCAbilityActive;

    public int Souls => souls;
    public bool IsCAbilityActive => isCAbilityActive;

    //how much power is banked right now, 0 to cMaxTiers
    public int CurrentTier => Mathf.Clamp(killsPerTier > 0 ? souls / killsPerTier : 0, 0, cMaxTiers);

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
    public event System.Action OnCAbility;
    public event System.Action OnUltimateStarted;
    public event System.Action OnUltimateEnded;
    public event System.Action<bool> OnFlightChanged;

    Vector3 Origin => firePoint != null ? firePoint.position : transform.position;
    Vector3 Forward => playerCamera != null ? playerCamera.transform.forward : transform.forward;

    void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;

        if (startWithMaxSouls)
            souls = killsPerTier * cMaxTiers;
    }

    //coroutines die when this is disabled, so anything they were going to clean up has to be
    //cleared here - otherwise an interrupted ultimate silently locks the class out, the same
    //trap the BladeMaster hit
    void OnDisable()
    {
        //a pause disables the component and leaves the object active; a class swap deactivates
        //the object. only the swap should undo everything.
        bool switchedAway = !gameObject.activeInHierarchy;

        //all three of these are run by coroutines which clear them on the way out, and each one
        //roots or freezes him while set. that exit is not guaranteed to arrive, so they are
        //always dropped rather than risking a permanently stuck ability.
        isUltimateActive = false;
        isCAbilityActive = false;
        isEAbilityActive = false;

        //re-derived from input every frame
        beamsFiring = false;
        isFiring = false;

        SetBeamsVisible(false);

        if (!switchedAway) return;

        RestoreCameraOrbit();

        if (playerMovement != null)
        {
            playerMovement.speedMultiplier = 1f;
            playerMovement.extraCameraDistance = 0f;
            playerMovement.cameraShoulderOffset = Vector2.zero;
            playerMovement.suspendJump = false;
            playerMovement.suspendDash = false;
            playerMovement.flightMode = false;
        }

        //flight is a plain toggle he holds, not something a coroutine owns, so pausing mid-air
        //leaves him flying - it is only dropped when another class takes over
        isFlying = false;
    }

    void Update()
    {
        bool isDemonClass = weaponSwitcher == null ||
                            weaponSwitcher.CurrentClass == WeaponSwitcher.PlayerClass.Demon;

        if (!isDemonClass)
        {

            //another class is up - give the camera rig back so his shoulder view doesn't linger
            if (playerMovement != null && playerMovement.cameraShoulderOffset != Vector2.zero)
            {
                playerMovement.cameraShoulderOffset = Vector2.zero;
                playerMovement.extraCameraDistance = 0f;
            }
            return;
        }

        HandleFlightInput();

        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame) TryActivateQ();
            if (Keyboard.current.eKey.wasPressedThisFrame) TryActivateE();
            if (Keyboard.current.cKey.wasPressedThisFrame) TryActivateC();
            if (Keyboard.current.xKey.wasPressedThisFrame) TryActivateUltimate();
        }

        HandleAttackInput();

        //driven from state every frame rather than set once, so pausing mid-ultimate cannot
        //leave the camera or movement stuck
        if (playerMovement != null)
        {
            //E and C root him completely for the length of their animations; the ultimate
            //only slows him. asserted every frame so a pause can't leave him stuck.
            bool rooted = isEAbilityActive || isCAbilityActive;

            if (rooted)
                playerMovement.speedMultiplier = 0f;
            else
                playerMovement.speedMultiplier = isUltimateActive ? ultimateMoveSpeedMultiplier : 1f;

            playerMovement.suspendJump = rooted;
            playerMovement.suspendDash = rooted;

            playerMovement.flightMode = isFlying;

            //asserted every frame rather than set once on class switch, so it survives a pause
            //or anything else that resets the camera rig
            playerMovement.cameraShoulderOffset = shoulderOffset;
            playerMovement.extraCameraDistance = cameraCloseness;
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
        if (isUltimateActive || isCAbilityActive)
            return;

        bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (clicked) TryFireWave();
    }

    void TryFireWave()
    {
        if (Time.time < nextWaveTime) return;
        nextWaveTime = Time.time + waveCooldown;

        OnFireStarted?.Invoke();
        StartCoroutine(Wave());
    }

    //a flat slab of damage that travels away from him. each wave remembers what it has already
    //caught, so a target standing in its path takes it once rather than every frame.
    IEnumerator Wave()
    {
        liveWaves++;
        isFiring = true;

        Vector3 dir = Forward;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;

        Vector3 point = Origin;
        Quaternion facing = Quaternion.LookRotation(dir);

        GameObject fx = null;
        if (waveVfx != null)
        {
            fx = Instantiate(waveVfx, point, facing * Quaternion.Euler(waveVfxRotationOffset));
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Clear(true);
                ps.Play(true);
            }
            if (!Mathf.Approximately(waveVfxScale, 1f)) fx.transform.localScale *= waveVfxScale;
        }

        List<VirusHealthBase> alreadyHit = new List<VirusHealthBase>();
        Vector3 halfExtents = new Vector3(waveWidth * 0.5f, waveHeight * 0.5f, 1f);

        float elapsed = 0f;
        while (elapsed < waveDuration)
        {
            elapsed += Time.deltaTime;
            point += waveSpeed * Time.deltaTime * dir;

            if (fx != null)
                fx.transform.position = point
                                        + facing * new Vector3(waveVfxPositionOffset.x,
                                                               waveVfxPositionOffset.y,
                                                               waveVfxPositionOffset.z);

            foreach (Collider c in Physics.OverlapBox(point, halfExtents, facing))
            {
                VirusHealthBase vh = c.GetComponentInParent<VirusHealthBase>();
                if (vh == null || alreadyHit.Contains(vh)) continue;
                if (VirusAllegiance.IsConverted(vh)) continue;
                alreadyHit.Add(vh);

                DealDamage(vh, waveDamage);
                Debug.DrawLine(point, vh.transform.position, new Color(1f, 0.4f, 0f), 0.3f);
            }

            if (drawWaveBounds) DrawWaveBox(point, facing, halfExtents);

            yield return null;
        }

        if (fx != null)
        {
            fx.transform.SetParent(null, true);
            Destroy(fx);
        }

        liveWaves--;
        if (liveWaves <= 0)
        {
            liveWaves = 0;
            isFiring = false;
            OnFireStopped?.Invoke();
        }
    }

    void DrawWaveBox(Vector3 centre, Quaternion rot, Vector3 half)
    {
        Vector3 r = rot * Vector3.right * half.x;
        Vector3 u = rot * Vector3.up * half.y;
        Color col = new Color(1f, 0.35f, 0f);

        Debug.DrawLine(centre - r - u, centre + r - u, col);
        Debug.DrawLine(centre + r - u, centre + r + u, col);
        Debug.DrawLine(centre + r + u, centre - r + u, col);
        Debug.DrawLine(centre - r + u, centre - r - u, col);
    }

    //aim at whatever the crosshair is over, so bombs converge on the reticle rather than
    //flying parallel to the camera
    //where the orb should land. takes whatever the crosshair is over, then pulls it back to
    //Bomb Max Range if that's further - so aiming at the horizon lobs it as far as he can
    //throw rather than firing it into the distance.
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
            //too far - throw as far as he can that way, and let it land on the ground
            Vector3 capped = from + flat.normalized * bombMaxRange;

            if (Physics.Raycast(capped + Vector3.up * 50f, Vector3.down, out RaycastHit ground,
                                200f, ~0, QueryTriggerInteraction.Ignore))
                return ground.point;

            capped.y = from.y;
            return capped;
        }

        return aimPoint;
    }

    //---------------------------------------------------------------- Q
    void TryActivateQ()
    {
        if (Time.time < qReadyTime) return;
        if (isUltimateActive || isCAbilityActive || isEAbilityActive) return;

        qReadyTime = Time.time + qCooldown;

        //animation and cooldown start now, the orb itself is held back
        OnBombThrown?.Invoke();
        StartCoroutine(LaunchOrb());
    }

    IEnumerator LaunchOrb()
    {
        if (qLaunchDelay > 0f) yield return new WaitForSeconds(qLaunchDelay);

        //aimed at launch rather than at the keypress, so turning during the wind-up still
        //throws where you are actually looking
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
            go.name = "DemonOrb";
            go.transform.position = origin;
        }

        go.transform.localScale = Vector3.one * Mathf.Max(0.05f, bombScale);

        //the orb sweeps forward with a SphereCast from its own position, so any collider it
        //carries would be the first thing that cast finds - it would detonate on spawn.
        //a rigidbody would fight the script for control of the arc.
        foreach (Collider col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        DemonBomb bomb = go.GetComponent<DemonBomb>();
        if (bomb == null) bomb = go.AddComponent<DemonBomb>();

        bomb.damage = bombDamage;
        bomb.radius = bombRadius;
        bomb.hitRadius = bombScale * 0.4f;

        bomb.secondaryDelay = bombSecondaryDelay;
        bomb.secondaryDamage = bombSecondaryDamage;
        bomb.secondaryRadius = bombSecondaryRadius;

        bomb.explosionVfx = bombExplosionVfx;
        bomb.explosionVfxScale = bombExplosionVfxScale;

        //no fallback - an empty slot means no effect on that stage
        bomb.secondaryVfx = bombSecondaryVfx;
        bomb.secondaryVfxScale = bombSecondaryVfxScale;

        //the bomb shakes the camera itself, since both blasts happen after it has left his hand
        bomb.cameraShake = cameraShake;
        bomb.impactShake = bombShake;
        bomb.secondaryShake = bombSecondaryShake;

        bomb.Launch(this, target, bombGravity, bombFlightTime);
    }

    //---------------------------------------------------------------- E
    void TryActivateE()
    {
        if (Time.time < eReadyTime) return;
        if (isUltimateActive || isCAbilityActive || isEAbilityActive) return;
        eReadyTime = Time.time + eCooldown;

        StartCoroutine(ELock());

        //animation fires now, the conversion lands partway through it
        OnTakeover?.Invoke();
        StartCoroutine(Takeover());
    }

    IEnumerator Takeover()
    {
        if (takeoverDelay > 0f) yield return new WaitForSeconds(takeoverDelay);

        //scanned at the moment it lands rather than on the keypress, so anything that walked
        //into range during the cast gets caught too
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
            allegiance.Begin(takeoverDuration, takeoverFreezeDuration, toSuspend.ToArray());

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

        Debug.Log($"Demon takeover: converted {converted} virus(es) for {takeoverDuration}s, " +
                  $"then frozen for {takeoverFreezeDuration}s");
    }

    //roots him for the length of the cast animation
    IEnumerator ELock()
    {
        isEAbilityActive = true;
        yield return new WaitForSeconds(eLockDuration);
        isEAbilityActive = false;
    }

    //---------------------------------------------------------------- C
    void TryActivateC()
    {
        if (Time.time < cReadyTime) return;
        if (isUltimateActive || isCAbilityActive || isEAbilityActive) return;

        int tier = CurrentTier;

        if (cRequiresSouls && tier < 1)
        {
            if (logSouls) Debug.Log($"Demon C: not enough souls ({souls}/{killsPerTier})");
            return;
        }

        //at least tier 1 so the no-souls case still reads off the front of the arrays
        tier = Mathf.Max(1, tier);

        cReadyTime = Time.time + cCooldown;

        //every soul goes into this one blast, banked or not
        if (logSouls) Debug.Log($"Demon C: unleashing {souls} soul(s) at tier {tier}");
        souls = 0;

        OnCAbility?.Invoke();
        StartCoroutine(SoulHarvest(tier));
    }

    IEnumerator SoulHarvest(int tier)
    {
        isCAbilityActive = true;

        float damage = TierValue(cDamagePerTier, tier, 40f);
        float radius = TierValue(cRadiusPerTier, tier, 8f);
        float shake  = TierValue(cShakePerTier, tier, 0.3f);
        float vfxScale = TierValue(cVfxScalePerTier, tier, 1f);
        if (vfxScale <= 0f) vfxScale = 1f;

        if (cVfx != null)
        {
            Vector3 flatForward = transform.forward;
            Transform t = playerController != null ? playerController.transform : transform;
            flatForward = t.forward;

            Vector3 pos = t.position
                          + t.right * cVfxPositionOffset.x
                          + Vector3.up * cVfxPositionOffset.y
                          + flatForward * cVfxPositionOffset.z;

            GameObject fx = Instantiate(cVfx, pos,
                                        Quaternion.LookRotation(flatForward) * Quaternion.Euler(cVfxRotationOffset));
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Clear(true);
                ps.Play(true);
            }

            //asset-pack particles usually scale from the hierarchy, not their own transform
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            fx.transform.localScale *= vfxScale;

            Destroy(fx, cVfxLifetime);
        }

        if (cDamageDelay > 0f) yield return new WaitForSeconds(cDamageDelay);

        Vector3 centre = playerController != null ? playerController.transform.position : transform.position;

        if (cameraShake != null && shake > 0f) cameraShake.AddTrauma(shake);

        //ring so the radius is readable while tuning
        const int segs = 40;
        for (int i = 0; i < segs; i++)
        {
            float a0 = i / (float)segs * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segs * Mathf.PI * 2f;
            Debug.DrawLine(centre + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius,
                           centre + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius,
                           new Color(0.7f, 0.1f, 1f), 1.5f);
        }

        List<VirusHealthBase> hit = new List<VirusHealthBase>();
        foreach (Collider c in Physics.OverlapSphere(centre, radius))
        {
            VirusHealthBase vh = c.GetComponentInParent<VirusHealthBase>();
            if (vh == null || hit.Contains(vh)) continue;
            if (VirusAllegiance.IsConverted(vh)) continue;
            hit.Add(vh);

            //captured before damaging - the hit may destroy it and the effect would have
            //nothing left to read a position from
            Vector3 point = vh.transform.position;

            if (cHitVfx != null)
            {
                GameObject fx = Instantiate(cHitVfx, point, Quaternion.identity);
                foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Clear(true);
                    ps.Play(true);
                }
                if (!Mathf.Approximately(cHitVfxScale, 1f)) fx.transform.localScale *= cHitVfxScale;
                Destroy(fx, cHitVfxLifetime);
            }

            DealDamage(vh, damage);
            Debug.DrawLine(centre, point, Color.white, 0.8f);
        }

        if (logSouls) Debug.Log($"Demon C: tier {tier}, {damage} damage over {radius}m, caught {hit.Count}");

        float remaining = cLockDuration - cDamageDelay;
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        isCAbilityActive = false;
    }

    //clamps rather than throwing if an array is the wrong length - the Inspector can leave
    //it short after Max Tiers is raised
    float TierValue(float[] values, int tier, float fallback)
    {
        if (values == null || values.Length == 0) return fallback;
        return values[Mathf.Clamp(tier - 1, 0, values.Length - 1)];
    }

    //---------------------------------------------------------------- X
    void TryActivateUltimate()
    {
        if (Time.time < ultimateReadyTime) return;
        if (isUltimateActive || isCAbilityActive || isEAbilityActive) return;

        ultimateReadyTime = Time.time + ultimateCooldown;
        StartCoroutine(LaserEyes());
    }

    IEnumerator LaserEyes()
    {
        isUltimateActive = true;
        OnUltimateStarted?.Invoke();

        GameObject charge = null;
        if (windupVfx != null)
        {
            Transform anchor = playerController != null ? playerController.transform : transform;

            //offset is applied in his local axes, so X really is his right regardless of facing
            Vector3 pos = EyeCentre()
                          + anchor.right * windupVfxPositionOffset.x
                          + Vector3.up * windupVfxPositionOffset.y
                          + anchor.forward * windupVfxPositionOffset.z;

            Quaternion rot = Quaternion.LookRotation(anchor.forward) * Quaternion.Euler(windupVfxRotationOffset);

            charge = Instantiate(windupVfx, pos, rot,
                                 windupVfxFollowsPlayer ? anchor : null);

            foreach (ParticleSystem ps in charge.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Clear(true);
                ps.Play(true);
            }

            if (!Mathf.Approximately(windupVfxScale, 1f))
            {
                //asset-pack effects usually scale from the hierarchy, not their own transform
                foreach (ParticleSystem ps in charge.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = ps.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
                charge.transform.localScale *= windupVfxScale;
            }
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
            FireBeam(beamFxLeft, EyeWorld(leftEyeOffset), aim, damageThisFrame);
            FireBeam(beamFxRight, EyeWorld(rightEyeOffset), aim, damageThisFrame);

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

    //beams pierce - everything on the line takes damage, which is what makes this crowd
    //control rather than a sniper shot
    void FireBeam(GameObject fx, Vector3 from, Vector3 aim, bool applyDamage)
    {
        Vector3 dir = (aim - from).normalized;
        Vector3 end = from + dir * beamRange;

        //stop the visual at the first thing it meets, so it doesn't hang through walls
        if (Physics.Raycast(from, dir, out RaycastHit wall, beamRange, ~0, QueryTriggerInteraction.Ignore))
            if (wall.collider.GetComponentInParent<VirusHealthBase>() == null &&
                wall.collider.GetComponentInParent<CharacterController>() == null)
                end = wall.point;

        if (fx != null)
        {
            //X/Y in his own axes so they stay predictable however the beam is angled,
            //Z along the beam so it can be pushed out in front of him
            Transform anchor = playerController != null ? playerController.transform : transform;

            fx.transform.position = from
                                    + anchor.right * beamVfxPositionOffset.x
                                    + Vector3.up * beamVfxPositionOffset.y
                                    + dir * beamVfxPositionOffset.z;

            fx.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(beamVfxRotationOffset);
        }

        if (drawBeamDebug)
            Debug.DrawLine(from, end, new Color(1f, 0.25f, 0.05f));

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

            //white marker on whatever is being burned, same read as the BladeMaster's hits
            if (drawBeamDebug)
            {
                Vector3 t = vh.transform.position;
                Debug.DrawLine(from, t, Color.white, beamTickInterval);
                Debug.DrawLine(t + Vector3.up * 1.2f, t - Vector3.up * 0.2f, Color.white, beamTickInterval);
            }
        }
    }

    void SetBeamsVisible(bool on)
    {
        if (on)
        {
            if (beamVfx == null) return;   // debug lines only

            if (beamFxLeft == null) beamFxLeft = SpawnBeamVfx("BeamFx_L");
            if (beamFxRight == null) beamFxRight = SpawnBeamVfx("BeamFx_R");
            return;
        }

        if (beamFxLeft != null) { Destroy(beamFxLeft); beamFxLeft = null; }
        if (beamFxRight != null) { Destroy(beamFxRight); beamFxRight = null; }
    }

    GameObject SpawnBeamVfx(string beamName)
    {
        GameObject go = Instantiate(beamVfx);
        go.name = beamName;

        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            //most beam prefabs ship as one-shots - their sub-systems run for a second or two
            //and stop, which leaves the beam invisible for most of its duration
            if (loopBeamVfx)
            {
                ParticleSystem.MainModule loopMain = ps.main;
                loopMain.loop = true;
            }

            ps.Clear(true);
            ps.Play(true);
        }

        if (!Mathf.Approximately(beamVfxScale, 1f))
        {
            //asset-pack effects usually scale from the hierarchy rather than their own transform
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            go.transform.localScale *= beamVfxScale;
        }

        return go;
    }

    //TEMPORARY soul readout, same idea as the BladeMaster's charge HUD. this component is
    //disabled on pause and on other classes, so OnGUI stops being called and it hides itself.
    GUIStyle soulHudStyle;

    void OnGUI()
    {
        if (!showSoulHud) return;
        if (weaponSwitcher != null && weaponSwitcher.CurrentClass != WeaponSwitcher.PlayerClass.Demon) return;

        soulHudStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontStyle = FontStyle.Bold
        };
        soulHudStyle.fontSize = soulHudFontSize;

        int tier = CurrentTier;

        //filled diamond per earned tier, hollow for the rest
        string pips = "";
        for (int i = 0; i < cMaxTiers; i++) pips += i < tier ? "◆" : "◇";

        string label = $"{pips}   {souls} souls";
        if (tier < cMaxTiers && killsPerTier > 0)
            label += $"   [{souls % killsPerTier}/{killsPerTier}]";

        Rect rect = new Rect(0f, soulHudTopMargin, Screen.width, soulHudFontSize * 2f);

        //offset copy behind it, otherwise the text vanishes against a bright scene
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), label, soulHudStyle);

        GUI.color = tier >= cMaxTiers ? new Color(0.8f, 0.4f, 1f) : Color.white;
        GUI.Label(rect, label, soulHudStyle);

        GUI.color = Color.white; // GUI.color is global state, leave it as we found it
    }

    //---------------------------------------------------------------- damage + lifesteal
    //every damage source routes through here, so the perk can never be forgotten on a new ability
    public void DealDamage(VirusHealthBase target, float amount)
    {
        if (target == null || amount <= 0f) return;

        //don't credit lifesteal for overkill - heal on what was actually taken off
        float dealt = Mathf.Min(amount, Mathf.Max(0f, target.CurrentHealth));

        //a soul is banked when HIS damage is what finishes the target. checked before the hit,
        //because afterwards the object may already be destroyed.
        bool lethal = amount >= target.CurrentHealth;

        target.TakeDamage(amount);

        if (lethal)
        {
            souls++;
            if (logSouls) Debug.Log($"Demon souls: {souls} (tier {CurrentTier}/{cMaxTiers})");
        }

        if (playerHealth == null || lifestealFraction <= 0f || dealt <= 0f) return;

        float heal = dealt * lifestealFraction;
        playerHealth.Heal(heal);

        if (logLifesteal)
            Debug.Log($"Demon lifesteal: +{heal:F1} from {dealt:F1} damage");
    }
}
