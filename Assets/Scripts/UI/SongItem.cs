using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SongItem : MonoBehaviour
{
    [Header("UI de Información")]
    public TMP_Text txtTituloArtista;
    public TMP_Text txtTonalidadTempo;
    public TMP_Text txtDetalles;

    [Header("Botón de Acción")]
    [SerializeField] private Button btnJugar;

    private SongListarResponse _myFullData;
    
    // Propiedad pública para acceder al modo de la canción
    public string SongMode => _myFullData?.mode;
    
    // Propiedad pública para verificar si es modo PIANO
    public bool IsPianoMode => _myFullData?.mode == "PIANO";

    public void Setup(SongListarResponse data)
    {
        _myFullData = data;

        // 1. Título - Artista
        if (txtTituloArtista != null)
            txtTituloArtista.text = $"{data.title} - {data.artist_name}";

        // 2. Tonalidad y Tempo
        if (txtTonalidadTempo != null)
            txtTonalidadTempo.text = $"Tonalidad: {data.musical_key} | Tempo: {data.tempo} BPM";

        // 3. Detalles: Género, Duración y Modo
        if (txtDetalles != null)
        {
            // La fórmula es: $minutos = \frac{segundos}{60}$
            float duracionMinutos = data.duration / 60f; 
            
            txtDetalles.text = $"Género: {data.musical_genre} | " +
                               $"Duración: {duracionMinutos:F1} min | " +
                               $"Modo: {data.mode}";
        }

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
            Debug.Log(_myFullData.mode);
            if (_myFullData.mode == "CANTO")
            {
                Debug.Log($"<color=cyan>[Escena]</color> Cambiando a SingGame con: {_myFullData.title}");
                Debug.Log("Seleccionaste: " + _myFullData.file_path);
                SceneManager.LoadScene("SingGame");
            }else if (_myFullData.mode == "PIANO")
            {
                Debug.Log($"<color=cyan>[Escena]</color> Cambiando a PianoGame con: {_myFullData.title}");
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
}