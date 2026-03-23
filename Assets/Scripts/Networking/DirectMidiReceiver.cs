using UnityEngine;
using System.Collections.Concurrent;
using System;

/// <summary>
/// 🎹 MIDI Input Receiver - Ultra Simple (uses native Android service)
/// Reads from pre-compiled Java bridge that handles MIDI callbacks correctly
/// </summary>
[DefaultExecutionOrder(-1000)]
public class DirectMidiReceiver : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float checkInterval = 0.001f;  // 1ms polling = ultra responsivo
    [SerializeField] private int maxEventsPerFrame = 32;
    [SerializeField] private bool verboseMidiLogging = false;
    
    // Queue compatible with MidiAudioManager
    public ConcurrentQueue<byte[]> messageQueue = new ConcurrentQueue<byte[]>();
    
    // Connection status
    private bool isMidiConnected = false;
    private float nextCheckTime = 0f;
    private float lastActivityTime = 0f;
    private const float TIMEOUT_SECONDS = 10f;
    
    // Events
    public delegate void ConnectionStatusChangedDelegate(bool isConnected);
    public event ConnectionStatusChangedDelegate OnConnectionStatusChanged;
    
#if UNITY_ANDROID
    private AndroidJavaObject midiService;
#endif

    void Start()
    {
        Debug.Log("<color=magenta>[MIDI]</color> 🎹 ===== INICIALIZANDO RECEPTOR MIDI =====");
        Debug.Log("<color=cyan>[MIDI]</color> Modo: Servicio Android compilado (sin threads)");
        Debug.Log($"<color=yellow>[MIDI DIAG]</color> Platform: {Application.platform}");
        
        nextCheckTime = 0f;
        lastActivityTime = Time.time;
        
#if UNITY_ANDROID
        Debug.Log("<color=green>[MIDI DIAG]</color> ✅ Compilado para UNITY_ANDROID");
        InitializeJavaBridge();
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
            AndroidJavaClass bridgeClass = new AndroidJavaClass("com.etheriavr.midi.MidiInputBridge");
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
        if (Time.time >= nextCheckTime)
        {
            nextCheckTime = Time.time + checkInterval;
            
#if UNITY_ANDROID
            // Check if Java service has MIDI data
            if (midiService == null)
            {
                Debug.LogError("<color=red>[MIDI UPDATE]</color> ❌ midiService es NULL!");
                return;
            }
            
            try
            {
                // Get static variables from Java bridge
                AndroidJavaClass bridgeClass = new AndroidJavaClass("com.etheriavr.midi.MidiInputBridge");
                bool javaConnected = bridgeClass.GetStatic<bool>("isConnected");
                
                // Update connection status
                if (javaConnected != isMidiConnected)
                {
                    UpdateConnectionStatus(javaConnected);
                }
                
                // Dequeue todos los eventos disponibles con un límite seguro por frame.
                int eventsDequeued = 0;
                
                for (int i = 0; i < Mathf.Max(1, maxEventsPerFrame); i++)
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
                        lastActivityTime = Time.time;

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
        
        // Timeout check
        if (isMidiConnected && (Time.time - lastActivityTime) > TIMEOUT_SECONDS)
        {
            Debug.LogWarning("<color=yellow>[MIDI]</color> ⏱️ TIMEOUT");
            UpdateConnectionStatus(false);
        }
    }

    private void UpdateConnectionStatus(bool connected)
    {
        if (isMidiConnected != connected)
        {
            isMidiConnected = connected;
            string status = connected ? "CONECTADO ✅" : "DESCONECTADO ❌";
            Debug.Log($"<color=green>[MIDI]</color> {status}");
            OnConnectionStatusChanged?.Invoke(connected);
            lastActivityTime = Time.time;
        }
    }

    public void SimulateMidiNoteOn(byte note, byte velocity)
    {
        byte[] data = new byte[3];
        data[0] = 0x90;  // Note On status
        data[1] = note;
        data[2] = velocity;
        messageQueue.Enqueue(data);
        lastActivityTime = Time.time;
        Debug.Log($"<color=cyan>[MIDI TEST]</color> Simulado: ON {note} vel={velocity}");
    }

    public void SimulateMidiNoteOff(byte note)
    {
        byte[] data = new byte[3];
        data[0] = 0x80;  // Note Off status
        data[1] = note;
        data[2] = 0;
        messageQueue.Enqueue(data);
        lastActivityTime = Time.time;
        Debug.Log($"<color=cyan>[MIDI TEST]</color> Simulado: OFF {note}");
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
#endif
    }
}
