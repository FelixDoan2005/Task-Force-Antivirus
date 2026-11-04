using UnityEngine;

//in a surround wall the spear is couched low and sticks out past the shields; the right hand is pulled onto the
//shaft with IK. kneeling in the front wall or standing behind it, each with its own hold (posed on the dummies)
[RequireComponent(typeof(Animator))]
public class SpearWallBrace : MonoBehaviour
{
    public Transform spear;

    [Header("Kneeling front wall (right, up, forward of the feet, before the model's scale)")]
    [Tooltip("Where on the spear the hand holds it, in the spear's own space.")]
    public Vector3 frontGrip = new Vector3(0.018f, 0.121f, 0.879f);
    public Vector3 frontHold = new Vector3(0.285f, 0.682f, 0.208f);
    public Vector3 frontElbow = new Vector3(0.287f, 0.617f, -0.026f);
    [Tooltip("Degrees the point dips below level (negative tips it up).")]
    public float frontDip = -20f;

    [Header("Standing wall behind it")]
    public Vector3 grip = new Vector3(0.018f, 0.121f, 0.733f);
    public Vector3 hold = new Vector3(0.28f, 0.958f, 0.148f);
    public Vector3 elbow = new Vector3(0.278f, 0.94f, -0.094f);
    public float dip = -20f;
    public float blendSpeed = 4f;

    [Header("Legs while standing braced (right, up, forward of the feet, before the model's scale)")]
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

    Animator animator;
    SoldierAI soldier;
    Vector3 restPosition;
    Quaternion restRotation;
    float weight;
    float row;

    void Awake()
    {
        animator = GetComponent<Animator>();
        soldier = GetComponentInParent<SoldierAI>();
        if (spear != null)
        {
            restPosition = spear.localPosition;
            restRotation = spear.localRotation;
        }
    }

    void Update()
    {
        bool braced = soldier != null && soldier.squad != null && soldier.AtPlace && soldier.squad.InWall(soldier, out row);
        weight = Mathf.MoveTowards(weight, braced ? 1f : 0f, blendSpeed * Time.deltaTime);
    }

    Vector3 Facing => soldier != null && soldier.squad != null ? soldier.squad.FacingFor(soldier) : transform.forward;
    bool Kneeling => row < 0.5f;

    Vector3 Point(Vector3 v)
    {
        Vector3 f = Facing;
        Vector3 right = Vector3.Cross(Vector3.up, f);
        return transform.position + (right * v.x + Vector3.up * v.y + f * v.z) * transform.lossyScale.y;
    }

    void OnAnimatorIK(int layer)
    {
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
        animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, weight);

        //standing braced: hips down, feet planted wide, back foot turned out. kneeling keeps the kneel
        float legs = Kneeling ? 0f : weight;
        foreach (AvatarIKGoal g in new[] { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot })
        {
            animator.SetIKPositionWeight(g, legs);
            animator.SetIKRotationWeight(g, legs);
        }
        animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, legs);
        animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, legs);
        if (weight <= 0f) return;

        animator.SetIKPosition(AvatarIKGoal.RightHand, Point(Kneeling ? frontHold : hold));
        animator.SetIKHintPosition(AvatarIKHint.RightElbow, Point(Kneeling ? frontElbow : elbow));
        if (legs <= 0f) return;

        animator.bodyPosition -= Vector3.up * hipDrop * transform.lossyScale.y * legs;
        Plant(AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, leftFoot, leftKnee, 0f);
        Plant(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, rightFoot, rightKnee, rightFootTurn);
    }

    void Plant(AvatarIKGoal foot, AvatarIKHint knee, Vector3 at, Vector3 kneeAt, float turn)
    {
        Vector3 target = Point(at);
        target.y = animator.GetIKPosition(foot).y;
        animator.SetIKPosition(foot, target);
        animator.SetIKRotation(foot, Quaternion.AngleAxis(turn, Vector3.up) * animator.GetIKRotation(foot));
        animator.SetIKHintPosition(knee, Point(kneeAt));
    }

    void LateUpdate()
    {
        if (spear == null) return;
        spear.localPosition = restPosition;
        spear.localRotation = restRotation;
        if (weight <= 0f) return;

        Vector3 g = Kneeling ? frontGrip : grip;
        Vector3 f = Facing;
        Vector3 axis = Quaternion.AngleAxis(Kneeling ? frontDip : dip, Vector3.Cross(Vector3.up, f)) * f;
        Vector3 at = Vector3.Lerp(spear.TransformPoint(g), Point(Kneeling ? frontHold : hold), weight);

        spear.rotation = Quaternion.Slerp(spear.rotation, Quaternion.LookRotation(axis, Vector3.up), weight);
        spear.position += at - spear.TransformPoint(g);
    }
}
