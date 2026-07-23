using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelCanvas : MonoBehaviour
{
    public GameObject nextStepPanel;
    void Start()
    {
        LevelManager.Instance.LevelEnded += OnLevelEnded;
    }

    private void OnLevelEnded(LevelResult result)
    {
        nextStepPanel.SetActive(true);
    }

    public void OnContinue()
    {
        AudioManager.Instance.Play(AudioClipId.GameStart);
        int next = 0;
        if(SceneManager.GetActiveScene().buildIndex == 0 && SceneManager.sceneCountInBuildSettings > 1)
            next = 1;
        SceneManager.LoadScene(next);
    }
}
