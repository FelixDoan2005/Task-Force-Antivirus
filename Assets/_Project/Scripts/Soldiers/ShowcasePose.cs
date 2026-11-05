using System.Collections.Generic;
using UnityEngine;

//holds a model in one frame of a clip, in the editor too, so it can be looked at and tweaked (weapon grips, colours).
//sits on a wrapper above the model, so applying the model's overrides never drags it into the prefab
[ExecuteAlways]
public class ShowcasePose : MonoBehaviour
{
    [Tooltip("The model to pose. Empty uses the first Animator underneath.")]
    public Animator target;
    public AnimationClip clip;
    [Range(0f, 1f)] public float time;
    [Tooltip("In play mode, loop the clip instead of holding the frame.")]
    public bool playInPlayMode = true;

    Transform probe;
    Vector3 probePosition;

    void OnEnable()
    {
        if (target == null) target = GetComponentInChildren<Animator>();
        probe = null;

        //in play mode the pose comes from here, not the soldier's own animator
        if (Application.isPlaying && target != null)
        {
            target.enabled = false;
            var driver = target.GetComponent<SoldierAnimatorDriver>();
            if (driver != null) driver.enabled = false;
        }
#if UNITY_EDITOR
        if (!Application.isPlaying) Watch(this);
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        watched.Remove(this);
#endif
    }

    //a changed clip or time gets posed on the editor's next tick
    void OnValidate() => probe = null;

    void Update()
    {
        if (!Application.isPlaying || target == null || clip == null) return;
        if (playInPlayMode) time = Mathf.Repeat(time + Time.deltaTime / Mathf.Max(0.01f, clip.length), 1f);
        Apply();
    }

    public void Apply()
    {
        if (target == null) target = GetComponentInChildren<Animator>();
        if (target == null || clip == null) return;

        clip.SampleAnimation(target.gameObject, time * clip.length);
        probe = target.isHuman ? target.GetBoneTransform(HumanBodyBones.Hips) : target.transform;
        probePosition = probe != null ? probe.localPosition : Vector3.zero;
    }

#if UNITY_EDITOR
    //in the editor nothing ticks a scene object reliably, and loading or applying resets the bones: so every editor
    //tick each one checks its hips are still where its pose put them, and poses again if not
    static readonly List<ShowcasePose> watched = new List<ShowcasePose>();
    static bool hooked;

    static void Watch(ShowcasePose pose)
    {
        if (!watched.Contains(pose)) watched.Add(pose);
        if (hooked) return;
        hooked = true;
        UnityEditor.EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (Application.isPlaying) return;
        for (int i = watched.Count - 1; i >= 0; i--)
        {
            ShowcasePose p = watched[i];
            if (p == null) { watched.RemoveAt(i); continue; }
            if (p.probe == null || p.probe.localPosition != p.probePosition) p.Apply();
        }
    }
#endif
}
