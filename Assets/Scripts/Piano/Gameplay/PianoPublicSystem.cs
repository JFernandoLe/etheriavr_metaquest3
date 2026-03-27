using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Sistema de público virtual que evoluciona según la precisión del jugador
/// El público aumenta gradualmente si lo haces bien y disminuye si fallas
/// </summary>
public class PianoPublicSystem : MonoBehaviour
{
    private struct PerformanceSample
    {
        public float time;
        public float normalizedScore;
    }

    [Header("Configuración del Público")]
    [SerializeField] private float performanceWindowSeconds = 10f;
    [SerializeField] private float samplesForFullConfidence = 10f;
    [SerializeField] private float riseUnitsPerSecond = 14f;
    [SerializeField] private float fallUnitsPerSecond = 5.5f;
    [SerializeField] private float excitementCurvePower = 1.2f;
    [SerializeField] private float idleDecayPerSecond = 2.2f;
    [SerializeField] private float maxTargetScore = 88f;
    [SerializeField] private float targetPenaltyOffset = 8f;
    
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
    private readonly List<PerformanceSample> performanceWindow = new List<PerformanceSample>();
    
    // Debug
    private List<string> recentEvents = new List<string>();
    private const int MAX_DEBUG_EVENTS = 10;
    
    void Awake()
    {
    }
    
    void Start()
    {
        gameplayScoring = FindObjectOfType<GameplayScoring>();
        midiAudioManager = FindObjectOfType<MidiAudioManager>();
        EnsureAudienceController();
        
        if (gameplayScoring != null)
        {
            gameplayScoring.OnNoteEvaluated += OnNoteEvaluatedCallback;
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

        PruneExpiredSamples();

        if (performanceWindow.Count > 0)
        {
            float accumulatedScore = 0f;
            foreach (PerformanceSample sample in performanceWindow)
            {
                accumulatedScore += sample.normalizedScore;
            }

            float averageScore = accumulatedScore / performanceWindow.Count;
            float curvedScore = Mathf.Pow(Mathf.Clamp01(averageScore), excitementCurvePower);
            float confidence = Mathf.Clamp01(performanceWindow.Count / Mathf.Max(samplesForFullConfidence, 1f));
            float mappedScore = Mathf.Clamp((curvedScore * maxTargetScore) - targetPenaltyOffset, 0f, 100f);
            float desiredTarget = mappedScore * confidence;
            targetPublicScore = Mathf.Lerp(targetPublicScore, desiredTarget, Time.deltaTime * Mathf.Lerp(0.7f, 1.4f, confidence));
        }
        else
        {
            targetPublicScore = Mathf.Max(0f, targetPublicScore - idleDecayPerSecond * Time.deltaTime);
        }

        float responseRate = targetPublicScore >= currentPublicScore ? riseUnitsPerSecond : fallUnitsPerSecond;
        currentPublicScore = Mathf.MoveTowards(currentPublicScore, targetPublicScore, responseRate * Time.deltaTime);
        currentPublicScore = Mathf.Clamp(currentPublicScore, 0f, 100f);
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
        performanceWindow.Clear();

        if (midiAudioManager != null)
        {
            midiAudioManager.InitializeApplauseSystem();
            midiAudioManager.StartApplauseLoop();
            midiAudioManager.SetApplauseVolume(0f);
        }
        
        Debug.Log("<color=green>[PianoPublic]</color> 🎮 Juego iniciado - Público en 0%");
    }
    
    /// <summary>
    /// Se llama cuando termina el juego
    /// </summary>
    public void EndGame()
    {
        isGameActive = false;

        if (midiAudioManager != null)
        {
            midiAudioManager.StopApplauseLoop();
        }
        
        float accuracy = totalNoteCount > 0 ? (correctNoteCount / totalNoteCount) * 100f : 0f;
        Debug.Log($"<color=yellow>[PianoPublic]</color> 🎬 Juego terminado - Público final: {currentPublicScore:F1}% | Precisión: {accuracy:F1}%");
    }
    
    /// <summary>
    /// Callback cuando aciertas una nota
    /// </summary>
    private void OnNoteEvaluatedCallback(GameNoteData note, float normalizedScore, int successfulUnits, int totalUnits)
    {
        totalNoteCount++;
        correctNoteCount += normalizedScore;

        performanceWindow.Add(new PerformanceSample
        {
            time = Time.time,
            normalizedScore = Mathf.Clamp01(normalizedScore)
        });

        float notePercent = normalizedScore * 100f;
        string eventMsg = $"🎹 Ventana +{notePercent:F0}% ({successfulUnits}/{totalUnits})";
        AddDebugEvent(eventMsg);
        Debug.Log($"<color=cyan>[PianoPublic]</color> {eventMsg}");
    }
    
    /// <summary>
    /// Se llama cuando tocas una nota que NO está en la canción
    /// </summary>
    public void OnWrongNoteDetected(int wrongMidiNote)
    {
        performanceWindow.Add(new PerformanceSample
        {
            time = Time.time,
            normalizedScore = 0f
        });

        string eventMsg = $"⚠️ NOTA EQUIVOCADA (MIDI {wrongMidiNote})";
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

    private void PruneExpiredSamples()
    {
        float minTime = Time.time - performanceWindowSeconds;
        for (int i = performanceWindow.Count - 1; i >= 0; i--)
        {
            if (performanceWindow[i].time < minTime)
            {
                performanceWindow.RemoveAt(i);
            }
        }
    }

    void OnDestroy()
    {
        if (gameplayScoring != null)
        {
            gameplayScoring.OnNoteEvaluated -= OnNoteEvaluatedCallback;
            gameplayScoring.OnGameFinished -= OnGameFinishedCallback;
        }
    }

    private void EnsureAudienceController()
    {
        GameObject gestorAudiencia = GameObject.Find("_GestorAudiencia");
        if (gestorAudiencia == null)
        {
            return;
        }

        ControladorAudiencia oldController = gestorAudiencia.GetComponent<ControladorAudiencia>();
        if (oldController != null)
        {
            oldController.enabled = false;
        }

        ControladorAudienciaPiano pianoController = gestorAudiencia.GetComponent<ControladorAudienciaPiano>();
        if (pianoController == null)
        {
            pianoController = gestorAudiencia.AddComponent<ControladorAudienciaPiano>();
        }

        pianoController.sistemaPublico = this;
        if (pianoController.jugador == null && Camera.main != null)
        {
            pianoController.jugador = Camera.main.transform;
        }
    }
}
