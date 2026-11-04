using UnityEngine;

//a model held in a shield-wall pose in the editor, with handles to drag: the left hand (and its shield) and, for a
//spearman, the right hand (and its spear). the arms bend to reach the handles. for tuning the braces by eye
[ExecuteAlways]
public class WallPoseDummy : MonoBehaviour
{
    [Tooltip("Ticked, the dummy keeps re-posing itself from the handles. Unticked, it's left alone so the arms, shield and spear can be moved directly.")]
    public bool follow = true;
    public Animator model;
    public AnimationClip clip;
    [Range(0f, 1f)] public float time = 0.5f;

    [Tooltip("Move and rotate: the shield's handle goes here, turned this way, with the left hand on it.")]
    public Transform shieldHandle;
    public Transform shield;
    public Vector3 shieldGrip = new Vector3(0f, 0.06f, 0.68f);

    [Tooltip("Move and rotate: the spear's grip goes here, pointing along it, with the right hand on it.")]
    public Transform spearHandle;
    public Transform spear;
    public Vector3 spearGrip = new Vector3(0f, 0.03f, 0.35f);
    [Tooltip("Where the spear arm's elbow points: right, up and forward of the feet, before the model's scale.")]
    public Vector3 spearElbow = new Vector3(0.3f, 0.85f, -0.15f);

    [Header("Legs (right, up, forward of the feet, before the model's scale)")]
    public bool braceLegs = true;
    [Tooltip("How far the hips sink.")]
    public float hipDrop = 0.06f;
    [Tooltip("Where each foot plants. Up is ignored: the ankle keeps its height.")]
    public Vector3 leftFoot = new Vector3(-0.22f, 0f, 0.5f);
    public Vector3 rightFoot = new Vector3(0.3f, 0f, -0.55f);
    [Tooltip("Degrees the back foot turns out.")]
    public float rightFootTurn = 40f;
    [Tooltip("Where each knee points.")]
    public Vector3 leftKnee = new Vector3(-0.25f, 0.5f, 1.2f);
    public Vector3 rightKnee = new Vector3(0.45f, 0.45f, 0.1f);

#if UNITY_EDITOR
    //nothing ticks scene objects reliably in the editor, so the editor's own update re-poses every dummy
    static readonly System.Collections.Generic.List<WallPoseDummy> dummies = new System.Collections.Generic.List<WallPoseDummy>();
    static bool hooked;

    void OnEnable()
    {
        if (!dummies.Contains(this)) dummies.Add(this);
        if (hooked) return;
        hooked = true;
        UnityEditor.EditorApplication.update += () =>
        {
            if (Application.isPlaying) return;
            dummies.RemoveAll(d => d == null);
            foreach (WallPoseDummy d in dummies)
                if (d.isActiveAndEnabled && d.follow) d.Apply();
        };
    }

    void OnDisable() => dummies.Remove(this);
#endif

    public void Apply()
    {
        if (model == null || clip == null) return;
        clip.SampleAnimation(model.gameObject, time * clip.length);

        if (shieldHandle != null)
        {
            Reach(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, shieldHandle.position);
            Place(shield, shieldGrip, shieldHandle);
        }
        if (spearHandle != null)
        {
            Transform m = model.transform;
            Vector3 elbow = m.position + (m.right * spearElbow.x + Vector3.up * spearElbow.y + m.forward * spearElbow.z) * m.lossyScale.y;
            Reach(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, spearHandle.position, elbow);

            //the fingers wrap the shaft: knuckles lined up along the spear
            Transform hand = model.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 across = model.GetBoneTransform(HumanBodyBones.RightIndexProximal).position - model.GetBoneTransform(HumanBodyBones.RightLittleProximal).position;
            hand.rotation = Quaternion.FromToRotation(across, spearHandle.forward) * hand.rotation;
            Place(spear, spearGrip, spearHandle);
        }
        if (braceLegs) BraceLegs();
    }

    Vector3 World(Vector3 v)
    {
        Transform m = model.transform;
        return m.position + (m.right * v.x + Vector3.up * v.y + m.forward * v.z) * m.lossyScale.y;
    }

    //sinks the hips and plants the feet wide; the hands stay exactly where they were
    public void BraceLegs()
    {
        Transform lHand = model.GetBoneTransform(HumanBodyBones.LeftHand), rHand = model.GetBoneTransform(HumanBodyBones.RightHand);
        Vector3 lh = lHand.position, rh = rHand.position;
        Quaternion lhr = lHand.rotation, rhr = rHand.rotation;
        Vector3 le = model.GetBoneTransform(HumanBodyBones.LeftLowerArm).position, re = model.GetBoneTransform(HumanBodyBones.RightLowerArm).position;

        model.GetBoneTransform(HumanBodyBones.Hips).position -= Vector3.up * hipDrop * model.transform.lossyScale.y;
        Plant(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, leftFoot, leftKnee, 0f);
        Plant(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, rightFoot, rightKnee, rightFootTurn);

        Reach(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, lh, le);
        Reach(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, rh, re);
        lHand.rotation = lhr;
        rHand.rotation = rhr;
    }

    void Plant(HumanBodyBones thigh, HumanBodyBones calf, HumanBodyBones footBone, Vector3 at, Vector3 knee, float turn)
    {
        Transform foot = model.GetBoneTransform(footBone);
        Quaternion flat = foot.rotation;
        Vector3 target = World(at);
        target.y = foot.position.y;
        Reach(thigh, calf, footBone, target, World(knee));
        foot.rotation = Quaternion.AngleAxis(turn, Vector3.up) * flat;
    }

    static void Place(Transform weapon, Vector3 grip, Transform handle)
    {
        if (weapon == null) return;
        weapon.rotation = handle.rotation;
        weapon.position += handle.position - weapon.TransformPoint(grip);
    }

    //two-bone arm: bends the elbow to the right length, then swings the arm onto the target
    public void Reach(HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone, Vector3 target, Vector3? elbowHint = null)
    {
        Transform up = model.GetBoneTransform(upperBone), mid = model.GetBoneTransform(lowerBone), end = model.GetBoneTransform(handBone);
        if (up == null || mid == null || end == null) return;

        Vector3 a = up.position, b = mid.position, c = end.position;
        float lab = (b - a).magnitude, lcb = (b - c).magnitude;
        float lat = Mathf.Clamp((target - a).magnitude, 0.01f, lab + lcb - 0.001f);

        float acab0 = Angle(c - a, b - a), babc0 = Angle(a - b, c - b), acat0 = Angle(c - a, target - a);
        float acab1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
        float babc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));

        Vector3 bend = Vector3.Cross(c - a, b - a).normalized;
        Vector3 swing = Vector3.Cross(c - a, target - a).normalized;
        if (bend.sqrMagnitude < 0.5f) bend = up.right;

        up.rotation = Quaternion.AngleAxis((acab1 - acab0) * Mathf.Rad2Deg, bend) * up.rotation;
        mid.rotation = Quaternion.AngleAxis((babc1 - babc0) * Mathf.Rad2Deg, bend) * mid.rotation;
        if (swing.sqrMagnitude > 0.5f) up.rotation = Quaternion.AngleAxis(acat0 * Mathf.Rad2Deg, swing) * up.rotation;

        //turn the whole arm about the shoulder-to-hand line until the elbow points at the hint
        if (elbowHint.HasValue)
        {
            Vector3 axis = (target - a).normalized;
            Vector3 now = Vector3.ProjectOnPlane(mid.position - a, axis);
            Vector3 want = Vector3.ProjectOnPlane(elbowHint.Value - a, axis);
            if (now.sqrMagnitude > 1e-6f && want.sqrMagnitude > 1e-6f)
                up.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(now, want, axis), axis) * up.rotation;
        }
    }

    static float Angle(Vector3 x, Vector3 y) => Mathf.Acos(Mathf.Clamp(Vector3.Dot(x.normalized, y.normalized), -1f, 1f));
}
