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
    private bool lastKnownMidiState = false;

    private void Start()
    {
        Debug.Log("<color=magenta>[Repertorio]</color> 📋 ===== INICIANDO REPERTORIO =====");
        
        CargarDatos();
        
        // Suscribirse al evento de cambio de estado MIDI
        if (MIDIConnectionManager.Instance != null)
        {
            Debug.Log("<color=green>[Repertorio]</color> ✅ MIDIConnectionManager encontrado");
            MIDIConnectionManager.Instance.OnMidiConnectionChanged += OnMidiStatusChanged;
            
            // Aplicar estado inicial si ya existe el manager
            bool currentState = MIDIConnectionManager.Instance.IsMidiConnected;
            Debug.Log($"<color=cyan>[Repertorio]</color> 📡 Estado MIDI inicial: {(currentState ? "CONECTADO ✅" : "DESCONECTADO ❌")}");
            OnMidiStatusChanged(currentState);
        }
        else
        {
            Debug.LogError("<color=red>[Repertorio]</color> ❌ MIDIConnectionManager NO ENCONTRADO!");
        }
        
        Debug.Log("<color=magenta>[Repertorio]</color> 📋 ===== REPERTORIO LISTO =====\n");
    }

    private void Update()
    {
        // Validar constantemente el estado de MIDI en tiempo real
        if (MIDIConnectionManager.Instance != null)
        {
            bool currentMidiState = MIDIConnectionManager.Instance.IsMidiConnected;
            
            // Si cambió el estado, actualizar botones
            if (currentMidiState != lastKnownMidiState)
            {
                lastKnownMidiState = currentMidiState;
                OnMidiStatusChanged(currentMidiState);
            }
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
        if (item == null)
        {
            Debug.LogWarning("<color=yellow>[Repertorio]</color> ⚠️ SongItem es NULL");
            return;
        }
        
        bool shouldEnable = true;
        string songTitle = item.gameObject.name;
        
        // Si es modo PIANO y NO hay MIDI conectado, deshabilitar
        if (item.IsPianoMode)
        {
            Debug.Log($"<color=cyan>[Repertorio]</color> 🎹 Canción PIANO detectada: {songTitle}");
            
            if (MIDIConnectionManager.Instance != null)
            {
                bool isMidiConnected = MIDIConnectionManager.Instance.IsMidiConnected;
                shouldEnable = isMidiConnected;
                
                string status = isMidiConnected ? "CONECTADO ✅ → HABILITADO" : "DESCONECTADO ❌ → DESHABILITADO";
                Debug.Log($"<color=cyan>[Repertorio]</color> {status} | {songTitle}");
            }
            else
            {
                Debug.LogError("<color=red>[Repertorio]</color> ❌ MIDIConnectionManager no encontrado, deshabilitando PIANO");
                shouldEnable = false;
            }
        }
        else
        {
            Debug.Log($"<color=blue>[Repertorio]</color> 🎤 Canción CANTO/OTRO: {songTitle} → SIEMPRE HABILITADO");
        }
        
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