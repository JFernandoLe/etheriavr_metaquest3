using UnityEngine;
using System.Collections.Concurrent;
using System;
using UnityEngine.SceneManagement;

/// <summary>
/// 🎹 MIDI Input Receiver - Ultra Simple (uses native Android service)
/// Reads from pre-compiled Java bridge that handles MIDI callbacks correctly
/// </summary>
[DefaultExecutionOrder(-1000)]
public class DirectMidiReceiver : MonoBehaviour
{
    private const string UnregisteredMidiDeviceName = "NO REGISTRADO";

    [Header("Configuración")]
    [SerializeField] private float checkInterval = 0.001f;  // 1ms polling = ultra responsivo
    [SerializeField] private int maxEventsPerFrame = 32;
    [SerializeField] private bool autoReconnect = true;
    [SerializeField] private float autoReconnectInterval = 1.5f;
    [SerializeField] private bool verboseMidiLogging = false;
    
    // Queue compatible with MidiAudioManager
    public ConcurrentQueue<byte[]> messageQueue = new ConcurrentQueue<byte[]>();
    public bool IsMidiConnected => isMidiConnected;
    
    // Connection status
    private bool isMidiConnected = false;
    private string currentMidiDeviceName = UnregisteredMidiDeviceName;
    private float nextCheckTime = 0f;
    private float lastActivityTime = 0f;
    private float nextReconnectAttemptTime = 0f;
    private bool manualDisconnectRequested = false;
    private bool validationActive = true;
    
    // Events
    public delegate void ConnectionStatusChangedDelegate(bool isConnected);
    public event ConnectionStatusChangedDelegate OnConnectionStatusChanged;
    public event Action OnMidiNoteActivity;

    public string CurrentMidiDeviceName => string.IsNullOrWhiteSpace(currentMidiDeviceName)
        ? UnregisteredMidiDeviceName
        : currentMidiDeviceName;
    public bool IsValidationActive => validationActive;
    
#if UNITY_ANDROID
    private AndroidJavaObject midiService;
    private AndroidJavaClass bridgeClass;
#endif

    void Start()
    {
        validationActive = MidiInitializer.ShouldEnableMidiForScene(SceneManager.GetActiveScene().name);
        maxEventsPerFrame = Mathf.Max(maxEventsPerFrame, 256);
        Debug.Log("<color=magenta>[MIDI]</color> 🎹 ===== INICIALIZANDO RECEPTOR MIDI =====");
        Debug.Log("<color=cyan>[MIDI]</color> Modo: Servicio Android compilado (sin threads)");
        Debug.Log($"<color=yellow>[MIDI DIAG]</color> Platform: {Application.platform}");
        
        nextCheckTime = 0f;
        lastActivityTime = Time.unscaledTime;
        nextReconnectAttemptTime = 0f;
        manualDisconnectRequested = false;
        
#if UNITY_ANDROID
        Debug.Log("<color=green>[MIDI DIAG]</color> ✅ Compilado para UNITY_ANDROID");
    if (validationActive)
    {
        InitializeJavaBridge();
    }
#else
        Debug.LogError("<color=red>[MIDI DIAG]</color> ❌ NO compilado para UNITY_ANDROID! Platform es: " + Application.platform);
#endif
    }

#if UNITY_ANDROID
    /// <summary>
    /// Get reference to compiled Java MIDI service
    /// </summary>
    private void InitializeJavaBridge()
    {
        try
        {
            Debug.Log("<color=cyan>[MIDI INIT]</color> PASO 1: Intentando obtener AndroidJavaClass...");
            if (bridgeClass == null)
            {
                bridgeClass = new AndroidJavaClass("com.etheriavr.midi.MidiInputBridge");
            }
            Debug.Log("<color=green>[MIDI INIT]</color> PASO 1 OK: bridgeClass obtenida");
            
            // Get Unity's current context
            Debug.Log("<color=cyan>[MIDI INIT]</color> PASO 2: Obteniendo UnityPlayer...");
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            Debug.Log("<color=green>[MIDI INIT]</color> PASO 2 OK: UnityPlayer obtenido");
            
            Debug.Log("<color=cyan>[MIDI INIT]</color> PASO 3: Obteniendo Activity...");
            AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            Debug.Log("<color=green>[MIDI INIT]</color> PASO 3 OK: Activity obtenida");
            
            Debug.Log("<color=cyan>[MIDI INIT]</color> PASO 4: Obteniendo Context...");
            AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext");
            Debug.Log("<color=green>[MIDI INIT]</color> PASO 4 OK: Context obtenido");
            
            // Get singleton instance and initialize
            Debug.Log("<color=cyan>[MIDI INIT]</color> PASO 5: Llamando getInstance()...");
            midiService = bridgeClass.CallStatic<AndroidJavaObject>("getInstance", context);
            Debug.Log("<color=green>[MIDI INIT]</color> PASO 5 OK: getInstance() retornó");
            
            if (midiService == null)
            {
                Debug.LogError("<color=red>[MIDI INIT]</color> ❌ PASO 5.5: midiService es NULL después de getInstance()!");
                return;
            }
            
            Debug.Log("<color=cyan>[MIDI INIT]</color> PASO 6: Llamando init()...");
            midiService.Call("init");
            Debug.Log("<color=green>[MIDI INIT]</color> PASO 6 OK: init() completado");
            
            Debug.Log("<color=green>[MIDI]</color> ✅ Java MIDI Service initialized correctamente");
        }
        catch (Exception e)
        {
            Debug.LogError($"<color=red>[MIDI]</color> ❌ Excepción en InitializeJavaBridge: {e.GetType().Name}: {e.Message}\nStackTrace:\n{e.StackTrace}");
        }
    }
#endif

    void Update()
    {
        if (!validationActive)
        {
            return;
        }

        if (Time.unscaledTime >= nextCheckTime)
        {
            nextCheckTime = Time.unscaledTime + checkInterval;
            
#if UNITY_ANDROID
            if (midiService == null || bridgeClass == null)
            {
                InitializeJavaBridge();
                if (midiService == null || bridgeClass == null)
                {
                    return;
                }
            }
            
            try
            {
                // Get static variables from Java bridge
                bool javaConnected = bridgeClass.GetStatic<bool>("isConnected");
                string javaDeviceName = bridgeClass.CallStatic<string>("getConnectedDeviceName");
                string resolvedDeviceName = string.IsNullOrWhiteSpace(javaDeviceName)
                    ? UnregisteredMidiDeviceName
                    : javaDeviceName;
                
                if (javaConnected)
                {
                    currentMidiDeviceName = resolvedDeviceName;
                    if (!isMidiConnected)
                    {
                        UpdateConnectionStatus(true);
                    }
                }
                else
                {
                    if (isMidiConnected)
                    {
                        UpdateConnectionStatus(false);
                    }

                    currentMidiDeviceName = UnregisteredMidiDeviceName;

                    if (autoReconnect && !manualDisconnectRequested && Time.unscaledTime >= nextReconnectAttemptTime)
                    {
                        RequestReconnectInternal(false);
                    }
                }
                
                // Dequeue todos los eventos disponibles con un límite seguro por frame.
                int eventsDequeued = 0;
                
                int safeEventBudget = Mathf.Max(1, maxEventsPerFrame);
                for (int i = 0; i < safeEventBudget; i++)
                {
                    // Call Java method that returns sbyte[3] array (JNI uses signed bytes)
                    sbyte[] eventData = bridgeClass.CallStatic<sbyte[]>("dequeueEvent");
                    if (eventData == null) break;  // No more events
                    
                    if (eventData.Length >= 3)
                    {
                        // Copy to queue for MidiAudioManager (convert from sbyte to byte)
                        byte[] midiData = new byte[3];
                        midiData[0] = (byte)eventData[0];
                        midiData[1] = (byte)eventData[1];
                        midiData[2] = (byte)eventData[2];
                        messageQueue.Enqueue(midiData);
                        
                        eventsDequeued++;
                        lastActivityTime = Time.unscaledTime;
                        RaiseMidiNoteActivity(midiData[0], midiData[1], midiData[2]);

                        if (verboseMidiLogging)
                        {
                            Debug.Log($"<color=green>[MIDI]</color> RX: 0x{midiData[0]:X2} data1={midiData[1]} data2={midiData[2]}");
                        }
                    }
                }
                
                if (verboseMidiLogging && eventsDequeued > 0)
                {
                    Debug.Log($"<color=cyan>[MIDI]</color> Dequeued {eventsDequeued} event(s)");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"<color=red>[MIDI UPDATE ERROR]</color> ❌ Excepción en Update: {e.GetType().Name}: {e.Message}");
            }
#else
            Debug.LogError("<color=red>[MIDI UPDATE]</color> ❌ NO es UNITY_ANDROID!");
#endif
        }
    }

    private void UpdateConnectionStatus(bool connected)
    {
        if (isMidiConnected != connected)
        {
            isMidiConnected = connected;
            if (!connected)
            {
                currentMidiDeviceName = UnregisteredMidiDeviceName;
                ClearQueuedMessages();
            }
            else
            {
                manualDisconnectRequested = false;
            }

            string status = connected ? "CONECTADO ✅" : "DESCONECTADO ❌";
            Debug.Log($"<color=green>[MIDI]</color> {status}");
            OnConnectionStatusChanged?.Invoke(connected);
            lastActivityTime = Time.unscaledTime;
        }
    }

    public void RequestReconnect()
    {
        if (!validationActive)
        {
            return;
        }

        RequestReconnectInternal(true);
    }

    public void DisconnectCurrentDevice()
    {
#if UNITY_ANDROID
        manualDisconnectRequested = true;
        nextReconnectAttemptTime = 0f;

        try
        {
            if (midiService != null)
            {
                midiService.Call("disconnectCurrentDevice");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"<color=yellow>[MIDI]</color> ⚠️ No se pudo desconectar el dispositivo actual: {e.Message}");
        }
#endif

        UpdateConnectionStatus(false);
    }

    public void SetValidationActive(bool active)
    {
        if (validationActive == active)
        {
            if (validationActive)
            {
                nextCheckTime = 0f;
            }

            return;
        }

        validationActive = active;

        if (!validationActive)
        {
            manualDisconnectRequested = true;
            nextReconnectAttemptTime = 0f;
            DisconnectCurrentDevice();
            return;
        }

        manualDisconnectRequested = false;
        nextCheckTime = 0f;
        RequestReconnectInternal(false);
    }

    public bool TryGetConnectedDeviceName(out string deviceName)
    {
        if (isMidiConnected && !string.IsNullOrWhiteSpace(currentMidiDeviceName) &&
            !string.Equals(currentMidiDeviceName, UnregisteredMidiDeviceName, StringComparison.OrdinalIgnoreCase))
        {
            deviceName = currentMidiDeviceName;
            return true;
        }

        deviceName = null;
        return false;
    }

    public string GetRegistrationDeviceName()
    {
        if (TryGetConnectedDeviceName(out string deviceName))
        {
            return deviceName;
        }

        return UnregisteredMidiDeviceName;
    }

    public void SimulateMidiNoteOn(byte note, byte velocity)
    {
        byte[] data = new byte[3];
        data[0] = 0x90;  // Note On status
        data[1] = note;
        data[2] = velocity;
        messageQueue.Enqueue(data);
        lastActivityTime = Time.unscaledTime;
        RaiseMidiNoteActivity(data[0], data[1], data[2]);
        Debug.Log($"<color=cyan>[MIDI TEST]</color> Simulado: ON {note} vel={velocity}");
    }

    public void SimulateMidiNoteOff(byte note)
    {
        byte[] data = new byte[3];
        data[0] = 0x80;  // Note Off status
        data[1] = note;
        data[2] = 0;
        messageQueue.Enqueue(data);
        lastActivityTime = Time.unscaledTime;
        Debug.Log($"<color=cyan>[MIDI TEST]</color> Simulado: OFF {note}");
    }

    private void RaiseMidiNoteActivity(byte status, byte data1, byte data2)
    {
        bool isNoteOn = (status & 0xF0) == 0x90 && data2 > 0;
        if (isNoteOn)
        {
            OnMidiNoteActivity?.Invoke();
        }
    }

    private void RequestReconnectInternal(bool userInitiated)
    {
#if UNITY_ANDROID
        manualDisconnectRequested = false;
        nextReconnectAttemptTime = Time.unscaledTime + Mathf.Max(0.25f, autoReconnectInterval);

        try
        {
            if (midiService == null || bridgeClass == null)
            {
                InitializeJavaBridge();
            }

            if (midiService != null)
            {
                midiService.Call("rescanDevices");
                if (userInitiated)
                {
                    Debug.Log("<color=cyan>[MIDI]</color> 🔄 Reescaneando dispositivos MIDI...");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"<color=yellow>[MIDI]</color> ⚠️ No se pudo reescanear MIDI: {e.Message}");
        }
#endif
    }

    private void ClearQueuedMessages()
    {
        while (messageQueue.TryDequeue(out _))
        {
        }
    }

    void OnDestroy()
    {
#if UNITY_ANDROID
        if (midiService != null)
        {
            try
            {
                midiService.Call("close");
                Debug.Log("<color=yellow>[MIDI]</color> Servicio cerrado");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error: {e.Message}");
            }
        }

        bridgeClass = null;
#endif
    }
}
