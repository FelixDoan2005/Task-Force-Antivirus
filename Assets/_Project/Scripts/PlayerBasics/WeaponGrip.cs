using UnityEngine;

//Holds a weapon steady in the hand across animations that grip it differently.
//
//A weapon parented to a hand bone inherits one fixed local offset, which only looks right
//for clips that pose the wrist the same way. Mixamo's rifle clips don't agree with each
//other - the idle and the sprints hold the rifle 70-80 degrees off where the firing clip
//does - so one offset can't serve them all.
//
//This reads the weights of whatever clips the Animator is actually playing and blends the
//matching offsets together. Because it uses real clip weights it stays correct through
//blend trees and mid-transition, rather than snapping at state boundaries.
public class WeaponGrip : MonoBehaviour
{
    [System.Serializable]
    public struct ClipGrip
    {
        [Tooltip("Clip name as it appears in the Animator, e.g. Cyborg_Idle.")]
        public string clipName;

        [Tooltip("Local rotation the weapon needs while this clip is playing.")]
        public Vector3 localEuler;
    }

    [Header("Setup")]
    [Tooltip("Left empty, it finds the Animator up the hierarchy.")]
    public Animator animator;

    [Header("Grip")]
    [Tooltip("Used by any clip without its own entry below.")]
    public Vector3 defaultLocalEuler = new Vector3(351.39f, 76.75f, 92.68f);

    [Tooltip("Per-clip overrides. Only clips that actually disagree need listing.")]
    public ClipGrip[] perClip;

    //---------------------------------------------------------------- aim
    //the grip above only decides how he HOLDS the gun, so the barrel ends up pointing wherever
    //the wrist happens to be and the tracer leaves the weapon at an angle. this swings the posed
    //weapon onto the crosshair - but only for the clips listed below, because the sideways and
    //backward clips hold the rifle across his body and forcing those onto the crosshair twists
    //the whole arm. the correction fades in and out with each clip's own weight.
    [Header("Aim")]
    [Tooltip("Point the barrel at the crosshair while the clips below are playing.")]
    public bool aimAtCrosshair = true;

    [Tooltip("Clips that keep their own grip and are NOT aimed, by name. Everything else gets " +
             "pointed at the crosshair. Each clip's animator weight decides how much of the " +
             "correction applies, so a blend between an aimed and an unaimed clip eases across.")]
    public string[] noAimClips = { "Cyborg_Idle", "Cyborg_Reload", "Cyborg_RunReload",
                                   "Cyborg_CrouchReload" };

    [Tooltip("The camera the crosshair belongs to. Empty finds the main camera.")]
    public Camera aimCamera;

    [Tooltip("What counts as upright - assign the Player. This must NOT be left to the Animator: " +
             "it sits on the 'root' bone, whose up is about 100 degrees off the character's, and " +
             "keeping the weapon upright against that is what tilts it on its side.")]
    public Transform upReference;

    [Tooltip("Which way the barrel runs in the weapon's own space.")]
    public Vector3 localBarrelAxis = Vector3.forward;

    [Tooltip("Which way is up on the weapon in its own space. This is what stops it rolling " +
             "over onto its side when he aims steeply down.")]
    public Vector3 localUpAxis = Vector3.up;

    [Tooltip("How far along the barrel axis the muzzle sits. The aim is worked out from the " +
             "muzzle, so the line leaving the barrel is the one that lands on the crosshair.")]
    public float muzzleDistance = 0.69f;

    [Tooltip("How far it may swing off the pose the animation gives it.")]
    [Range(0f, 180f)] public float maxAimAngle = 70f;

    [Tooltip("How far out the crosshair is traced when nothing is in the way.")]
    public float aimRange = 45f;

    public LayerMask aimMask = ~0;

    [Tooltip("How far DOWN the weapon will follow the crosshair, in degrees below level. Past " +
             "this it keeps pointing ahead as if he were shooting someone standing in front of " +
             "him, instead of folding his arms under himself to track the floor.")]
    [Range(-90f, 0f)] public float minAimElevation = -10f;

    [Tooltip("The same limit going up.")]
    [Range(0f, 90f)] public float maxAimElevation = 45f;

    [Tooltip("Once he is looking further down than the limit above, the weapon stops turning " +
             "left and right too and simply points the way he is facing. Aiming up is unaffected.")]
    public bool lockHeadingWhenAimingDown = true;

    [Tooltip("Degrees past the limit over which it eases into that lock, so it settles straight " +
             "instead of snapping the moment you cross the line.")]
    [Range(1f, 45f)] public float headingLockBlend = 15f;

    //---------------------------------------------------------------- upper body
    //aiming the weapon alone only works while the target is roughly where the clip already
    //points. shooting at the floor asks for 80 degrees of correction the arm never makes, so the
    //gun swings off on its own and the pose falls apart. leaning the chest into the shot is what
    //a person actually does, and it hands most of that angle to the body instead of the wrist.
    [Header("Aim - upper body")]
    [Tooltip("Chest bone to lean into the aim, e.g. spine_02. Empty leaves the body alone.")]
    public Transform spineBone;

    [Tooltip("How much of the aim's up/down angle the chest takes on. The weapon covers the rest.")]
    [Range(0f, 1f)] public float spineFollow = 0.7f;

    [Tooltip("How far the chest may lean, up or down.")]
    [Range(0f, 80f)] public float maxSpineBend = 50f;

    void Awake()
    {
        //includeInactive: the class is disabled in the hierarchy while another one is selected,
        //and the default overload skips inactive objects, so this would otherwise come back null
        if (animator == null) animator = GetComponentInParent<Animator>(true);
        if (aimCamera == null) aimCamera = Camera.main;

        //walk up past the bones to the object that is actually upright in the world
        if (upReference == null)
        {
            var pc = GetComponentInParent<PlayerController>(true);
            if (pc != null) upReference = pc.transform;
        }
    }

    //the tip of the barrel, wherever the weapon currently is
    public Vector3 MuzzlePosition =>
        transform.TransformPoint(localBarrelAxis.normalized * muzzleDistance);

    Quaternion GripFor(string clipName)
    {
        for (int i = 0; i < perClip.Length; i++)
            if (perClip[i].clipName == clipName)
                return Quaternion.Euler(perClip[i].localEuler);

        return Quaternion.Euler(defaultLocalEuler);
    }

    //after the Animator has posed the skeleton, so this isn't overwritten
    void LateUpdate()
    {
        if (animator == null || !animator.isActiveAndEnabled) return;

        Quaternion acc = Quaternion.identity;
        float total = 0f;

        Accumulate(animator.GetCurrentAnimatorClipInfo(0), ref acc, ref total);

        //mid-transition the next state's clips carry weight too, so folding them in keeps
        //the weapon moving smoothly instead of popping when the state finally switches
        if (animator.IsInTransition(0))
            Accumulate(animator.GetNextAnimatorClipInfo(0), ref acc, ref total);

        if (total > 0f) transform.localRotation = acc;

        if (!aimAtCrosshair || aimCamera == null) return;

        //everything playing wants to be aimed except the clips that opted out
        float aimWeight = 0f;
        AccumulateAimWeight(animator.GetCurrentAnimatorClipInfo(0), ref aimWeight);
        if (animator.IsInTransition(0))
            AccumulateAimWeight(animator.GetNextAnimatorClipInfo(0), ref aimWeight);

        if (aimWeight <= 0.0001f) return;

        float w = Mathf.Clamp01(aimWeight);
        Vector3 aimPoint = ClampElevation(AimPoint());

        //the chest leans first - it moves the hand, and so the weapon with it
        if (spineBone != null) BendSpine(aimPoint, w);

        AimBarrel(aimPoint, w);
    }

    Vector3 AimPoint()
    {
        Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        return Physics.Raycast(ray, out RaycastHit hit, aimRange, aimMask,
                               QueryTriggerInteraction.Ignore)
            ? hit.point
            : ray.origin + ray.direction * aimRange;
    }

    //holds the aim inside the range the pose can actually carry. looking further down than this
    //keeps the weapon level and forward rather than dragging it round to follow the crosshair.
    Vector3 ClampElevation(Vector3 point)
    {
        Vector3 up = upReference != null ? upReference.up : Vector3.up;
        Vector3 pivot = transform.position;

        Vector3 toTarget = point - pivot;
        float distance = toTarget.magnitude;
        if (distance < 0.0001f) return point;

        float elevation = 90f - Vector3.Angle(up, toTarget);
        float clamped = Mathf.Clamp(elevation, minAimElevation, maxAimElevation);
        if (Mathf.Approximately(clamped, elevation)) return point;

        Vector3 facing = upReference != null ? upReference.forward : transform.forward;
        facing = Vector3.ProjectOnPlane(facing, up).normalized;

        Vector3 flat = Vector3.ProjectOnPlane(toTarget, up);

        //straight up or straight down leaves no heading to keep, so fall back to his facing
        flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : facing;

        //aiming down, the weapon stops tracking sideways as well and just points where he faces.
        //eased in over a few degrees rather than switched, or it snaps as you cross the limit.
        if (lockHeadingWhenAimingDown && elevation < minAimElevation)
        {
            float t = Mathf.Clamp01((minAimElevation - elevation) / headingLockBlend);
            flat = Vector3.Slerp(flat, facing, t).normalized;
        }

        float rad = clamped * Mathf.Deg2Rad;
        return pivot + (flat * Mathf.Cos(rad) + up * Mathf.Sin(rad)) * distance;
    }

    //leans the chest up or down the way he is aiming, so steep shots come from the whole body
    void BendSpine(Vector3 aimPoint, float weight)
    {
        Vector3 up = upReference != null ? upReference.up : Vector3.up;

        Vector3 toTarget = aimPoint - spineBone.position;
        Vector3 flat = Vector3.ProjectOnPlane(toTarget, up);
        if (flat.sqrMagnitude < 0.0001f) return;

        //positive above the horizon, negative below it
        float elevation = 90f - Vector3.Angle(up, toTarget);

        float bend = Mathf.Clamp(-elevation * spineFollow, -maxSpineBend, maxSpineBend) * weight;

        //the axis across the aim, so he leans along the shot rather than twisting off it
        Vector3 axis = Vector3.Cross(up, flat.normalized);

        spineBone.rotation = Quaternion.AngleAxis(bend, axis) * spineBone.rotation;
    }

    void AccumulateAimWeight(AnimatorClipInfo[] infos, ref float weight)
    {
        for (int i = 0; i < infos.Length; i++)
        {
            if (infos[i].clip == null || infos[i].weight <= 0.0001f) continue;

            bool excluded = false;
            for (int c = 0; c < noAimClips.Length; c++)
                if (noAimClips[c] == infos[i].clip.name) { excluded = true; break; }

            if (!excluded) weight += infos[i].weight;
        }
    }

    //builds the rotation that puts the barrel on the crosshair while keeping the weapon upright
    //relative to him. built from an explicit up rather than as the shortest turn from where the
    //gun happens to be, because the shortest turn rolls the rifle onto its side at steep angles.
    void AimBarrel(Vector3 aimPoint, float weight)
    {
        Quaternion posed = transform.rotation;

        //maps the weapon's own axes onto a plain forward/up frame, so the aim can be expressed
        //as "point this way, stay upright" no matter how the model was authored
        Quaternion fromWeapon = Quaternion.Inverse(
            Quaternion.LookRotation(localBarrelAxis.normalized, localUpAxis.normalized));

        Vector3 up = upReference != null ? upReference.up : Vector3.up;

        //repeated: turning the gun moves the muzzle, which moves the line it has to point down
        for (int i = 0; i < 3; i++)
        {
            Vector3 toTarget = aimPoint - MuzzlePosition;
            if (toTarget.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(toTarget.normalized, up) * fromWeapon;
        }

        //never let it pull too far from the animation, then fade it in by the clip's weight
        Quaternion aimed = Quaternion.RotateTowards(posed, transform.rotation, maxAimAngle);
        transform.rotation = Quaternion.Slerp(posed, aimed, weight);
    }

    void Accumulate(AnimatorClipInfo[] infos, ref Quaternion acc, ref float total)
    {
        for (int i = 0; i < infos.Length; i++)
        {
            float w = infos[i].weight;
            if (w <= 0.0001f || infos[i].clip == null) continue;

            Quaternion q = GripFor(infos[i].clip.name);

            //incremental weighted average - each new clip pulls the result toward it by its
            //share of the total weight so far
            if (total <= 0f) { acc = q; total = w; }
            else { acc = Quaternion.Slerp(acc, q, w / (total + w)); total += w; }
        }
    }
}
