using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndGameManager : MonoBehaviour
{
    public static bool gameEnded = false;

    public SongLoader songLoader;
    public ScoreManager scoreManager;

    public GameObject endGameUISing;

    public TextMeshPro globalText;
    public TextMeshPro pitchText;
    public TextMeshPro rhythmText;

    private bool shown = false;

    void Start()
    {
        gameEnded = false;
        endGameUISing.SetActive(false);
    }

    void Update()
    {
        if (shown) return;

        if (songLoader == null || songLoader.audioSource == null)
            return;

        float time = songLoader.audioSource.time;
        float duration = songLoader.audioSource.clip.length;

        if (time >= duration - 0.1f)
        {
            ShowResultsSing();
        }
    }

    void ShowResultsSing()
    {
        if (shown) return;

        shown = true;
        gameEnded = true;

        float pitch = scoreManager.accuracyPercent;
        float rhythm = scoreManager.rhythmPercent;
        float global = (pitch + rhythm) / 2f;

        pitchText.text = pitch.ToString("F0") + "%";
        rhythmText.text = rhythm.ToString("F0") + "%";
        globalText.text = global.ToString("F0") + "%";

        Transform cam = Camera.main.transform;
        endGameUISing.transform.position = new Vector3(0, 1, -10);    //cam.position + cam.forward * 4f;
        endGameUISing.transform.LookAt(cam);
        endGameUISing.transform.Rotate(0, 180, 0);

        endGameUISing.SetActive(true);
        songLoader.audioSource.Pause();
    }

    public void ReiniciarCancion()
    {
        gameEnded = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void IrMenuPrincipal()
    {
        gameEnded = false;
        SceneManager.LoadScene("HomeScene");
    }
}