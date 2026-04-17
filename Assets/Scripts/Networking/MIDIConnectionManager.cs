using System;
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
    public string CurrentDeviceName { get; private set; } = UserSession.UnregisteredMidiDeviceName;
    
    // Evento para notificar cambios de estado a otros componentes
    public delegate void MidiConnectionChanged(bool isConnected);
    public event MidiConnectionChanged OnMidiConnectionChanged;
    
    private MIDIStatusReceiver statusReceiver;
    private AuthService authService;
    
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
        EnsureStatusReceiver();
        RefreshStateFromReceiver();
        
        Debug.Log("<color=magenta>[MIDI Manager]</color> 🎯 ===== MIDI MANAGER LISTO =====\n");
    }

    void Update()
    {
        if (statusReceiver == null || statusReceiver.CurrentReceiver == null)
        {
            EnsureStatusReceiver();
        }

        RefreshStateFromReceiver();
    }

    public DirectMidiReceiver GetReceiver()
    {
        EnsureStatusReceiver();
        return statusReceiver != null ? statusReceiver.CurrentReceiver : FindObjectOfType<DirectMidiReceiver>();
    }

    public void RequestReconnect()
    {
        DirectMidiReceiver receiver = GetReceiver();
        if (receiver != null)
        {
            receiver.RequestReconnect();
        }
    }

    public void DisconnectCurrentDevice()
    {
        DirectMidiReceiver receiver = GetReceiver();
        if (receiver != null)
        {
            receiver.DisconnectCurrentDevice();
        }
    }
    
    /// <summary>
    /// Callback cuando se recibe un update del estado MIDI
    /// </summary>
    private void HandleMidiStatusUpdate(bool isConnected) 
    {
        string resolvedDeviceName = ResolveCurrentDeviceName();
        bool statusChanged = IsMidiConnected != isConnected;
        bool deviceNameChanged = !string.Equals(CurrentDeviceName, resolvedDeviceName, StringComparison.Ordinal);

        IsMidiConnected = isConnected;
        CurrentDeviceName = resolvedDeviceName;

        if (statusChanged) 
        {
            // Notificar a todos los suscriptores
            OnMidiConnectionChanged?.Invoke(isConnected);
            
            // Log visual
            string status = isConnected ? "CONECTADO" : "DESCONECTADO";
            string color = isConnected ? "green" : "red";
            Debug.Log($"<color={color}>[MIDI Manager]</color> Estado MIDI: {status}");
        }

        if ((statusChanged || deviceNameChanged) && isConnected)
        {
            UpdateRuntimeSessionForConnectedDevice(resolvedDeviceName);
        }
    }

    private void EnsureStatusReceiver()
    {
        if (statusReceiver == null)
        {
            statusReceiver = GetComponent<MIDIStatusReceiver>();
        }

        if (statusReceiver == null)
        {
            statusReceiver = gameObject.AddComponent<MIDIStatusReceiver>();
        }

        if (statusReceiver != null)
        {
            statusReceiver.OnStatusReceived -= HandleMidiStatusUpdate;
            statusReceiver.OnStatusReceived += HandleMidiStatusUpdate;
        }
    }

    private void RefreshStateFromReceiver()
    {
        DirectMidiReceiver receiver = GetReceiver();
        if (receiver == null)
        {
            return;
        }

        CurrentDeviceName = receiver.CurrentMidiDeviceName;
        HandleMidiStatusUpdate(receiver.IsMidiConnected);
    }

    private string ResolveCurrentDeviceName()
    {
        DirectMidiReceiver receiver = statusReceiver != null ? statusReceiver.CurrentReceiver : null;
        if (receiver == null)
        {
            receiver = FindObjectOfType<DirectMidiReceiver>();
        }

        return receiver != null ? receiver.CurrentMidiDeviceName : UserSession.UnregisteredMidiDeviceName;
    }

    private void UpdateRuntimeSessionForConnectedDevice(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName) ||
            string.Equals(deviceName, UserSession.UnregisteredMidiDeviceName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string previousDeviceName = UserSession.Instance != null
            ? UserSession.Instance.midiDeviceName
            : null;

        if (UserSession.Instance != null)
        {
            UserSession.Instance.UpdateMidiDeviceName(deviceName);
        }

        if (string.Equals(previousDeviceName, deviceName, StringComparison.Ordinal) ||
            UserSession.Instance == null ||
            !UserSession.Instance.IsLoggedIn ||
            UserSession.Instance.userId <= 0 ||
            string.IsNullOrEmpty(UserSession.Instance.token))
        {
            return;
        }

        EnsureAuthService();
        if (authService == null)
        {
            return;
        }

        UserConfigurationRequest request = new UserConfigurationRequest
        {
            midi_device_name = deviceName,
            audience_intensity = string.IsNullOrWhiteSpace(UserSession.Instance.audienceIntensity)
                ? UserSession.DefaultAudienceIntensity
                : UserSession.Instance.audienceIntensity
        };

        StartCoroutine(authService.UpdateUserConfiguration(
            UserSession.Instance.userId,
            request,
            onSuccess: (_) => Debug.Log("<color=cyan>[MIDI Manager]</color> Configuración MIDI sincronizada con API"),
            onError: (error) => Debug.LogWarning($"[MIDI Manager] No se pudo sincronizar el dispositivo MIDI conectado: {error}")
        ));
    }

    private void EnsureAuthService()
    {
        if (authService == null)
        {
            authService = FindObjectOfType<AuthService>(true);
        }

        if (authService == null)
        {
            authService = gameObject.AddComponent<AuthService>();
        }
    }
    
    void OnDestroy()
    {
        if (statusReceiver != null)
        {
            statusReceiver.OnStatusReceived -= HandleMidiStatusUpdate;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
