using UnityEngine;
using System.Collections;

public class PlayerRespawn : MonoBehaviour
{
    [Header("Respawn")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] public float respawnDelay = 5f;

    HeroHealth heroHealth;
    HeroController heroController;
    WeaponSwitcher weaponSwitcher;
    CharacterController characterController;

    void Awake()
    {
        heroHealth = GetComponent<HeroHealth>();
        heroController = GetComponent<HeroController>();
        weaponSwitcher = GetComponentInChildren<WeaponSwitcher>();
        characterController = GetComponent<CharacterController>();
    }

    void OnEnable()
    {
        heroHealth.OnDied += HandleDeath;
    }

    void OnDisable()
    {
        heroHealth.OnDied -= HandleDeath;
    }

    void HandleDeath()
    {
        SetPlayerActive(false);
        StartCoroutine(RespawnAfterDelay());
    }

    IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);

        if (spawnPoint != null)
            transform.position = spawnPoint.position;

        heroHealth.Respawn();
        SetPlayerActive(true);
    }

    void SetPlayerActive(bool active)
    {
        if (heroController != null) heroController.enabled = active;
        if (weaponSwitcher != null) weaponSwitcher.enabled = active;
        if (characterController != null) characterController.enabled = active;
    }
}
