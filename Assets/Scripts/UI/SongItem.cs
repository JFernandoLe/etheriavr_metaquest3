using UnityEngine;
using System;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
    
    // Propiedad pública para acceder al modo de la canción
    public string SongMode => _myFullData?.mode;
    
    // Propiedad pública para verificar si es modo PIANO
    public bool IsPianoMode => string.Equals(_myFullData?.mode, "PIANO", StringComparison.OrdinalIgnoreCase);

    public void Setup(SongListarResponse data)
    {
        _myFullData = data;

        // 1. Título - Artista
        if (txtTituloArtista != null)
            txtTituloArtista.text = $"{data.title} - {data.artist_name}";

        // 2. Tonalidad y Tempo
        if (txtTonalidadTempo != null)
            txtTonalidadTempo.text = $"Tonalidad: {data.musical_key} | Tempo: {data.tempo} BPM \n";

        // 3. Detalles: Género, Duración y Modo
        if (txtDetalles != null)
        {
            // La fórmula es: $minutos = \frac{segundos}{60}$
            float duracionMinutos = (data.duration / 60f)%60; 
            
            txtDetalles.text = $"Modo: {data.mode}";
        }
        VerificarCompatibilidadVocal(data.musical_key, data.mode);
        // Configuración del botón específico
        if (btnJugar != null)
        {
            btnJugar.onClick.RemoveAllListeners();
            btnJugar.onClick.AddListener(CargarCancionEnJuego);
        }
        else
        {
            Debug.LogWarning($"<color=yellow>[Aviso]</color> No se asignó el botón btnJugar en el prefab de {data.title}");
        }
    }

    private void CargarCancionEnJuego()
    {
        // Guardamos TODO el objeto en el Manager persistente
        if (SelectedSongManager.Instance != null)
        {
            SelectedSongManager.Instance.selectedSong = _myFullData;
            if (string.Equals(_myFullData.mode, "CANTO", StringComparison.OrdinalIgnoreCase))
            {
                SelectedSongManager.Instance.BeginSongSelectionMeasurement(_myFullData, "SingGame");
                SceneManager.LoadScene("SingGame");
            }else if (string.Equals(_myFullData.mode, "PIANO", StringComparison.OrdinalIgnoreCase))
            {
                SelectedSongManager.Instance.BeginSongSelectionMeasurement(_myFullData, "PianoGame");
                SceneManager.LoadScene("PianoGame");
            }
        }
        else
        {
            Debug.LogError("No se encontró el SelectedSongManager en la escena.");
        }
    }
    
    /// <summary>
    /// Actualiza el estado del botón (habilitado/deshabilitado)
    /// Se usa para deshabilitar canciones de PIANO cuando no hay MIDI conectado
    /// </summary>
    public void UpdateButtonState(bool isEnabled)
    {
        if (btnJugar != null)
        {
            btnJugar.interactable = isEnabled;
            
            // Opcional: Cambiar la opacidad visual para indicar que está deshabilitado
            var colors = btnJugar.colors;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Gris semi-transparente
            btnJugar.colors = colors;
        }
    }

    private void VerificarCompatibilidadVocal(string tonalidad, string modo)
    {
        if (txtAdvertencia == null) return;

        txtAdvertencia.text = "";
        txtAdvertencia.gameObject.SetActive(false);

        if (modo != "CANTO" || UserSession.Instance == null || string.IsNullOrEmpty(UserSession.Instance.tessitura))
            return;

        string t = UserSession.Instance.tessitura.ToUpper();
        tonalidad = tonalidad.ToUpper();

        // Definimos grupos lógicos de tonalidades
        // Agudas: G, G#, A, A#, B (Suelen requerir notas de cabeza o falsete)
        bool esAguda = tonalidad.Contains("G") || tonalidad.Contains("A") || tonalidad.Contains("B");

        // Graves: C, C#, D, D#, E (Suelen quedar en el registro bajo/pecho)
        bool esGrave = tonalidad.Contains("C") || tonalidad.Contains("D") || tonalidad.Contains("E");

        string mensaje = "";

        // --- LÓGICA PARA VOCES MASCULINAS ---
        if (t == "BASS" && esAguda)
            mensaje = "<color=orange>Advertencia: Muy alta para tu registro de Bajo.</color>";

        else if (t == "BARITONE" && (tonalidad.Contains("A") || tonalidad.Contains("B")))
            mensaje = "<color=orange>Advertencia: Esta cancion suele ser alta para un Baritono.</color>";

        else if (t == "TENOR" && esGrave)
            mensaje = "<color=yellow>Advertencia: Puede quedarte algo grave para tu voz de Tenor.</color>";

        // --- LÓGICA PARA VOCES FEMENINAS ---
        else if (t == "CONTRALTO" && esAguda)
            mensaje = "<color=orange>Advertencia: Tonalidad alta para una voz Contralto.</color>";

        else if (t == "MEZZO_SOPRANO" && tonalidad.Contains("B"))
            mensaje = "<color=orange>Advertencia: El tono Si (B) puede ser muy exigente para Mezzos.</color>";

        else if (t == "SOPRANO" && esGrave)
            mensaje = "<color=yellow>Advertencia: Tonalidad baja para tu registro de Soprano.</color>";

        // Mostrar si hay mensaje
        if (!string.IsNullOrEmpty(mensaje))
        {
            txtAdvertencia.text = mensaje;
            txtAdvertencia.gameObject.SetActive(true);
        }
    }
}