using UnityEngine;
using System;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Tarjeta de una canción del repertorio: muestra sus datos, avisa si la tonalidad
/// no encaja con la tesitura del usuario y lanza la escena de práctica correspondiente.
/// </summary>
public class SongItem : MonoBehaviour
{
    [Header("UI de Información")]
    public TMP_Text txtTituloArtista;
    public TMP_Text txtTonalidadTempo;
    public TMP_Text txtDetalles;
    public TMP_Text txtAdvertencia;

    [Header("Botón de Acción")]
    [SerializeField] private Button btnJugar;

    private SongListarResponse _myFullData;

    public string SongMode => _myFullData?.mode;

    public bool IsPianoMode => string.Equals(_myFullData?.mode, "PIANO", StringComparison.OrdinalIgnoreCase);

    public void Setup(SongListarResponse data)
    {
        _myFullData = data;

        if (txtTituloArtista != null) txtTituloArtista.text = $"{data.title} - {data.artist_name}";
        if (txtTonalidadTempo != null) txtTonalidadTempo.text = $"Tonalidad: {data.musical_key} | Tempo: {data.tempo} BPM \n";
        if (txtDetalles != null) txtDetalles.text = $"Duracion: {FormatDuration(data.duration)} | Modo: {data.mode}";

        VerificarCompatibilidadVocal(data.musical_key, data.mode);

        if (btnJugar == null)
        {
            Debug.LogWarning($"<color=yellow>[Aviso]</color> No se asignó el botón btnJugar en el prefab de {data.title}");
            return;
        }

        btnJugar.onClick.RemoveAllListeners();
        btnJugar.onClick.AddListener(CargarCancionEnJuego);
    }

    private static string FormatDuration(float durationSeconds)
    {
        TimeSpan duration = TimeSpan.FromSeconds(Mathf.Max(0f, durationSeconds));

        return duration.TotalHours >= 1d ? duration.ToString(@"h\:mm\:ss") : duration.ToString(@"m\:ss");
    }

    private void CargarCancionEnJuego()
    {
        if (SelectedSongManager.Instance == null)
        {
            Debug.LogError("No se encontró el SelectedSongManager en la escena.");
            return;
        }

        // El manager es persistente: la escena de juego lee de ahí la canción elegida.
        SelectedSongManager.Instance.selectedSong = _myFullData;

        string targetScene = string.Equals(_myFullData.mode, "CANTO", StringComparison.OrdinalIgnoreCase) ? "SingGame"
            : string.Equals(_myFullData.mode, "PIANO", StringComparison.OrdinalIgnoreCase) ? "PianoGame"
            : null;

        if (targetScene == null) return;

        SelectedSongManager.Instance.BeginSongSelectionMeasurement(_myFullData, targetScene);

        // Fade solo en Repertorio → PianoGame. Canto y el resto cargan normal.
        if (IsPianoMode) SceneTransition.Load(targetScene, "Preparando práctica de piano...");
        else SceneManager.LoadScene(targetScene);
    }

    /// <summary>
    /// Habilita o deshabilita el botón de jugar (por ejemplo, canciones de piano sin MIDI).
    /// </summary>
    public void UpdateButtonState(bool isEnabled)
    {
        if (btnJugar == null) return;

        btnJugar.interactable = isEnabled;

        ColorBlock colors = btnJugar.colors;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        btnJugar.colors = colors;
    }

    /// <summary>
    /// Avisa cuando la tonalidad suele quedar fuera del registro cómodo de la tesitura
    /// registrada. Es orientativo: se agrupan las tonalidades en agudas y graves.
    /// </summary>
    private void VerificarCompatibilidadVocal(string tonalidad, string modo)
    {
        if (txtAdvertencia == null) return;

        txtAdvertencia.text = "";
        txtAdvertencia.gameObject.SetActive(false);

        if (modo != "CANTO" || UserSession.Instance == null || string.IsNullOrEmpty(UserSession.Instance.tessitura)) return;

        string tessitura = UserSession.Instance.tessitura.ToUpper();
        tonalidad = tonalidad.ToUpper();

        // Agudas (G, A, B) suelen pedir notas de cabeza; graves (C, D, E) caen en el registro de pecho.
        bool esAguda = tonalidad.Contains("G") || tonalidad.Contains("A") || tonalidad.Contains("B");
        bool esGrave = tonalidad.Contains("C") || tonalidad.Contains("D") || tonalidad.Contains("E");

        string mensaje = tessitura switch
        {
            "BASS" when esAguda => "<color=orange>Advertencia: Muy alta para tu registro de Bajo.</color>",
            "BARITONE" when tonalidad.Contains("A") || tonalidad.Contains("B") => "<color=orange>Advertencia: Esta cancion suele ser alta para un Baritono.</color>",
            "TENOR" when esGrave => "<color=yellow>Advertencia: Puede quedarte algo grave para tu voz de Tenor.</color>",
            "CONTRALTO" when esAguda => "<color=orange>Advertencia: Tonalidad alta para una voz Contralto.</color>",
            "MEZZO_SOPRANO" when tonalidad.Contains("B") => "<color=orange>Advertencia: El tono Si (B) puede ser muy exigente para Mezzos.</color>",
            "SOPRANO" when esGrave => "<color=yellow>Advertencia: Tonalidad baja para tu registro de Soprano.</color>",
            _ => null
        };

        if (string.IsNullOrEmpty(mensaje)) return;

        txtAdvertencia.text = mensaje;
        txtAdvertencia.gameObject.SetActive(true);
    }
}
