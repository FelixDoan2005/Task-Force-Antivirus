using UnityEngine;

//shared lookups for the mound and the things it bursts into. the mound hunts like the rest of
//the set - whatever damageable is nearest - while its moundlings want the player and nothing
//else, so the two searches are kept apart rather than one being filtered down to the other.
public static class MoundTarget
{
    public static IDamageable NearestDamageable(Transform self, float range)
    {
        return TargetScan.NearestDamageable(self, range);
    }

    //no range on this one: a moundling has already been thrown into the middle of the fight and
    //should keep coming however far the player runs
    public static PlayerHealth NearestPlayer(Vector3 from)
    {
        PlayerHealth best = null;
        float bestDistance = float.MaxValue;

        foreach (PlayerHealth player in Object.FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
        {
            if (player == null || !player.IsTargetable) continue;

            float d = (player.transform.position - from).sqrMagnitude;
            if (d >= bestDistance) continue;

            bestDistance = d;
            best = player;
        }

        return best;
    }

    //the flat gap between two bodies rather than their origins, so a wide creature stops when it
    //is actually touching rather than shoving its target around
    public static float SurfaceGap(Transform self, Collider selfCollider, Transform target)
    {
        if (target == null) return float.MaxValue;

        Collider targetCollider = target.GetComponentInChildren<Collider>();

        Vector3 me = self.position;
        Vector3 theirs = targetCollider != null ? targetCollider.bounds.ClosestPoint(me) : target.position;
        Vector3 mine = selfCollider != null ? selfCollider.bounds.ClosestPoint(theirs) : me;

        Vector3 between = theirs - mine;
        between.y = 0f;

        return between.magnitude;
    }
}
