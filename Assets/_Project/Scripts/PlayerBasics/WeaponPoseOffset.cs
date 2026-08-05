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
    public CharacterAnimatorDriver animatorDriver; // tells us which ult swing variant is playing

    [Header("Rest Pose (use Capture Rest Pose)")]
    public Vector3 restPosition;
    public Vector3 restEuler;
    public bool restCaptured;

    [Header("Dash Offset")]
    [Tooltip("Highest priority. The dash clip poses the arms its own way, so this needs tuning " +
             "independently of the run offset - the same numbers won't look the same.")]
    public Vector3 dashRotationOffset;
    public Vector3 dashPositionOffset;

    [Header("E Cast Offsets")]
    [Tooltip("Applied during the thrust, i.e. the first half of the cast.")]
    public Vector3 castThrustRotationOffset;
    public Vector3 castThrustPositionOffset;

    [Tooltip("Applied once the horizontal slash starts (after Slash Delay on BladeMaster).")]
    public Vector3 castSlashRotationOffset;
    public Vector3 castSlashPositionOffset;

    [Header("Ult Swing Offsets")]
    [Tooltip("One entry per ult swing clip. Set the array size to match Ultimate Swing Variants on the driver.")]
    public Vector3[] ultSwingRotationOffsets = new Vector3[11];
    public Vector3[] ultSwingPositionOffsets = new Vector3[11];

    [Tooltip("Which ult swing index 'Capture Ult Swing Offset' writes to.")]
    public int captureUltSwing;

    [Header("Combo Attack Offsets")]
    [Tooltip("One entry per combo step (0 = first swing). Stops the blade swinging into the camera on particular attacks.")]
    public Vector3[] comboRotationOffsets = new Vector3[5];
    public Vector3[] comboPositionOffsets = new Vector3[5];

    [Tooltip("Which combo step 'Capture Combo Offset' writes to.")]
    public int captureComboStep;

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

    [Header("Snap")]
    [Tooltip("Jump straight to the dash pose instead of easing into it. The dash is too short for a " +
             "blend to finish, so easing leaves it looking different depending on what you were doing before.")]
    public bool snapToDashPose = true;

    [Tooltip("Keep holding the dash pose for this long after the dash movement ends - the dash " +
             "animation runs longer than the movement does, so without this the sword snaps back early.")]
    public float dashPoseHold = 0.35f;

    [Header("Blend")]
    public float blendSpeed = 8f;
    public float moveThreshold = 0.1f;

    [Header("Debug")]
    public bool logActivePose;

    Vector3 currentPosOffset;
    //held as a quaternion, not euler angles. lerping euler components interpolates each axis
    //separately, which sends the blade the long way round (through the character) whenever two
    //poses differ a lot - e.g. run to sprint. Slerp always takes the shortest arc.
    Quaternion currentRotOffset = Quaternion.identity;
    float dashPoseUntil;
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

        //the dash animation outlasts the dash movement, so keep the pose alive a little longer
        //than IsDashing or the sword snaps back while the character is still swinging
        if (bladeMaster != null && bladeMaster.IsDashing)
            dashPoseUntil = Time.time + dashPoseHold;

        bool inDashPose = Time.time < dashPoseUntil;

        //dashing wins outright so the sword is consistent whether you dashed from standing or running
        if (inDashPose)
        {
            targetPos = dashPositionOffset;
            targetRot = dashRotationOffset;
            pose = "Dash";
        }
        //the cast has two halves with very different sword positions, so it swaps pose
        //partway through rather than holding one for the whole animation
        else if (bladeMaster != null && bladeMaster.IsCasting)
        {
            bool thrusting = bladeMaster.CastPhase == 0;
            targetPos = thrusting ? castThrustPositionOffset : castSlashPositionOffset;
            targetRot = thrusting ? castThrustRotationOffset : castSlashRotationOffset;
            pose = thrusting ? "CastThrust" : "CastSlash";
        }
        //each ult swing clip gets its own placement too - same problem, 11 more clips
        else if (bladeMaster != null && bladeMaster.IsUltimateActive && animatorDriver != null
                 && animatorDriver.CurrentUltSwing >= 0
                 && animatorDriver.CurrentUltSwing < ultSwingRotationOffsets.Length)
        {
            int swing = animatorDriver.CurrentUltSwing;
            targetPos = ultSwingPositionOffsets[swing];
            targetRot = ultSwingRotationOffsets[swing];
            pose = $"UltSwing{swing}";
        }
        //mid-swing each attack gets its own placement, since some of the clips throw the blade
        //toward the camera at the arc they're posed at
        else if (bladeMaster != null && bladeMaster.IsAttacking
                 && bladeMaster.CurrentComboStep >= 0
                 && bladeMaster.CurrentComboStep < comboRotationOffsets.Length)
        {
            int step = bladeMaster.CurrentComboStep;
            targetPos = comboPositionOffsets[step];
            targetRot = comboRotationOffsets[step];
            pose = $"Attack{step}";
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

        //a dash is over before any blend could finish, so easing into it would leave the sword
        //carrying whatever pose it came from - snap so it looks identical from standing, running
        //or sprinting
        bool snap = snapToDashPose && inDashPose;

        float t = snap ? 1f : blendSpeed * Time.deltaTime;
        currentPosOffset = Vector3.Lerp(currentPosOffset, targetPos, t);
        currentRotOffset = Quaternion.Slerp(currentRotOffset, Quaternion.Euler(targetRot), t);

        transform.localPosition = restPosition + currentPosOffset;
        transform.localRotation = RestRotation * currentRotOffset;
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

    [ContextMenu("Capture Cast Thrust Offset")]
    void CaptureCastThrust() => Capture(ref castThrustPositionOffset, ref castThrustRotationOffset, "Cast thrust");

    [ContextMenu("Capture Cast Slash Offset")]
    void CaptureCastSlash() => Capture(ref castSlashPositionOffset, ref castSlashRotationOffset, "Cast slash");

    [ContextMenu("Preview Cast Thrust Offset")]
    void PreviewCastThrust() => Apply(castThrustPositionOffset, castThrustRotationOffset);

    [ContextMenu("Preview Cast Slash Offset")]
    void PreviewCastSlash() => Apply(castSlashPositionOffset, castSlashRotationOffset);

    [ContextMenu("Capture Ult Swing Offset")]
    void CaptureUltSwing()
    {
        if (captureUltSwing < 0 || captureUltSwing >= ultSwingRotationOffsets.Length)
        {
            Debug.LogWarning($"captureUltSwing {captureUltSwing} is outside the array.", this);
            return;
        }
        Capture(ref ultSwingPositionOffsets[captureUltSwing],
                ref ultSwingRotationOffsets[captureUltSwing],
                $"Ult swing {captureUltSwing}");
    }

    [ContextMenu("Preview Ult Swing Offset")]
    void PreviewUltSwing()
    {
        if (captureUltSwing < 0 || captureUltSwing >= ultSwingRotationOffsets.Length) return;
        Apply(ultSwingPositionOffsets[captureUltSwing], ultSwingRotationOffsets[captureUltSwing]);
    }

    [ContextMenu("Capture Combo Offset")]
    void CaptureCombo()
    {
        if (captureComboStep < 0 || captureComboStep >= comboRotationOffsets.Length)
        {
            Debug.LogWarning($"captureComboStep {captureComboStep} is outside the array.", this);
            return;
        }
        Capture(ref comboPositionOffsets[captureComboStep],
                ref comboRotationOffsets[captureComboStep],
                $"Combo step {captureComboStep}");
    }

    [ContextMenu("Preview Combo Offset")]
    void PreviewCombo()
    {
        if (captureComboStep < 0 || captureComboStep >= comboRotationOffsets.Length) return;
        Apply(comboPositionOffsets[captureComboStep], comboRotationOffsets[captureComboStep]);
    }

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
