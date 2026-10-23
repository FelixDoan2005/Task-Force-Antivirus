using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraPivot;
    public Transform followCamera;

    [Header("Movement")]
    public float moveSpeed = 6f;
    public float sprintSpeed = 10f;
    public float gravity = -20f;
    public float speedMultiplier = 1f; // e.g. 0.2f while blocking = moves at 20% speed

    [Tooltip("Separate channel for slows put on you by enemies. 1 = normal. The class scripts " +
             "own speedMultiplier and rewrite it every frame, so a debuff cannot live there.")]
    public float debuffSpeedMultiplier = 1f;

    [Tooltip("Abilities can set this to block jumping without touching gravity, so you still fall normally.")]
    public bool suspendJump;

    [Tooltip("Abilities can set this to block the double-tap dash. Read by DashAbility.")]
    public bool suspendDash;

    [Tooltip("Classes can set this to drop the player out of a sprint without blocking movement - " +
             "e.g. a shooter that can't fire at a full sprint. Holding shift simply stops counting.")]
    public bool suspendSprint;

    [Header("Crouch")]
    [Tooltip("Classes opt in to crouching. Off by default so the melee classes are untouched.")]
    public bool allowCrouch;

    [Tooltip("Controller height while crouched. Standing height is read once on Awake.")]
    public float crouchHeight = 1.2f;

    [Tooltip("Fraction of normal speed while crouched.")]
    public float crouchSpeedMultiplier = 0.5f;

    [Tooltip("How fast the collider eases between standing and crouched.")]
    public float crouchLerpSpeed = 10f;

    [Tooltip("How much of the crouch the camera follows down. 1 keeps the view at his shoulder " +
             "as he drops, 0 leaves it hanging at standing height looking down on him.")]
    [Range(0f, 1f)] public float crouchCameraFollow = 1f;

    //true while actually crouched. read by the active class's animator driver.
    public bool IsCrouching { get; private set; }

    float standingHeight;
    Vector3 standingCenter;

    [Header("Jump")]
    public float jumpHeight = 1.2f;

    [Tooltip("Abilities can scale the jump. 1 = normal. Set every frame by whatever wants it, like speedMultiplier.")]
    public float jumpHeightMultiplier = 1f;

    [Tooltip("Allows jumping for a short time after leaving the ground.")]
    public float coyoteTime = 0.12f;

    [Tooltip("Makes falling faster than rising for a snappier feel.")]
    public float fallMultiplier = 1.6f;

    [Header("Ground Check")]
    public float groundCheckRadius = 0.3f;
    public float groundCheckOffset = 0.15f;
    public LayerMask groundMask = ~0;

    [Header("Camera Collision")]
    public float cameraCollisionBuffer = 0.2f;
    int cameraCollisionMask;

    [Header("Camera Pullback")]
    [Tooltip("Extra distance abilities can request. Set it and the camera eases out; set it back to 0 and it eases in.")]
    public float extraCameraDistance;

    [Tooltip("How fast the camera eases toward the requested extra distance. Lower = more cinematic.")]
    public float cameraDistanceLerpSpeed = 2f;

    [Tooltip("Extra distance while sprinting. Added on top of any ability pullback rather than replacing it.")]
    public float sprintCameraPullback = 1.5f;

    float currentExtraCamDistance;

    [Tooltip("Abilities can set this to freeze mouse look. The camera keeps updating distance/collision, it just stops turning.")]
    public bool suspendLook;

    [Tooltip("Abilities can set this to ease the camera to a level pitch, so locking doesn't leave you staring at the floor.")]
    public bool easePitchToTarget;

    [Tooltip("Pitch to ease toward. 0 is dead level, positive looks down.")]
    public float targetPitch = 5f;

    public float pitchEaseSpeed = 3f;

    [Header("Look")]
    public float mouseSensitivity = 0.02f; // start around 0.015 - 0.03
    public bool lockVerticalLook = false;
    // assign Main Camera transform
    public float minCamDistance = 1.2f;     // closer when looking down
    public float maxCamDistance = 0.8f;     // further when looking up
    public float camHeightOffset = 0.0f;    // keep 0 for now

    CharacterController controller;
    Vector3 velocity;

    float lastGroundedTime = -999f;
    float pitch;
    float basePivotY;
    Vector3 baseCameraLocalPos;
    Transform camTransform;

    public bool suspendGravity;

    //lets an ability hand the player vertical motion directly - the grapple uses it to carry
    //her over a ledge as the hook lets go, and to make sure a cancelled pull starts from zero
    public void SetVerticalVelocity(float value) => velocity.y = value;
    public float VerticalVelocity => velocity.y;

    [Header("Flight")]
    [Tooltip("Set by a class that can fly. Gravity is replaced by direct vertical control.")]
    public bool flightMode;

    [Tooltip("Climb/descend speed while flying. Space rises, Left Ctrl drops.")]
    public float flightVerticalSpeed = 9f;

    [Header("Camera Orbit")]
    [Tooltip("Degrees to swing the camera around the player. 180 puts it in front, looking back at them.")]
    public float cameraYawOffset;

    [Tooltip("How fast the camera eases toward Camera Yaw Offset.")]
    public float cameraYawLerpSpeed = 4f;

    float currentYawOffset;

    [Header("Camera Shoulder")]
    [Tooltip("Over-the-shoulder offset a class can request. X = right of the player, Y = up. " +
             "Ranged classes want this; melee ones usually leave it at zero.")]
    public Vector2 cameraShoulderOffset;

    [Tooltip("How fast the camera slides to the shoulder when a class asks for it.")]
    public float cameraShoulderLerpSpeed = 6f;

    Vector2 currentShoulder;

    //raw WASD input this frame, -1..1 on each axis. read by the active class's animator driver
    public Vector2 MoveInput { get; private set; }

    //true only while sprinting AND actually moving
    public bool IsSprinting { get; private set; }


    void Awake()
    {
        controller = GetComponent<CharacterController>();
        standingHeight = controller.height;
        standingCenter = controller.center;
        velocity.y = -2f;
        groundMask &= ~LayerMask.GetMask("Player");
        cameraCollisionMask = ~LayerMask.GetMask("Player");

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        pitch = 10f;
        basePivotY = cameraPivot.localPosition.y;
        camTransform = Camera.main.transform;
        baseCameraLocalPos = camTransform.localPosition;
        camTransform = followCamera;
        if (camTransform != null)
            baseCameraLocalPos = camTransform.localPosition;
    }

    bool CheckGrounded()
    {
        Vector3 origin = new(controller.bounds.center.x, controller.bounds.min.y + groundCheckOffset, controller.bounds.center.z);
        return Physics.CheckSphere(origin, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);
    }

    void Update()
    {
        Look();
        MoveAndJump();
    }

    void Look()
    {
        if (Mouse.current == null) return;

        //zeroing the delta rather than returning early keeps the camera distance and wall
        //collision below still updating, so an ability's pullback still animates while locked
        Vector2 mouseDelta = suspendLook
            ? Vector2.zero
            : 60f * mouseSensitivity * Time.deltaTime * Mouse.current.delta.ReadValue();

        // yaw (left/right) rotates the hero
        transform.Rotate(Vector3.up * mouseDelta.x);

        // pitch (up/down) rotates the camera pivot
        pitch -= mouseDelta.y;

        //an ability can ask for a level view - eases there rather than snapping, so locking
        //the camera while you're staring at the floor still ends up looking forward
        if (easePitchToTarget)
            pitch = Mathf.Lerp(pitch, targetPitch, pitchEaseSpeed * Time.deltaTime);

        // clamp so you can't flip the camera
        pitch = Mathf.Clamp(pitch, -80f, 60f);

        if (cameraPivot != null)
        {
            //eased so an ability swinging the camera round reads as a move, not a cut
            currentYawOffset = Mathf.LerpAngle(currentYawOffset, cameraYawOffset,
                                               cameraYawLerpSpeed * Time.deltaTime);

            cameraPivot.localRotation = Quaternion.Euler(pitch, currentYawOffset, 0f);
            // small vertical slide based on pitch (tweak values)
            float t = Mathf.InverseLerp(-80f, 60f, pitch); // 0..1
            float yOffset = Mathf.Lerp(0f, 0.4f, t);  // down when looking down, up when looking up

            //the collider is already easing between the two heights, so measuring it keeps the
            //camera exactly in step with the body rather than running a second timer of its own
            float crouchDrop = (standingHeight - controller.height) * crouchCameraFollow;

            Vector3 p = cameraPivot.localPosition;
            p.y = basePivotY + yOffset - crouchDrop;
            cameraPivot.localPosition = p;
        }

        if (camTransform != null)
        {
            float camT = Mathf.InverseLerp(60f, -80f, pitch); // 0..1 across full pitch range
            float dist = Mathf.Lerp(minCamDistance, maxCamDistance, camT);
            float neutralBoost = 0.8f * (1f - Mathf.Abs(camT - 0.5f) * 2f); // 0 at extremes, max at center
            dist += neutralBoost;

            //ease toward whatever pullback is being asked for, so it glides out and back
            //instead of snapping. applied before the collision check so walls still clamp it.
            //ability pullback and sprint pullback add together rather than overriding each other.
            float targetExtra = extraCameraDistance + (IsSprinting ? sprintCameraPullback : 0f);
            currentExtraCamDistance = Mathf.Lerp(currentExtraCamDistance, targetExtra,
                                                 cameraDistanceLerpSpeed * Time.deltaTime);
            dist += currentExtraCamDistance;

            //slide toward whatever shoulder offset the active class wants, so switching between
            //a melee and a ranged class glides rather than cutting
            currentShoulder = Vector2.Lerp(currentShoulder, cameraShoulderOffset,
                                           cameraShoulderLerpSpeed * Time.deltaTime);

            // camera collision: don't let the desired position clip through walls
            Vector3 desiredLocalOffset = new(baseCameraLocalPos.x + currentShoulder.x,
                                             baseCameraLocalPos.y + currentShoulder.y,
                                             -dist);
            Vector3 desiredWorldPos = cameraPivot.TransformPoint(desiredLocalOffset);
            Vector3 toCam = desiredWorldPos - cameraPivot.position;
            float desiredLen = toCam.magnitude;

            if (desiredLen > 0.001f && Physics.Raycast(cameraPivot.position, toCam.normalized, out RaycastHit hit, desiredLen, cameraCollisionMask, QueryTriggerInteraction.Ignore))
            {
                dist *= hit.distance / desiredLen;
                dist = Mathf.Max(dist - cameraCollisionBuffer, 0.05f);
            }

            Vector3 c = baseCameraLocalPos;
            c.x += currentShoulder.x;   // over the shoulder for ranged classes
            c.y += currentShoulder.y;
            c.z = -dist;                // behind the pivot
            camTransform.localPosition = c;
        }
    }

    //hold Left Ctrl to crouch. the collider shrinks from the top, so his feet stay put, and
    //standing back up is refused if there's something overhead - otherwise you'd pop through
    //a ceiling and get shoved out of the level.
    void HandleCrouch()
    {
        if (!allowCrouch || flightMode)
        {
            //flight already owns Left Ctrl for descending
            if (IsCrouching) IsCrouching = false;
            RestoreStandingCollider();
            return;
        }

        bool wants = Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;

        if (!wants && IsCrouching && BlockedAbove())
            wants = true;   // stuck under something, stay down

        IsCrouching = wants;

        float targetHeight = IsCrouching ? crouchHeight : standingHeight;
        float targetCenterY = standingCenter.y - (standingHeight - targetHeight) * 0.5f;

        controller.height = Mathf.Lerp(controller.height, targetHeight, crouchLerpSpeed * Time.deltaTime);
        controller.center = Vector3.Lerp(controller.center,
                                         new Vector3(standingCenter.x, targetCenterY, standingCenter.z),
                                         crouchLerpSpeed * Time.deltaTime);
    }

    void RestoreStandingCollider()
    {
        if (Mathf.Approximately(controller.height, standingHeight)) return;
        controller.height = Mathf.Lerp(controller.height, standingHeight, crouchLerpSpeed * Time.deltaTime);
        controller.center = Vector3.Lerp(controller.center, standingCenter, crouchLerpSpeed * Time.deltaTime);
    }

    bool BlockedAbove()
    {
        Vector3 origin = transform.position + controller.center;
        float castDist = (standingHeight - controller.height) + 0.05f;
        return Physics.SphereCast(origin, controller.radius * 0.95f, Vector3.up,
                                  out _, castDist, groundMask, QueryTriggerInteraction.Ignore);
    }

    void MoveAndJump()
    {
        if (suspendGravity)
        {
            //another system (e.g. the ultimate, or the grapple) has full control right now.
            //these have to be cleared rather than just left alone - they're what the animator
            //blend trees read, so whatever was held at the moment control was taken away would
            //otherwise keep playing, running on the spot while the player is frozen.
            MoveInput = Vector2.zero;
            IsSprinting = false;
            return;
        }

        // Read movement input (WASD)
        Vector2 input = Vector2.zero; //resets movement
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) input.y += 1.0f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1.0f;
            if (Keyboard.current.dKey.isPressed) input.x += 1.0f;
            if (Keyboard.current.aKey.isPressed) input.x -= 1.0f;
        }

        MoveInput = input; // exposed so each class's model can drive its own locomotion blend tree

        HandleCrouch();

        Vector3 move = transform.right * input.x + transform.forward * input.y;
        if (move.sqrMagnitude > 1f) move.Normalize();

        // Sprint - you can't sprint from a crouch, you stand up first
        bool sprintHeld = !suspendSprint && !IsCrouching
                          && Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        float speed = sprintHeld ? sprintSpeed : moveSpeed;

        //only counts as sprinting if we're actually moving, otherwise holding shift while
        //standing still would push the blend tree into the sprint clip
        IsSprinting = sprintHeld && input.sqrMagnitude > 0.01f;

        float crouchScale = IsCrouching ? crouchSpeedMultiplier : 1f;
        controller.Move(speed * speedMultiplier * debuffSpeedMultiplier * crouchScale * Time.deltaTime * move);

        //flight replaces gravity and jumping entirely - Space climbs, Left Ctrl drops, and
        //you hang where you leave off. handled before grounding so none of the jump logic runs.
        if (flightMode)
        {
            float climb = 0f;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.isPressed) climb += 1f;
                if (Keyboard.current.leftCtrlKey.isPressed) climb -= 1f;
            }

            velocity.y = 0f;
            controller.Move(climb * flightVerticalSpeed * speedMultiplier * debuffSpeedMultiplier * Time.deltaTime * Vector3.up);
            return;
        }

        // Grounding
        bool grounded = CheckGrounded();
        if (grounded)
            lastGroundedTime = Time.time;

        // stick to ground a bit so grounded stays stable
        if (grounded && velocity.y < 0f)
            velocity.y = -2f;

        // Jump: holding Space jumps immediately every time we're grounded (or just left it, via coyote time)
        bool jumpHeld = !suspendJump && Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        bool canCoyoteJump = (Time.time - lastGroundedTime) <= coyoteTime;

        if (jumpHeld && canCoyoteJump && velocity.y <= 0.01f)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * jumpHeightMultiplier * -2f * gravity);
        }

        // Gravity (faster fall feels more responsive)
        if (!suspendGravity)
        {
            float grav = gravity;
            if (velocity.y < 0f) grav *= fallMultiplier;

            velocity.y += grav * Time.deltaTime;

            // Apply vertical motion
            controller.Move(new Vector3(0f, velocity.y, 0f) * Time.deltaTime);
        }

    }
}
