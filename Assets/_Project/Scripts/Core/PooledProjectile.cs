using System.Collections.Generic;
using UnityEngine;

//bookkeeping for one pooled object: which prefab it came from, and what it looked like before
//anyone started hanging things off it.
//
//that second part matters more than it sounds. an explosive arrow gets a VFX parented to it and
//the comment where that happens says it "dies with the arrow" - true when the arrow was
//destroyed, false the moment the arrow is reused instead. without this the thing would come back
//out of the pool still wearing every effect it has ever been given.
public class PooledProjectile : MonoBehaviour
{
    public GameObject Prefab { get; private set; }

    readonly List<Transform> bornWith = new List<Transform>();

    public void Adopt(GameObject prefab)
    {
        Prefab = prefab;

        bornWith.Clear();
        foreach (Transform child in transform) bornWith.Add(child);
    }

    //strip anything added since, and put back what was always there
    public void Restore()
    {
        //an arrow that lodged in a virus parented itself with worldPositionStays, so Unity
        //rewrote its localScale to cancel out the bone's - and those bones run to nearly six
        //times. destroying the arrow used to throw that away; handing it back does not, so
        //without this the next shot leaves the bow several times its proper size.
        if (Prefab != null) transform.localScale = Prefab.transform.localScale;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

            if (bornWith.Contains(child))
            {
                child.gameObject.SetActive(true);
                continue;
            }

            Object.Destroy(child.gameObject);
        }
    }
}
