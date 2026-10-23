using System.Collections.Generic;
using UnityEngine;

//reuses projectiles instead of creating and destroying them.
//
//the archer's ultimate puts hundreds of arrows in the air at once, and every one of them used to
//be a fresh Instantiate on the way in and a Destroy on the way out - the allocation, the
//component wake-up, and then the garbage. that is a spike exactly when the screen is busiest.
//
//this changes nothing about how an arrow behaves. it is the same prefab, the same components and
//the same values; it just gets handed back rather than thrown away.
public static class ProjectilePool
{
    static readonly Dictionary<GameObject, Stack<GameObject>> idle =
        new Dictionary<GameObject, Stack<GameObject>>();

    //somewhere out of the way to keep sleeping projectiles, so they are not loose in the
    //hierarchy and are not children of anything that might be destroyed under them
    static Transform yard;

    public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        if (!idle.TryGetValue(prefab, out Stack<GameObject> stack))
            idle[prefab] = stack = new Stack<GameObject>();

        GameObject instance = null;

        //an arrow lodged in a virus is parented to it, so when that virus dies the arrow goes
        //with it. the pool can therefore be holding entries that no longer exist.
        while (stack.Count > 0 && instance == null) instance = stack.Pop();

        if (instance == null)
        {
            instance = Object.Instantiate(prefab, position, rotation);
            instance.AddComponent<PooledProjectile>().Adopt(prefab);
        }
        else
        {
            instance.transform.SetParent(null, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
        }

        PooledProjectile tag = instance.GetComponent<PooledProjectile>();
        if (tag != null) tag.Restore();

        return instance;
    }

    public static void Release(GameObject instance)
    {
        if (instance == null) return;

        PooledProjectile tag = instance.GetComponent<PooledProjectile>();

        //not ours - it was made some other way, so it is destroyed the way it always was
        if (tag == null || tag.Prefab == null)
        {
            Object.Destroy(instance);
            return;
        }

        if (yard == null)
        {
            var host = new GameObject("ProjectilePool");
            Object.DontDestroyOnLoad(host);
            yard = host.transform;
        }

        //unparented first: it may be stuck in a virus, and being destroyed along with its host
        //is exactly what we are trying to avoid
        instance.transform.SetParent(yard, false);
        instance.SetActive(false);

        if (!idle.TryGetValue(tag.Prefab, out Stack<GameObject> stack))
            idle[tag.Prefab] = stack = new Stack<GameObject>();

        if (!stack.Contains(instance)) stack.Push(instance);
    }
}
