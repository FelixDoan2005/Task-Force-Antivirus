using UnityEngine;
using UnityEngine.UI;

public class CrosshairTarget : MonoBehaviour
{
    public Image[] parts;
    public Image dotImage;
    public GameObject crosshairRoot;
    public RectTransform crosshairRect;
    public float rifleCrosshairScale = 0.5f;
    public Camera cam;
    public float range = 100f;

    void Update()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            if (hit.collider.GetComponentInParent<VirusHealthBase>() != null)
                SetColor(Color.red);
            else
                SetColor(Color.white);
        }
        else
        {
            SetColor(Color.white);
        }
    }

    int currentWeaponMode;
    bool hiddenByMenu;

    public void SetWeaponMode(int weaponIndex)
    {
        currentWeaponMode = weaponIndex;
        bool isRifle = weaponIndex == 2;

        foreach (var p in parts)
            if (p != null) p.gameObject.SetActive(true);

        if (dotImage != null)
            dotImage.gameObject.SetActive(false);

        if (isRifle && crosshairRect != null)
            crosshairRect.localScale = Vector3.one * rifleCrosshairScale;

        ApplyVisibility();
    }

    //called when the pause/class menu opens or closes - hides the crosshair for every class,
    //not just the melee ones
    public void SetMenuOpen(bool open)
    {
        hiddenByMenu = open;
        ApplyVisibility();
    }

    //two independent reasons to hide it, so they're resolved in one place rather than
    //each caller stomping the other's SetActive
    void ApplyVisibility()
    {
        bool hideForWeapon = currentWeaponMode == 1; // melee classes
        if (crosshairRoot != null)
            crosshairRoot.SetActive(!hideForWeapon && !hiddenByMenu);
    }

    void SetColor(Color c)
    {
        foreach (var img in parts)
            if (img != null) img.color = c;

        if (dotImage != null)
            dotImage.color = c;
    }
}