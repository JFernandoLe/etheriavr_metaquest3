using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Sistema de público virtual que evoluciona según la precisión del jugador
/// El público aumenta gradualmente si lo haces bien y disminuye si fallas
/// </summary>
public class PianoPublicSystem : MonoBehaviour
{
    [Header("Configuración del Público")]
    [SerializeField] private float publicBaseThreshold = 0.5f;  // Precisión mínima para que aumente
    [SerializeField] private float perfectIncrement = 2.0f;     // Cuánto sube por nota perfecta
    [SerializeField] private float goodIncrement = 1.0f;        // Cuánto sube por nota buena
    [SerializeField] private float missedDecrement = 5.0f;      // Penalización por nota fallida
    [SerializeField] private float wrongNoteDecrement = 3.0f;   // Penalización por nota equivocada
    [SerializeField] private float lerpSpeed = 3.0f;            // Velocidad de amortiguación (mayor = más rápido)
    [SerializeField] private float decayRate = 0.05f;           // Decaimiento natural por segundo sin input
    
    [Header("Aplausos")]
    [SerializeField] private AudioSource applauseSource;
    [SerializeField] private float maxApplauseVolume = 0.8f;
    
    // Estado
    private float currentPublicScore = 0f;     // 0-100
    private float targetPublicScore = 0f;      // Score objetivo (antes de lerp)
    private float totalNoteCount = 0f;
    private float correctNoteCount = 0f;
    
    // Referencias
    private GameplayScoring gameplayScoring;
    private MidiAudioManager midiAudioManager;
    private float gameStartTime;
    private bool isGameActive = false;
    
    // Debug
    private List<string> recentEvents = new List<string>();
    private const int MAX_DEBUG_EVENTS = 10;
    
    void Awake()
    {
        // Auto-detectar AudioSource para aplausos
        if (applauseSource == null)
        {
            applauseSource = GetComponent<AudioSource>();
            if (applauseSource == null)
            {
                applauseSource = gameObject.AddComponent<AudioSource>();
                applauseSource.playOnAwake = false;
            }
        }
    }
    
    void Start()
    {
        gameplayScoring = FindObjectOfType<GameplayScoring>();
        midiAudioManager = FindObjectOfType<MidiAudioManager>();
        
        if (gameplayScoring != null)
        {
            gameplayScoring.OnNoteHit += OnNoteHitCallback;
            gameplayScoring.OnNoteMissed += OnNoteMissedCallback;
            gameplayScoring.OnGameFinished += OnGameFinishedCallback;
            Debug.Log("<color=green>[PianoPublic]</color> ✅ Suscrito a eventos de GameplayScoring");
        }
        
        currentPublicScore = 0f;
        targetPublicScore = 0f;
        totalNoteCount = 0f;
        correctNoteCount = 0f;
        
        Debug.Log("<color=yellow>[PianoPublic]</color> 👥 Sistema de público inicializado");
    }
    
    void Update()
    {
        if (!isGameActive) return;
        
        // Aplicar decaimiento natural (el público se aburre sin progreso)
        float decayAmount = decayRate * Time.deltaTime;
        targetPublicScore = Mathf.Max(0f, targetPublicScore - decayAmount);
        
        // Suavizar transición hacia el score objetivo
        currentPublicScore = Mathf.Lerp(currentPublicScore, targetPublicScore, lerpSpeed * Time.deltaTime);
        currentPublicScore = Mathf.Clamp01(currentPublicScore / 100f) * 100f; // Clamp 0-100
        
        // Actualizar volumen de aplausos según score del público
        if (applauseSource != null && applauseSource.isPlaying)
        {
            applauseSource.volume = (currentPublicScore / 100f) * maxApplauseVolume;
        }
    }
    
    /// <summary>
    /// Se llama cuando el juego comienza
    /// </summary>
    public void StartGame()
    {
        isGameActive = true;
        gameStartTime = Time.time;
        currentPublicScore = 0f;
        targetPublicScore = 0f;
        totalNoteCount = 0f;
        correctNoteCount = 0f;
        recentEvents.Clear();
        
        // Cargar y tocar aplausos suavecitos al inicio
        if (applauseSource != null)
        {
            AudioClip applauseClip = Resources.Load<AudioClip>("Sounds/aplause");
            if (applauseClip != null)
            {
                applauseSource.clip = applauseClip;
                applauseSource.volume = 0.1f; // Muy bajo al inicio
                applauseSource.loop = true;
                applauseSource.Play();
                Debug.Log("<color=cyan>[PianoPublic]</color> 🎵 Aplausos iniciados (bajo volumen)");
            }
            else
            {
                Debug.LogWarning("<color=yellow>[PianoPublic]</color> ⚠️ No se encontró Resources/Sounds/aplause");
            }
        }
        
        Debug.Log("<color=green>[PianoPublic]</color> 🎮 Juego iniciado - Público en 0%");
    }
    
    /// <summary>
    /// Se llama cuando termina el juego
    /// </summary>
    public void EndGame()
    {
        isGameActive = false;
        
        if (applauseSource != null && applauseSource.isPlaying)
        {
            applauseSource.Stop();
        }
        
        float accuracy = totalNoteCount > 0 ? (correctNoteCount / totalNoteCount) * 100f : 0f;
        Debug.Log($"<color=yellow>[PianoPublic]</color> 🎬 Juego terminado - Público final: {currentPublicScore:F1}% | Precisión: {accuracy:F1}%");
    }
    
    /// <summary>
    /// Callback cuando aciertas una nota
    /// </summary>
    private void OnNoteHitCallback(GameNoteData note, bool isPerfect)
    {
        totalNoteCount++;
        correctNoteCount++;
        
        // Aumentar score según precisión
        float increment = isPerfect ? perfectIncrement : goodIncrement;
        targetPublicScore = Mathf.Min(100f, targetPublicScore + increment);
        
        string eventMsg = isPerfect 
            ? $"🟢 PERFECTO +{perfectIncrement} → {targetPublicScore:F1}%"
            : $"🟡 BIEN +{goodIncrement} → {targetPublicScore:F1}%";
        
        AddDebugEvent(eventMsg);
        Debug.Log($"<color=cyan>[PianoPublic]</color> {eventMsg}");
    }
    
    /// <summary>
    /// Callback cuando fallas una nota
    /// </summary>
    private void OnNoteMissedCallback(GameNoteData note)
    {
        totalNoteCount++;
        
        // Penalización fuerte por fallar
        targetPublicScore = Mathf.Max(0f, targetPublicScore - missedDecrement);
        
        string eventMsg = $"❌ FALLADA -{missedDecrement} → {targetPublicScore:F1}%";
        AddDebugEvent(eventMsg);
        Debug.Log($"<color=red>[PianoPublic]</color> {eventMsg}");
    }
    
    /// <summary>
    /// Se llama cuando tocas una nota que NO está en la canción
    /// </summary>
    public void OnWrongNoteDetected(int wrongMidiNote)
    {
        totalNoteCount++;
        
        // Penalización moderada
        targetPublicScore = Mathf.Max(0f, targetPublicScore - wrongNoteDecrement);
        
        string eventMsg = $"⚠️ NOTA EQUIVOCADA (MIDI {wrongMidiNote}) -{wrongNoteDecrement} → {targetPublicScore:F1}%";
        AddDebugEvent(eventMsg);
        Debug.Log($"<color=orange>[PianoPublic]</color> {eventMsg}");
    }
    
    /// <summary>
    /// Se llama cuando termina el juego (para mostrar estadísticas)
    /// </summary>
    private void OnGameFinishedCallback(GameplayResults results)
    {
        EndGame();
    }
    
    /// <summary>
    /// Obtener el score actual del público (0-100)
    /// </summary>
    public float GetCurrentPublicScore()
    {
        return currentPublicScore;
    }
    
    /// <summary>
    /// Obtener el score objetivo (antes de suavizado)
    /// </summary>
    public float GetTargetPublicScore()
    {
        return targetPublicScore;
    }
    
    /// <summary>
    /// Mostrar estadísticas del público
    /// </summary>
    public void LogStatistics()
    {
        float accuracy = totalNoteCount > 0 ? (correctNoteCount / totalNoteCount) * 100f : 0f;
        Debug.Log($"<color=yellow>[PianoPublic STATS]</color>");
        Debug.Log($"  👥 Score actual: {currentPublicScore:F1}%");
        Debug.Log($"  🎯 Score objetivo: {targetPublicScore:F1}%");
        Debug.Log($"  🎵 Notas totales: {totalNoteCount}");
        Debug.Log($"  ✅ Notas correctas: {correctNoteCount}");
        Debug.Log($"  📊 Precisión: {accuracy:F1}%");
    }
    
    private void AddDebugEvent(string eventMsg)
    {
        recentEvents.Add(eventMsg);
        if (recentEvents.Count > MAX_DEBUG_EVENTS)
        {
            recentEvents.RemoveAt(0);
        }
    }
}
