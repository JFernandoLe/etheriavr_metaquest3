using UnityEngine;

/// <summary>
/// Gestor global del estado de conexión MIDI
/// Singleton persistente entre escenas
/// </summary>
public class MIDIConnectionManager : MonoBehaviour
{
    public static MIDIConnectionManager Instance { get; private set; }
    
    [Header("Estado de Conexión MIDI")]
    public bool IsMidiConnected { get; private set; } = false;
    
    // Evento para notificar cambios de estado a otros componentes
    public delegate void MidiConnectionChanged(bool isConnected);
    public event MidiConnectionChanged OnMidiConnectionChanged;
    
    private MIDIStatusReceiver statusReceiver;
    
    void Awake() 
    {
        // Patrón Singleton con persistencia entre escenas
        if (Instance == null) 
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            Debug.Log("<color=magenta>[MIDI Manager]</color> 🎯 ===== INICIALIZANDO MIDI CONNECTION MANAGER =====");
            Debug.Log($"<color=cyan>[MIDI Manager]</color> Singleton creado - IsMidiConnected inicial: {IsMidiConnected}");
        } 
        else 
        {
            Debug.Log("<color=yellow>[MIDI Manager]</color> ⚠️ Ya existe un MIDI Manager, destruyendo duplicado");
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        Debug.Log("<color=cyan>[MIDI Manager]</color> Buscando MIDIStatusReceiver...");
        
        // Crear el receptor de estado MIDI
        statusReceiver = gameObject.AddComponent<MIDIStatusReceiver>();
        
        if (statusReceiver != null)
        {
            statusReceiver.OnStatusReceived += HandleMidiStatusUpdate;
            Debug.Log("<color=green>[MIDI Manager]</color> ✅ MIDIStatusReceiver creado y suscrito");
        }
        else
        {
            Debug.LogError("<color=red>[MIDI Manager]</color> ❌ No se pudo crear MIDIStatusReceiver");
        }
        
        Debug.Log("<color=magenta>[MIDI Manager]</color> 🎯 ===== MIDI MANAGER LISTO =====\n");
    }
    
    /// <summary>
    /// Callback cuando se recibe un update del estado MIDI
    /// </summary>
    private void HandleMidiStatusUpdate(bool isConnected) 
    {
        if (IsMidiConnected != isConnected) 
        {
            IsMidiConnected = isConnected;
            
            // Notificar a todos los suscriptores
            OnMidiConnectionChanged?.Invoke(isConnected);
            
            // Log visual
            string status = isConnected ? "CONECTADO" : "DESCONECTADO";
            string color = isConnected ? "green" : "red";
            Debug.Log($"<color={color}>[MIDI Manager]</color> Estado MIDI: {status}");
        }
    }
    
    void OnDestroy()
    {
        if (statusReceiver != null)
        {
            statusReceiver.OnStatusReceived -= HandleMidiStatusUpdate;
        }
    }
}
