using UnityEngine;

//a whole-body pose for each row of the shield wall, captured off the posed dummies: every bone, and how the
//shield and spear sit in the hands
[CreateAssetMenu(menuName = "Squad/Wall Pose")]
public class WallPoseData : ScriptableObject
{
    [System.Serializable]
    public class Pose
    {
        public Vector3[] positions;
        public Quaternion[] rotations;
        public Vector3 shieldPosition;
        public Quaternion shieldRotation = Quaternion.identity;
        public Vector3 spearPosition;
        public Quaternion spearRotation = Quaternion.identity;
    }

    public string[] bones;
    [Tooltip("Where the standing man stands from the kneeling one: metres behind, to his left, and up.")]
    public float pairBehind = 0.49f;
    public float pairSide = 0.65f;
    public float pairLift;
    [Tooltip("Gap between one pair and the next along the wall, from a second posed pair. 0 uses the surround's spacing.")]
    public float pairGap;
    public Pose front;
    public Pose back;
}
