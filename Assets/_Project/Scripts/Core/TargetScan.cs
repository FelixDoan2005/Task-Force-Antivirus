using System.Collections.Generic;
using UnityEngine;

//one place for "what is the nearest thing near me that I can hurt", shared by every virus.
//
//each virus used to run its own Physics.OverlapSphere every frame - two of them, one for moving
//and one for swinging - and then call GetComponentInParent<IDamageable>() on every collider that
//came back. on a bare test plane that is nothing, because the query returns about a dozen
//colliders. on a dressed map it returns every crate, panel and prop inside the radius, and the
//speed virus searches a forty metre sphere. fifty viruses then walk several thousand hierarchies
//a frame looking for damageables that are almost never there.
//
//two things fix that without changing a single decision the viruses make:
//
//  * the query writes into one shared buffer instead of allocating a fresh array per call
//  * which collider belongs to which damageable is remembered, so the hierarchy walk happens
//    once per collider rather than once per collider per frame. most of what it remembers is
//    "this one is not a target", which is exactly the answer that used to cost the most.
public static class TargetScan
{
    //deliberately generous. a query that fills it is already pathological, and truncating is
    //the one way this could change behaviour, so it is worth the 2KB.
    const int Capacity = 256;

    static readonly Collider[] buffer = new Collider[Capacity];

    //collider -> whatever owns it, including null for "nothing here". a destroyed collider leaves
    //a dead key behind, so the whole thing is dropped periodically rather than grown forever.
    static readonly Dictionary<Collider, IDamageable> owners = new Dictionary<Collider, IDamageable>();

    const float ForgetAfter = 10f;
    static float forgetAt;

    //includeWalls off finds people only - players, and allied soldiers once they exist - for
    //anything that should go for a person in range before it settles for the wall
    public static IDamageable NearestDamageable(Transform self, float range, bool includeWalls = true)
    {
        int count = Physics.OverlapSphereNonAlloc(self.position, range, buffer);

        Transform root = self.root;
        Vector3 from = self.position;

        IDamageable closest = null;
        float closestDistance = Mathf.Infinity;

        for (int i = 0; i < count; i++)
        {
            Collider hit = buffer[i];
            if (hit == null) continue;

            if (hit.transform.root == root) continue;          //itself

            IDamageable damageable = OwnerOf(hit);
            if (damageable == null || !damageable.IsTargetable) continue;
            if (!includeWalls && damageable is WallHealth) continue;

            //measured to the nearest point on the collider, not its centre: a wall is forty
            //metres wide and its centre is meaningless to something standing against it
            float d = (hit.ClosestPoint(from) - from).sqrMagnitude;
            if (d >= closestDistance) continue;

            closestDistance = d;
            closest = damageable;
        }

        return closest;
    }

    static IDamageable OwnerOf(Collider hit)
    {
        //a full clear rather than a prune: it drops dead keys and stale answers in one go, and
        //once every ten seconds the cost of rebuilding is nothing against what it saves
        if (Time.unscaledTime >= forgetAt)
        {
            owners.Clear();
            forgetAt = Time.unscaledTime + ForgetAfter;
        }

        if (owners.TryGetValue(hit, out IDamageable known)) return known;

        IDamageable found = hit.GetComponentInParent<IDamageable>();
        owners[hit] = found;

        return found;
    }
}
