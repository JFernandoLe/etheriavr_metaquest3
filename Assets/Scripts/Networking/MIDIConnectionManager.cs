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
            
            Debug.Log("<color=cyan>[MIDI Manager]</color> Iniciado - Esperando heartbeat del servidor...");
        } 
        else 
        {
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        // Crear el receptor de estado MIDI
        statusReceiver = gameObject.AddComponent<MIDIStatusReceiver>();
        statusReceiver.OnStatusReceived += HandleMidiStatusUpdate;
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
