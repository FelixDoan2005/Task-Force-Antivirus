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

    //when the slash actually fires. a gap above 0 positions it relative to the thrust rather
    //than to the keypress, so nudging the thrust carries the slash with it
    public float EffectiveSlashDelay => slashGapAfterThrust > 0f
        ? thrustDelay + slashGapAfterThrust
        : slashDelay;

    //0 while the thrust is going out, 1 once the slash starts - lets the sword be posed
    //differently for each half of the cast animation
    public int CastPhase => (Time.time - castStartTime) < EffectiveSlashDelay ? 0 : 1;

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

    //fired when C is activated
    public event System.Action OnCAbility;

    public bool IsCAbilityActive => isCAbilityActive;

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

    //everything about one swing's slash effect, grouped so each combo step is a single
    //foldout in the Inspector rather than the same index across six separate arrays
    [System.Serializable]
    public struct ComboSlashFx
    {
        [Tooltip("Leave empty for no effect on this swing.")]
        public GameObject vfx;

        [Tooltip("Wait this long after the click before it appears, so it lands with the blade.")]
        public float delay;

        [Tooltip("Relative to the player: X = right, Y = up, Z = forward.")]
        public Vector3 positionOffset;

        [Tooltip("Z rolls the slash (180 flips it), Y sweeps it the other way.")]
        public Vector3 rotationOffset;

        [Tooltip("0 is treated as 1.")]
        public float scale;

        [Tooltip("0 falls back to 2 seconds.")]
        public float lifetime;
    }

    [Header("Combo FX")]
    [Tooltip("One entry per combo step, in order.")]
    public ComboSlashFx[] comboSlashFx = new ComboSlashFx[5];

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

    [Header("Q - Dash FX")]
    [Tooltip("Parented to the player for the length of the dash, so it follows automatically.")]
    public GameObject dashVfxPrefab;
    [Tooltip("Relative to the dash direction: X = right, Y = up, Z = forward.")]
    public Vector3 dashVfxPositionOffset;
    public Vector3 dashVfxRotationOffset;
    public float dashVfxScale = 1f;
    [Tooltip("How long the trail keeps playing after the dash ends.")]
    public float dashVfxLinger = 0.3f;

    [Tooltip("Seconds between repeat spawns along the dash path, leaving a continuous streak. " +
             "0 spawns a single copy parented to the player instead.")]
    public float dashVfxSpawnInterval = 0.03f;

    [Tooltip("Spawned once where the dash finishes - the spin at the end.")]
    public GameObject dashEndVfxPrefab;
    public Vector3 dashEndVfxPositionOffset;
    public Vector3 dashEndVfxRotationOffset;
    public float dashEndVfxScale = 1f;
    public float dashEndVfxLifetime = 1.5f;

    [Tooltip("Spawned on each virus the dash passes through.")]
    public GameObject dashHitVfxPrefab;
    public float dashHitVfxScale = 1f;
    public float dashHitVfxLifetime = 1f;

    [Tooltip("Camera trauma added when the dash starts. 0 for none.")]
    public float dashShake = 0.25f;

    float qReadyTime;
    bool isDashing;
    GameObject activeDashVfx;
    float dashVfxTimer;
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
    [Tooltip("Delay from pressing E to the horizontal slash firing. Ignored if Slash Gap is above 0.")]
    public float slashDelay = 0.75f;

    [Tooltip("Gap between the thrust and the slash. Above 0 this overrides Slash Delay, so you " +
             "tune the spacing between the two hits directly instead of juggling two absolute times.")]
    public float slashGapAfterThrust = 0f;
    public float slashDamage = 20f;
    public float slashRange = 25f;     // travels much further
    public float slashWidth = 10f;     // and is enormously wide
    public float slashSpeed = 22f;
    public float waveHeight = 3f;      // vertical thickness of both waves

    [Tooltip("Where each wave starts, relative to the player: X = right, Y = up, Z = forward. " +
             "Moves the actual hitbox, not just the effect.")]
    public Vector3 thrustOriginOffset;
    public Vector3 slashOriginOffset;

    [Header("E - FX")]
    [Tooltip("Travels along with the thrust wave. The pack's Projectile prefabs suit this well.")]
    public GameObject thrustVfxPrefab;
    [Tooltip("Relative to the wave: X = right, Y = up, Z = along its travel direction.")]
    public Vector3 thrustVfxPositionOffset;
    public Vector3 thrustVfxRotationOffset;
    public float thrustVfxScale = 1f;

    [Tooltip("Travels along with the horizontal slash wave.")]
    public GameObject slashVfxPrefab;
    [Tooltip("Relative to the wave: X = right, Y = up, Z = along its travel direction.")]
    public Vector3 slashVfxPositionOffset;
    public Vector3 slashVfxRotationOffset;
    public float slashVfxScale = 1f;

    [Tooltip("How long a wave's effect lingers after the wave finishes travelling.")]
    public float waveVfxLinger = 0.4f;

    float eReadyTime;
    bool isCasting; // blocks attacks/abilities for the length of the cast
    float castStartTime; // used to work out which half of the cast we're in

    [Header("C - Charged Blast")]
    public float cCooldown = 10f;

    [Tooltip("How long C locks out other actions - line this up with the animation length.")]
    public float cLockDuration = 1f;

    [Header("C - Charges")]
    [Tooltip("Cap on stored charges. Everything below is indexed by charge level, so changing " +
             "this resizes those arrays.")]
    public int cMaxCharges = 5;

    [Tooltip("How many auto attacks have to land to earn one charge.")]
    public int cAutoHitsPerCharge = 5;

    [Tooltip("On: every left click counts, whether or not it connects. Off: only clicks that " +
             "actually damage something count - which means swings at a target that already " +
             "died don't progress the meter.")]
    public bool cCountEveryClick = true;

    [Tooltip("Charges granted when the Q dash fires. 0 disables this source.")]
    public int cChargesPerDash = 1;

    [Tooltip("Charges granted the moment the ultimate finishes. 0 disables this source.")]
    public int cChargesFromUltimate = 5;

    [Tooltip("Off lets C be cast at 0 charges, using the 1-charge values.")]
    public bool cRequiresCharge = true;

    [Tooltip("Prints charge gains and spends to the console.")]
    public bool logCharges;

    //temporary - drawn with OnGUI so it needs no Canvas or prefab wiring. replace with a proper
    //UI element on the HUD once the BladeMaster's interface is designed.
    [Tooltip("TEMPORARY: on-screen charge readout at the top of the screen.")]
    public bool showChargeHud = true;
    public int chargeHudFontSize = 26;
    public float chargeHudTopMargin = 24f;

    int cCharges;
    int autoHitProgress; // auto attacks landed toward the next charge
    bool autoClickCounted; // whether the current left click has already been counted

    public int CCharges => cCharges;
    public int CMaxCharges => cMaxCharges;

    //0-1 progress toward the next charge from auto attacks - for a future UI meter
    public float AutoHitProgress01 => cAutoHitsPerCharge > 0
        ? autoHitProgress / (float)cAutoHitsPerCharge
        : 0f;

    //one effect the C ability spawns. an array of these so C can layer several - e.g. a ground
    //circle that stays put plus a shield that follows you, each on its own timing.
    [System.Serializable]
    public struct AbilityFx
    {
        [Tooltip("Leave empty to skip this one.")]
        public GameObject vfx;

        [Tooltip("Wait this long after the ability fires before spawning it.")]
        public float delay;

        [Tooltip("Relative to the player: X = right, Y = up, Z = forward. Ground circles want Y near 0.")]
        public Vector3 positionOffset;

        public Vector3 rotationOffset;

        [Tooltip("Fallback size, used for any charge level whose Scale Per Charge entry is 0.")]
        public float scale;

        [Tooltip("Size at each charge level: element 0 = 1 charge, element 4 = 5 charges. " +
                 "Any entry left at 0 falls back to Scale above.")]
        public float[] scalePerCharge;

        [Tooltip("0 means it lives until the ability's lock ends.")]
        public float lifetime;

        [Tooltip("Parent to the player so it follows. Off leaves it where it was cast.")]
        public bool followPlayer;
    }

    [Header("C - AOE Explosion")]
    [Tooltip("Damage at each charge level: element 0 = 1 charge, element 4 = 5 charges.")]
    public float[] cDamagePerCharge = { 40f, 70f, 110f, 165f, 240f };

    [Tooltip("Blast radius at each charge level, centred on the player.")]
    public float[] cRadiusPerCharge = { 4f, 5f, 6f, 7.5f, 9f };

    [Tooltip("Delay from pressing C to the blast landing - line this up with the animation.")]
    public float cDamageDelay = 0.4f;

    [Tooltip("Spawned on each virus caught in the blast. Both entries fire at every charge level - " +
             "layer two prefabs and give each its own Scale Per Charge.")]
    public ImpactFx[] cHitFx = new ImpactFx[2];

    [Tooltip("Camera trauma at each charge level.")]
    public float[] cShakePerCharge = { 0.2f, 0.3f, 0.4f, 0.55f, 0.75f };

    [Header("C - FX")]
    [Tooltip("Effects C spawns, each with its own timing and placement.")]
    public AbilityFx[] cVfx = new AbilityFx[2];

    float cReadyTime;
    bool isCAbilityActive;

    [Header("X - Ultimate: Blink Execution")]
    public float ultimateCooldown = 60f;
    public float ultimateWindup = 5f; // how long the player stands idle before the blink sequence starts
    public float ultimateRadius = 10f; // enemies within this radius (of where the windup started) get marked
    public float ultimateDamagePerHit = 20f;
    public int totalSlashes = 50; // if fewer targets than this exist, extras stack onto whoever has the most health left
    public float blinkInterval = 0.15f; // time between each blink/hit
    public float ultimateCameraPullback = 4f; // how far the camera eases out for the duration

    [Header("X - Ultimate FX")]
    public CameraShake cameraShake;

    [Tooltip("Shake level held during the windup, 0-1. Ramps from 0 to this over the windup.")]
    [Range(0f, 1f)] public float windupShakeIntensity = 0.6f;

    [Tooltip("Trauma added by each blink strike.")]
    public float blinkShake = 0.22f;

    [Tooltip("Spawned once where the player stands during the windup. Wind / charge-up particles.")]
    public GameObject windupVfxPrefab;

    [Tooltip("Pause after returning to the start position before all the stored damage lands.")]
    public float ultimateDamageDelay = 1f;

    [Tooltip("How far below the feet still counts as standing on something. Raise it if the " +
             "ultimate refuses to fire on stairs or uneven ground.")]
    public float groundCheckDistance = 0.3f;

    //an effect spawned at a victim's position. offsets are relative to that victim, not the player.
    [System.Serializable]
    public struct ImpactFx
    {
        [Tooltip("Leave empty to skip this one.")]
        public GameObject vfx;

        [Tooltip("Stagger it after the hit lands, so several effects don't all pop at once.")]
        public float delay;

        [Tooltip("Relative to the victim's position.")]
        public Vector3 positionOffset;
        public Vector3 rotationOffset;

        [Tooltip("Fallback size. Used as-is by the ultimate's effects, and by any charge level " +
                 "whose Scale Per Charge entry is 0.")]
        public float scale;

        [Tooltip("C only - size at each charge level: element 0 = 1 charge, element 4 = 5 charges. " +
                 "Any entry left at 0 falls back to Scale above.")]
        public float[] scalePerCharge;

        [Tooltip("0 falls back to 2 seconds.")]
        public float lifetime;
    }

    [Tooltip("Spawned on each victim when the stored damage lands. Add as many layers as you want.")]
    public ImpactFx[] ultimateFinisherFx = new ImpactFx[2];

    [Tooltip("Camera trauma when the stored damage lands.")]
    public float ultimateFinisherShake = 0.6f;

    [Tooltip("Spawned at each blink, facing the target. Slash / impact particles.")]
    public GameObject blinkSlashVfxPrefab;

    [Tooltip("How long spawned VFX live before being destroyed. Used as the fallback when a " +
             "specific lifetime below is left at 0.")]
    public float vfxLifetime = 2f;

    [Tooltip("Lifetime for the windup effect. 0 = destroyed when the windup ends.")]
    public float windupVfxLifetime = 0f;

    [Tooltip("Lifetime for each blink's slash effect. 0 = use Vfx Lifetime above.")]
    public float blinkSlashVfxLifetime = 0f;

    float ultimateReadyTime;
    bool isUltimateActive; // true for the whole windup + blink sequence - blocks Attack()/Q/E while it runs

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        //OnValidate is editor-only and isn't guaranteed to have run before play, so the arrays
        //get sized here too - otherwise a short/empty Scale Per Charge silently falls back to
        //the flat Scale and it looks like per-charge scaling does nothing
        NormaliseChargeConfig();
    }

    //keeps every per-charge array exactly Max Charges long, so the Inspector always shows one
    //slot per charge level instead of leaving you to resize five separate lists by hand
    void OnValidate()
    {
        NormaliseChargeConfig();
    }

    void NormaliseChargeConfig()
    {
        cMaxCharges = Mathf.Max(1, cMaxCharges);
        cAutoHitsPerCharge = Mathf.Max(0, cAutoHitsPerCharge);

        ResizeChargeArray(ref cDamagePerCharge);
        ResizeChargeArray(ref cRadiusPerCharge);
        ResizeChargeArray(ref cShakePerCharge);

        //both FX arrays are structs, so each entry has to be copied out, changed, and put back
        if (cVfx != null)
        {
            for (int i = 0; i < cVfx.Length; i++)
            {
                AbilityFx fx = cVfx[i];
                ResizeChargeArray(ref fx.scalePerCharge);
                cVfx[i] = fx;
            }
        }

        if (cHitFx != null)
        {
            for (int i = 0; i < cHitFx.Length; i++)
            {
                ImpactFx fx = cHitFx[i];
                ResizeChargeArray(ref fx.scalePerCharge);
                cHitFx[i] = fx;
            }
        }
    }

    //grows by repeating the last value rather than padding with zeros, so a new slot starts
    //somewhere sensible instead of silently disabling itself
    void ResizeChargeArray(ref float[] values)
    {
        if (values != null && values.Length == cMaxCharges) return;

        float[] resized = new float[cMaxCharges];
        for (int i = 0; i < resized.Length; i++)
        {
            if (values != null && values.Length > 0)
                resized[i] = values[Mathf.Min(i, values.Length - 1)];
        }
        values = resized;
    }

    //if this gets disabled mid-ultimate (class switch, pause) Update stops running, so the
    //multipliers would stay stuck at their ultimate values - reset them on the way out
    void OnDisable()
    {
        //coroutines die when this component is disabled or the object deactivated, so any
        //state they were meant to clear on the way out has to be cleared here instead -
        //otherwise a cast or ultimate interrupted by a pause/class switch leaves these stuck
        //true and silently blocks every ability from then on
        isCasting = false;
        isUltimateActive = false;
        isDashing = false;
        isCAbilityActive = false;

        //the dash trail is parented to the player, so it would survive a class switch otherwise
        if (activeDashVfx != null)
        {
            Destroy(activeDashVfx);
            activeDashVfx = null;
        }

        if (playerHealth != null) playerHealth.damageTakenMultiplier = 1f;
        if (playerMovement != null)
        {
            playerMovement.speedMultiplier = 1f;
            playerMovement.suspendJump = false;
            playerMovement.suspendDash = false;
            playerMovement.extraCameraDistance = 0f;
            playerMovement.suspendLook = false;
            playerMovement.easePitchToTarget = false;

            //only the ultimate sets this, and only its coroutine clears it - so an ult cut short
            //by a pause or class switch would leave the player frozen in place for good
            playerMovement.suspendGravity = false;
        }
    }

    void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame) TryActivateQ();
            if (Keyboard.current.eKey.wasPressedThisFrame) TryActivateE();
            if (Keyboard.current.cKey.wasPressedThisFrame) TryActivateC();
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

            //lay down a fresh copy every interval so the effect streaks along the whole path
            if (dashVfxSpawnInterval > 0f && dashVfxPrefab != null)
            {
                dashVfxTimer += Time.deltaTime;
                while (dashVfxTimer >= dashVfxSpawnInterval)
                {
                    dashVfxTimer -= dashVfxSpawnInterval;
                    SpawnDashTrailVfx();
                }
            }
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

                    //impact on whoever we just carved through
                    if (dashHitVfxPrefab != null)
                    {
                        GameObject hitFx = SpawnVfx(dashHitVfxPrefab, vh.transform.position,
                                                    Quaternion.LookRotation(dashDirection));
                        if (hitFx != null)
                        {
                            if (!Mathf.Approximately(dashHitVfxScale, 1f))
                                hitFx.transform.localScale *= dashHitVfxScale;
                            Destroy(hitFx, dashHitVfxLifetime);
                        }
                    }
                }
             }


            if (Time.time >= dashEndTime || blocked)
            {
                isDashing = false;
                hitDuringDash.Clear();

                //let the trail finish playing where the dash stopped instead of cutting out
                if (activeDashVfx != null)
                {
                    activeDashVfx.transform.SetParent(null, true); // unparent so it stays put
                    Destroy(activeDashVfx, dashVfxLinger);
                    activeDashVfx = null;
                }

                //the spin at the end of the dash
                if (dashEndVfxPrefab != null)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, dashDirection);
                    Vector3 endOffset = side * dashEndVfxPositionOffset.x
                                        + Vector3.up * dashEndVfxPositionOffset.y
                                        + dashDirection * dashEndVfxPositionOffset.z;

                    GameObject endFx = SpawnVfx(dashEndVfxPrefab,
                                                playerController.transform.position + endOffset,
                                                Quaternion.LookRotation(dashDirection) * Quaternion.Euler(dashEndVfxRotationOffset));
                    if (endFx != null)
                    {
                        if (!Mathf.Approximately(dashEndVfxScale, 1f))
                            endFx.transform.localScale *= dashEndVfxScale;
                        Destroy(endFx, dashEndVfxLifetime);
                    }
                }

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
        if (isCAbilityActive) return;
        if (Time.time < attackReadyTime) return; // still mid-swing, ignore the click

        attackReadyTime = Time.time + comboStagger;

        if (Time.time - lastAttackTime > comboWindow)
            comboStep = 0; // window expired, back to hit 1
        else
            comboStep = (comboStep + 1) % maxComboHits; // clicked in time, advance (loops after hit 5)

        lastAttackTime = Time.time;
        autoClickCounted = false; // this click hasn't damaged anything yet

        //counted here rather than at the damage site, so a swing into thin air (or at something
        //the previous swing already killed) still moves the meter
        if (cCountEveryClick)
        {
            autoClickCounted = true;
            RegisterAutoHitLanded();
        }

        OnComboAttack?.Invoke(comboStep); // let the animator play the matching swing clip
        SpawnComboSlashVfx();

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

        //one entry per virus, not per collider - a virus with a body collider plus a couple of
        //trigger volumes was being added several times and taking the swing's damage once each
        HashSet<VirusHealthBase> seen = new HashSet<VirusHealthBase>();

        Collider[] hits = Physics.OverlapSphere(origin, range);
        foreach (Collider c in hits)
        {
            VirusHealthBase vh = c.GetComponentInParent<VirusHealthBase>();
            if (vh == null) continue;

            Vector3 dirToTarget = (c.transform.position - origin).normalized;
            if (Vector3.Angle(Forward, dirToTarget) > meleeArc * 0.5f) continue; // outside the melee cone entirely

            if (!seen.Add(vh)) continue;

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

            DealAutoDamage(target.virus);
            Debug.DrawLine(origin, target.virus.transform.position, Color.white, 0.2f);
        }
    }

    // forward-facing hit check for the stab, since it thrusts along Forward instead of sweeping through the right/up arc
    // uses the same generous meleeArc as the other swings, not the old narrow stabArc, so a target dead ahead doesn't get missed
    // just because the stab's origin is shifted sideways/up toward center-mass
    void HitStab(Vector3 origin)
    {
        Vector3 viewForward = Forward;

        //same per-virus de-duplication as the sweeping swings
        HashSet<VirusHealthBase> seen = new HashSet<VirusHealthBase>();

        Collider[] hits = Physics.OverlapSphere(origin, range);
        foreach (Collider c in hits)
        {
            VirusHealthBase vh = c.GetComponentInParent<VirusHealthBase>();
            if (vh == null) continue;

            Vector3 dirToTarget = (c.transform.position - origin).normalized;
            if (Vector3.Angle(viewForward, dirToTarget) > meleeArc * 0.5f) continue;

            if (!seen.Add(vh)) continue;

            DealAutoDamage(vh);
            Debug.DrawLine(origin, vh.transform.position, Color.white, 0.2f);
        }
    }

    //every point where a left-click swing actually damages something goes through here, so the
    //charge counter can't drift out of sync with what the sword really hit. counted once per
    //click no matter how many viruses that click caught.
    void DealAutoDamage(VirusHealthBase virus)
    {
        virus.TakeDamage(damage);

        if (autoClickCounted) return;
        autoClickCounted = true;

        RegisterAutoHitLanded();
    }

    void TryActivateQ()
    {
        if (Time.time < qReadyTime) return;
        if (isUltimateActive) return;
        if (isBlocking) return;
        if (isCasting) return;
        if (isCAbilityActive) return;
        if (weaponSwitcher != null && weaponSwitcher.CurrentClass != WeaponSwitcher.PlayerClass.BladeMaster) return;
        qReadyTime = Time.time + qCooldown;

        dashDirection = playerCamera.transform.forward;
        dashDirection.y = 0f;
        dashDirection = dashDirection.normalized;
        dashSpeed = dashDistance / dashDuration; // tune by distance instead of speed directly
        isDashing = true;
        dashEndTime = Time.time + dashDuration;
        hitDuringDash.Clear();

        if (cameraShake != null && dashShake > 0f)
            cameraShake.AddTrauma(dashShake);

        //granted on the dash going off, not on it connecting - it's a mobility reward, not a hit one
        AddCharges(cChargesPerDash, "Q dash");

        dashVfxTimer = 0f;

        if (dashVfxSpawnInterval > 0f)
        {
            //repeat-spawn mode: drop one now, the dash loop keeps laying more along the path
            SpawnDashTrailVfx();
        }
        else if (dashVfxPrefab != null)
        {
            //single-copy mode: parented to the player so it follows without being moved each frame
            activeDashVfx = SpawnDashVfxAt(playerController.transform.position, playerController.transform);
        }

        OnDash?.Invoke();
    }

    void TryActivateE()
    {
        if (Time.time < eReadyTime) return;
        if (isUltimateActive) return;
        if (isBlocking) return;
        if (isCasting) return;
        if (isCAbilityActive) return;
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

        StartCoroutine(FireShockwave(thrustDelay, thrustDamage, thrustRange, thrustWidth, thrustSpeed,
                                     Color.cyan, thrustOriginOffset, thrustVfxPrefab, thrustVfxPositionOffset,
                                     thrustVfxRotationOffset, thrustVfxScale));
        StartCoroutine(FireShockwave(EffectiveSlashDelay, slashDamage, slashRange, slashWidth, slashSpeed,
                                     Color.magenta, slashOriginOffset, slashVfxPrefab, slashVfxPositionOffset,
                                     slashVfxRotationOffset, slashVfxScale));

        yield return new WaitForSeconds(castLockDuration);
        isCasting = false;
    }

    //sweeps a wide flat box forward along the facing direction, damaging each virus once.
    //width is what separates the two waves - the thrust is narrow and hard hitting, the slash
    //is enormously wide but weaker.
    IEnumerator FireShockwave(float delay, float damage, float range, float width, float speed,
                              Color debugColor, Vector3 originOffset, GameObject vfxPrefab,
                              Vector3 vfxPositionOffset, Vector3 vfxRotationOffset, float vfxScale)
    {
        yield return new WaitForSeconds(delay);

        Vector3 dir = Forward;
        dir.y = 0f;
        dir = dir.normalized;
        Vector3 side = Vector3.Cross(Vector3.up, dir); // horizontal axis of the wave

        //shift where the wave actually begins - moves the hitbox, not just the visual
        Vector3 origin = Origin
                         + side * originOffset.x
                         + Vector3.up * originOffset.y
                         + dir * originOffset.z;

        Quaternion waveRotation = Quaternion.LookRotation(dir);
        Vector3 halfExtents = new Vector3(width * 0.5f, waveHeight * 0.5f, 0.5f);

        //offset is relative to the wave itself, so it holds up whichever way you're facing
        Vector3 vfxOffset = side * vfxPositionOffset.x
                            + Vector3.up * vfxPositionOffset.y
                            + dir * vfxPositionOffset.z;

        //the effect rides along with the wave rather than being a one-off flash, so what you
        //see lines up with where the hitbox actually is
        GameObject vfx = SpawnVfx(vfxPrefab, origin + vfxOffset,
                                  waveRotation * Quaternion.Euler(vfxRotationOffset));
        if (vfx != null && !Mathf.Approximately(vfxScale, 1f))
            vfx.transform.localScale *= vfxScale;

        HashSet<VirusHealthBase> alreadyHit = new HashSet<VirusHealthBase>();
        float travelled = 0f;

        while (travelled < range)
        {
            travelled += speed * Time.deltaTime;
            Vector3 point = origin + dir * travelled;

            if (vfx != null) vfx.transform.position = point + vfxOffset;

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

        //let it finish playing where it stopped rather than vanishing the instant the wave ends
        if (vfx != null) Destroy(vfx, waveVfxLinger);
    }

    //spends every stored charge at once - damage, radius, shake and effect size all scale with
    //how many went in
    void TryActivateC()
    {
        if (Time.time < cReadyTime) return;
        if (isUltimateActive) return;
        if (isBlocking) return;
        if (isCasting) return;
        if (isCAbilityActive) return;
        if (weaponSwitcher != null && weaponSwitcher.CurrentClass != WeaponSwitcher.PlayerClass.BladeMaster) return;

        if (cRequiresCharge && cCharges <= 0)
        {
            if (logCharges) Debug.Log("BladeMaster C: no charges stored");
            return;
        }

        //at least 1 so the 0-charge case (when charges aren't required) still uses the first
        //entry of each array rather than falling off the front
        int spent = Mathf.Max(1, cCharges);
        cCharges = 0;
        autoHitProgress = 0; // a spend resets progress toward the next one too

        if (logCharges) Debug.Log($"BladeMaster C: spent {spent} charge(s)");

        cReadyTime = Time.time + cCooldown;

        OnCAbility?.Invoke();
        StartCoroutine(CAbilitySequence(spent));
    }

    IEnumerator CAbilitySequence(int charges)
    {
        isCAbilityActive = true;

        //each entry runs on its own delay, so one coroutine per effect rather than trying to
        //interleave them here
        if (cVfx != null)
        {
            for (int i = 0; i < cVfx.Length; i++)
            {
                if (cVfx[i].vfx != null)
                    StartCoroutine(SpawnAbilityFx(cVfx[i], cLockDuration, charges));
            }
        }

        StartCoroutine(CExplosion(charges));

        yield return new WaitForSeconds(cLockDuration);
        isCAbilityActive = false;
    }

    //AOE blast centred on the player, landing partway through the animation
    IEnumerator CExplosion(int charges)
    {
        //read the scaled values up front - the arrays could be re-tuned mid-delay in the editor
        float damage = ChargeValue(cDamagePerCharge, charges, 60f);
        float radius = ChargeValue(cRadiusPerCharge, charges, 6f);
        float shake = ChargeValue(cShakePerCharge, charges, 0.4f);

        if (cDamageDelay > 0f)
            yield return new WaitForSeconds(cDamageDelay);

        Vector3 centre = playerController.transform.position;

        if (cameraShake != null && shake > 0f)
            cameraShake.AddTrauma(shake);

        //ring so the radius is visible while tuning
        const int segments = 32;
        for (int i = 0; i < segments; i++)
        {
            float a0 = (i / (float)segments) * Mathf.PI * 2f;
            float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
            Vector3 p0 = centre + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
            Vector3 p1 = centre + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
            Debug.DrawLine(p0, p1, Color.yellow, 1f);
        }

        //a virus can have several colliders, so track which ones have already been counted
        HashSet<VirusHealthBase> alreadyHit = new HashSet<VirusHealthBase>();

        foreach (Collider c in Physics.OverlapSphere(centre, radius))
        {
            VirusHealthBase vh = c.GetComponentInParent<VirusHealthBase>();
            if (vh == null || alreadyHit.Contains(vh)) continue;
            alreadyHit.Add(vh);

            //capture the position before damaging, in case the hit destroys it and a delayed
            //effect has nothing left to read from
            Vector3 hitPoint = vh.transform.position;

            if (cHitFx != null)
            {
                foreach (ImpactFx fx in cHitFx)
                {
                    if (fx.vfx == null) continue;

                    if (fx.delay > 0f) StartCoroutine(SpawnImpactFxAfter(fx, hitPoint, charges));
                    else SpawnImpactFxAt(fx, hitPoint, charges);
                }
            }

            vh.TakeDamage(damage);
            Debug.DrawLine(centre, hitPoint, Color.white, 0.5f);
        }
    }

    //picks the entry for this charge level, clamping rather than throwing if the array is the
    //wrong length - the Inspector can leave it short after Max Charges is raised
    float ChargeValue(float[] values, int charges, float fallback)
    {
        if (values == null || values.Length == 0) return fallback;
        return values[Mathf.Clamp(charges - 1, 0, values.Length - 1)];
    }

    //called once per auto attack that connects with at least one virus, whatever it hit -
    //landing on five things at once is still one attack landed
    //one left click that dealt damage = one count. five of those = one charge.
    void RegisterAutoHitLanded()
    {
        //clamped rather than trusted - a 0 left in the Inspector used to make this return
        //immediately, which looked exactly like the counter being broken
        int needed = Mathf.Max(1, cAutoHitsPerCharge);

        autoHitProgress++;

        if (logCharges)
            Debug.Log($"BladeMaster auto landed: {autoHitProgress}/{needed}");

        if (autoHitProgress < needed) return;

        autoHitProgress = 0;
        AddCharges(1, "auto attacks");
    }

    //TEMPORARY charge readout. this component is disabled on pause and on other classes, so
    //OnGUI stops being called then and the readout hides itself without any extra checks.
    GUIStyle chargeHudStyle;

    void OnGUI()
    {
        if (!showChargeHud) return;
        if (weaponSwitcher != null && weaponSwitcher.CurrentClass != WeaponSwitcher.PlayerClass.BladeMaster) return;

        //built lazily - GUI.skin isn't available before the first OnGUI call
        chargeHudStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontStyle = FontStyle.Bold
        };
        chargeHudStyle.fontSize = chargeHudFontSize;

        //filled diamond per stored charge, hollow for the rest
        string pips = "";
        for (int i = 0; i < cMaxCharges; i++)
            pips += i < cCharges ? "◆" : "◇";

        string label = $"{pips}   {cCharges}/{cMaxCharges}";

        //progress toward the next one, so it's obvious the auto attacks are counting
        if (cAutoHitsPerCharge > 0 && cCharges < cMaxCharges)
            label += $"   [{autoHitProgress}/{cAutoHitsPerCharge}]";

        Rect rect = new Rect(0f, chargeHudTopMargin, Screen.width, chargeHudFontSize * 2f);

        //offset copy behind it, otherwise white text vanishes against a bright scene
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), label, chargeHudStyle);

        GUI.color = cCharges >= cMaxCharges ? Color.yellow : Color.white;
        GUI.Label(rect, label, chargeHudStyle);

        GUI.color = Color.white; // GUI.color is global state, leave it as we found it
    }

    void AddCharges(int amount, string source)
    {
        if (amount <= 0) return;

        int before = cCharges;
        cCharges = Mathf.Clamp(cCharges + amount, 0, cMaxCharges);

        if (logCharges && cCharges != before)
            Debug.Log($"BladeMaster charges: {cCharges}/{cMaxCharges} (+{cCharges - before} from {source})");
    }

    //spawns one AbilityFx entry after its delay and cleans it up afterwards
    IEnumerator SpawnAbilityFx(AbilityFx fx, float defaultLifetime, int charges)
    {
        if (fx.delay > 0f)
            yield return new WaitForSeconds(fx.delay);

        Vector3 flatForward = Forward;
        flatForward.y = 0f;
        flatForward = flatForward.normalized;
        Vector3 side = Vector3.Cross(Vector3.up, flatForward);

        Vector3 spawnPos = playerController.transform.position
                           + side * fx.positionOffset.x
                           + Vector3.up * fx.positionOffset.y
                           + flatForward * fx.positionOffset.z;

        //parenting makes it follow you, which suits a shield; unparented suits a ground circle
        GameObject go = SpawnVfx(fx.vfx, spawnPos,
                                 Quaternion.LookRotation(flatForward) * Quaternion.Euler(fx.rotationOffset),
                                 fx.followPlayer ? playerController.transform : null);
        if (go == null) yield break;

        //size comes from this charge level's entry, falling back to the flat Scale when that
        //entry is 0 - so an effect that shouldn't grow can just be left alone
        float scale = ChargeValue(fx.scalePerCharge, charges, 0f);
        if (scale <= 0f) scale = fx.scale;
        if (scale <= 0f) scale = 1f;

        ApplyVfxScale(go, scale);

        float life = fx.lifetime > 0f ? fx.lifetime : Mathf.Max(0f, defaultLifetime - fx.delay);
        yield return new WaitForSeconds(life);

        if (go != null)
        {
            //unparent first so it doesn't keep trailing you while it fades
            go.transform.SetParent(null, true);
            Destroy(go);
        }
    }

    void TryActivateUltimate()
    {
        if (Time.time < ultimateReadyTime) return;
        if (isUltimateActive) return;
        if (isBlocking) return;
        if (isCasting) return;
        if (isCAbilityActive) return;
        if (weaponSwitcher != null && weaponSwitcher.CurrentClass != WeaponSwitcher.PlayerClass.BladeMaster) return;

        //the windup suspends gravity, so firing this mid-jump used to freeze the player hanging
        //in the air for the whole 5 seconds. checked before the cooldown is set, so a rejected
        //press costs nothing.
        if (!IsGrounded())
        {
            if (logCharges) Debug.Log("BladeMaster ultimate: must be on the ground");
            return;
        }

        ultimateReadyTime = Time.time + ultimateCooldown;

        StartCoroutine(UltimateSequence());
    }

    //CharacterController.isGrounded only reports true on the frame after a Move() that hit the
    //floor, so it flickers on slopes and steps - the raycast is the reliable half
    bool IsGrounded()
    {
        if (playerController == null) return true; // nothing to test against, don't block the ability

        if (playerController.isGrounded) return true;

        //cast from just inside the capsule's base so it can't start below the floor
        Vector3 origin = playerController.transform.position
                         + playerController.center
                         - Vector3.up * (playerController.height * 0.5f - playerController.radius);

        return Physics.SphereCast(origin, playerController.radius * 0.9f, Vector3.down,
                                  out _, groundCheckDistance + playerController.radius * 0.1f,
                                  ~0, QueryTriggerInteraction.Ignore);
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

        //wind / charge-up effect, parented so it follows if anything moves the player
        GameObject windupVfx = SpawnVfx(windupVfxPrefab, startPosition, Quaternion.identity,
                                        playerController.transform);

        //0 keeps the default behaviour of living exactly as long as the windup does
        if (windupVfx != null && windupVfxLifetime > 0f)
        {
            Destroy(windupVfx, windupVfxLifetime);
            windupVfx = null; // already scheduled - don't destroy it a second time below
        }

        float windupTimer = 0f;
        while (windupTimer < ultimateWindup)
        {
            windupTimer += Time.deltaTime;

            //rumble that builds as the windup progresses. held at a level rather than added to,
            //since decay drains faster than small per-frame additions could ever accumulate
            if (cameraShake != null)
                cameraShake.SetTraumaFloor(windupShakeIntensity * (windupTimer / ultimateWindup));

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

        if (windupVfx != null) Destroy(windupVfx); // windup's over either way

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
        //damage is stored up rather than applied per strike, so nothing dies mid-sequence and the
        //whole total lands at once after the blademaster has finished and returned
        Dictionary<VirusHealthBase, float> pendingDamage = new Dictionary<VirusHealthBase, float>();

        foreach (VirusHealthBase target in hitSequence)
        {
            if (target == null) continue; // could've been destroyed by something else mid-sequence

            float randomAngle = Random.Range(0f, 360f);
            Vector3 randomDir = Quaternion.Euler(0f, randomAngle, 0f) * Vector3.forward;
            Vector3 blinkPoint = target.transform.position + randomDir * range;
            blinkPoint.y = target.transform.position.y; // keep it at the target's height instead of floating/embedding

            TeleportTo(blinkPoint);
            FaceTarget(target.transform.position);

            pendingDamage.TryGetValue(target, out float stored);
            pendingDamage[target] = stored + ultimateDamagePerHit;

            OnUltimateBlink?.Invoke();

            //punch the camera on every strike - these stack, so a fast blink sequence builds
            //into a proper rumble rather than 25 identical little taps
            if (cameraShake != null)
                cameraShake.AddTrauma(blinkShake);

            //slash effect at the victim, turned to face the way we struck from
            if (blinkSlashVfxPrefab != null)
            {
                Vector3 toTarget = target.transform.position - blinkPoint;
                Quaternion facing = toTarget.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(toTarget)
                    : Quaternion.identity;

                GameObject vfx = SpawnVfx(blinkSlashVfxPrefab, target.transform.position, facing);
                if (vfx != null)
                    Destroy(vfx, blinkSlashVfxLifetime > 0f ? blinkSlashVfxLifetime : vfxLifetime);
            }

            Debug.DrawLine(blinkPoint, target.transform.position, Color.magenta, blinkInterval);

            yield return new WaitForSeconds(blinkInterval);

            //once every marked target has taken enough stored damage to die there's nothing left
            //worth hitting, so cut the sequence short rather than slashing corpses
            if (AllTargetsLethallyDamaged(targetList, pendingDamage))
                break;
        }

        TeleportTo(startPosition);
        playerController.transform.rotation = startRotation; // face the way we did before the ult

        OnUltimateEnded?.Invoke(); // animation returns to idle while the damage is still pending

        //beat of stillness before it all lands
        yield return new WaitForSeconds(ultimateDamageDelay);

        ApplyPendingUltimateDamage(pendingDamage);

        //straight to full, so C can be fired immediately off the back of the ultimate
        AddCharges(cChargesFromUltimate, "ultimate");

        if (playerMovement != null)
        {
            playerMovement.suspendGravity = false;
            playerMovement.extraCameraDistance = 0f; // eases back in
            playerMovement.suspendLook = false;
            playerMovement.easePitchToTarget = false;
        }
        isUltimateActive = false;
    }

    //true once every target still alive has enough damage stored up to finish it
    bool AllTargetsLethallyDamaged(List<VirusHealthBase> targets, Dictionary<VirusHealthBase, float> pending)
    {
        foreach (VirusHealthBase t in targets)
        {
            if (t == null) continue; // already gone, doesn't need any more

            pending.TryGetValue(t, out float stored);
            if (stored < t.CurrentHealth) return false;
        }
        return true;
    }

    //everything the ultimate racked up lands at the same moment, with an effect on each victim
    void ApplyPendingUltimateDamage(Dictionary<VirusHealthBase, float> pending)
    {
        if (cameraShake != null && ultimateFinisherShake > 0f)
            cameraShake.AddTrauma(ultimateFinisherShake);

        foreach (KeyValuePair<VirusHealthBase, float> entry in pending)
        {
            VirusHealthBase target = entry.Key;
            if (target == null) continue;

            //capture the position before damaging - the hit may destroy the target, and a
            //delayed effect would then have no transform left to read from
            Vector3 hitPoint = target.transform.position;

            if (ultimateFinisherFx != null)
            {
                foreach (ImpactFx fx in ultimateFinisherFx)
                {
                    if (fx.vfx == null) continue;

                    if (fx.delay > 0f)
                        StartCoroutine(SpawnImpactFxAfter(fx, hitPoint));
                    else
                        SpawnImpactFxAt(fx, hitPoint);
                }
            }

            target.TakeDamage(entry.Value);
        }
    }

    //charges of 0 means "not a charged ability" - the flat Scale is used and Scale Per Charge
    //is ignored, which is what the ultimate's finisher effects want
    IEnumerator SpawnImpactFxAfter(ImpactFx fx, Vector3 position, int charges = 0)
    {
        yield return new WaitForSeconds(fx.delay);
        SpawnImpactFxAt(fx, position, charges);
    }

    void SpawnImpactFxAt(ImpactFx fx, Vector3 position, int charges = 0)
    {
        GameObject go = SpawnVfx(fx.vfx, position + fx.positionOffset, Quaternion.Euler(fx.rotationOffset));
        if (go == null) return;

        float scale = charges > 0 ? ChargeValue(fx.scalePerCharge, charges, 0f) : 0f;
        if (scale <= 0f) scale = fx.scale;   // that charge level left blank
        if (scale <= 0f) scale = 1f;

        ApplyVfxScale(go, scale);

        Destroy(go, fx.lifetime > 0f ? fx.lifetime : 2f);
    }

    //turn to face whoever we just blinked to, otherwise the swing animation plays side-on
    void FaceTarget(Vector3 targetPosition)
    {
        Vector3 dir = targetPosition - playerController.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            playerController.transform.rotation = Quaternion.LookRotation(dir);
    }

    //slash effect for whichever swing of the combo just fired. each step has its own slot so
    //you can drop different prefabs in and compare them swing by swing.
    void SpawnComboSlashVfx()
    {
        if (comboSlashFx == null || comboStep < 0 || comboStep >= comboSlashFx.Length) return;
        if (comboSlashFx[comboStep].vfx == null) return; // empty slot = no effect on this swing

        //capture the step now - comboStep will have moved on by the time a delayed spawn fires
        int step = comboStep;
        float delay = comboSlashFx[step].delay;

        if (delay > 0f)
            StartCoroutine(SpawnComboSlashAfter(step, delay));
        else
            SpawnComboSlashNow(step);
    }

    IEnumerator SpawnComboSlashAfter(int step, float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnComboSlashNow(step);
    }

    void SpawnComboSlashNow(int step)
    {
        if (comboSlashFx == null || step < 0 || step >= comboSlashFx.Length) return;

        ComboSlashFx fx = comboSlashFx[step];
        if (fx.vfx == null) return;

        Vector3 flatForward = Forward;
        flatForward.y = 0f;
        flatForward = flatForward.normalized;

        //offsets are relative to the way the player is facing, so they hold up no matter
        //which direction you're swinging in
        Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward);

        Vector3 spawnPos = Origin
                           + flatRight * fx.positionOffset.x
                           + Vector3.up * fx.positionOffset.y
                           + flatForward * fx.positionOffset.z;

        Quaternion rot = Quaternion.LookRotation(flatForward) * Quaternion.Euler(fx.rotationOffset);

        GameObject vfx = SpawnVfx(fx.vfx, spawnPos, rot);
        if (vfx == null) return;

        //0 means "not set" for both of these, so a freshly resized array doesn't produce
        //invisible effects or ones that get destroyed the instant they spawn
        float scale = fx.scale > 0f ? fx.scale : 1f;
        float life = fx.lifetime > 0f ? fx.lifetime : 2f;

        if (!Mathf.Approximately(scale, 1f))
            vfx.transform.localScale *= scale;

        Destroy(vfx, life);
    }

    //one copy of the dash trail effect, positioned and rotated relative to the dash direction
    GameObject SpawnDashVfxAt(Vector3 position, Transform parent = null)
    {
        if (dashVfxPrefab == null) return null;

        Vector3 side = Vector3.Cross(Vector3.up, dashDirection);
        Vector3 offset = side * dashVfxPositionOffset.x
                         + Vector3.up * dashVfxPositionOffset.y
                         + dashDirection * dashVfxPositionOffset.z;

        GameObject go = SpawnVfx(dashVfxPrefab, position + offset,
                                 Quaternion.LookRotation(dashDirection) * Quaternion.Euler(dashVfxRotationOffset),
                                 parent);

        if (go != null && !Mathf.Approximately(dashVfxScale, 1f))
            go.transform.localScale *= dashVfxScale;

        return go;
    }

    //drops an unparented copy at the player's current spot so it stays behind as a streak
    void SpawnDashTrailVfx()
    {
        GameObject go = SpawnDashVfxAt(playerController.transform.position);
        if (go != null) Destroy(go, dashVfxLinger);
    }

    //spawns a VFX prefab and explicitly starts its particle systems. a lot of asset-store
    //effects ship with Play On Awake disabled (their demo scenes trigger them from script),
    //so instantiating alone gets you an invisible object that does nothing.
    GameObject SpawnVfx(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null) return null;

        GameObject go = Instantiate(prefab, position, rotation, parent);
        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Clear(true);
            ps.Play(true);
        }
        return go;
    }

    //scaling a VFX root does nothing on most asset-pack prefabs: their particle systems ship with
    //Scaling Mode = Local, which ignores every parent transform, so the root grows while the
    //particles stay the same size. switching them to Hierarchy makes them follow the root.
    void ApplyVfxScale(GameObject go, float scale)
    {
        if (go == null || Mathf.Approximately(scale, 1f)) return;

        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main; // struct accessor - has to go through a local
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        go.transform.localScale *= scale;
    }

    // CharacterController fights direct position changes unless briefly disabled first
    void TeleportTo(Vector3 position)
    {
        playerController.enabled = false;
        playerController.transform.position = position;
        playerController.enabled = true;
    }
}
