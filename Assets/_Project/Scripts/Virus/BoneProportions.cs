using System.Collections.Generic;
using UnityEngine;

//re-proportions a creature by scaling whole bone families - longer legs, a bigger head - so one
//mesh can produce visibly different animals rather than the same one at different sizes.
//
//it runs in LateUpdate because the clips in this pack animate bone scale (477 curves in the walk
//alone), so anything set earlier is overwritten the same frame. the multiplier is applied to the
//scale the bone had in its bind pose rather than to whatever it is now, or it would compound
//itself every frame and the creature would balloon.
public class BoneProportions : MonoBehaviour
{
    [System.Serializable]
    public struct Rule
    {
        [Tooltip("Any bone whose name contains this gets scaled. 'DEF_leg' catches every leg.")]
        public string nameContains;

        [Tooltip("1 leaves it alone. Uniform, deliberately - scaling one axis of a bone that " +
                 "then rotates shears the mesh.")]
        public float scale;
    }

    [Tooltip("Applied in order, so a later rule can override an earlier one for a specific bone.")]
    public Rule[] rules;

    readonly List<Transform> bones = new List<Transform>();
    readonly List<Vector3> baseScales = new List<Vector3>();

    //what the creature draws with, so the pinning can be skipped while none of it is on screen
    Renderer[] renderers;

    void Awake()
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            float factor = 1f;
            bool matched = false;

            foreach (Rule r in rules)
            {
                if (string.IsNullOrEmpty(r.nameContains)) continue;
                if (!t.name.Contains(r.nameContains)) continue;

                factor = r.scale;
                matched = true;
            }

            if (!matched || Mathf.Approximately(factor, 1f)) continue;

            bones.Add(t);
            baseScales.Add(t.localScale * factor);
        }

        renderers = GetComponentsInChildren<Renderer>(true);
    }

    void LateUpdate()
    {
        //off screen there is nothing to hold the shape against: the Animator is culled, so it is
        //not writing bone scales either, and whatever was pinned last frame is still pinned.
        //this matters because the bloom carries 2,916 bones - that is 2,916 transform writes a
        //frame, each one dirtying the hierarchy, for something nobody is looking at.
        if (!AnythingVisible()) return;

        for (int i = 0; i < bones.Count; i++)
            if (bones[i] != null) bones[i].localScale = baseScales[i];
    }

    bool AnythingVisible()
    {
        if (renderers == null || renderers.Length == 0) return true;

        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null && renderers[i].isVisible) return true;

        return false;
    }
}
