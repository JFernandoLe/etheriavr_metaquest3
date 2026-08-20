using System;
using UnityEngine;

public class MIDIConnectionManager : MonoBehaviour
{
    public static MIDIConnectionManager Instance { get; private set; }
    
    [Header("Estado de Conexión MIDI")]
    public bool IsMidiConnected { get; private set; }
    public string CurrentDeviceName { get; private set; } = UserSession.UnregisteredMidiDeviceName;
    
    public delegate void MidiConnectionChanged(bool isConnected);
    public event MidiConnectionChanged OnMidiConnectionChanged;
    
    private MIDIStatusReceiver statusReceiver;
    private AuthService authService;
    
    private void Awake() 
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    private void Start()
    {
        EnsureStatusReceiver();
        RefreshStateFromReceiver();
    }

    private void Update()
    {
        if (!statusReceiver || !statusReceiver.CurrentReceiver) EnsureStatusReceiver();
        RefreshStateFromReceiver();
    }

    public DirectMidiReceiver GetReceiver()
    {
        EnsureStatusReceiver();
        return statusReceiver?.CurrentReceiver ?? FindObjectOfType<DirectMidiReceiver>();
    }

    public void RequestReconnect() => GetReceiver()?.RequestReconnect();
    public void DisconnectCurrentDevice() => GetReceiver()?.DisconnectCurrentDevice();
    
    private void HandleMidiStatusUpdate(bool isConnected) 
    {
        string resolvedDeviceName = GetReceiver()?.CurrentMidiDeviceName ?? UserSession.UnregisteredMidiDeviceName;
        bool statusChanged = IsMidiConnected != isConnected;
        bool deviceNameChanged = CurrentDeviceName != resolvedDeviceName;

        IsMidiConnected = isConnected;
        CurrentDeviceName = resolvedDeviceName;

        if (statusChanged) 
        {
            OnMidiConnectionChanged?.Invoke(isConnected);
            Debug.Log($"<color={(isConnected ? "green" : "red")}>[MIDI Manager]</color> Estado MIDI: {(isConnected ? "CONECTADO" : "DESCONECTADO")}");
        }

        if ((statusChanged || deviceNameChanged) && isConnected)
            UpdateRuntimeSessionForConnectedDevice(resolvedDeviceName);
    }

    private void EnsureStatusReceiver()
    {
        if (statusReceiver) return;
        if (!TryGetComponent(out statusReceiver)) statusReceiver = gameObject.AddComponent<MIDIStatusReceiver>();
        
        statusReceiver.OnStatusReceived -= HandleMidiStatusUpdate;
        statusReceiver.OnStatusReceived += HandleMidiStatusUpdate;
    }

    private void RefreshStateFromReceiver()
    {
        var receiver = GetReceiver();
        if (!receiver) return;

        CurrentDeviceName = receiver.CurrentMidiDeviceName;
        HandleMidiStatusUpdate(receiver.IsMidiConnected);
    }

    private void UpdateRuntimeSessionForConnectedDevice(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName) || deviceName.Equals(UserSession.UnregisteredMidiDeviceName, StringComparison.OrdinalIgnoreCase)) return;

        var session = UserSession.Instance;
        if (!session) return;

        string prevDevice = session.midiDeviceName;
        session.UpdateMidiDeviceName(deviceName);

        if (prevDevice == deviceName || !session.IsLoggedIn || session.userId <= 0 || string.IsNullOrEmpty(session.token)) return;

        EnsureAuthService();
        if (!authService) return;

        var request = new UserConfigurationRequest
        {
            midi_device_name = deviceName,
            audience_intensity = string.IsNullOrWhiteSpace(session.audienceIntensity) ? UserSession.DefaultAudienceIntensity : session.audienceIntensity
        };

        StartCoroutine(authService.UpdateUserConfiguration(session.userId, request,
            _ => Debug.Log("<color=cyan>[MIDI Manager]</color> Configuración MIDI sincronizada con API"),
            err => Debug.LogWarning($"[MIDI Manager] No se pudo sincronizar el dispositivo MIDI: {err}")
        ));
    }

    private void EnsureAuthService() => 
        authService ??= FindObjectOfType<AuthService>(true) ?? gameObject.AddComponent<AuthService>();
    
    private void OnDestroy()
    {
        if (statusReceiver) statusReceiver.OnStatusReceived -= HandleMidiStatusUpdate;
        if (Instance == this) Instance = null;
    }
}