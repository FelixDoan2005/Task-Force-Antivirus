using UnityEngine;

//spawns an effect prefab and actually starts it. plenty of the asset packs ship their prefabs
//with Play On Awake turned off - the demo scenes trigger them from a script - so dropping one
//into an inspector slot and instantiating it gives you a silent, invisible object and looks
//exactly like the effect is "not working". this plays every system in the prefab on spawn.
public static class VfxSpawner
{
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation,
                                   Transform parent = null, float lifetime = 0f)
    {
        if (prefab == null) return null;

        GameObject fx = parent != null
            ? Object.Instantiate(prefab, position, rotation, parent)
            : Object.Instantiate(prefab, position, rotation);

        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            ps.Play(false);

        //0 means it is parented to something that will clean it up, like the arrow it rides on
        if (lifetime > 0f) Object.Destroy(fx, lifetime);

        return fx;
    }
}
