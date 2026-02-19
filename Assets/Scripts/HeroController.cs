using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class HeroController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraPivot;

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
    public bool lockVerticalLook = true;

    CharacterController controller;
    Vector3 velocity;

    float lastGroundedTime = -999f;
    float lastJumpPressedTime = -999f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        LookHorizontalOnly();
        MoveAndJump();
    }

    void LookHorizontalOnly()
    {
        if (Mouse.current == null) return;

        Vector2 mouseDelta = 60f * mouseSensitivity * Time.deltaTime * Mouse.current.delta.ReadValue();

        // Horizontal only
        transform.Rotate(Vector3.up * mouseDelta.x);

        // keep camera level
        if (cameraPivot != null && lockVerticalLook)
            cameraPivot.localRotation = Quaternion.identity;
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
