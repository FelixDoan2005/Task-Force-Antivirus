using UnityEngine;

//the bloom only ever cares about the wall, so the target search is a wall search rather than
//the nearest-IDamageable sweep the other viruses run. that is the whole difference between it
//and them: a basic virus will happily stop and chew on whoever wanders past, this one walks
//through a fight without looking up.
public static class BloomVirusTarget
{
    public static WallHealth Nearest(Vector3 from)
    {
        WallHealth best = null;
        float bestDistance = float.MaxValue;

        foreach (WallHealth wall in Object.FindObjectsByType<WallHealth>(FindObjectsSortMode.None))
        {
            if (wall == null || !wall.IsTargetable) continue;

            float d = (wall.transform.position - from).sqrMagnitude;
            if (d >= bestDistance) continue;

            bestDistance = d;
            best = wall;
        }

        return best;
    }

    //the flat gap between the two bodies rather than between their origins. a wall is 10m wide
    //and 20m tall, so anything measured from its centre is meaningless - a radius-based check
    //would have the virus stop and swing at thin air several metres out.
    public static float SurfaceGap(Transform self, Collider selfCollider, WallHealth wall)
    {
        if (wall == null) return float.MaxValue;

        Collider wallCollider = wall.GetComponentInChildren<Collider>();

        Vector3 me = self.position;
        Vector3 theirs = wallCollider != null ? wallCollider.bounds.ClosestPoint(me) : wall.transform.position;
        Vector3 mine = selfCollider != null ? selfCollider.bounds.ClosestPoint(theirs) : me;

        Vector3 between = theirs - mine;
        between.y = 0f;

        return between.magnitude;
    }
}
