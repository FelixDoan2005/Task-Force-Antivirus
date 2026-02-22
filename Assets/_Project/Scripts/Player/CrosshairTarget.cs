using UnityEngine;
using UnityEngine.UI;

public class CrosshairTarget : MonoBehaviour
{
    public Image[] parts;
    public Camera cam;
    public float range = 100f;

    void Update()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            if (hit.collider.GetComponentInParent<ZombieHealth>() != null)
                SetColor(Color.red);
            else
                SetColor(Color.white);
        }
        else
        {
            SetColor(Color.white);
        }
    }

    void SetColor(Color c)
    {
        foreach (var img in parts)
            img.color = c;
    }
}