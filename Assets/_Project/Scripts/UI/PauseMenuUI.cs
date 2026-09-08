using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject classSelectorPanel;
    [SerializeField] private WeaponSwitcher weaponSwitcher;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    bool isPaused;

    void Start()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (classSelectorPanel != null)
            classSelectorPanel.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    void TogglePause()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            if (pausePanel != null) pausePanel.SetActive(true);
        }
        else
        {
            // closing entirely - hide every panel, not just whichever one happens to be open
            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (classSelectorPanel != null) classSelectorPanel.SetActive(false);
        }

        if (weaponSwitcher != null)
        {
            weaponSwitcher.SetWeaponInputEnabled(!isPaused);
            weaponSwitcher.SetCrosshairMenuOpen(isPaused);
        }

        Time.timeScale = isPaused ? 0f : 1f;
        Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isPaused;
    }

    public void OnResumeClicked()
    {
        TogglePause();
    }

    public void OnMainMenuClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void OnRestartClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnSettingsClicked()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void OnSettingsBackClicked()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    public void OnClassSelectorClicked()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (classSelectorPanel != null) classSelectorPanel.SetActive(true);
    }

    public void OnClassSelectorBackClicked()
    {
        if (classSelectorPanel != null) classSelectorPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    // T0 - demigod tier (4 slots)
    public void SelectT0(int index)
    {
        switch (index)
        {
            case 0: SelectViaWeaponSwitcher((int)WeaponSwitcher.PlayerClass.Bow); break; // MasterArcher
            case 1: SelectViaWeaponSwitcher((int)WeaponSwitcher.PlayerClass.BladeMaster); break; // BladeMaster
            case 2: SelectViaWeaponSwitcher((int)WeaponSwitcher.PlayerClass.Demon); break; // Demon
            case 3: NotImplementedYet("Jester"); break;
        }
    }

    // T1 - elite tier (4 slots, none built yet)
    public void SelectT1(int index)
    {
        switch (index)
        {
            case 0: NotImplementedYet("Temp"); break;
            case 1: NotImplementedYet("Temp"); break;
            case 2: NotImplementedYet("Temp"); break;
            case 3: NotImplementedYet("Temp"); break;
        }
    }

    // T2 - breach squad tier (4 slots, none built yet)
    public void SelectT2(int index)
    {
        switch (index)
        {
            case 0: NotImplementedYet("Temp"); break;
            case 1: NotImplementedYet("Temp"); break;
            case 2: NotImplementedYet("Temp"); break;
            case 3: NotImplementedYet("Temp"); break;
        }
    }

    // T3 - firewall grunt tier (4 slots)
    public void SelectT3(int index)
    {
        switch (index)
        {
            case 0: SelectViaWeaponSwitcher((int)WeaponSwitcher.PlayerClass.Sword); break; // Swordsmen
            case 1: SelectViaWeaponSwitcher((int)WeaponSwitcher.PlayerClass.Riflemen); break; // Riflemen
            case 2: NotImplementedYet("Temp"); break;
            case 3: NotImplementedYet("Temp"); break;
        }
    }

    void NotImplementedYet(string className)
    {
        Debug.Log($"{className} isn't built yet.");
    }

    void SelectViaWeaponSwitcher(int weaponSwitcherIndex)
    {
        if (weaponSwitcher != null)
        {
            weaponSwitcher.SetWeaponInputEnabled(true);
            weaponSwitcher.SetActive(weaponSwitcherIndex);
            weaponSwitcher.SetCrosshairMenuOpen(false);
        }

        if (classSelectorPanel != null) classSelectorPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        isPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
