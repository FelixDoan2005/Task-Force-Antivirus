using UnityEngine;
using UnityEngine.InputSystem;

//a traversal grapple, not a swing. it hauls the player to whatever they aimed at and then lets
//go, so it gets you over a wall or up onto a platform - it deliberately cannot be used to hang
//in mid air, which is what the previous version did by leaving gravity suspended on arrival.
[RequireComponent(typeof(CharacterController))] //safety net
public class GrapplingHook : MonoBehaviour
{
    [Header("Grapple")]
    public float grappleSpeed = 30f;
    public float grappleCooldown = 2f;
    public float grappleRange = 40f;

    [Tooltip("How close to the anchor counts as having arrived.")]
    public float arriveDistance = 1.2f;

    [Tooltip("Safety net. The pull is dropped after this long whatever happens, so a hook that " +
             "somehow never arrives can't leave the player stuck with gravity switched off.")]
    public float maxGrappleTime = 3f;

    [Tooltip("Aims this far above whatever the ray hit. Shooting the face of a ledge would " +
             "otherwise drag you into the wall below it instead of onto the top.")]
    public float anchorLift = 0.8f;

    [Tooltip("Upward speed she keeps at the moment the hook lets go. This is what carries her " +
             "over the lip of a ledge rather than sliding back down the wall. 0 = a dead drop.")]
    public float releaseUpwardSpeed = 4f;

    [Tooltip("How long she throws for before the line goes taut and the pull starts. Without " +
             "this the hang pose takes over on the same frame as the throw and hides it.")]
    public float throwWindup = 0.3f;

    [Header("Weapon Restriction")]
    public WeaponSwitcher weaponSwitcher;
    public int[] allowedWeaponIndices = { 0 }; // 0 = bow, for now

    [Tooltip("Assign the Master Archer. The hook is refused while she is aimed down sights or " +
             "holding a drawn shot - both are committed stances, and throwing a hook out of one " +
             "reads as a misfire. Leave empty to allow it regardless.")]
    public MasterArcher archer;

    public PlayerController playerController;
    public Camera playerCamera;

    //---------------------------------------------------------------- state
    CharacterController controller;
    bool isGrapple;
    float nextGrappleReadyTime;
    float grappleEndsAt;
    Vector3 grapplePoint;

    //the throw happens first and the pull follows it, so the two poses read in order
    bool throwing;
    float pullStartsAt;

    //only true once the line is taut - the throw itself is not part of it, or the hang pose
    //would cover the throw animation
    public bool IsGrappling => isGrapple;
    public bool IsThrowing => throwing;

    //for the animator: the throw is a one-off, the hang is held for the whole flight
    public event System.Action OnGrappleThrown;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void OnDisable()
    {
        //gravity is not ours to keep - if this component goes away mid-pull the player would
        //otherwise be left floating for good
        throwing = false;
        if (isGrapple) Release(false);
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.fKey.wasPressedThisFrame) TryGrapple();

        //space cuts the line early, so you can bail out part way - or cancels the throw
        if ((isGrapple || throwing) && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            throwing = false;
            if (isGrapple) Release(false);
            return;
        }

        //the throw is playing; the line goes taut when it finishes
        if (throwing && Time.time >= pullStartsAt)
        {
            throwing = false;
            BeginPull();
        }

        if (!isGrapple) return;

        Vector3 toAnchor = grapplePoint - transform.position;

        //arrived, ran out of time, or something is in the way - all end the same way
        if (toAnchor.magnitude <= arriveDistance || Time.time >= grappleEndsAt)
        {
            Release(true);
            return;
        }

        Vector3 before = transform.position;
        controller.Move(grappleSpeed * Time.deltaTime * toAnchor.normalized);

        //barely moved despite trying to, so a wall or the floor is blocking the line - treat that
        //as having arrived rather than grinding against it until the timer runs out
        float moved = Vector3.Distance(before, transform.position);
        if (moved < grappleSpeed * Time.deltaTime * 0.1f) Release(true);
    }

    void TryGrapple()
    {
        if (isGrapple || throwing) return;
        if (Time.time < nextGrappleReadyTime) return;
        if (!IsCurrentWeaponAllowed()) return;

        //no throwing the hook out of a committed stance - aimed in, or holding a drawn shot
        if (archer != null && (archer.IsAiming || archer.IsDrawing())) return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, grappleRange)) return; //nothing in range

        //lifted, so a hit on the side of a platform still lands her on top of it
        grapplePoint = hit.point + Vector3.up * anchorLift;

        //she throws first and is pulled after. gravity is left alone during the throw so she is
        //still standing (or falling) normally while it plays.
        throwing = true;
        pullStartsAt = Time.time + throwWindup;
        nextGrappleReadyTime = Time.time + grappleCooldown;

        OnGrappleThrown?.Invoke();
    }

    void BeginPull()
    {
        isGrapple = true;
        grappleEndsAt = Time.time + maxGrappleTime;
        playerController.suspendGravity = true;
    }

    //the whole point of the rework: the pull always ends with her falling again
    void Release(bool arrived)
    {
        isGrapple = false;
        playerController.suspendGravity = false;

        //a little of the pull is kept as upward motion so the last stretch carries her over the
        //edge she was aiming at. without it she stops dead at the wall and drops straight back.
        if (arrived && releaseUpwardSpeed > 0f)
            playerController.SetVerticalVelocity(releaseUpwardSpeed);
        else
            playerController.SetVerticalVelocity(0f);
    }

    bool IsCurrentWeaponAllowed()
    {
        if (weaponSwitcher == null) return true;

        int current = weaponSwitcher.ActiveWeaponIndex;
        foreach (int allowed in allowedWeaponIndices)
        {
            if (allowed == current) return true;
        }

        return false;
    }
}
