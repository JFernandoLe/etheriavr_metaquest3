using UnityEngine;
using System.Collections.Concurrent;
using System;
using UnityEngine.SceneManagement;

/// <summary>
/// Receptor de entrada MIDI. Delega en un servicio Android nativo (MidiInputBridge)
/// que gestiona los callbacks MIDI, y se sondea desde Update para no usar threads.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class DirectMidiReceiver : MonoBehaviour
{
    private const string UnregisteredMidiDeviceName = "NO REGISTRADO";

    [Header("Configuración")]
    [Tooltip("Sondeo de eventos MIDI (s). ~72 Hz alinea con Quest y reduce JNI.")]
    [SerializeField] private float checkInterval = 0.014f;
    [Tooltip("Sondeo de estado de conexión (s). Más lento: no hace falta 72 Hz.")]
    [SerializeField] private float connectionPollInterval = 0.5f;
    [SerializeField] private int maxEventsPerFrame = 64;
    [SerializeField] private bool autoReconnect = true;
    [SerializeField] private float autoReconnectInterval = 5f;
    [SerializeField] private bool verboseMidiLogging = false;

    /// <summary>Cola de respaldo (si nadie consume el callback inmediato).</summary>
    public ConcurrentQueue<byte[]> messageQueue = new ConcurrentQueue<byte[]>();

    /// <summary>
    /// Disparado en el mismo frame en que se drena el evento del puente Java.
    /// MidiAudioManager se suscribe aquí para sonar sin esperar otro Update.
    /// </summary>
    public event Action<byte, byte, byte> OnRawMidiEvent;

    private bool isMidiConnected = false;
    private string currentMidiDeviceName = UnregisteredMidiDeviceName;
    private float nextCheckTime = 0f;
    private float nextConnectionPollTime = 0f;
    private float nextReconnectAttemptTime = 0f;
    private bool manualDisconnectRequested = false;
    private bool validationActive = true;

    public delegate void ConnectionStatusChangedDelegate(bool isConnected);
    public event ConnectionStatusChangedDelegate OnConnectionStatusChanged;
    public event Action OnMidiNoteActivity;

    public bool IsMidiConnected => isMidiConnected;

    public string CurrentMidiDeviceName => string.IsNullOrWhiteSpace(currentMidiDeviceName)
        ? UnregisteredMidiDeviceName
        : currentMidiDeviceName;

#if UNITY_ANDROID
    private AndroidJavaObject midiService;
    private AndroidJavaClass bridgeClass;
#endif

    void Start()
    {
        validationActive = MidiInitializer.ShouldEnableMidiForScene(SceneManager.GetActiveScene().name);
        maxEventsPerFrame = Mathf.Max(maxEventsPerFrame, 64);

        nextCheckTime = 0f;
        nextReconnectAttemptTime = 0f;
        manualDisconnectRequested = false;

#if UNITY_ANDROID
        if (validationActive) InitializeJavaBridge();
#else
        Debug.LogError($"[MIDI] No compilado para UNITY_ANDROID. Platform: {Application.platform}");
#endif
    }

#if UNITY_ANDROID
    /// <summary>Obtiene el singleton del servicio Java de MIDI y lo inicializa.</summary>
    private void InitializeJavaBridge()
    {
        try
        {
            bridgeClass ??= new AndroidJavaClass("com.etheriavr.midi.MidiInputBridge");

            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext");

            midiService = bridgeClass.CallStatic<AndroidJavaObject>("getInstance", context);
            if (midiService == null)
            {
                Debug.LogError("[MIDI] getInstance() devolvió null, el servicio Java no está disponible");
                return;
            }

            midiService.Call("init");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MIDI] Excepción en InitializeJavaBridge: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
    }
#endif

    void Update()
    {
        if (!validationActive || Time.unscaledTime < nextCheckTime) return;

        nextCheckTime = Time.unscaledTime + checkInterval;

#if UNITY_ANDROID
        if (midiService == null || bridgeClass == null)
        {
            InitializeJavaBridge();
            if (midiService == null || bridgeClass == null) return;
        }

        try
        {
            if (Time.unscaledTime >= nextConnectionPollTime)
            {
                nextConnectionPollTime = Time.unscaledTime + Mathf.Max(0.1f, connectionPollInterval);
                PollConnectionState();
            }

            DrainPendingEvents();
        }
        catch (Exception e)
        {
            Debug.LogError($"[MIDI] Excepción en Update: {e.GetType().Name}: {e.Message}");
        }
#endif
    }

#if UNITY_ANDROID
    private void PollConnectionState()
    {
        bool javaConnected = bridgeClass.GetStatic<bool>("isConnected");

        if (javaConnected)
        {
            // Solo pregunta el nombre cuando cambia el estado o aún no lo tenemos.
            if (!isMidiConnected || currentMidiDeviceName == UnregisteredMidiDeviceName)
            {
                string javaDeviceName = bridgeClass.CallStatic<string>("getConnectedDeviceName");
                currentMidiDeviceName = string.IsNullOrWhiteSpace(javaDeviceName)
                    ? UnregisteredMidiDeviceName
                    : javaDeviceName;
            }

            if (!isMidiConnected) UpdateConnectionStatus(true);
            return;
        }

        if (isMidiConnected) UpdateConnectionStatus(false);
        currentMidiDeviceName = UnregisteredMidiDeviceName;

        if (autoReconnect && !manualDisconnectRequested && Time.unscaledTime >= nextReconnectAttemptTime)
            RequestReconnectInternal(false);
    }

    /// <summary>Vacía la cola del puente Java con un presupuesto máximo por frame.</summary>
    private void DrainPendingEvents()
    {
        int eventsDequeued = 0;
        int safeEventBudget = Mathf.Max(1, maxEventsPerFrame);
        bool hasImmediateListener = OnRawMidiEvent != null;

        for (int i = 0; i < safeEventBudget; i++)
        {
            // JNI entrega bytes con signo, hay que reinterpretarlos.
            sbyte[] eventData = bridgeClass.CallStatic<sbyte[]>("dequeueEvent");
            if (eventData == null) break;
            if (eventData.Length < 3) continue;

            byte status = (byte)eventData[0];
            byte data1 = (byte)eventData[1];
            byte data2 = (byte)eventData[2];

            // Audio/scoring inmediato: evita un frame extra por la ConcurrentQueue.
            if (hasImmediateListener)
            {
                OnRawMidiEvent.Invoke(status, data1, data2);
            }
            else
            {
                messageQueue.Enqueue(new byte[] { status, data1, data2 });
            }

            eventsDequeued++;
            RaiseMidiNoteActivity(status, data2);

            if (verboseMidiLogging)
                Debug.Log($"[MIDI] RX: 0x{status:X2} data1={data1} data2={data2}");
        }

        if (verboseMidiLogging && eventsDequeued > 0)
            Debug.Log($"[MIDI] Dequeued {eventsDequeued} event(s)");
    }
#endif

    private void UpdateConnectionStatus(bool connected)
    {
        if (isMidiConnected == connected) return;

        isMidiConnected = connected;

        if (connected)
        {
            manualDisconnectRequested = false;
        }
        else
        {
            currentMidiDeviceName = UnregisteredMidiDeviceName;
            ClearQueuedMessages();
        }

        Debug.Log($"[MIDI] {(connected ? "CONECTADO" : "DESCONECTADO")}");
        OnConnectionStatusChanged?.Invoke(connected);
    }

    public void RequestReconnect()
    {
        if (validationActive) RequestReconnectInternal(true);
    }

    public void DisconnectCurrentDevice()
    {
#if UNITY_ANDROID
        manualDisconnectRequested = true;
        nextReconnectAttemptTime = 0f;

        try
        {
            midiService?.Call("disconnectCurrentDevice");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MIDI] No se pudo desconectar el dispositivo actual: {e.Message}");
        }
#endif

        UpdateConnectionStatus(false);
    }

    /// <summary>
    /// Activa o suspende el monitoreo. Al desactivar no se fuerza la desconexión a propósito:
    /// evita emitir eventos que molestarían a otros modos como el de canto.
    /// </summary>
    public void SetValidationActive(bool active)
    {
        if (validationActive == active)
        {
            if (validationActive) nextCheckTime = 0f;
            return;
        }

        validationActive = active;
        manualDisconnectRequested = false;
        nextReconnectAttemptTime = 0f;

        if (!active)
        {
            ClearQueuedMessages();
            return;
        }

        nextCheckTime = 0f;
        RequestReconnectInternal(false);
    }

    public bool TryGetConnectedDeviceName(out string deviceName)
    {
        bool hasRealDevice = isMidiConnected
                             && !string.IsNullOrWhiteSpace(currentMidiDeviceName)
                             && !string.Equals(currentMidiDeviceName, UnregisteredMidiDeviceName, StringComparison.OrdinalIgnoreCase);

        deviceName = hasRealDevice ? currentMidiDeviceName : null;
        return hasRealDevice;
    }

    public string GetRegistrationDeviceName() =>
        TryGetConnectedDeviceName(out string deviceName) ? deviceName : UnregisteredMidiDeviceName;

    private void RaiseMidiNoteActivity(byte status, byte velocity)
    {
        if ((status & 0xF0) == 0x90 && velocity > 0) OnMidiNoteActivity?.Invoke();
    }

    private void RequestReconnectInternal(bool userInitiated)
    {
#if UNITY_ANDROID
        manualDisconnectRequested = false;
        nextReconnectAttemptTime = Time.unscaledTime + Mathf.Max(0.25f, autoReconnectInterval);

        try
        {
            if (midiService == null || bridgeClass == null) InitializeJavaBridge();
            if (midiService == null) return;

            midiService.Call("rescanDevices");
            if (userInitiated) Debug.Log("[MIDI] Reescaneando dispositivos MIDI...");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MIDI] No se pudo reescanear MIDI: {e.Message}");
        }
#endif
    }

    private void ClearQueuedMessages()
    {
        while (messageQueue.TryDequeue(out _)) { }
    }

    void OnDestroy()
    {
#if UNITY_ANDROID
        try
        {
            midiService?.Call("close");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MIDI] Error cerrando el servicio: {e.Message}");
        }

        bridgeClass = null;
#endif
    }
}
