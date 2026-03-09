using UnityEngine;
using System.Collections.Generic;

public class ShowRepertorio : MonoBehaviour
{
    [Header("Configuración UI")]
    [SerializeField] private GameObject songBoxPrefab; // Tu molde café
    [SerializeField] private Transform songBoxContainer; // El 'Content' del ScrollView

    [Header("Servicios")]
    [SerializeField] private AuthService authService;
    
    // Lista para mantener referencia de todos los items creados
    private List<SongItem> songItems = new List<SongItem>();

    private void Start()
    {
        CargarDatos();
        
        // Suscribirse al evento de cambio de estado MIDI
        if (MIDIConnectionManager.Instance != null)
        {
            MIDIConnectionManager.Instance.OnMidiConnectionChanged += OnMidiStatusChanged;
            
            // Aplicar estado inicial si ya existe el manager
            OnMidiStatusChanged(MIDIConnectionManager.Instance.IsMidiConnected);
        }
    }

    private void CargarDatos()
    {
        // Limpiamos basura previa
        foreach (Transform child in songBoxContainer) Destroy(child.gameObject);
        songItems.Clear();

        StartCoroutine(authService.GetSongs(
            onSuccess: (json) => {
                SongListWrapper wrapper = JsonUtility.FromJson<SongListWrapper>(json);
                
                foreach (var song in wrapper.songs)
                {
                    // Creamos la cajita dinámicamente
                    GameObject newBox = Instantiate(songBoxPrefab, songBoxContainer);
                    
                    // Le pasamos los datos
                    SongItem item = newBox.GetComponent<SongItem>();
                    if (item != null) 
                    {
                        item.Setup(song);
                        songItems.Add(item);
                        
                        // Aplicar estado inicial del botón según MIDI
                        UpdateSongItemButton(item);
                    }
                }
            },
            onError: (err) => Debug.LogError("Error al cargar canciones: " + err)
        ));
    }
    
    /// <summary>
    /// Callback cuando cambia el estado de conexión MIDI
    /// </summary>
    private void OnMidiStatusChanged(bool isMidiConnected)
    {
        Debug.Log($"<color=yellow>[Repertorio]</color> MIDI {(isMidiConnected ? "conectado" : "desconectado")} - Actualizando botones...");
        
        // Actualizar todos los items de canción
        foreach (var item in songItems)
        {
            if (item != null)
            {
                UpdateSongItemButton(item);
            }
        }
    }
    
    /// <summary>
    /// Actualiza el estado del botón de un SongItem específico
    /// Solo deshabilita canciones de PIANO cuando no hay MIDI
    /// </summary>
    private void UpdateSongItemButton(SongItem item)
    {
        if (item == null) return;
        
        bool shouldEnable = true;
        
        // Si es modo PIANO y NO hay MIDI conectado, deshabilitar
        if (item.IsPianoMode)
        {
            if (MIDIConnectionManager.Instance != null)
            {
                shouldEnable = MIDIConnectionManager.Instance.IsMidiConnected;
            }
            else
            {
                // Si no existe el manager, asumir que no hay MIDI
                shouldEnable = false;
            }
        }
        // Si es modo CANTO u otro modo, siempre habilitado
        
        item.UpdateButtonState(shouldEnable);
    }
    
    private void OnDestroy()
    {
        // Desuscribirse del evento al destruir el componente
        if (MIDIConnectionManager.Instance != null)
        {
            MIDIConnectionManager.Instance.OnMidiConnectionChanged -= OnMidiStatusChanged;
        }
    }
}