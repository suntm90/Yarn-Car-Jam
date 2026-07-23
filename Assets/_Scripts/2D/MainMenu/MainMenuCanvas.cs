using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuCanvas : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    public void OnPlayBtn()
    {
        AudioManager.Instance.Play(AudioClipId.GameStart);
        SceneManager.LoadScene("SampleScene");
    }
}
