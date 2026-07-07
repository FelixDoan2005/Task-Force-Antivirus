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

    [Header("Jump")]
    public float jumpHeight = 1.2f;

    [Tooltip("Allows jumping for a short time after leaving the ground.")]
    public float coyoteTime = 0.12f;

    [Tooltip("If you press jump shortly before landing, it will still jump.")]
    public float jumpBufferTime = 0.12f;

    [Tooltip("Makes falling faster than rising for a snappier feel.")]
    public float fallMultiplier = 1.6f;

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
    float lastJumpPressedTime = -999f;
    float pitch;
    float basePivotY;
    Vector3 baseCameraLocalPos;
    Transform camTransform;


    void Awake()
    {
        controller = GetComponent<CharacterController>();

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

    void Update()
    {
        Look();
        MoveAndJump();
    }

    void Look()
    {
        if (Mouse.current == null) return;

        Vector2 mouseDelta = 60f * mouseSensitivity * Time.deltaTime * Mouse.current.delta.ReadValue();

        // yaw (left/right) rotates the hero
        transform.Rotate(Vector3.up * mouseDelta.x);

        // pitch (up/down) rotates the camera pivot
        pitch -= mouseDelta.y;

        // clamp so you can't flip the camera
        pitch = Mathf.Clamp(pitch, -45f, 45f);

        if (cameraPivot != null)
        {
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            // small vertical slide based on pitch (tweak values)
            float t = Mathf.InverseLerp(-20f, 45f, pitch); // 0..1
            float yOffset = Mathf.Lerp(0f, 0.4f, t);  // down when looking down, up when looking up

            Vector3 p = cameraPivot.localPosition;
            p.y = basePivotY + yOffset;
            cameraPivot.localPosition = p;
        }

        if (camTransform != null)
        {
            float camT = Mathf.InverseLerp(45f, -45f, pitch); // 0..1 across full pitch range
            float dist = Mathf.Lerp(minCamDistance, maxCamDistance, camT);
            float neutralBoost = 0.8f * (1f - Mathf.Abs(camT - 0.5f) * 2f); // 0 at extremes, max at center
            dist += neutralBoost;

            Vector3 c = baseCameraLocalPos;
            c.z = -dist; // behind the pivot
            camTransform.localPosition = c;
        }
    }

    void MoveAndJump()
    {
        // Read movement input (WASD)
        Vector2 input = Vector2.zero; //resets movement
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) input.y += 1.0f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1.0f;
            if (Keyboard.current.dKey.isPressed) input.x += 1.0f;
            if (Keyboard.current.aKey.isPressed) input.x -= 1.0f;

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
                lastJumpPressedTime = Time.time;
        }

        Vector3 move = transform.right * input.x + transform.forward * input.y;
        if (move.sqrMagnitude > 1f) move.Normalize();

        // Sprint
        float speed = moveSpeed;
        if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
            speed = sprintSpeed;

        controller.Move(speed * Time.deltaTime * move);

        // Grounding
        bool grounded = controller.isGrounded;
        if (grounded)
            lastGroundedTime = Time.time;

        // stick to ground a bit so grounded stays stable
        if (grounded && velocity.y < 0f)
            velocity.y = -2f;

        // Jump buffering + coyote time
        bool canCoyoteJump = (Time.time - lastGroundedTime) <= coyoteTime;
        bool hasJumpBuffered = (Time.time - lastJumpPressedTime) <= jumpBufferTime;

        if (hasJumpBuffered && canCoyoteJump)
        {
            // consume the buffered jump so it doesn't double-trigger
            lastJumpPressedTime = -999f;

            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Gravity (faster fall feels more responsive)
        float grav = gravity;
        if (velocity.y < 0f) grav *= fallMultiplier;

        velocity.y += grav * Time.deltaTime;

        // Apply vertical motion
        controller.Move(new Vector3(0f, velocity.y, 0f) * Time.deltaTime);
    }
}
