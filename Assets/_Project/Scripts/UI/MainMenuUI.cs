using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject playSubPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Scene Names")]
    [SerializeField] private string sandboxSceneName = "SampleScene";

    void Start()
    {
        ShowMain();
    }

    // Main panel buttons
    public void OnPlayClicked()
    {
        mainPanel.SetActive(false);
        playSubPanel.SetActive(true);
    }

    public void OnSettingsClicked()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void OnVideoAudioClicked()
    {
        //need to fill;
    }

    public void OnControlsClicked()
    {
        //need to fill;
    }

    // Play sub-panel buttons
    public void OnCampaignClicked()
    {
        SceneManager.LoadScene(sandboxSceneName);
    }

    public void OnEndlessModeClicked()
    {
        SceneManager.LoadScene(sandboxSceneName);
    }

    // Back buttons
    public void OnBackClicked()
    {
        ShowMain();
    }

    public void OnQuitClicked()
    {
        Application.Quit();
    }

    void ShowMain()
    {
        mainPanel.SetActive(true);
        playSubPanel.SetActive(false);
        settingsPanel.SetActive(false);
    }
}
