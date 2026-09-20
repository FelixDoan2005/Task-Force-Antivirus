using System.Collections.Generic;
using UnityEngine;

//finds the nearest point on a character's ACTUAL animated surface.
//
//colliders on a creature like the tank virus are always a rough approximation - spheres on a few
//bones, and nothing at all over the spikes and legs - so an arrow stops wherever that crude shape
//happens to be and hangs in the air half a metre from anything you can see. this bakes the posed
//mesh and finds the real surface, so the arrow can be placed where it looks like it hit.
//
//the bake is the expensive part, so results are cached per renderer per frame: a fifty-arrow
//volley landing on the same virus in the same frame pays for one bake, not fifty.
public static class MeshSurfaceSnap
{
    class Baked
    {
        public int frame = -1;
        public Vector3[] points;
    }

    static readonly Dictionary<SkinnedMeshRenderer, Baked> cache =
        new Dictionary<SkinnedMeshRenderer, Baked>();

    static UnityEngine.Mesh scratch;

    [Tooltip("Every nth vertex is kept. The full mesh is far finer than an arrow needs.")]
    const int VertexStride = 3;

    public static bool TryFindSurface(GameObject target, Vector3 near,
                                      out Vector3 surfacePoint, out Transform bone)
    {
        surfacePoint = near;
        bone = null;

        if (target == null) return false;

        float best = float.MaxValue;
        SkinnedMeshRenderer bestRenderer = null;

        foreach (SkinnedMeshRenderer smr in target.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Vector3[] points = PointsFor(smr);
            if (points == null) continue;

            for (int i = 0; i < points.Length; i++)
            {
                float d = (points[i] - near).sqrMagnitude;
                if (d >= best) continue;

                best = d;
                surfacePoint = points[i];
                bestRenderer = smr;
            }
        }

        if (bestRenderer == null) return false;

        //whichever bone is closest owns it, so the arrow rides that limb as it animates rather
        //than the object root - on a creature this size a leg moves metres on its own
        bone = NearestBone(bestRenderer, surfacePoint);
        return true;
    }

    static Vector3[] PointsFor(SkinnedMeshRenderer smr)
    {
        if (smr == null || smr.sharedMesh == null) return null;

        if (!cache.TryGetValue(smr, out Baked entry))
        {
            entry = new Baked();
            cache[smr] = entry;
        }

        //one bake per renderer per frame, however many arrows arrive
        if (entry.frame == Time.frameCount && entry.points != null) return entry.points;

        if (scratch == null) scratch = new UnityEngine.Mesh();
        smr.BakeMesh(scratch, true);

        Vector3[] verts = scratch.vertices;
        int kept = (verts.Length + VertexStride - 1) / VertexStride;

        if (entry.points == null || entry.points.Length != kept)
            entry.points = new Vector3[kept];

        //baked vertices are in the renderer's space, so they are lifted into the world once here
        Transform t = smr.transform;
        for (int i = 0, k = 0; i < verts.Length && k < kept; i += VertexStride, k++)
            entry.points[k] = t.TransformPoint(verts[i]);

        entry.frame = Time.frameCount;
        return entry.points;
    }

    static Transform NearestBone(SkinnedMeshRenderer smr, Vector3 point)
    {
        Transform[] bones = smr.bones;
        if (bones == null || bones.Length == 0) return smr.transform;

        Transform best = smr.transform;
        float bestDistance = float.MaxValue;

        foreach (Transform b in bones)
        {
            if (b == null) continue;

            float d = (b.position - point).sqrMagnitude;
            if (d >= bestDistance) continue;

            bestDistance = d;
            best = b;
        }

        return best;
    }

    //renderers are destroyed with their viruses, so the cache would grow forever otherwise
    public static void Forget(GameObject target)
    {
        if (target == null) return;

        foreach (SkinnedMeshRenderer smr in target.GetComponentsInChildren<SkinnedMeshRenderer>())
            cache.Remove(smr);
    }
}
