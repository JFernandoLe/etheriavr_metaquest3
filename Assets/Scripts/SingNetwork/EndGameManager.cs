using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class EndGameManager : MonoBehaviour
{
    public static bool gameEnded = false;

    [Header("Referencias de Escena")]
    public SongLoader songLoader;
    public ScoreManager scoreManager;
    public GameObject endGameUISing;

    [Header("UI Textos")]
    public TextMeshPro globalText;
    public TextMeshPro pitchText;
    public TextMeshPro rhythmText;

    private bool shown = false;
    private AuthService authService;

    void Start()
    {
        gameEnded = false;
        endGameUISing.SetActive(false);

        // Busca automáticamente el AuthService en la escena
        authService = FindObjectOfType<AuthService>();
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

        // --- INICIO DEL GUARDADO ---
        EnviarSesionAlBackend(pitch, rhythm);

        // Ajuste visual de la UI
        ConfigurarUIAlFinal();

        endGameUISing.SetActive(true);
        songLoader.audioSource.Pause();
    }

    private void EnviarSesionAlBackend(float tuning, float rhythm)
    {
        // 1. Verificación de Usuario (vía UserSession persistente)
        if (UserSession.Instance == null || !UserSession.Instance.IsLoggedIn)
        {
            Debug.LogWarning("[EndGame] No se guarda: Usuario no logueado.");
            return;
        }

        // 2. Verificación de Canción (vía SelectedSongManager persistente)
        if (SelectedSongManager.Instance == null || SelectedSongManager.Instance.selectedSong == null)
        {
            Debug.LogWarning("[EndGame] No se guarda: No hay canción seleccionada.");
            return;
        }

        // 3. Crear la petición con tus modelos exactos
        PracticeSessionRequest request = new PracticeSessionRequest
        {
            user_id = UserSession.Instance.userId,
            song_id = SelectedSongManager.Instance.selectedSong.id, // Verifica que sea .id
            practice_datetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            practice_mode = "CANTO",
            tuning_score = tuning,
            rhythm_score = rhythm,
            harmony_score = 0
        };

        // 4. Envío mediante el AuthService de la escena
        if (authService != null)
        {
            StartCoroutine(authService.SavePracticeSession(request,
                onSuccess: (res) => Debug.Log("<color=green>[Backend] Sesión guardada con éxito.</color>"),
                onError: (err) => Debug.LogError("[Backend] Error al guardar: " + err)
            ));
        }
        else
        {
            Debug.LogError("[EndGame] No se encontró AuthService en la escena.");
        }
    }

    private void ConfigurarUIAlFinal()
    {
        Transform cam = Camera.main.transform;
        endGameUISing.transform.position = new Vector3(0, 1, -10);
        endGameUISing.transform.LookAt(cam);
        endGameUISing.transform.Rotate(0, 180, 0);
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