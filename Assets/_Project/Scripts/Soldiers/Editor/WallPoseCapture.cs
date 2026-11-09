using System.Linq;
using UnityEditor;
using UnityEngine;

//Tools > Squad > Capture Spear Wall Pose: copies the posed wall pair on the dummies (kneeling front, standing
//back) bone for bone into the pose the spearmen take in the surround. rebuild the soldiers after
public static class WallPoseCapture
{
    public const string Asset = "Assets/_Project/Art/Characters/ElfSoldiers/SpearWallPose.asset";
    const string Pair = "WallPoseDummies/Wall Pair (move the back one)";

    [MenuItem("Tools/Squad/Capture Spear Wall Pose")]
    public static void Capture()
    {
        GameObject pair = GameObject.Find(Pair);
        if (pair == null) { Debug.LogError("No " + Pair + " in the open scene"); return; }
        var front = pair.transform.Find("Front - kneeling").GetComponent<WallPoseDummy>();
        var back = pair.transform.Find("Back - standing (move me)").GetComponent<WallPoseDummy>();

        var data = AssetDatabase.LoadAssetAtPath<WallPoseData>(Asset);
        bool created = data == null;
        if (created) data = ScriptableObject.CreateInstance<WallPoseData>();

        Transform[] skeleton = Skeleton(front);
        data.bones = skeleton.Select(t => t.name).ToArray();
        data.front = Read(front, data.bones);
        data.back = Read(back, data.bones);

        //where the standing man stands, from the kneeling one's point of view
        Transform f = front.model.transform;
        Vector3 d = back.model.transform.position - f.position;
        data.pairBehind = -Vector3.Dot(d, f.forward);
        data.pairSide = -Vector3.Dot(d, f.right);
        data.pairLift = d.y;

        //a second kneeling man beside the first sets the gap between pairs along the wall
        Transform next = pair.transform.Find("Front - kneeling (1)");
        WallPoseDummy nextDummy = next != null ? next.GetComponent<WallPoseDummy>() : null;
        data.pairGap = nextDummy != null ? Mathf.Abs(Vector3.Dot(nextDummy.model.transform.position - f.position, f.right)) : 0f;

        if (created) AssetDatabase.CreateAsset(data, Asset);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log(string.Format("Spear wall pose captured: {0} bones per row; the standing man {1:0.00}m behind, {2:0.00}m to the left, {3:0.00}m up; pairs {4:0.00}m apart. " +
                                "Rebuild the soldiers (Tools > Squad > Build Elf Soldiers) to use it.", data.bones.Length, data.pairBehind, data.pairSide, data.pairLift, data.pairGap));
    }

    static Transform[] Skeleton(WallPoseDummy d) => d.model.transform.Find("Armature").GetComponentsInChildren<Transform>(true);

    static WallPoseData.Pose Read(WallPoseDummy d, string[] names)
    {
        Transform[] skeleton = Skeleton(d);
        var p = new WallPoseData.Pose
        {
            positions = new Vector3[names.Length],
            rotations = new Quaternion[names.Length]
        };
        for (int i = 0; i < names.Length; i++)
        {
            Transform t = skeleton.FirstOrDefault(x => x.name == names[i]);
            if (t == null) continue;
            p.positions[i] = t.localPosition;
            p.rotations[i] = t.localRotation;
        }
        p.shieldPosition = d.shield.localPosition;
        p.shieldRotation = d.shield.localRotation;
        p.spearPosition = d.spear.localPosition;
        p.spearRotation = d.spear.localRotation;
        return p;
    }
}
