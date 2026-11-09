using UnityEngine;

//standing still out of the wall: the body turns to face ahead, the shield squares up flat on, a spear stands
//straight up and a sword is held angled forward. on the model, after the animator and the wall pose
[DefaultExecutionOrder(1001)]
public class IdleStance : MonoBehaviour
{
    [Tooltip("The standing idle turns the body about 55 degrees right; standing still, the whole body turns back left this much.")]
    public float idleTurnLeft = 25f;
    public bool squareShield = true;
    public bool uprightWeapon = true;
    [Tooltip("A sword leans forward from upright this many degrees, held out the way a sword is held; a spear stands straight.")]
    public float swordForwardTilt = 40f;
    [Tooltip("And leans out to the sword-arm side this many degrees.")]
    public float swordOutwardTilt = 10f;
    public float blendSpeed = 3f;

    SoldierAI soldier;
    Animator animator;
    Transform shield, weapon, leftHand, rightHand;
    Vector3 weaponAxis = Vector3.forward;
    bool isSword;
    Quaternion restRotation;
    Vector3 lastPosition;
    float still;

    void Awake()
    {
        soldier = GetComponentInParent<SoldierAI>();
        animator = GetComponent<Animator>();
        restRotation = transform.localRotation;
        if (animator != null && animator.isHuman)
        {
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
        foreach (Transform t in GetComponentsInChildren<Transform>(false))
        {
            if (t.name == "SM_Shield") shield = t;
            else if (t.name == "SM_Glaive" || (t.name == "SM_Sword" && weapon == null)) weapon = t;
        }

        isSword = weapon != null && weapon.name == "SM_Sword";

        //the weapon's length is its mesh's longest side
        MeshFilter mf = weapon != null ? weapon.GetComponent<MeshFilter>() : null;
        if (mf != null && mf.sharedMesh != null)
        {
            Vector3 size = mf.sharedMesh.bounds.size;
            weaponAxis = size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward;
        }
    }

    //swinging or flinching, the animator shows
    bool Acting()
    {
        if (animator == null) return false;
        AnimatorStateInfo now = animator.GetCurrentAnimatorStateInfo(0);
        if (now.IsName("Attack") || now.IsName("Impact")) return true;
        return animator.IsInTransition(0) && (animator.GetNextAnimatorStateInfo(0).IsName("Attack") || animator.GetNextAnimatorStateInfo(0).IsName("Impact"));
    }

    void LateUpdate()
    {
        if (soldier == null) return;
        Vector3 pos = soldier.transform.position;
        float speed = Time.deltaTime > 0f ? (pos - lastPosition).magnitude / Time.deltaTime : 0f;
        lastPosition = pos;

        bool inWall = soldier.squad != null && soldier.AtPlace && soldier.squad.InWall(soldier, out _);
        bool idle = !inWall && !Acting() && speed < 0.2f && soldier.Target == null;
        still = Mathf.MoveTowards(still, idle ? 1f : 0f, blendSpeed * Time.deltaTime);

        transform.localRotation = restRotation * Quaternion.Euler(0f, -idleTurnLeft * still, 0f);
        if (still <= 0f) return;

        //shield upright (its length is local Z, its face -Y), face ahead, turned about the hand holding it
        if (squareShield && shield != null)
        {
            Quaternion target = Quaternion.LookRotation(Vector3.up, -soldier.transform.forward);
            Turn(shield, leftHand, Quaternion.Slerp(Quaternion.identity, target * Quaternion.Inverse(shield.rotation), still));
        }

        //spear straight up; sword leaning forward and a little out. whichever end is already higher stays on top
        if (uprightWeapon && weapon != null)
        {
            Vector3 along = weapon.TransformDirection(weaponAxis);
            if (along.y < 0f) along = -along;
            Vector3 aim = Vector3.up;
            if (isSword)
            {
                Transform body = soldier.transform;
                aim = Quaternion.AngleAxis(swordForwardTilt, body.right) * Quaternion.AngleAxis(-swordOutwardTilt, body.forward) * Vector3.up;
            }
            Turn(weapon, rightHand, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(along, aim), still));
        }
    }

    static void Turn(Transform t, Transform about, Quaternion turn)
    {
        Vector3 pivot = about != null ? about.position : t.position;
        t.position = pivot + turn * (t.position - pivot);
        t.rotation = turn * t.rotation;
    }
}
