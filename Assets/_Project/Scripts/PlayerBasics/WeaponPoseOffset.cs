using UnityEngine;

//sits on a weapon that's parented to a hand bone. nudges its pose depending on what the
//character is doing, so the sword sits differently when idle, running, backing up and sprinting.
//runs in LateUpdate because the Animator poses the bones in Update - offsetting any earlier
//would just get overwritten the same frame.
//
//TUNING: don't type the offsets by hand. Instead, in EDIT mode (not playing):
//  1. put the sword where you want it to rest, then right-click the component
//     header -> "Capture Rest Pose"
//  2. drag/rotate the sword to where it should sit while running, then
//     "Capture Run Offset"  (same for Back / Sprint)
//each capture stores the difference from the rest pose, so you position visually
//with the normal gizmo and never guess numbers.
public class WeaponPoseOffset : MonoBehaviour
{
    [Header("Setup")]
    public PlayerController playerMovement;
    public BladeMaster bladeMaster; // needed for the block pose

    [Header("Rest Pose (use Capture Rest Pose)")]
    public Vector3 restPosition;
    public Vector3 restEuler;
    public bool restCaptured;

    [Header("Dash Offset")]
    [Tooltip("Highest priority. The dash clip poses the arms its own way, so this needs tuning " +
             "independently of the run offset - the same numbers won't look the same.")]
    public Vector3 dashRotationOffset;
    public Vector3 dashPositionOffset;

    [Header("Block Offset")]
    [Tooltip("Takes priority over every other pose while right-click is held.")]
    public Vector3 blockRotationOffset;
    public Vector3 blockPositionOffset;

    [Header("Run Offset")]
    public Vector3 runRotationOffset;
    public Vector3 runPositionOffset;

    [Header("Run Back Offset")]
    public Vector3 backRotationOffset;
    public Vector3 backPositionOffset;

    [Header("Sprint Offset")]
    public Vector3 sprintRotationOffset;
    public Vector3 sprintPositionOffset;

    [Header("Blend")]
    public float blendSpeed = 8f;
    public float moveThreshold = 0.1f;

    [Header("Debug")]
    public bool logActivePose;

    Vector3 currentPosOffset;
    Vector3 currentRotOffset;
    string lastPose = "";

    Quaternion RestRotation => Quaternion.Euler(restEuler);

    void Awake()
    {
        //fall back to wherever it sits now if a rest pose was never captured
        if (!restCaptured)
        {
            restPosition = transform.localPosition;
            restEuler = transform.localRotation.eulerAngles;
        }

        if (playerMovement == null)
            Debug.LogWarning($"{name}: WeaponPoseOffset has no Player Movement assigned - " +
                             "every offset will be ignored and the weapon will stay at its resting pose.", this);
    }

    void LateUpdate()
    {
        Vector3 targetPos = Vector3.zero;
        Vector3 targetRot = Vector3.zero;
        string pose = "Rest";

        //dashing wins outright so the sword is consistent whether you dashed from standing or running
        if (bladeMaster != null && bladeMaster.IsDashing)
        {
            targetPos = dashPositionOffset;
            targetRot = dashRotationOffset;
            pose = "Dash";
        }
        //blocking overrides the rest - the pose is what matters, not what your feet are doing
        else if (bladeMaster != null && bladeMaster.IsBlocking)
        {
            targetPos = blockPositionOffset;
            targetRot = blockRotationOffset;
            pose = "Block";
        }
        else if (playerMovement != null)
        {
            bool moving = playerMovement.MoveInput.sqrMagnitude > moveThreshold * moveThreshold;
            bool backing = playerMovement.MoveInput.y < -moveThreshold;

            //backing up wins over sprinting - sprinting backwards should still use the back pose
            if (backing)
            {
                targetPos = backPositionOffset;
                targetRot = backRotationOffset;
                pose = "Back";
            }
            else if (playerMovement.IsSprinting)
            {
                targetPos = sprintPositionOffset;
                targetRot = sprintRotationOffset;
                pose = "Sprint";
            }
            else if (moving)
            {
                targetPos = runPositionOffset;
                targetRot = runRotationOffset;
                pose = "Run";
            }
        }

        if (logActivePose && pose != lastPose)
        {
            Debug.Log($"WeaponPoseOffset -> {pose}");
            lastPose = pose;
        }

        float t = blendSpeed * Time.deltaTime;
        currentPosOffset = Vector3.Lerp(currentPosOffset, targetPos, t);
        currentRotOffset = Vector3.Lerp(currentRotOffset, targetRot, t);

        transform.localPosition = restPosition + currentPosOffset;
        transform.localRotation = RestRotation * Quaternion.Euler(currentRotOffset);
    }

    // ---- tuning helpers, all usable from the component's right-click menu in edit mode ----

    [ContextMenu("Capture Rest Pose")]
    void CaptureRest()
    {
        restPosition = transform.localPosition;
        restEuler = transform.localRotation.eulerAngles;
        restCaptured = true;
        Debug.Log($"Rest pose captured: pos {restPosition}, euler {restEuler}", this);
    }

    [ContextMenu("Capture Dash Offset")]
    void CaptureDash() => Capture(ref dashPositionOffset, ref dashRotationOffset, "Dash");

    [ContextMenu("Capture Block Offset")]
    void CaptureBlock() => Capture(ref blockPositionOffset, ref blockRotationOffset, "Block");

    [ContextMenu("Capture Run Offset")]
    void CaptureRun() => Capture(ref runPositionOffset, ref runRotationOffset, "Run");

    [ContextMenu("Capture Back Offset")]
    void CaptureBack() => Capture(ref backPositionOffset, ref backRotationOffset, "Back");

    [ContextMenu("Capture Sprint Offset")]
    void CaptureSprint() => Capture(ref sprintPositionOffset, ref sprintRotationOffset, "Sprint");

    void Capture(ref Vector3 posOffset, ref Vector3 rotOffset, string label)
    {
        if (!restCaptured)
        {
            Debug.LogWarning("Capture the rest pose first, otherwise the offset has nothing to measure against.", this);
            return;
        }

        posOffset = transform.localPosition - restPosition;
        rotOffset = (Quaternion.Inverse(RestRotation) * transform.localRotation).eulerAngles;
        Debug.Log($"{label} offset captured: pos {posOffset}, rot {rotOffset}", this);
    }

    // ---- preview, so you can see a pose in edit mode before capturing over it ----

    [ContextMenu("Preview Rest")]
    void PreviewRest() => Apply(Vector3.zero, Vector3.zero);

    [ContextMenu("Preview Dash")]
    void PreviewDash() => Apply(dashPositionOffset, dashRotationOffset);

    [ContextMenu("Preview Block")]
    void PreviewBlock() => Apply(blockPositionOffset, blockRotationOffset);

    [ContextMenu("Preview Run")]
    void PreviewRun() => Apply(runPositionOffset, runRotationOffset);

    [ContextMenu("Preview Back")]
    void PreviewBack() => Apply(backPositionOffset, backRotationOffset);

    [ContextMenu("Preview Sprint")]
    void PreviewSprint() => Apply(sprintPositionOffset, sprintRotationOffset);

    void Apply(Vector3 posOffset, Vector3 rotOffset)
    {
        transform.localPosition = restPosition + posOffset;
        transform.localRotation = RestRotation * Quaternion.Euler(rotOffset);
    }
}
