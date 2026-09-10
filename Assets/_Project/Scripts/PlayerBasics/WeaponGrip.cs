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

    void Awake()
    {
        //includeInactive: the class is disabled in the hierarchy while another one is selected,
        //and the default overload skips inactive objects, so this would otherwise come back null
        if (animator == null) animator = GetComponentInParent<Animator>(true);
    }

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

        if (total <= 0f) return;

        transform.localRotation = acc;
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
