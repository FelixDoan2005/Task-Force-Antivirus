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

        if (pausePanel != null)
            pausePanel.SetActive(isPaused);

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

    public void SelectClass(int index)
    {
        if (weaponSwitcher != null)
            weaponSwitcher.SetActive(index);

        if (classSelectorPanel != null) classSelectorPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        isPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
