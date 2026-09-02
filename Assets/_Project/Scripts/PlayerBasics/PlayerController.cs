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

    [Tooltip("Abilities can set this to block jumping without touching gravity, so you still fall normally.")]
    public bool suspendJump;

    [Tooltip("Abilities can set this to block the double-tap dash. Read by DashAbility.")]
    public bool suspendDash;

    [Header("Jump")]
    public float jumpHeight = 1.2f;

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

    //raw WASD input this frame, -1..1 on each axis. read by the active class's animator driver
    public Vector2 MoveInput { get; private set; }

    //true only while sprinting AND actually moving
    public bool IsSprinting { get; private set; }


    void Awake()
    {
        controller = GetComponent<CharacterController>();
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

            Vector3 p = cameraPivot.localPosition;
            p.y = basePivotY + yOffset;
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

            // camera collision: don't let the desired position clip through walls
            Vector3 desiredLocalOffset = new(baseCameraLocalPos.x, baseCameraLocalPos.y, -dist);
            Vector3 desiredWorldPos = cameraPivot.TransformPoint(desiredLocalOffset);
            Vector3 toCam = desiredWorldPos - cameraPivot.position;
            float desiredLen = toCam.magnitude;

            if (desiredLen > 0.001f && Physics.Raycast(cameraPivot.position, toCam.normalized, out RaycastHit hit, desiredLen, cameraCollisionMask, QueryTriggerInteraction.Ignore))
            {
                dist *= hit.distance / desiredLen;
                dist = Mathf.Max(dist - cameraCollisionBuffer, 0.05f);
            }

            Vector3 c = baseCameraLocalPos;
            c.z = -dist; // behind the pivot
            camTransform.localPosition = c;
        }
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

        Vector3 move = transform.right * input.x + transform.forward * input.y;
        if (move.sqrMagnitude > 1f) move.Normalize();

        // Sprint
        bool sprintHeld = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        float speed = sprintHeld ? sprintSpeed : moveSpeed;

        //only counts as sprinting if we're actually moving, otherwise holding shift while
        //standing still would push the blend tree into the sprint clip
        IsSprinting = sprintHeld && input.sqrMagnitude > 0.01f;

        controller.Move(speed * speedMultiplier * Time.deltaTime * move);

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
            controller.Move(climb * flightVerticalSpeed * speedMultiplier * Time.deltaTime * Vector3.up);
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
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
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
