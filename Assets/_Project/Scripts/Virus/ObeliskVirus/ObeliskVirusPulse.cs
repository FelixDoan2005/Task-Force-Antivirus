using System.Collections.Generic;
using UnityEngine;

//one slow pulse, used by both the obelisk's rhythm and its dying burst. the two differ only in
//radius, strength and how long it lasts, so they share this rather than each owning a copy.
public static class ObeliskVirusPulse
{
    //generous: a wide pulse over a crowded fight. a query that fills it is already pathological.
    const int Capacity = 128;

    static readonly Collider[] buffer = new Collider[Capacity];

    //a body has several colliders, so the same victim comes back more than once per query
    static readonly List<Slowable> caught = new List<Slowable>();

    //returns how many separate targets it caught, so a caller can tell a pulse that landed from
    //one that hit nothing
    public static int Emit(Vector3 centre, float radius, float multiplier, float duration)
    {
        caught.Clear();

        int count = Physics.OverlapSphereNonAlloc(centre, radius, buffer,
                                                  ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider hit = buffer[i];
            if (hit == null) continue;

            //the good side. when allied soldiers exist this is the one line that widens - give
            //them a PlayerHealth or add their health type here and the pulse picks them up.
            PlayerHealth victim = hit.GetComponentInParent<PlayerHealth>();
            if (victim == null || !victim.IsTargetable) continue;

            Slowable slowable = Slowable.For(victim);
            if (slowable == null || caught.Contains(slowable)) continue;

            caught.Add(slowable);
            slowable.ApplySlow(multiplier, duration);
        }

        return caught.Count;
    }

    //fires the effect for a pulse. anything can go in the slot - this ring, a particle system
    //from a pack, whatever you build - because the spawner only plays what it finds. a prefab
    //carrying an ExpandingRingVFX is additionally told how wide to grow, so the effect keeps
    //matching the radius even after you change it.
    public static void Show(GameObject prefab, Vector3 centre, float radius, float lifetime)
    {
        if (prefab == null) return;

        GameObject fx = VfxSpawner.Spawn(prefab, centre, Quaternion.identity, null, lifetime);
        if (fx == null) return;

        foreach (ExpandingRingVFX ring in fx.GetComponentsInChildren<ExpandingRingVFX>(true))
            ring.Play(radius);
    }

    //a flat ring at the creature's feet rather than a wire sphere. the pulse is judged on the
    //ground - how much of the floor it covers - and at this size a sphere reads as a ball
    //hanging in the air, which tells you nothing about where it reaches.
    public static void DrawRing(Vector3 centre, float radius, Color colour)
    {
        const int Segments = 72;

        Gizmos.color = colour;

        Vector3 previous = centre + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= Segments; i++)
        {
            float a = i / (float)Segments * Mathf.PI * 2f;

            Vector3 next = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);

            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
