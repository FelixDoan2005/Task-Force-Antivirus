using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

//single-target destruction, the opposite of the Demon. a heavy gun and augmented reflexes:
//the rifle fires where he aims, Q scans an area and paints everything in it, E pushes his
//augments into overdrive, and his perk pays him for fighting alone.
public class CyborgClass : MonoBehaviour
{
    [Header("Setup")]
    public Camera playerCamera;
    public Transform firePoint;

    [Tooltip("Where his shots leave the gun while he is crouched, since the standing point sits " +
             "up at shoulder height. Falls back to the standing one if this is empty.")]
    public Transform crouchedFirePoint;
    public CharacterController playerController;
    public PlayerController playerMovement;
    public PlayerHealth playerHealth;
    public WeaponSwitcher weaponSwitcher;
    public CameraShake cameraShake;

    //---------------------------------------------------------------- perk: duellist
    [Header("Perk - Combat Augments")]
    [Tooltip("Extra damage when he is fighting one target alone, as a fraction. 0.4 = +40%.")]
    public float soloDamageBonus = 0.4f;

    [Tooltip("How many nearby enemies it takes to wipe the bonus out completely.")]
    public int crowdSize = 5;

    [Tooltip("Enemies inside this radius count toward the crowd.")]
    public float crowdCheckRadius = 14f;

    [Tooltip("Prints the multiplier on every hit, so the perk can be verified.")]
    public bool logDamage;

    //---------------------------------------------------------------- auto attack: the rifle
    [Header("Auto Attack - Rifle")]
    public float bulletDamage = 20f;

    [Tooltip("Seconds between shots while holding fire.")]
    public float fireInterval = 0.12f;

    [Tooltip("Furthest he can acquire and hold a lock.")]
    public float lockRange = 45f;

    [Tooltip("How far off centre a target can be and still be acquired, in degrees.")]
    public float lockAngle = 35f;

    [Tooltip("How much the gun helps your aim. 1 = perfectly on the locked target, " +
             "0 = it fires exactly where the crosshair points.")]
    [Range(0f, 1f)] public float aimAssist = 1f;

    [Tooltip("Fired at the muzzle on every shot.")]
    public GameObject muzzleVfx;
    public float muzzleVfxScale = 1f;

    [Tooltip("The tip of the gun model. Visual effects spawn here. The hitscan still fires from " +
             "Fire Point, which is shared with other classes.")]
    public Transform muzzlePoint;

    [Tooltip("How long each shot's flash stays up. Keep it under the fire interval so full-auto " +
             "flickers per shot instead of blurring into one constant flash.")]
    public float muzzleVfxLifetime = 0.08f;

    [Tooltip("Spawned where the shot lands.")]
    public GameObject impactVfx;
    public float impactVfxScale = 1f;
    public float impactVfxLifetime = 1.5f;

    [Tooltip("Camera kick per shot. Small - this fires fast.")]
    public float fireShake = 0.05f;

    [Tooltip("How long the tracer line stays drawn.")]
    public float tracerDuration = 0.05f;

    [Tooltip("Draws the tracer in the game view, not just the scene view.")]
    public bool showTracerInGame = true;
    public Color tracerColour = new Color(1f, 0.85f, 0.3f);
    public float tracerWidth = 0.03f;

    [Tooltip("Optional. Left empty, tracers use a plain unlit material.")]
    public Material tracerMaterial;

    Material runtimeTracerMaterial;

    float nextShotTime;
    GameObject activeMuzzleFx;
    VirusHealthBase lockedTarget;
    public VirusHealthBase LockedTarget => lockedTarget;

    //---------------------------------------------------------------- ammo
    [Header("Ammo")]
    public int magazineSize = 60;

    [Tooltip("Full spare magazines he spawns with, on top of the loaded one.")]
    public int startingSpareMags = 6;

    [Tooltip("Seconds a reload takes. R reloads by hand; an empty mag reloads on its own.")]
    public float reloadTime = 2f;

    [Tooltip("Kills needed to earn one spare magazine. 0 turns it off.")]
    public int killsPerBonusMag = 5;

    [Tooltip("Spare magazines added when the ultimate goes off.")]
    public int ultimateBonusMags = 6;

    int currentAmmo;
    int reserveAmmo;
    bool isReloading;
    float reloadEndTime;
    float currentReloadDuration;
    int killsTowardMag;

    public int CurrentAmmo => currentAmmo;
    public int ReserveAmmo => reserveAmmo;
    public int SpareMags => magazineSize > 0 ? reserveAmmo / magazineSize : 0;
    public bool IsReloading => isReloading;

    //how long the reload in progress takes. fixed when it starts, so overdrive ending partway
    //through doesn't change the animation's speed mid-reload
    public float CurrentReloadDuration => isReloading ? currentReloadDuration : EffectiveReloadTime;
    public event System.Action OnReloadStarted;

    //---------------------------------------------------------------- Q: area scan
    [Header("Q - Area Scan")]
    public float qCooldown = 12f;

    [Tooltip("Everything inside this radius gets painted.")]
    public float scanRadius = 25f;

    [Tooltip("How long the paint holds before falling off.")]
    public float markDuration = 8f;

    [Tooltip("Bonus damage the moment it lands, as a fraction.")]
    public float markStartingBonus = 0.1f;

    [Tooltip("How much more it adds per second while it holds.")]
    public float markRampPerSecond = 0.08f;

    [Tooltip("Cap on the ramp. 0.75 = a painted target eventually takes +75%.")]
    public float markMaxBonus = 0.75f;

    [Tooltip("Played on him when the scan goes out.")]
    public GameObject scanVfx;
    public float scanVfxScale = 1f;
    public float scanVfxLifetime = 2f;

    [Tooltip("Played on each target the scan paints.")]
    public GameObject markVfx;
    public float markVfxScale = 1f;

    public float scanShake = 0.2f;

    [Tooltip("Draws the scan ring and the lines to each painted target in the game view, not just the scene view.")]
    public bool showScanInGame = true;
    public Color scanLineColour = Color.cyan;
    public float scanLineWidth = 0.06f;
    public float scanVisualDuration = 1.5f;

    [Tooltip("Height the target lines are drawn at, so they run chest to chest rather than along the floor.")]
    public float scanLineHeight = 1f;

    [Tooltip("Optional. Left empty, the lines use a plain unlit material that fades cleanly.")]
    public Material scanLineMaterial;

    float qReadyTime;

    //---------------------------------------------------------------- E: overdrive
    [Header("E - Overdrive")]
    public float eCooldown = 18f;

    [Tooltip("How long his augments stay pushed past their limits.")]
    public float overdriveDuration = 6f;

    [Tooltip("Move speed multiplier. 1.4 = 40% faster.")]
    public float overdriveMoveSpeedMultiplier = 1.4f;

    [Tooltip("Jump height multiplier. 1.5 = jumps 50% higher.")]
    public float overdriveJumpMultiplier = 1.5f;

    [Tooltip("Fire rate multiplier. Stacks with the ultimate's.")]
    public float overdriveFireRateMultiplier = 1.5f;

    [Tooltip("Reload speed multiplier. 2 = reloads take half as long.")]
    public float overdriveReloadSpeedMultiplier = 2f;

    [Tooltip("Played on him when overdrive kicks in, and follows him for the duration.")]
    public GameObject overdriveVfx;
    public float overdriveVfxScale = 1f;
    public float overdriveShake = 0.25f;

    float eReadyTime;
    float overdriveUntil;
    GameObject activeOverdriveFx;

    //true once this class has pushed its buffs onto the shared PlayerController, so switching
    //away hands the movement back exactly once instead of fighting other classes every frame
    bool movementBuffApplied;

    public bool IsOverdriveActive => Time.time < overdriveUntil;
    public float OverdriveRemaining => Mathf.Max(0f, overdriveUntil - Time.time);
    public event System.Action OnOverdriveStarted;

    float EffectiveReloadTime => IsOverdriveActive
        ? reloadTime / Mathf.Max(0.01f, overdriveReloadSpeedMultiplier)
        : reloadTime;

    //---------------------------------------------------------------- X: triple lock
    [Header("X - Targeting Override")]
    public float ultimateCooldown = 60f;

    [Tooltip("How long the rifle fires at three targets at once.")]
    public float ultimateDuration = 10f;

    [Tooltip("How many targets it splits across.")]
    public int ultimateTargets = 3;

    [Tooltip("Damage multiplier per shot while it is up. 1 means all three take full damage.")]
    public float ultimateDamageMultiplier = 1f;

    public float ultimateShake = 0.3f;

    [Tooltip("Fire rate multiplier while the ultimate is up. 2 = twice as fast.")]
    public float ultimateFireRateMultiplier = 2f;

    [Tooltip("No ammo is spent and no reloads happen while the ultimate is up.")]
    public bool ultimateInfiniteAmmo = true;

    float ultimateReadyTime;
    float ultimateUntil;
    public bool IsUltimateActive => Time.time < ultimateUntil;

    //---------------------------------------------------------------- C: cash in the stacks
    [Header("C - Overload")]
    public float cCooldown = 20f;

    [Tooltip("How long C roots him - line this up with the animation.")]
    public float cLockDuration = 1.2f;

    [Tooltip("Delay from pressing C to the damage landing.")]
    public float cDamageDelay = 0.5f;

    [Tooltip("Damage per stack spent. A full 50-stack overload is 600 - level with the Demon's orb.")]
    public float damagePerStack = 12f;

    [Tooltip("Minimum stacks needed before C will fire at all.")]
    public int minimumStacks = 10;

    public GameObject overloadVfx;
    public float overloadVfxScale = 1f;
    public float overloadShake = 0.6f;

    float cReadyTime;
    bool isCAbilityActive;
    public bool IsCAbilityActive => isCAbilityActive;

    //---------------------------------------------------------------- markers
    [Header("Markers")]
    [Tooltip("Shown above a virus once its stacks are maxed out.")]
    public GameObject fullStackMarker;

    public float markerHeight = 2.4f;

    [Tooltip("Draws lock circles and target rings in the scene view.")]
    public bool drawTargetRings = true;

    [Tooltip("Draws a marker on every virus a shot lands on, sized by how hard the hit was.")]
    public bool drawHitMarkers = true;

    public float hitMarkerDuration = 0.35f;

    readonly Dictionary<VirusHealthBase, GameObject> activeMarkers = new Dictionary<VirusHealthBase, GameObject>();

    //---------------------------------------------------------------- camera
    [Header("Camera - Over the Shoulder")]
    [Tooltip("He aims down a crosshair, so the camera sits off to one side instead of dead " +
             "behind him. X = right of the player, Y = up. Negative X puts it over the left shoulder.")]
    public Vector2 shoulderOffset = new Vector2(0.7f, 0.25f);

    [Tooltip("How much closer than the default third-person distance. Negative pulls in.")]
    public float cameraCloseness = -0.9f;

    //---------------------------------------------------------------- aim down sights
    [Header("Aim Down Sights")]
    public bool allowAds = true;

    [Tooltip("Field of view while aiming. The hip-fire value is read off the camera on start, " +
             "so it follows whatever the camera is already set to.")]
    public float adsFieldOfView = 40f;

    [Tooltip("How quickly it zooms in and back out.")]
    public float adsZoomSpeed = 12f;

    [Tooltip("Where the camera sits while aiming - usually tighter in than the hip-fire offset.")]
    public Vector2 adsShoulderOffset = new Vector2(0.45f, 0.15f);

    [Tooltip("How much closer again the camera pulls while aiming.")]
    public float adsCameraCloseness = -1.4f;

    [Tooltip("How much of his speed he keeps while aiming. He also can't sprint while aimed in.")]
    [Range(0.1f, 1f)] public float adsMoveSpeedMultiplier = 0.6f;

    //---------------------------------------------------------------- crouched framing
    [Header("Camera - Crouched")]
    [Tooltip("Nudges the camera up or down on top of the pivot already dropping with him. The " +
             "pivot follow in PlayerController is what keeps the view at his shoulder - leave " +
             "this at 0 unless the framing needs a tweak. It moves the camera without tilting " +
             "it, so the crosshair keeps pointing where it did standing up.")]
    public float crouchCameraLift = 0f;

    [Tooltip("How far the camera backs off while crouched. Positive pushes it out.")]
    public float crouchCameraPullback = 0.25f;

    public bool IsAiming { get; private set; }

    //the unzoomed FOV, taken from the camera rather than hard coded
    float hipFieldOfView;

    //---------------------------------------------------------------- HUD
    [Header("HUD (temporary)")]
    public bool showCyborgHud = true;
    public int hudFontSize = 22;
    public float hudTopMargin = 24f;

    //---------------------------------------------------------------- events for the animator
    public event System.Action OnShotFired;
    public event System.Action OnScan;
    public event System.Action OnUltimateStarted;
    public event System.Action OnOverload;

    //crouching drops the gun over a metre, so the shot has to leave from down there too -
    //otherwise he fires from where his shoulder would have been and clips the cover he is behind
    Transform ActiveFirePoint =>
        crouchedFirePoint != null && playerMovement != null && playerMovement.IsCrouching
            ? crouchedFirePoint
            : firePoint;

    Vector3 Origin => ActiveFirePoint != null ? ActiveFirePoint.position : transform.position;
    Vector3 Forward => playerCamera != null ? playerCamera.transform.forward : transform.forward;
    Transform Body => playerController != null ? playerController.transform : transform;

    public bool IsFiring { get; private set; }

    void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera != null) hipFieldOfView = playerCamera.fieldOfView;

        //in Awake rather than OnEnable, so ammo survives switching classes and back
        currentAmmo = magazineSize;
        reserveAmmo = startingSpareMags * magazineSize;
    }

    //coroutines die when this is disabled, so anything they were going to clear has to be
    //cleared here - the same trap the BladeMaster and Demon both hit
    void OnDisable()
    {
        isCAbilityActive = false;
        ultimateUntil = 0f;
        overdriveUntil = 0f;
        IsFiring = false;
        isReloading = false;

        ClearAllMarkers();
        ClearOverdriveFx();

        if (playerMovement != null)
        {
            playerMovement.speedMultiplier = 1f;
            playerMovement.jumpHeightMultiplier = 1f;
            playerMovement.suspendJump = false;
            playerMovement.suspendDash = false;
            playerMovement.suspendSprint = false;
            playerMovement.allowCrouch = false;
            playerMovement.cameraShoulderOffset = Vector2.zero;
            playerMovement.extraCameraDistance = 0f;
        }
        movementBuffApplied = false;
        IsAiming = false;

        if (playerCamera != null && hipFieldOfView > 0f)
            playerCamera.fieldOfView = hipFieldOfView;

        ClearMuzzleFx();
    }

    void Update()
    {
        bool isCyborg = weaponSwitcher == null ||
                        weaponSwitcher.CurrentClass == WeaponSwitcher.PlayerClass.Cyborg;

        if (!isCyborg)
        {
            IsFiring = false;
            isReloading = false;
            IsAiming = false;
            ClearMuzzleFx();

            //the next class inherits this camera, so the zoom can't be left applied
            if (playerCamera != null && hipFieldOfView > 0f)
                playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, hipFieldOfView,
                                                      Time.deltaTime * adsZoomSpeed);

            //another class is up - give the camera rig and the sprint back so nothing lingers
            if (playerMovement != null)
            {
                playerMovement.suspendSprint = false;
                playerMovement.allowCrouch = false;

                //overdrive's speed and jump would otherwise carry over onto the next class
                if (movementBuffApplied)
                {
                    playerMovement.speedMultiplier = 1f;
                    playerMovement.jumpHeightMultiplier = 1f;
                    movementBuffApplied = false;
                }

                if (playerMovement.cameraShoulderOffset != Vector2.zero)
                {
                    playerMovement.cameraShoulderOffset = Vector2.zero;
                    playerMovement.extraCameraDistance = 0f;
                }
            }
            return;
        }

        AcquireLock();
        TickMarks();
        UpdateMarkers();

        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame) TryActivateQ();
            if (Keyboard.current.eKey.wasPressedThisFrame) TryActivateE();
            if (Keyboard.current.cKey.wasPressedThisFrame) TryActivateC();
            if (Keyboard.current.xKey.wasPressedThisFrame) TryActivateUltimate();
        }

        //held rather than toggled, and dropped while he is rooted by the C ability
        IsAiming = allowAds && !isCAbilityActive &&
                   Mouse.current != null && Mouse.current.rightButton.isPressed;

        HandleFireInput();

        //asserted every frame so a pause can't leave him stuck or permanently buffed
        if (playerMovement != null)
        {
            bool rooted = isCAbilityActive;
            bool overdrive = IsOverdriveActive;

            float speed = rooted ? 0f : (overdrive ? overdriveMoveSpeedMultiplier : 1f);
            if (IsAiming) speed *= adsMoveSpeedMultiplier;
            playerMovement.speedMultiplier = speed;
            playerMovement.jumpHeightMultiplier = overdrive ? overdriveJumpMultiplier : 1f;
            movementBuffApplied = true;

            playerMovement.suspendJump = rooted;
            playerMovement.suspendDash = rooted;

            //he can't shoot straight at a dead sprint - opening fire drops him back to a run,
            //so the Firing tree lands on the shoot-while-running clip instead of the sprint
            playerMovement.suspendSprint = IsFiring || IsAiming;

            //a soldier with a rifle can take a knee; the melee classes leave this off
            playerMovement.allowCrouch = !rooted;

            //asserted every frame rather than set once on class switch, so it survives a pause
            //or anything else that resets the camera rig
            Vector2 shoulder = IsAiming ? adsShoulderOffset : shoulderOffset;
            float closeness = IsAiming ? adsCameraCloseness : cameraCloseness;

            //stacked on top of whichever of those is in play, so crouching reframes him the same
            //way whether he is hip firing or aimed in
            if (playerMovement.IsCrouching)
            {
                shoulder.y += crouchCameraLift;
                closeness += crouchCameraPullback;
            }

            playerMovement.cameraShoulderOffset = shoulder;
            playerMovement.extraCameraDistance = closeness;
        }

        //eased rather than snapped, so it reads as the gun coming up
        if (playerCamera != null)
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView,
                                                  IsAiming ? adsFieldOfView : hipFieldOfView,
                                                  Time.deltaTime * adsZoomSpeed);
    }

    //---------------------------------------------------------------- target lock
    //sticky: an acquired target is kept while it stays alive and roughly in front, so the lock
    //doesn't flick to whatever wanders closer mid-fight
    void AcquireLock()
    {
        if (lockedTarget != null && IsValidTarget(lockedTarget, lockAngle * 1.6f)) return;
        lockedTarget = FindBestTarget(lockAngle);
    }

    bool IsValidTarget(VirusHealthBase v, float angle)
    {
        if (v == null) return false;

        Vector3 to = v.transform.position - Origin;
        if (to.magnitude > lockRange) return false;
        return Vector3.Angle(Forward, to) <= angle * 0.5f;
    }

    VirusHealthBase FindBestTarget(float angle)
    {
        VirusHealthBase best = null;
        float bestScore = float.MaxValue;

        foreach (Collider c in Physics.OverlapSphere(Origin, lockRange))
        {
            VirusHealthBase v = c.GetComponentInParent<VirusHealthBase>();
            if (v == null) continue;

            Vector3 to = v.transform.position - Origin;
            float off = Vector3.Angle(Forward, to);
            if (off > angle * 0.5f) continue;

            //prefer what he is actually looking at over what is merely nearest
            float score = off + to.magnitude * 0.35f;
            if (score < bestScore) { bestScore = score; best = v; }
        }

        return best;
    }

    List<VirusHealthBase> FindNearest(int count, float range)
    {
        var found = new List<VirusHealthBase>();
        foreach (Collider c in Physics.OverlapSphere(Origin, range))
        {
            VirusHealthBase v = c.GetComponentInParent<VirusHealthBase>();
            if (v != null && !found.Contains(v)) found.Add(v);
        }

        found.Sort((a, b) =>
            (a.transform.position - Origin).sqrMagnitude.CompareTo(
            (b.transform.position - Origin).sqrMagnitude));

        if (found.Count > count) found.RemoveRange(count, found.Count - count);
        return found;
    }

    //---------------------------------------------------------------- the rifle
    void HandleFireInput()
    {
        //before the early-out below, so a reload keeps ticking through an overload
        TickReload();

        if (isCAbilityActive) { IsFiring = false; return; }

        bool held = Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool infinite = IsUltimateActive && ultimateInfiniteAmmo;

        if (!infinite)
        {
            bool reloadPressed = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
            if (reloadPressed || currentAmmo <= 0) TryReload();
        }

        //not firing while reloading or dry, so the animator doesn't hold the firing pose
        bool canShoot = infinite || (!isReloading && currentAmmo > 0);
        IsFiring = held && canShoot;

        if (!IsFiring || Time.time < nextShotTime) return;

        //the ultimate and overdrive both speed the gun up, and stack if both are running
        float rate = 1f;
        if (IsUltimateActive) rate *= Mathf.Max(0.01f, ultimateFireRateMultiplier);
        if (IsOverdriveActive) rate *= Mathf.Max(0.01f, overdriveFireRateMultiplier);
        nextShotTime = Time.time + fireInterval / rate;

        if (!infinite) currentAmmo--;

        if (IsUltimateActive)
        {
            //re-picked every volley so it keeps up as targets die
            foreach (var v in FindNearest(ultimateTargets, lockRange))
                if (v != null) Shoot(v, bulletDamage * ultimateDamageMultiplier);
        }
        else
        {
            //regular fire is free aim. the lock still runs for the abilities that use it (the
            //overload picks its target from it), it just no longer steers normal shots.
            Shoot(null, bulletDamage);
        }

        OnShotFired?.Invoke();

        if (cameraShake != null && fireShake > 0f) cameraShake.AddTrauma(fireShake);
    }

    //hitscan rather than a projectile - it is a rifle, and the lock already decides who it hits
    void Shoot(VirusHealthBase target, float damage)
    {
        Vector3 origin = Origin;

        if (muzzleVfx != null)
        {
            Transform tip = muzzlePoint != null
                ? muzzlePoint
                : (ActiveFirePoint != null ? ActiveFirePoint : transform);

            //one flash at a time - a new shot replaces the last rather than stacking on it
            ClearMuzzleFx();
            activeMuzzleFx = Instantiate(muzzleVfx, tip.position, Quaternion.LookRotation(Forward), tip);

            //WarFX flashes are authored to loop, so left alone each one keeps burning for its
            //full duration after the shot. forcing a single play is what makes it a flash.
            foreach (ParticleSystem ps in activeMuzzleFx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.loop = false;
            }

            PlayAll(activeMuzzleFx, muzzleVfxScale);
            Destroy(activeMuzzleFx, muzzleVfxLifetime);
        }

        Vector3 aimPoint;

        if (target != null)
        {
            //aim assist blends between the crosshair and the locked target's centre of mass
            Vector3 locked = AimPointOf(target);
            aimPoint = Vector3.Lerp(CrosshairPoint(), locked, aimAssist);
        }
        else
        {
            //free aim - he fires at whatever is under the crosshair
            aimPoint = CrosshairPoint();
        }

        Vector3 dir = (aimPoint - origin).normalized;

        //the shot stops at whatever it meets first, so cover works
        VirusHealthBase hitVirus = null;
        Vector3 endPoint = origin + dir * lockRange;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, lockRange, ~0, QueryTriggerInteraction.Ignore))
        {
            endPoint = hit.point;
            hitVirus = hit.collider.GetComponentInParent<VirusHealthBase>();
        }

        //a locked target that the ray missed by a hair still counts - the augments are the point
        if (hitVirus == null && target != null && aimAssist >= 0.999f)
        {
            hitVirus = target;
            endPoint = AimPointOf(target);
        }

        Debug.DrawLine(origin, endPoint, new Color(1f, 0.85f, 0.3f), tracerDuration);

        //the same line as real geometry, starting at the gun tip when there is one
        if (showTracerInGame)
            SpawnTracer(muzzlePoint != null ? muzzlePoint.position : origin, endPoint);

        if (hitVirus == null) return;

        if (impactVfx != null)
        {
            GameObject fx = Instantiate(impactVfx, endPoint, Quaternion.LookRotation(-dir));
            PlayAll(fx, impactVfxScale);
            Destroy(fx, impactVfxLifetime);
        }

        BulletHit(hitVirus, damage);
    }

    //what the crosshair is actually on. the camera sits over his shoulder, so a shot fired from
    //the gun parallel to the camera would land beside the crosshair - find the point along the
    //camera's view first, then the shot flies from the gun to that point.
    Vector3 CrosshairPoint()
    {
        if (playerCamera == null) return Origin + Forward * lockRange;

        Transform cam = playerCamera.transform;
        int mask = ~LayerMask.GetMask("Player");   // the camera looks past his own body

        if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, lockRange + 10f,
                            mask, QueryTriggerInteraction.Ignore))
            return hit.point;

        return cam.position + cam.forward * lockRange;
    }

    Vector3 AimPointOf(VirusHealthBase v)
    {
        Collider col = v.GetComponentInChildren<Collider>();
        return col != null ? col.bounds.center : v.transform.position + Vector3.up;
    }

    //---------------------------------------------------------------- Q: area scan
    void TryActivateQ()
    {
        if (Time.time < qReadyTime) return;
        if (isCAbilityActive) return;

        qReadyTime = Time.time + qCooldown;

        Vector3 centre = Body.position;
        int painted = 0;

        if (scanVfx != null)
        {
            GameObject fx = Instantiate(scanVfx, centre, Body.rotation);
            PlayAll(fx, scanVfxScale);
            Destroy(fx, scanVfxLifetime);
        }

        if (cameraShake != null && scanShake > 0f) cameraShake.AddTrauma(scanShake);

        var seen = new List<VirusHealthBase>();
        var paintedTransforms = new List<Transform>();
        foreach (Collider c in Physics.OverlapSphere(centre, scanRadius))
        {
            VirusHealthBase v = c.GetComponentInParent<VirusHealthBase>();
            if (v == null || seen.Contains(v)) continue;
            seen.Add(v);

            GetOrAddVulnerability(v).ApplyMark(markDuration, markStartingBonus);

            if (markVfx != null)
            {
                GameObject fx = Instantiate(markVfx, v.transform.position + Vector3.up * markerHeight,
                                            Quaternion.identity, v.transform);
                PlayAll(fx, markVfxScale);
                Destroy(fx, markDuration);
            }

            Debug.DrawLine(centre, v.transform.position, Color.cyan, 1.5f);
            paintedTransforms.Add(v.transform);
            painted++;
        }

        //ring so the scan radius is readable while tuning
        DrawRing(centre, scanRadius, Color.cyan, 1.5f);

        //the same ring and lines, as real geometry the game camera renders
        if (showScanInGame)
            ScanPulseVisual.Spawn(centre, scanRadius, paintedTransforms, scanLineColour,
                                  scanLineWidth, scanVisualDuration, scanLineHeight, scanLineMaterial);

        OnScan?.Invoke();
        Debug.Log($"Cyborg scan: painted {painted} target(s) for {markDuration}s");
    }

    void TickMarks()
    {
        //walked backwards off the shared list: anything that dies mid-loop removes itself from
        //it, and going forwards would then skip whatever slid into the gap
        List<VirusVulnerability> all = VirusVulnerability.Active;

        for (int i = all.Count - 1; i >= 0; i--)
            if (all[i] != null) all[i].TickMark(markRampPerSecond, markMaxBonus);
    }

    //---------------------------------------------------------------- E: overdrive
    //a timed buff rather than a coroutine - every effect reads IsOverdriveActive each frame, so
    //there's nothing to clean up if he's interrupted, paused or swapped out mid-buff
    void TryActivateE()
    {
        if (Time.time < eReadyTime) return;
        if (isCAbilityActive) return;

        eReadyTime = Time.time + eCooldown;
        overdriveUntil = Time.time + overdriveDuration;

        if (overdriveVfx != null)
        {
            ClearOverdriveFx();
            activeOverdriveFx = Instantiate(overdriveVfx, Body.position, Body.rotation, Body);
            PlayAll(activeOverdriveFx, overdriveVfxScale);
            Destroy(activeOverdriveFx, overdriveDuration);
        }

        if (cameraShake != null && overdriveShake > 0f) cameraShake.AddTrauma(overdriveShake);

        OnOverdriveStarted?.Invoke();
    }

    void ClearOverdriveFx()
    {
        if (activeOverdriveFx != null) Destroy(activeOverdriveFx);
        activeOverdriveFx = null;
    }

    //---------------------------------------------------------------- X
    void TryActivateUltimate()
    {
        if (Time.time < ultimateReadyTime) return;
        if (isCAbilityActive) return;

        ultimateReadyTime = Time.time + ultimateCooldown;
        ultimateUntil = Time.time + ultimateDuration;

        reserveAmmo += ultimateBonusMags * magazineSize;

        if (cameraShake != null && ultimateShake > 0f) cameraShake.AddTrauma(ultimateShake);

        OnUltimateStarted?.Invoke();
    }

    //---------------------------------------------------------------- C
    void TryActivateC()
    {
        if (Time.time < cReadyTime) return;
        if (isCAbilityActive) return;
        if (lockedTarget == null) return;

        VirusVulnerability v = lockedTarget.GetComponent<VirusVulnerability>();
        if (v == null || v.cardHits < minimumStacks) return;

        cReadyTime = Time.time + cCooldown;
        StartCoroutine(Overload(lockedTarget, v));
    }

    IEnumerator Overload(VirusHealthBase target, VirusVulnerability v)
    {
        isCAbilityActive = true;
        OnOverload?.Invoke();

        if (cDamageDelay > 0f) yield return new WaitForSeconds(cDamageDelay);

        if (target != null && v != null)
        {
            int spent = v.ConsumeCardHits();
            float damage = spent * damagePerStack;

            if (overloadVfx != null)
            {
                GameObject fx = Instantiate(overloadVfx, AimPointOf(target), Quaternion.identity);
                PlayAll(fx, overloadVfxScale);
                Destroy(fx, 3f);
            }

            if (cameraShake != null && overloadShake > 0f) cameraShake.AddTrauma(overloadShake);

            //the stacks are gone, so this hit deliberately does not get their bonus
            DealDamage(target, damage, false);

            Debug.Log($"Cyborg overload: spent {spent} stacks for {damage} damage");
        }

        float remaining = cLockDuration - cDamageDelay;
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        isCAbilityActive = false;
    }

    //---------------------------------------------------------------- reload
    //the magazine refills when the reload finishes rather than when it starts, so switching away
    //mid-reload can't hand out a free mag
    void TryReload()
    {
        if (isReloading || currentAmmo >= magazineSize || reserveAmmo <= 0) return;

        isReloading = true;
        currentReloadDuration = EffectiveReloadTime;
        reloadEndTime = Time.time + currentReloadDuration;
        OnReloadStarted?.Invoke();
    }

    void TickReload()
    {
        if (!isReloading || Time.time < reloadEndTime) return;

        int taken = Mathf.Min(magazineSize - currentAmmo, reserveAmmo);
        currentAmmo += taken;
        reserveAmmo -= taken;
        isReloading = false;
    }

    void RegisterKill()
    {
        if (killsPerBonusMag <= 0) return;

        killsTowardMag++;
        if (killsTowardMag < killsPerBonusMag) return;

        killsTowardMag = 0;
        reserveAmmo += magazineSize;
    }

    //---------------------------------------------------------------- damage
    //every shot routes through here, so the stack, the paint and the perk are applied once and
    //can never be forgotten on a new ability
    public void BulletHit(VirusHealthBase target, float baseDamage)
    {
        if (target == null) return;

        VirusVulnerability v = GetOrAddVulnerability(target);
        DealDamage(target, baseDamage, true);

        //stack after the hit, so the shot that adds a stack doesn't also benefit from it
        v.AddCardHit();
    }

    public void DealDamage(VirusHealthBase target, float amount, bool useVulnerability)
    {
        if (target == null || amount <= 0f) return;

        float mult = SoloMultiplier();

        if (useVulnerability)
        {
            VirusVulnerability v = target.GetComponent<VirusVulnerability>();
            if (v != null) mult *= v.DamageMultiplier;
        }

        float final = amount * mult;

        //checked on both sides of the hit so a target already on its way out (destroyed at the
        //end of the frame) can't be counted twice by a second bullet
        bool wasAlive = target.CurrentHealth > 0f;
        target.TakeDamage(final);
        if (wasAlive && target.CurrentHealth <= 0f) RegisterKill();

        if (drawHitMarkers) DrawHitMarker(target, mult);

        if (logDamage)
            Debug.Log($"Cyborg hit for {final:F1} (base {amount}, x{mult:F2})");
    }

    //the perk: full bonus alone, nothing in a crowd
    public float SoloMultiplier()
    {
        int nearby = 0;
        foreach (Collider c in Physics.OverlapSphere(Body.position, crowdCheckRadius))
        {
            VirusHealthBase v = c.GetComponentInParent<VirusHealthBase>();
            if (v != null) nearby++;
        }

        float crowded = Mathf.Clamp01((nearby - 1f) / Mathf.Max(1f, crowdSize - 1f));
        return 1f + soloDamageBonus * (1f - crowded);
    }

    VirusVulnerability GetOrAddVulnerability(VirusHealthBase target)
    {
        VirusVulnerability v = target.GetComponent<VirusVulnerability>();
        if (v == null) v = target.gameObject.AddComponent<VirusVulnerability>();
        return v;
    }

    //---------------------------------------------------------------- effects helper
    //ported from RifleWeapon. one material shared by every tracer - making a new one per shot
    //at this fire rate would leak hundreds of materials a minute
    void SpawnTracer(Vector3 start, Vector3 end)
    {
        if (runtimeTracerMaterial == null)
            runtimeTracerMaterial = tracerMaterial != null ? tracerMaterial : new Material(Shader.Find("Sprites/Default"));

        GameObject tracer = new GameObject("CyborgTracer");
        LineRenderer lr = tracer.AddComponent<LineRenderer>();
        lr.sharedMaterial = runtimeTracerMaterial;
        lr.startColor = tracerColour;
        lr.endColor = tracerColour;
        lr.startWidth = tracerWidth;
        lr.endWidth = tracerWidth;
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        Destroy(tracer, tracerDuration);
    }

    void ClearMuzzleFx()
    {
        if (activeMuzzleFx != null) Destroy(activeMuzzleFx);
        activeMuzzleFx = null;
    }

    void PlayAll(GameObject fx, float scale)
    {
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Clear(true);
            ps.Play(true);
        }

        if (Mathf.Approximately(scale, 1f)) return;

        //asset-pack effects usually scale from the hierarchy, not their own transform
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        fx.transform.localScale *= scale;
    }

    //---------------------------------------------------------------- markers
    void UpdateMarkers()
    {
        List<VirusVulnerability> all = VirusVulnerability.Active;

        for (int i = all.Count - 1; i >= 0; i--)
        {
            VirusVulnerability v = all[i];
            if (v == null) continue;

            VirusHealthBase health = v.Health;
            if (health == null) continue;

            bool wants = v.FullyStacked;

            if (wants && !activeMarkers.ContainsKey(health) && fullStackMarker != null)
            {
                GameObject m = Instantiate(fullStackMarker,
                                           health.transform.position + Vector3.up * markerHeight,
                                           Quaternion.identity, health.transform);
                activeMarkers[health] = m;
            }

            //a purple diamond drawn in the scene view, so it reads even with no marker prefab
            if (wants && drawTargetRings)
                DrawDiamond(health.transform.position + Vector3.up * markerHeight, 0.4f,
                            new Color(0.7f, 0.2f, 1f));
        }

        //reused rather than rebuilt: this ran every frame and threw the list away each time
        deadMarkers.Clear();

        foreach (var kv in activeMarkers)
            if (kv.Key == null) deadMarkers.Add(kv.Key);

        foreach (var k in deadMarkers) activeMarkers.Remove(k);
    }

    //scratch space for the sweep above, kept so it is not allocated sixty times a second
    readonly List<VirusHealthBase> deadMarkers = new List<VirusHealthBase>();

    void ClearAllMarkers()
    {
        foreach (var kv in activeMarkers)
            if (kv.Value != null) Destroy(kv.Value);
        activeMarkers.Clear();
    }

    //a cross on whatever was just hit. it grows and shifts from white toward purple as the
    //multiplier climbs, so how softened up a target is reads at a glance.
    void DrawHitMarker(VirusHealthBase target, float multiplier)
    {
        Vector3 at = AimPointOf(target);

        float t = Mathf.InverseLerp(1f, 3f, multiplier);
        float size = Mathf.Lerp(0.35f, 0.9f, t);
        Color colour = Color.Lerp(Color.white, new Color(0.75f, 0.25f, 1f), t);

        //drawn in camera space so it reads as a marker rather than a shape lying in the world
        Transform cam = playerCamera != null ? playerCamera.transform : Body;
        Vector3 right = cam.right * size;
        Vector3 up = cam.up * size;

        Debug.DrawLine(at - right - up, at + right + up, colour, hitMarkerDuration);
        Debug.DrawLine(at - right + up, at + right - up, colour, hitMarkerDuration);
    }

    void DrawDiamond(Vector3 centre, float size, Color colour)
    {
        Vector3 up = Vector3.up * size, right = Vector3.right * size, fwd = Vector3.forward * size;
        Debug.DrawLine(centre + up, centre + right, colour);
        Debug.DrawLine(centre + right, centre - up, colour);
        Debug.DrawLine(centre - up, centre - right, colour);
        Debug.DrawLine(centre - right, centre + up, colour);
        Debug.DrawLine(centre + up, centre + fwd, colour);
        Debug.DrawLine(centre + fwd, centre - up, colour);
        Debug.DrawLine(centre - up, centre - fwd, colour);
        Debug.DrawLine(centre - fwd, centre + up, colour);
    }

    void DrawRing(Vector3 centre, float radius, Color colour, float duration = 0f)
    {
        const int segs = 32;
        for (int i = 0; i < segs; i++)
        {
            float a0 = i / (float)segs * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segs * Mathf.PI * 2f;
            Debug.DrawLine(centre + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius,
                           centre + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius,
                           colour, duration);
        }
    }

    //---------------------------------------------------------------- scene-view feedback
    void LateUpdate()
    {
        if (!drawTargetRings) return;
        if (weaponSwitcher != null && weaponSwitcher.CurrentClass != WeaponSwitcher.PlayerClass.Cyborg) return;

        if (lockedTarget != null)
            DrawRing(lockedTarget.transform.position, 1.1f, Color.yellow);

        if (IsUltimateActive)
            foreach (var v in FindNearest(ultimateTargets, lockRange))
                if (v != null) DrawRing(v.transform.position, 1.4f, new Color(1f, 0.4f, 0f));
    }

    //---------------------------------------------------------------- HUD
    GUIStyle hudStyle;

    void OnGUI()
    {
        if (!showCyborgHud) return;
        if (weaponSwitcher != null && weaponSwitcher.CurrentClass != WeaponSwitcher.PlayerClass.Cyborg) return;

        hudStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontStyle = FontStyle.Bold
        };
        hudStyle.fontSize = hudFontSize;

        string label;

        if (lockedTarget != null)
        {
            VirusVulnerability v = lockedTarget.GetComponent<VirusVulnerability>();
            int stacks = v != null ? v.cardHits : 0;
            float bonus = v != null ? (v.DamageMultiplier - 1f) * 100f : 0f;

            label = $"LOCKED   stacks {stacks}   +{bonus:F0}%   solo x{SoloMultiplier():F2}";
            if (v != null && v.FullyStacked) label += "   ◆ MAXED";
        }
        else
        {
            label = $"no lock   ·   solo x{SoloMultiplier():F2}";
        }

        if (IsOverdriveActive) label += $"   [OVERDRIVE {OverdriveRemaining:F1}s]";
        if (IsUltimateActive) label += "   [OVERRIDE]";

        Rect rect = new Rect(0f, hudTopMargin, Screen.width, hudFontSize * 2f);

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), label, hudStyle);

        GUI.color = IsOverdriveActive ? new Color(0.4f, 1f, 1f) : Color.white;
        GUI.Label(rect, label, hudStyle);

        //ammo, one line under the lock readout
        bool infiniteNow = IsUltimateActive && ultimateInfiniteAmmo;
        string ammo;
        if (infiniteNow) ammo = "AMMO  INFINITE";
        else if (isReloading) ammo = $"RELOADING...   {reserveAmmo} spare";
        else ammo = $"AMMO  {currentAmmo} / {magazineSize}   ·   {reserveAmmo} spare ({SpareMags} mags)";

        if (killsPerBonusMag > 0) ammo += $"   ·   +1 mag in {killsPerBonusMag - killsTowardMag} kills";

        Rect ammoRect = new Rect(0f, hudTopMargin + hudFontSize * 1.6f, Screen.width, hudFontSize * 2f);

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.Label(new Rect(ammoRect.x + 2f, ammoRect.y + 2f, ammoRect.width, ammoRect.height), ammo, hudStyle);

        bool dry = !infiniteNow && currentAmmo <= 0 && reserveAmmo <= 0;
        GUI.color = dry ? new Color(1f, 0.35f, 0.35f)
                  : infiniteNow ? new Color(1f, 0.6f, 0.1f)
                  : Color.white;
        GUI.Label(ammoRect, ammo, hudStyle);

        GUI.color = Color.white; // GUI.color is global state, leave it as we found it
    }
}
