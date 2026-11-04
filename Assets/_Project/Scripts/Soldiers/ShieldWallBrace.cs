using UnityEngine;

//in a surround wall the left arm holds the shield out on the left side (clear of the body and any spear), square to
//the front; the back rows lean it over the kneeling row. the hand is moved there with IK and stays on the handle
[RequireComponent(typeof(Animator))]
public class ShieldWallBrace : MonoBehaviour
{
    public Transform shield;
    [Tooltip("The handle, in the shield's own space.")]
    public Vector3 handle = new Vector3(0f, 0.06f, 0.68f);

    [Tooltip("Kneeling front row: where the left hand holds the shield, as left, up and forward of the feet (before the model's scale).")]
    public Vector3 frontHold = new Vector3(0.32f, 0.55f, 0.42f);
    [Tooltip("Standing back rows: the same.")]
    public Vector3 backHold = new Vector3(0.36f, 1.05f, 0.25f);
    [Tooltip("Where the left elbow points in each row (left, up, forward). Zero leaves it to the IK.")]
    public Vector3 frontElbow;
    public Vector3 backElbow;
    [Tooltip("Degrees the top leans out in the kneeling front row; negative leans it back so the face tips up.")]
    public float frontLean = 0f;
    [Tooltip("The same for the standing rows behind.")]
    public float lean = 15f;
    public float blendSpeed = 4f;

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
        if (shield != null)
        {
            restPosition = shield.localPosition;
            restRotation = shield.localRotation;
        }
    }

    void Update()
    {
        bool braced = soldier != null && soldier.squad != null && soldier.AtPlace && soldier.squad.InWall(soldier, out row);
        weight = Mathf.MoveTowards(weight, braced ? 1f : 0f, blendSpeed * Time.deltaTime);
    }

    Vector3 Facing => soldier.squad.FacingFor(soldier);

    Vector3 HoldPoint()
    {
        Vector3 f = Facing;
        Vector3 left = -Vector3.Cross(Vector3.up, f);
        Vector3 h = Vector3.Lerp(frontHold, backHold, row);
        return transform.position + (left * h.x + Vector3.up * h.y + f * h.z) * transform.lossyScale.y;
    }

    void OnAnimatorIK(int layer)
    {
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
        Vector3 e = Vector3.Lerp(frontElbow, backElbow, row);
        animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, e == Vector3.zero ? 0f : weight);
        if (weight <= 0f) return;
        animator.SetIKPosition(AvatarIKGoal.LeftHand, HoldPoint());
        if (e != Vector3.zero)
        {
            Vector3 f = Facing;
            Vector3 left = -Vector3.Cross(Vector3.up, f);
            animator.SetIKHintPosition(AvatarIKHint.LeftElbow, transform.position + (left * e.x + Vector3.up * e.y + f * e.z) * transform.lossyScale.y);
        }
    }

    void LateUpdate()
    {
        if (shield == null) return;
        shield.localPosition = restPosition;
        shield.localRotation = restRotation;
        if (weight <= 0f) return;

        //face (-y) out, top (+z) up, leaning forward in the back rows; the handle where the hand was sent
        Vector3 f = Facing;
        Vector3 top = Quaternion.AngleAxis(Mathf.Lerp(frontLean, lean, row), Vector3.Cross(Vector3.up, f)) * Vector3.up;
        Vector3 at = Vector3.Lerp(shield.TransformPoint(handle), HoldPoint(), weight);
        shield.rotation = Quaternion.Slerp(shield.rotation, Quaternion.LookRotation(top, -f), weight);
        shield.position += at - shield.TransformPoint(handle);
    }
}
