using UnityEngine;

//in the surround's walls the soldier takes the captured pose exactly, bone for bone: kneeling in the front wall,
//standing in the walls behind. runs after the animator and the other braces
[DefaultExecutionOrder(1000)]
public class WallPoseApplier : MonoBehaviour
{
    public WallPoseData pose;
    public Transform shield;
    public Transform spear;
    public float blendSpeed = 4f;

    [Tooltip("How fast it lets go of the pose for an attack or a flinch, so the clip shows.")]
    public float releaseSpeed = 10f;

    SoldierAI soldier;
    Transform[] bones;
    Vector3 restPosition;
    Vector3 shieldRestPosition, spearRestPosition;
    Quaternion shieldRestRotation, spearRestRotation;
    float weight;
    float row;
    Animator animator;

    void Awake()
    {
        soldier = GetComponentInParent<SoldierAI>();
        restPosition = transform.localPosition;
        if (shield != null) { shieldRestPosition = shield.localPosition; shieldRestRotation = shield.localRotation; }
        if (spear != null) { spearRestPosition = spear.localPosition; spearRestRotation = spear.localRotation; }
        animator = GetComponent<Animator>();

        if (pose == null || pose.bones == null) return;
        var all = GetComponentsInChildren<Transform>(true);
        bones = new Transform[pose.bones.Length];
        for (int i = 0; i < bones.Length; i++)
            foreach (Transform t in all)
                if (t.name == pose.bones[i]) { bones[i] = t; break; }
    }

    //swinging (the sword attacks and blocks) or flinching, the animator shows through the pose
    bool Acting()
    {
        if (animator == null) return false;
        AnimatorStateInfo now = animator.GetCurrentAnimatorStateInfo(0);
        if (now.IsName("Attack") || now.IsName("Impact")) return true;
        if (!animator.IsInTransition(0)) return false;
        AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
        return next.IsName("Attack") || next.IsName("Impact");
    }

    void LateUpdate()
    {
        if (bones == null) return;
        bool inWall = soldier != null && soldier.squad != null && soldier.AtPlace && soldier.squad.InWall(soldier, out row);
        bool acting = inWall && Acting();
        weight = Mathf.MoveTowards(weight, inWall && !acting ? 1f : 0f, (acting ? releaseSpeed : blendSpeed) * Time.deltaTime);

        bool back = row >= 0.5f;
        transform.localPosition = restPosition + Vector3.up * (back ? pose.pairLift * weight : 0f);
        if (weight <= 0f) return;

        WallPoseData.Pose p = back ? pose.back : pose.front;
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null) continue;
            bones[i].localPosition = Vector3.Lerp(bones[i].localPosition, p.positions[i], weight);
            bones[i].localRotation = Quaternion.Slerp(bones[i].localRotation, p.rotations[i], weight);
        }
        if (shield != null)
        {
            shield.localPosition = Vector3.Lerp(shieldRestPosition, p.shieldPosition, weight);
            shield.localRotation = Quaternion.Slerp(shieldRestRotation, p.shieldRotation, weight);
        }
        if (spear != null)
        {
            spear.localPosition = Vector3.Lerp(spearRestPosition, p.spearPosition, weight);
            spear.localRotation = Quaternion.Slerp(spearRestRotation, p.spearRotation, weight);
        }
    }
}
