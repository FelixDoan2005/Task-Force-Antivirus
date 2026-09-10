using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

//single-target destruction, the opposite of the Demon. a heavy gun and augmented reflexes:
//the rifle locks on and does not spread, Q scans an area and paints everything in it, E blinks
//behind one target for an execute, and his perk pays him for fighting alone.
public class CyborgClass : MonoBehaviour
{
    [Header("Setup")]
    public Camera playerCamera;
    public Transform firePoint;
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

    [Tooltip("Spawned where the shot lands.")]
    public GameObject impactVfx;
    public float impactVfxScale = 1f;
    public float impactVfxLifetime = 1.5f;

    [Tooltip("Camera kick per shot. Small - this fires fast.")]
    public float fireShake = 0.05f;

    [Tooltip("How long the tracer line stays drawn.")]
    public float tracerDuration = 0.05f;

    float nextShotTime;
    VirusHealthBase lockedTarget;
    public VirusHealthBase LockedTarget => lockedTarget;

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

    float qReadyTime;

    //---------------------------------------------------------------- E: blink execute
    [Header("E - Blink Execute")]
    public float eCooldown = 18f;

    [Tooltip("How long he waits for a confirm before the ability cancels itself.")]
    public float confirmTimeout = 5f;

    [Tooltip("How far behind the target he lands.")]
    public float blinkBehindDistance = 2.2f;

    [Tooltip("Damage of the knife. A single heavy hit rather than a flurry.")]
    public float knifeDamage = 180f;

    [Tooltip("Multiplier applied on top, because it lands from behind.")]
    public float knifeCritMultiplier = 2f;

    [Tooltip("Delay from the blink to the knife landing - line this up with the animation.")]
    public float knifeDelay = 0.35f;

    [Tooltip("How long he is rooted for the execute - line this up with the animation.")]
    public float blinkLockDuration = 1.4f;

    public GameObject blinkVfx;
    public float blinkVfxScale = 1f;
    public GameObject knifeHitVfx;
    public float knifeHitVfxScale = 1f;
    public float blinkShake = 0.45f;

    float eReadyTime;
    bool awaitingConfirm;
    VirusHealthBase confirmTarget;
    float confirmExpiry;
    bool isBlinking;

    public bool AwaitingConfirm => awaitingConfirm;
    public bool IsBlinking => isBlinking;

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

    //---------------------------------------------------------------- HUD
    [Header("HUD (temporary)")]
    public bool showCyborgHud = true;
    public int hudFontSize = 22;
    public float hudTopMargin = 24f;

    //---------------------------------------------------------------- events for the animator
    public event System.Action OnShotFired;
    public event System.Action OnScan;
    public event System.Action OnBlinkExecute;
    public event System.Action OnUltimateStarted;
    public event System.Action OnOverload;

    Vector3 Origin => firePoint != null ? firePoint.position : transform.position;
    Vector3 Forward => playerCamera != null ? playerCamera.transform.forward : transform.forward;
    Transform Body => playerController != null ? playerController.transform : transform;

    public bool IsFiring { get; private set; }

    void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
    }

    //coroutines die when this is disabled, so anything they were going to clear has to be
    //cleared here - the same trap the BladeMaster and Demon both hit
    void OnDisable()
    {
        isCAbilityActive = false;
        isBlinking = false;
        awaitingConfirm = false;
        confirmTarget = null;
        ultimateUntil = 0f;
        IsFiring = false;

        ClearAllMarkers();

        if (playerMovement != null)
        {
            playerMovement.speedMultiplier = 1f;
            playerMovement.suspendJump = false;
            playerMovement.suspendDash = false;
            playerMovement.suspendSprint = false;
            playerMovement.allowCrouch = false;
            playerMovement.cameraShoulderOffset = Vector2.zero;
            playerMovement.extraCameraDistance = 0f;
        }
    }

    void Update()
    {
        bool isCyborg = weaponSwitcher == null ||
                        weaponSwitcher.CurrentClass == WeaponSwitcher.PlayerClass.Cyborg;

        if (!isCyborg)
        {
            if (awaitingConfirm) CancelConfirm();
            IsFiring = false;

            //another class is up - give the camera rig and the sprint back so nothing lingers
            if (playerMovement != null)
            {
                playerMovement.suspendSprint = false;
                playerMovement.allowCrouch = false;

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

        HandleFireInput();

        //rooted for the execute and the overload, asserted every frame so a pause can't
        //leave him stuck
        if (playerMovement != null)
        {
            bool rooted = isCAbilityActive || isBlinking;
            playerMovement.speedMultiplier = rooted ? 0f : 1f;
            playerMovement.suspendJump = rooted;
            playerMovement.suspendDash = rooted;

            //he can't shoot straight at a dead sprint - opening fire drops him back to a run,
            //so the Firing tree lands on the shoot-while-running clip instead of the sprint
            playerMovement.suspendSprint = IsFiring;

            //a soldier with a rifle can take a knee; the melee classes leave this off
            playerMovement.allowCrouch = !rooted;

            //asserted every frame rather than set once on class switch, so it survives a pause
            //or anything else that resets the camera rig
            playerMovement.cameraShoulderOffset = shoulderOffset;
            playerMovement.extraCameraDistance = cameraCloseness;
        }
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
        if (isCAbilityActive || isBlinking) { IsFiring = false; return; }

        //while waiting on a confirm the mouse buttons mean yes and no, not fire
        if (awaitingConfirm) { IsFiring = false; HandleConfirmInput(); return; }

        bool held = Mouse.current != null && Mouse.current.leftButton.isPressed;
        IsFiring = held;

        if (!held || Time.time < nextShotTime) return;
        nextShotTime = Time.time + fireInterval;

        if (IsUltimateActive)
        {
            //re-picked every volley so it keeps up as targets die
            foreach (var v in FindNearest(ultimateTargets, lockRange))
                if (v != null) Shoot(v, bulletDamage * ultimateDamageMultiplier);
        }
        else
        {
            Shoot(lockedTarget, bulletDamage);
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
            GameObject fx = Instantiate(muzzleVfx, origin, Quaternion.LookRotation(Forward),
                                        firePoint != null ? firePoint : transform);
            PlayAll(fx, muzzleVfxScale);
            Destroy(fx, 1f);
        }

        Vector3 aimPoint;

        if (target != null)
        {
            //aim assist blends between the crosshair and the locked target's centre of mass
            Vector3 locked = AimPointOf(target);
            Vector3 crosshair = origin + Forward * lockRange;
            aimPoint = Vector3.Lerp(crosshair, locked, aimAssist);
        }
        else
        {
            //nothing locked - he still fires, it just goes where he is pointing
            aimPoint = origin + Forward * lockRange;
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

        if (hitVirus == null) return;

        if (impactVfx != null)
        {
            GameObject fx = Instantiate(impactVfx, endPoint, Quaternion.LookRotation(-dir));
            PlayAll(fx, impactVfxScale);
            Destroy(fx, impactVfxLifetime);
        }

        BulletHit(hitVirus, damage);
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
        if (isCAbilityActive || isBlinking) return;

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
            painted++;
        }

        //ring so the scan radius is readable while tuning
        DrawRing(centre, scanRadius, Color.cyan, 1.5f);

        OnScan?.Invoke();
        Debug.Log($"Cyborg scan: painted {painted} target(s) for {markDuration}s");
    }

    void TickMarks()
    {
        foreach (var v in Object.FindObjectsByType<VirusVulnerability>(FindObjectsSortMode.None))
            v.TickMark(markRampPerSecond, markMaxBonus);
    }

    //---------------------------------------------------------------- E: blink execute
    void TryActivateE()
    {
        if (Time.time < eReadyTime) return;
        if (isCAbilityActive || isBlinking) return;

        //already choosing - a second E press cancels, same as left click
        if (awaitingConfirm) { CancelConfirm(); return; }

        var nearest = FindNearest(1, lockRange);
        if (nearest.Count == 0) return;

        confirmTarget = nearest[0];
        awaitingConfirm = true;
        confirmExpiry = Time.time + confirmTimeout;
    }

    void HandleConfirmInput()
    {
        if (confirmTarget == null || Time.time >= confirmExpiry) { CancelConfirm(); return; }
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame) { CancelConfirm(); return; }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            awaitingConfirm = false;
            eReadyTime = Time.time + eCooldown;   // only spent on a confirm, not on opening the prompt
            StartCoroutine(BlinkExecute(confirmTarget));
            confirmTarget = null;
        }
    }

    void CancelConfirm()
    {
        awaitingConfirm = false;
        confirmTarget = null;
    }

    IEnumerator BlinkExecute(VirusHealthBase target)
    {
        isBlinking = true;
        OnBlinkExecute?.Invoke();

        if (target != null)
        {
            //behind them relative to how they are facing
            Vector3 behind = target.transform.position - target.transform.forward * blinkBehindDistance;

            if (Physics.Raycast(behind + Vector3.up * 5f, Vector3.down, out RaycastHit ground,
                                20f, ~0, QueryTriggerInteraction.Ignore))
                behind = ground.point;

            TeleportTo(behind);
            FaceTarget(target.transform.position);

            if (blinkVfx != null)
            {
                GameObject fx = Instantiate(blinkVfx, Body.position, Body.rotation);
                PlayAll(fx, blinkVfxScale);
                Destroy(fx, 3f);
            }

            if (cameraShake != null && blinkShake > 0f) cameraShake.AddTrauma(blinkShake);
        }

        //the knife lands partway through the animation, not the instant he arrives
        if (knifeDelay > 0f) yield return new WaitForSeconds(knifeDelay);

        if (target != null)
        {
            if (knifeHitVfx != null)
            {
                GameObject fx = Instantiate(knifeHitVfx, AimPointOf(target), Quaternion.identity);
                PlayAll(fx, knifeHitVfxScale);
                Destroy(fx, 2f);
            }

            //from behind, so it crits - and it still takes the stacks and the paint
            DealDamage(target, knifeDamage * knifeCritMultiplier, true);
        }

        float remaining = blinkLockDuration - knifeDelay;
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        isBlinking = false;
    }

    //---------------------------------------------------------------- X
    void TryActivateUltimate()
    {
        if (Time.time < ultimateReadyTime) return;
        if (isCAbilityActive || isBlinking) return;

        ultimateReadyTime = Time.time + ultimateCooldown;
        ultimateUntil = Time.time + ultimateDuration;

        if (cameraShake != null && ultimateShake > 0f) cameraShake.AddTrauma(ultimateShake);

        OnUltimateStarted?.Invoke();
    }

    //---------------------------------------------------------------- C
    void TryActivateC()
    {
        if (Time.time < cReadyTime) return;
        if (isCAbilityActive || isBlinking) return;
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
        target.TakeDamage(final);

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
        foreach (var v in Object.FindObjectsByType<VirusVulnerability>(FindObjectsSortMode.None))
        {
            VirusHealthBase health = v.GetComponent<VirusHealthBase>();
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

        var dead = new List<VirusHealthBase>();
        foreach (var kv in activeMarkers)
            if (kv.Key == null) dead.Add(kv.Key);
        foreach (var k in dead) activeMarkers.Remove(k);
    }

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

    //---------------------------------------------------------------- helpers
    void TeleportTo(Vector3 position)
    {
        //the CharacterController fights direct position changes unless briefly disabled
        if (playerController == null) { Body.position = position; return; }

        playerController.enabled = false;
        playerController.transform.position = position;
        playerController.enabled = true;
    }

    void FaceTarget(Vector3 target)
    {
        Vector3 dir = target - Body.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) Body.rotation = Quaternion.LookRotation(dir);
    }

    //---------------------------------------------------------------- scene-view feedback
    void LateUpdate()
    {
        if (!drawTargetRings) return;
        if (weaponSwitcher != null && weaponSwitcher.CurrentClass != WeaponSwitcher.PlayerClass.Cyborg) return;

        if (lockedTarget != null)
            DrawRing(lockedTarget.transform.position, 1.1f, Color.yellow);

        if (awaitingConfirm && confirmTarget != null)
        {
            DrawRing(confirmTarget.transform.position, 1.6f, Color.cyan);
            DrawRing(confirmTarget.transform.position, 1.9f, Color.cyan);
        }

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

        if (awaitingConfirm)
        {
            label = "RIGHT CLICK to execute   ·   LEFT CLICK to cancel";
        }
        else if (lockedTarget != null)
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

        if (IsUltimateActive) label += "   [OVERRIDE]";

        Rect rect = new Rect(0f, hudTopMargin, Screen.width, hudFontSize * 2f);

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), label, hudStyle);

        GUI.color = awaitingConfirm ? Color.cyan : Color.white;
        GUI.Label(rect, label, hudStyle);

        GUI.color = Color.white; // GUI.color is global state, leave it as we found it
    }
}
