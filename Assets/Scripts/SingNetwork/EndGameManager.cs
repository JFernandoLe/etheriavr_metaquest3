using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndGameManager : MonoBehaviour
{
    public static bool gameEnded = false;

    public SongLoader songLoader;
    public ScoreManager scoreManager;

    public GameObject endGameUI;

    public TextMeshPro globalText;
    public TextMeshPro pitchText;
    public TextMeshPro rhythmText;

    private bool shown = false;

    void Start()
    {
        gameEnded = false;
        endGameUI.SetActive(false);
    }

    void Update()
    {
        if (shown) return;

        if (songLoader == null || songLoader.audioSource == null)
            return;

        AudioSource audio = songLoader.audioSource;

        if (audio.clip == null)
            return;

        if (!audio.isPlaying)
            return;

        float time = audio.time;
        float duration = audio.clip.length;

        if (!shown && audio.clip != null && audio.isPlaying && audio.time >= audio.clip.length - 0.1f)
        {
            ShowResults();
        }
    }

    void ShowResults()
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

        //posicionar frente al jugador
        Transform cam = Camera.main.transform;
        endGameUI.transform.position = cam.position + cam.forward * 4f;
        endGameUI.transform.LookAt(cam);
        endGameUI.transform.Rotate(0, 180, 0);

        endGameUI.SetActive(true);

        //SOLO PAUSA AUDIO
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