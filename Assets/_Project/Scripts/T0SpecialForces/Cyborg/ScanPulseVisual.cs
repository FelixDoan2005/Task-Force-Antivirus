using System.Collections.Generic;
using UnityEngine;

//draws the Q scan in the game view. Debug.DrawLine only ever renders in the scene view, so this
//builds the same ring and target lines out of LineRenderers the camera can actually see.
//one of these is spawned per scan; it fades itself out and then cleans itself up.
public class ScanPulseVisual : MonoBehaviour
{
    const int RingSegments = 64;

    //lifts the ring off the floor so it doesn't z-fight with the ground
    const float RingLift = 0.05f;

    readonly List<LineRenderer> lines = new List<LineRenderer>();
    readonly List<Transform> targets = new List<Transform>();
    LineRenderer ring;

    Vector3 centre;
    float duration;
    float age;
    Color colour;
    float lineHeight;

    static Material defaultMaterial;

    public static ScanPulseVisual Spawn(Vector3 centre, float radius, IList<Transform> targets,
                                        Color colour, float width, float duration,
                                        float lineHeight, Material material = null)
    {
        var go = new GameObject("ScanPulseVisual");
        var v = go.AddComponent<ScanPulseVisual>();

        v.centre = centre;
        v.colour = colour;
        v.duration = duration;
        v.lineHeight = lineHeight;

        if (material == null) material = DefaultMaterial();

        //the ring stays where the scan went off, even if he moves on
        v.ring = v.MakeLine("Ring", material, width, true);
        v.ring.positionCount = RingSegments;
        for (int i = 0; i < RingSegments; i++)
        {
            float a = i / (float)RingSegments * Mathf.PI * 2f;
            v.ring.SetPosition(i, centre + new Vector3(Mathf.Cos(a) * radius, RingLift, Mathf.Sin(a) * radius));
        }

        foreach (Transform t in targets)
        {
            if (t == null) continue;
            v.targets.Add(t);
            LineRenderer lr = v.MakeLine("Line", material, width, false);
            lr.positionCount = 2;
            v.lines.Add(lr);
        }

        v.ApplyFrame();
        return v;
    }

    //Sprites/Default is always included in builds and draws vertex colour with alpha under URP,
    //which is exactly what a fading line needs - no custom material to set up
    static Material DefaultMaterial()
    {
        if (defaultMaterial == null) defaultMaterial = new Material(Shader.Find("Sprites/Default"));
        return defaultMaterial;
    }

    LineRenderer MakeLine(string name, Material mat, float width, bool loop)
    {
        var child = new GameObject(name);
        child.transform.SetParent(transform, false);

        var lr = child.AddComponent<LineRenderer>();
        lr.sharedMaterial = mat;
        lr.useWorldSpace = true;
        lr.loop = loop;
        lr.widthMultiplier = width;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    void LateUpdate()
    {
        age += Time.deltaTime;
        if (age >= duration)
        {
            Destroy(gameObject);
            return;
        }

        ApplyFrame();
    }

    void ApplyFrame()
    {
        //full strength for the first half so it reads clearly, then fades out of the way
        float t = duration > 0f ? age / duration : 1f;
        float alpha = t < 0.5f ? 1f : Mathf.InverseLerp(1f, 0.5f, t);

        Color c = colour;
        c.a *= alpha;

        ring.startColor = c;
        ring.endColor = c;

        Vector3 from = centre + Vector3.up * lineHeight;

        for (int i = 0; i < lines.Count; i++)
        {
            LineRenderer lr = lines[i];

            //the target died mid-pulse - its line just goes away
            if (targets[i] == null)
            {
                lr.enabled = false;
                continue;
            }

            //tracks the target, so a line still points at a virus that moved after being scanned
            lr.SetPosition(0, from);
            lr.SetPosition(1, targets[i].position + Vector3.up * lineHeight);
            lr.startColor = c;
            lr.endColor = c;
        }
    }
}
