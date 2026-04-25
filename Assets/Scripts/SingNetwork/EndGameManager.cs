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

    [Header("UI Puntajes Centrales")]
    public TextMeshPro globalText;
    public TextMeshPro pitchText;
    public TextMeshPro rhythmText;

    [Header("UI Detalles Inferiores (NUEVO)")]
    public TextMeshPro cancionDetalleTxt;
    public TextMeshPro modoDetalleTxt;
    public TextMeshPro fechaDetalleTxt;
    public TextMeshPro horaDetalleTxt;
    public TextMeshPro duracionDetalleTxt;

    private bool shown = false;
    private AuthService authService;

    void Start()
    {
        gameEnded = false;
        endGameUISing.SetActive(false);
        authService = FindObjectOfType<AuthService>();
    }

    void Update()
    {
        if (shown) return;
        if (songLoader == null || songLoader.audioSource == null) return;

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

        // 1. C�lculos de puntaje
        float pitch = scoreManager.accuracyPercent;
        float rhythm = scoreManager.rhythmPercent;
        float global = (pitch + rhythm) / 2f;

        // 2. Asignar puntajes centrales
        pitchText.text = pitch.ToString("F0") + "%";
        rhythmText.text = rhythm.ToString("F0") + "%";
        globalText.text = global.ToString("F0") + "%";

        // 3. LLENAR DATOS INFERIORES (NUEVO)
        LlenarDatosInferiores();

        // 4. Guardado y UI
        EnviarSesionAlBackend(pitch, rhythm);
        ConfigurarUIAlFinal();

        endGameUISing.SetActive(true);
        songLoader.audioSource.Pause();
    }

    private void LlenarDatosInferiores()
    {
        // Obtener nombre de la canci�n
        if (SelectedSongManager.Instance != null && SelectedSongManager.Instance.selectedSong != null)
        {
            if (cancionDetalleTxt != null)
                cancionDetalleTxt.text = "Cancion: " + SelectedSongManager.Instance.selectedSong.title;
        }

        // Modo (Fijo en CANTO para esta escena)
        if (modoDetalleTxt != null) modoDetalleTxt.text = "Modo: Canto";

        // Fecha y Hora actuales
        DateTime ahora = DateTime.Now;
        if (fechaDetalleTxt != null) fechaDetalleTxt.text = "Fecha: " + ahora.ToString("yyyy-MM-dd");
        if (horaDetalleTxt != null) horaDetalleTxt.text = "Hora: " + ahora.ToString("HH:mm");

        // Duraci�n (calculada del AudioSource)
        if (duracionDetalleTxt != null && songLoader.audioSource.clip != null)
        {
            float totalSeconds = songLoader.audioSource.clip.length;
            int minutes = Mathf.FloorToInt(totalSeconds / 60);
            int seconds = Mathf.FloorToInt(totalSeconds % 60);
            duracionDetalleTxt.text = string.Format("Duracion: {0:0}:{1:00}", minutes, seconds);
        }
    }

    private void EnviarSesionAlBackend(float tuning, float rhythm)
    {
        if (UserSession.Instance == null || !UserSession.Instance.IsLoggedIn) return;
        if (SelectedSongManager.Instance == null || SelectedSongManager.Instance.selectedSong == null) return;

        PracticeSessionRequest request = new PracticeSessionRequest
        {
            user_id = UserSession.Instance.userId,
            song_id = SelectedSongManager.Instance.selectedSong.id,
            practice_datetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            practice_mode = "CANTO",
            tuning_score = tuning,
            rhythm_score = rhythm,
            harmony_score = 0
        };

        Debug.Log($"[SessionAudit] Finalizando sesion canto | song={SelectedSongManager.Instance.selectedSong.title} | songId={SelectedSongManager.Instance.selectedSong.id} | mode={request.practice_mode}");

        if (authService != null)
        {
            StartCoroutine(authService.SavePracticeSession(request,
                onSuccess: (res) => Debug.Log($"[SessionAudit] Sesion canto registrada | song={SelectedSongManager.Instance.selectedSong.title} | songId={SelectedSongManager.Instance.selectedSong.id}"),
                onError: (err) => Debug.LogError("[SessionAudit] Error registrando sesion canto | detalle=" + err)
            ));
        }
    }

    private void ConfigurarUIAlFinal()
    {
        Transform cam = Camera.main.transform;
        endGameUISing.transform.position = cam.position + cam.forward * 2.5f;
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