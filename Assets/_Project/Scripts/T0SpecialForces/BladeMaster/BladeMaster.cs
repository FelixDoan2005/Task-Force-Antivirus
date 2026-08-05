using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class BladeMaster : MonoBehaviour
{
    [Header("Setup")]
    public Camera playerCamera;
    public Transform attackPoint;
    public CharacterController playerController; // BladeMaster lives on the weapon object, not the player root, so this needs to be wired up in the Inspector
    public PlayerController playerMovement; // used to freeze movement (suspendGravity) during the ultimate's windup + blink sequence, and to slow movement while blocking
    public PlayerHealth playerHealth; // used to reduce incoming damage while blocking
    public WeaponSwitcher weaponSwitcher; // used to check the player is actually in the BladeMaster class before letting abilities fire

    [Header("Block")]
    public float blockMoveSpeedMultiplier = 0.2f; // 20% of normal move speed while blocking
    public float blockDamageTakenMultiplier = 0.25f; // takes 25% of incoming damage (i.e. 75% reduction) while blocking
    bool isBlocking;

    public bool IsBlocking => isBlocking; // read by CharacterAnimatorDriver
    public bool IsDashing => isDashing;   // read by WeaponPoseOffset so the sword pose stays consistent mid-dash

    //true for the length of a swing (the stagger window), and which swing it is - lets the
    //weapon pose script give each of the 5 attacks its own sword placement
    public bool IsAttacking => Time.time < attackReadyTime;
    public int CurrentComboStep => comboStep;

    public bool IsUltimateActive => isUltimateActive;
    public bool IsCasting => isCasting; // true for the length of the E cast

    //0 while the thrust is going out, 1 once the slash starts - lets the sword be posed
    //differently for each half of the cast animation
    public int CastPhase => (Time.time - castStartTime) < slashDelay ? 0 : 1;

    //fired the moment a dash starts, so the animator driver can trigger the Q clip.
    //kept as an event so this class doesn't need to know an Animator exists.
    public event System.Action OnDash;

    //ultimate: windup begins, one per blink-strike, and the whole thing finishing
    public event System.Action OnUltimateStarted;
    public event System.Action OnUltimateBlink;
    public event System.Action OnUltimateEnded;

    //fired on every basic attack, carrying which step of the combo it is (0-4)
    public event System.Action<int> OnComboAttack;

    //fired when E is activated
    public event System.Action OnEnhancedActivated;

    [Header("Weapon")]
    public float damage = 5f;
    public float range = 2f;
    public float cooldown = 0.6f;
    public float swingDuration = 0.2f; // time over which a swing's hits land, staggered by which side of the arc they're on
    public float meleeArc = 100f; // generous forward-facing cone (degrees) - anything roughly ahead and in range can be hit by any swing
    public float forwardReach = 0.8f; // how far out in front of the body (fraction of range) the swing arcs sit

    [Header("Combo")]
    public int maxComboHits = 5;
    public float comboWindow = 1f; // how long you have after a hit to click again and continue the combo

    [Tooltip("Minimum gap between swings - clicks faster than this are ignored, so the combo can't be spammed. Keep it below Combo Window or the combo can never continue.")]
    public float comboStagger = 0.3f;

    int comboStep; // 0 = first hit in the sequence
    float lastAttackTime = -999f;
    float attackReadyTime; // enforces comboStagger

    Vector3 Origin => attackPoint != null ? attackPoint.position : transform.position;
    Vector3 Right => playerCamera.transform.right;
    Vector3 Up => playerCamera.transform.up;
    Vector3 Forward => playerCamera.transform.forward;

    [Header("Q - Dash Strike")]
    public float qCooldown = 8f;
    public float dashSpeed = 25f;
    public float dashDuration = 0.25f;
    public float dashDistance = 50f; // TODO: not wired in yet - if you want to tune by distance instead of speed*duration, derive dashSpeed = dashDistance / dashDuration in TryActivateQ
    public float dashHitRadius = 1f; // radius checked around the player each tick while dashing
    public float dashDamage = 100f;

    float qReadyTime;
    bool isDashing;
    Vector3 dashDirection;
    float dashEndTime;
    HashSet<VirusHealthBase> hitDuringDash = new HashSet<VirusHealthBase>(); // so a single dash doesn't multi-hit the same target every frame
    HashSet<Collider> ignoredDuringDash = new HashSet<Collider>(); // colliders we've told Physics to ignore for this dash, so we can restore them after

    [Header("E - Twin Shockwave (cast)")]
    public float eCooldown = 12f;
    public float castLockDuration = 1.2f; // how long the whole cast animation blocks other actions

    [Tooltip("Move speed multiplier while casting. 0 roots you in place for the whole animation.")]
    public float castMoveSpeedMultiplier = 0f;

    [Header("E - Thrust (first wave)")]
    [Tooltip("Delay from pressing E to the thrust firing - line this up with the thrust in the animation.")]
    public float thrustDelay = 0.25f;
    public float thrustDamage = 40f;
    public float thrustRange = 14f;
    public float thrustWidth = 1.5f;   // narrow, it's a piercing stab
    public float thrustSpeed = 30f;

    [Header("E - Slash (second wave)")]
    [Tooltip("Delay from pressing E to the horizontal slash firing.")]
    public float slashDelay = 0.75f;
    public float slashDamage = 20f;
    public float slashRange = 25f;     // travels much further
    public float slashWidth = 10f;     // and is enormously wide
    public float slashSpeed = 22f;
    public float waveHeight = 3f;      // vertical thickness of both waves

    float eReadyTime;
    bool isCasting; // blocks attacks/abilities for the length of the cast
    float castStartTime; // used to work out which half of the cast we're in

    [Header("X - Ultimate: Blink Execution")]
    public float ultimateCooldown = 60f;
    public float ultimateWindup = 5f; // how long the player stands idle before the blink sequence starts
    public float ultimateRadius = 10f; // enemies within this radius (of where the windup started) get marked
    public float ultimateDamagePerHit = 20f;
    public int totalSlashes = 50; // if fewer targets than this exist, extras stack onto whoever has the most health left
    public float blinkInterval = 0.15f; // time between each blink/hit
    public float ultimateCameraPullback = 4f; // how far the camera eases out for the duration

    float ultimateReadyTime;
    bool isUltimateActive; // true for the whole windup + blink sequence - blocks Attack()/Q/E while it runs

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    //if this gets disabled mid-ultimate (class switch, pause) Update stops running, so the
    //multipliers would stay stuck at their ultimate values - reset them on the way out
    void OnDisable()
    {
        if (playerHealth != null) playerHealth.damageTakenMultiplier = 1f;
        if (playerMovement != null)
        {
            playerMovement.speedMultiplier = 1f;
            playerMovement.suspendJump = false;
            playerMovement.suspendDash = false;
            playerMovement.extraCameraDistance = 0f;
            playerMovement.suspendLook = false;
            playerMovement.easePitchToTarget = false;
        }
    }

    void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame) TryActivateQ();
            if (Keyboard.current.eKey.wasPressedThisFrame) TryActivateE();
            if (Keyboard.current.xKey.wasPressedThisFrame) TryActivateUltimate();
        }

        //block - holding right click, only while actually in the BladeMaster class and not frozen mid-ultimate
        bool isBladeMasterClass = weaponSwitcher != null && weaponSwitcher.CurrentClass == WeaponSwitcher.PlayerClass.BladeMaster;
        isBlocking = isBladeMasterClass && !isUltimateActive && Mouse.current != null && Mouse.current.rightButton.isPressed;

        if (playerMovement != null)
        {
            //casting roots you in place - it takes priority over the block slowdown, though the
            //two can't really overlap since blocking stops you casting in the first place
            if (isCasting)
                playerMovement.speedMultiplier = castMoveSpeedMultiplier;
            else
                playerMovement.speedMultiplier = isBlocking ? blockMoveSpeedMultiplier : 1f;

            playerMovement.suspendJump = isCasting;
            playerMovement.suspendDash = isCasting;

            //driven from state every frame rather than set once when the ult starts. pausing
            //disables this component and OnDisable clears them, so they have to be re-asserted
            //here or unpausing mid-ult would hand camera control back to the player.
            playerMovement.suspendLook = isUltimateActive;
            playerMovement.easePitchToTarget = isUltimateActive;
            playerMovement.extraCameraDistance = isUltimateActive ? ultimateCameraPullback : 0f;
        }

        //fully invulnerable for the whole ultimate - the windup roots you in place and the blink
        //phase teleports you into the middle of everything, so taking damage through either is unfair
        if (playerHealth != null)
        {
            if (isUltimateActive)
                playerHealth.damageTakenMultiplier = 0f;
            else
                playerHealth.damageTakenMultiplier = isBlocking ? blockDamageTakenMultiplier : 1f;
        }

        //q ability
        if (isDashing)
        {
            // ignore collision with any virus we're about to run into, so the dash passes through them
            // physically instead of the CharacterController stopping on contact - only walls/terrain should block it
            // scan radius must cover this frame's full travel distance, not just dashHitRadius, or fast dashes
            // can reach a virus before it's ever been added to the ignore list
            float scanRadius = Mathf.Max(dashHitRadius, dashSpeed * Time.deltaTime) + 0.5f;
            Collider[] nearby = Physics.OverlapSphere(playerController.transform.position, scanRadius);
            foreach (Collider c in nearby)
            {
                if (c.GetComponentInParent<VirusHealthBase>() == null) continue;
                if (ignoredDuringDash.Add(c))
                    Physics.IgnoreCollision(playerController, c, true);
            }

            // CollisionFlags.Sides is Unity's own result from the Move() sweep - reliable even when the
            // CharacterController slides along a wall hit at an angle, unlike inferring it from distance moved
            CollisionFlags flags = playerController.Move(dashSpeed * Time.deltaTime * dashDirection);
            bool blocked = (flags & CollisionFlags.Sides) != 0;
            Collider[] hits = Physics.OverlapSphere(transform.position, dashHitRadius);

            foreach (Collider c in hits)
            {
                VirusHealthBase vh = c.GetComponentInParent<VirusHealthBase>();
                if (vh==null) continue;

                if (!hitDuringDash.Contains(vh))
                {
                    vh.TakeDamage(dashDamage);
                    Debug.DrawLine(playerCamera.transform.position, vh.transform.position, Color.white, 0.2f);
                    hitDuringDash.Add(vh);
                }
             }


            if (Time.time >= dashEndTime || blocked)
            {
                isDashing = false;
                hitDuringDash.Clear();

                // restore normal collision with everything this dash ignored
                foreach (Collider c in ignoredDuringDash)
                {
                    if (c != null)
                        Physics.IgnoreCollision(playerController, c, false);
                }
                ignoredDuringDash.Clear();
            }
        }

    }

    public void Attack()
    {
        if (isUltimateActive) return; // standing idle / mid-blink-sequence - no attacking
        if (isBlocking) return; // can't attack while holding block
        if (isCasting) return;  // locked out for the length of the E cast
        if (Time.time < attackReadyTime) return; // still mid-swing, ignore the click

        attackReadyTime = Time.time + comboStagger;

        if (Time.time - lastAttackTime > comboWindow)
            comboStep = 0; // window expired, back to hit 1
        else
            comboStep = (comboStep + 1) % maxComboHits; // clicked in time, advance (loops after hit 5)

        lastAttackTime = Time.time;

        OnComboAttack?.Invoke(comboStep); // let the animator play the matching swing clip

        switch (comboStep)
        {
            case 0: Swing1_DiagonalUp(); break;
            case 1: Swing2_TopRightToLeft(); break;
            case 2: Swing3_HorizontalAcross(); break;
            case 3: Swing4_BottomRightToTopLeft(); break;
            case 4: Swing5_PullBackStab(); break;
        }
    }

    // 1: red - sheathed at the left hip, diagonal slash up to the right at 45 degrees
    void Swing1_DiagonalUp()
    {
        Slash(new Vector2(-1f, -1f), new Vector2(1f, 1f), Color.red);
    }

    // 2: blue - blade drops slightly from swing 1's end, then swings at a flatter angle from top-right to the left
    void Swing2_TopRightToLeft()
    {
        DrawSwingArc(new Vector2(1f, 1f), new Vector2(1f, 0.5f), Color.gray, 0.15f); // the "drop" reposition, not a hit
        Slash(new Vector2(1f, 0.5f), new Vector2(-0.8f, 0f), Color.blue); // steeper angle, more outward reach
    }

    // 3: green - starts exactly where swing 2 ends, straight across horizontally left to right
    void Swing3_HorizontalAcross()
    {
        Slash(new Vector2(-0.8f, 0f), new Vector2(1f, 0f), Color.green);
    }

    // 4: yellow - same shallow angle as swing 2, but reversed: bottom-right to top-left
    void Swing4_BottomRightToTopLeft()
    {
        Slash(new Vector2(1f, -0.2f), new Vector2(-1f, 0.5f), Color.yellow);
    }

    // 5: black - final hit, pull the blade back then thrust; shifted up/right from the holster (bottom-left) so it stabs from center-mass, not from the hip
    void Swing5_PullBackStab()
    {
        Vector3 origin = Origin + (Right * 0.5f + Up * 0.5f) * (range * 0.3f);
        Vector3 pullBackPoint = origin - Forward * (range * 0.4f);
        Vector3 thrustPoint = origin + Forward * range;

        Debug.DrawLine(origin, pullBackPoint, Color.black, 0.25f);
        Debug.DrawLine(pullBackPoint, thrustPoint, Color.black, 0.25f);
        HitStab(origin);
    }

    // draws a swing's slash line, then hits whatever falls within that same coloured arc
    void Slash(Vector2 startLocal, Vector2 endLocal, Color color, float drawDuration = 0.25f)
    {
        DrawSwingArc(startLocal, endLocal, color, drawDuration);
        HitSwingArc(startLocal, endLocal);
    }

    // draws the swing's slash path out in front of the body (pushed forward by forwardReach), so it can be tuned visually in Scene view
    // NOTE: startLocal/endLocal are NOT normalized - their magnitude controls how far left/right/up/down that point reaches
    void DrawSwingArc(Vector2 startLocal, Vector2 endLocal, Color color, float duration = 0.25f)
    {
        Vector3 origin = Origin;
        Vector3 forwardOffset = Forward * range * forwardReach;
        Vector3 startPoint = origin + forwardOffset + (Right * startLocal.x + Up * startLocal.y) * range;
        Vector3 endPoint = origin + forwardOffset + (Right * endLocal.x + Up * endLocal.y) * range;

        Debug.DrawLine(startPoint, endPoint, color, duration);
    }

    // eligibility is a generous forward-facing cone (meleeArc) - anything roughly ahead and in range gets hit, same as a real sword would
    // connect with whatever's directly in front of you regardless of which decorative direction the swing animation goes.
    // startLocal/endLocal only decide the ORDER hits land in (which side of the swing they're on), not whether they land at all.
    void HitSwingArc(Vector2 startLocal, Vector2 endLocal)
    {
        Vector3 origin = Origin;
        Vector3 startDir = (Forward * forwardReach + Right * startLocal.x + Up * startLocal.y).normalized;
        Vector3 endDir = (Forward * forwardReach + Right * endLocal.x + Up * endLocal.y).normalized;
        float startSide = Vector3.Dot(startDir, Right);
        float endSide = Vector3.Dot(endDir, Right);

        List<(VirusHealthBase virus, float t)> targets = new List<(VirusHealthBase, float)>();

        Collider[] hits = Physics.OverlapSphere(origin, range);
        foreach (Collider c in hits)
        {
            VirusHealthBase vh = c.GetComponentInParent<VirusHealthBase>();
            if (vh == null) continue;

            Vector3 dirToTarget = (c.transform.position - origin).normalized;
            if (Vector3.Angle(Forward, dirToTarget) > meleeArc * 0.5f) continue; // outside the melee cone entirely

            float targetSide = Vector3.Dot(dirToTarget, Right);
            float t = Mathf.InverseLerp(startSide, endSide, targetSide); // which side of the swing they're on, for stagger order only

            targets.Add((vh, t));
        }

        targets.Sort((a, b) => a.t.CompareTo(b.t)); // hit order follows whichever side the blade swings in from

        StartCoroutine(SwingDamage(targets, origin));
    }

    // staggers each hit across swingDuration based on how far along the arc the target is - same idea as SwordWeapon's SwingDamage
    IEnumerator SwingDamage(List<(VirusHealthBase virus, float t)> targets, Vector3 origin)
    {
        float startTime = Time.time;
        foreach (var target in targets)
        {
            float hitTime = startTime + swingDuration * target.t;
            yield return new WaitUntil(() => Time.time >= hitTime);

            if (target.virus == null) continue;

            target.virus.TakeDamage(damage);
            Debug.DrawLine(origin, target.virus.transform.position, Color.white, 0.2f);
        }
    }

    // forward-facing hit check for the stab, since it thrusts along Forward instead of sweeping through the right/up arc
    // uses the same generous meleeArc as the other swings, not the old narrow stabArc, so a target dead ahead doesn't get missed
    // just because the stab's origin is shifted sideways/up toward center-mass
    void HitStab(Vector3 origin)
    {
        Vector3 viewForward = Forward;

        Collider[] hits = Physics.OverlapSphere(origin, range);
        foreach (Collider c in hits)
        {
            VirusHealthBase vh = c.GetComponentInParent<VirusHealthBase>();
            if (vh == null) continue;

            Vector3 dirToTarget = (c.transform.position - origin).normalized;
            if (Vector3.Angle(viewForward, dirToTarget) > meleeArc * 0.5f) continue;

            vh.TakeDamage(damage);
            Debug.DrawLine(origin, vh.transform.position, Color.white, 0.2f);
        }
    }

    void TryActivateQ()
    {
        if (Time.time < qReadyTime) return;
        if (isUltimateActive) return;
        if (isBlocking) return;
        if (isCasting) return;
        if (weaponSwitcher != null && weaponSwitcher.CurrentClass != WeaponSwitcher.PlayerClass.BladeMaster) return;
        qReadyTime = Time.time + qCooldown;

        dashDirection = playerCamera.transform.forward;
        dashDirection.y = 0f;
        dashDirection = dashDirection.normalized;
        dashSpeed = dashDistance / dashDuration; // tune by distance instead of speed directly
        isDashing = true;
        dashEndTime = Time.time + dashDuration;
        hitDuringDash.Clear();

        OnDash?.Invoke();
    }

    void TryActivateE()
    {
        if (Time.time < eReadyTime) return;
        if (isUltimateActive) return;
        if (isBlocking) return;
        if (isCasting) return;
        if (weaponSwitcher != null && weaponSwitcher.CurrentClass != WeaponSwitcher.PlayerClass.BladeMaster) return;
        eReadyTime = Time.time + eCooldown;

        OnEnhancedActivated?.Invoke(); // animator plays the thrust-then-slash clip
        StartCoroutine(CastSequence());
    }

    //both waves are scheduled off the same cast, each with its own delay so they line up with
    //the thrust and the slash in the animation
    IEnumerator CastSequence()
    {
        isCasting = true;
        castStartTime = Time.time;

        StartCoroutine(FireShockwave(thrustDelay, thrustDamage, thrustRange, thrustWidth, thrustSpeed, Color.cyan));
        StartCoroutine(FireShockwave(slashDelay, slashDamage, slashRange, slashWidth, slashSpeed, Color.magenta));

        yield return new WaitForSeconds(castLockDuration);
        isCasting = false;
    }

    //sweeps a wide flat box forward along the facing direction, damaging each virus once.
    //width is what separates the two waves - the thrust is narrow and hard hitting, the slash
    //is enormously wide but weaker.
    IEnumerator FireShockwave(float delay, float damage, float range, float width, float speed, Color debugColor)
    {
        yield return new WaitForSeconds(delay);

        Vector3 origin = Origin;
        Vector3 dir = Forward;
        dir.y = 0f;
        dir = dir.normalized;
        Vector3 side = Vector3.Cross(Vector3.up, dir); // horizontal axis of the wave

        Quaternion waveRotation = Quaternion.LookRotation(dir);
        Vector3 halfExtents = new Vector3(width * 0.5f, waveHeight * 0.5f, 0.5f);

        HashSet<VirusHealthBase> alreadyHit = new HashSet<VirusHealthBase>();
        float travelled = 0f;

        while (travelled < range)
        {
            travelled += speed * Time.deltaTime;
            Vector3 point = origin + dir * travelled;

            foreach (Collider c in Physics.OverlapBox(point, halfExtents, waveRotation))
            {
                VirusHealthBase vh = c.GetComponentInParent<VirusHealthBase>();
                if (vh == null || alreadyHit.Contains(vh)) continue;

                vh.TakeDamage(damage);
                alreadyHit.Add(vh);

                //white hit marker - a line from the wave to whoever it caught, plus a small
                //cross on the target itself so it's visible even when the wave is behind them
                Vector3 target = vh.transform.position;
                Debug.DrawLine(point, target, Color.white, 0.4f);
                Debug.DrawLine(target + Vector3.up * 1.2f, target - Vector3.up * 0.2f, Color.white, 0.4f);
                Debug.DrawLine(target - side * 0.6f, target + side * 0.6f, Color.white, 0.4f);
            }

            //draw the wave as a horizontal bar so its width is obvious in the scene view
            Debug.DrawLine(point - side * (width * 0.5f), point + side * (width * 0.5f), debugColor, 0.1f);
            yield return null;
        }
    }

    void TryActivateUltimate()
    {
        if (Time.time < ultimateReadyTime) return;
        if (isUltimateActive) return;
        if (isBlocking) return;
        if (isCasting) return;
        if (weaponSwitcher != null && weaponSwitcher.CurrentClass != WeaponSwitcher.PlayerClass.BladeMaster) return;
        ultimateReadyTime = Time.time + ultimateCooldown;

        StartCoroutine(UltimateSequence());
    }

    IEnumerator UltimateSequence()
    {
        isUltimateActive = true;
        if (playerMovement != null)
        {
            playerMovement.suspendGravity = true;
            playerMovement.extraCameraDistance = ultimateCameraPullback; // eases out from here
            playerMovement.suspendLook = true;       // stop mouse look
            playerMovement.easePitchToTarget = true; // but pan up to level first, don't lock facing the floor
        }

        OnUltimateStarted?.Invoke();

        Vector3 startPosition = playerController.transform.position;
        //FaceTarget spins the player toward each victim, so remember which way they were
        //looking and put them back - the camera's yaw follows this transform, so restoring
        //it restores the view direction too
        Quaternion startRotation = playerController.transform.rotation;

        // idle windup - continuously rescan every frame so enemies that walk into range mid-windup get marked
        // too (HashSet.Add ignores anything already marked), drawing a scan line to everyone currently marked
        HashSet<VirusHealthBase> targets = new HashSet<VirusHealthBase>();
        float windupTimer = 0f;
        while (windupTimer < ultimateWindup)
        {
            windupTimer += Time.deltaTime;

            Collider[] hits = Physics.OverlapSphere(startPosition, ultimateRadius);
            foreach (Collider c in hits)
            {
                VirusHealthBase vh = c.GetComponentInParent<VirusHealthBase>();
                if (vh != null)
                    targets.Add(vh);
            }

            foreach (VirusHealthBase target in targets)
            {
                if (target == null) continue;
                Debug.DrawLine(startPosition, target.transform.position, Color.red, 0f);
            }

            yield return null;
        }

        // safety exit - nothing was ever marked during the whole windup, so skip the blink phase entirely.
        // Cooldown's already ticking from the moment X was pressed, so nothing extra needed there.
        if (targets.Count == 0)
        {
            isUltimateActive = false;
            if (playerMovement != null)
            {
                playerMovement.suspendGravity = false;
                playerMovement.extraCameraDistance = 0f;
                playerMovement.suspendLook = false;
                playerMovement.easePitchToTarget = false;
            }
            OnUltimateEnded?.Invoke();
            yield break;
        }

        // build the 50-slash sequence: every target gets a slash first, then remaining slashes stack onto
        // whichever target currently has the most (virtual) health left - so one big enemy gets all 50,
        // and a room of equal-health enemies split them roughly evenly.
        List<VirusHealthBase> targetList = new List<VirusHealthBase>(targets);
        List<VirusHealthBase> hitSequence = new List<VirusHealthBase>();
        Dictionary<VirusHealthBase, float> remainingHealth = new Dictionary<VirusHealthBase, float>();
        foreach (VirusHealthBase t in targetList)
            remainingHealth[t] = t.CurrentHealth;

        for (int i = 0; i < totalSlashes; i++)
        {
            VirusHealthBase best = null;
            float bestHealth = float.NegativeInfinity;
            foreach (VirusHealthBase t in targetList)
            {
                if (t == null) continue;
                if (remainingHealth[t] > bestHealth)
                {
                    bestHealth = remainingHealth[t];
                    best = t;
                }
            }

            if (best == null) break; // everything's already dead

            hitSequence.Add(best);
            remainingHealth[best] -= ultimateDamagePerHit;
        }

        // shuffle the actual visit order so it plays out randomly, not "same enemy 5 times in a row"
        for (int i = hitSequence.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (hitSequence[i], hitSequence[j]) = (hitSequence[j], hitSequence[i]);
        }

        // blink to each target in turn and slash it - a fresh random spot around the target every single time,
        // so repeat visits (same target hit multiple times, whether back-to-back or later in the sequence)
        // never land in the same place twice
        foreach (VirusHealthBase target in hitSequence)
        {
            if (target == null) continue; // could've been destroyed by an earlier hit in this same sequence

            float randomAngle = Random.Range(0f, 360f);
            Vector3 randomDir = Quaternion.Euler(0f, randomAngle, 0f) * Vector3.forward;
            Vector3 blinkPoint = target.transform.position + randomDir * range;
            blinkPoint.y = target.transform.position.y; // keep it at the target's height instead of floating/embedding

            TeleportTo(blinkPoint);
            FaceTarget(target.transform.position);
            target.TakeDamage(ultimateDamagePerHit);
            OnUltimateBlink?.Invoke();
            Debug.DrawLine(blinkPoint, target.transform.position, Color.magenta, blinkInterval);

            yield return new WaitForSeconds(blinkInterval);
        }

        TeleportTo(startPosition);
        playerController.transform.rotation = startRotation; // face the way we did before the ult

        if (playerMovement != null)
        {
            playerMovement.suspendGravity = false;
            playerMovement.extraCameraDistance = 0f; // eases back in
            playerMovement.suspendLook = false;
            playerMovement.easePitchToTarget = false;
        }
        isUltimateActive = false;
        OnUltimateEnded?.Invoke();
    }

    //turn to face whoever we just blinked to, otherwise the swing animation plays side-on
    void FaceTarget(Vector3 targetPosition)
    {
        Vector3 dir = targetPosition - playerController.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            playerController.transform.rotation = Quaternion.LookRotation(dir);
    }

    // CharacterController fights direct position changes unless briefly disabled first
    void TeleportTo(Vector3 position)
    {
        playerController.enabled = false;
        playerController.transform.position = position;
        playerController.enabled = true;
    }
}
