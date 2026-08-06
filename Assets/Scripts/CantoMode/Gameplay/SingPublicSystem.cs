using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Sistema de público virtual para MODO CANTO
/// Análogo a PianoPublicSystem pero adaptado para evaluación de canto
/// El público reacciona gradualmente según la precisión del cantante
/// </summary>
public class SingPublicSystem : MonoBehaviour
{
    private const string AudienceSyncTag = "[AudienceSync-Sing]";

    [Header("Configuración del Público")]
    [SerializeField] private float performanceWindowSeconds = 4.0f;       // REDUCIDO de 5.25 - responde mas rapido
    [SerializeField] private float stableWindowSamples = 3f;              // REDUCIDO de 5 - menos muestras para reaccionar
    [SerializeField] private float periodicSampleInterval = 0.12f;        // REDUCIDO de 0.18 - muestras mas frecuentes
    [SerializeField] private float reactionCurvePower = 0.85f;            // REDUCIDO de 1.08 - curva mas suave, sube mas
    [SerializeField] private float riseLerpSpeed = 8f;                   // AUMENTADO de 5 - sube mas rapido
    [SerializeField] private float fallLerpSpeed = 6f;                   // REDUCIDO de 8.5 - baja mas lento
    [SerializeField] private float inertiaLerpSpeed = 5f;                // AUMENTADO de 3.4 - mas respuesta
    [SerializeField] private float pulseDecayPerSecond = 22f;            // REDUCIDO de 26 - pulsos duran mas
    [SerializeField] private float earlyHitBoost = 25f;                  // AUMENTADO de 18 - mas impacto cuando aciertas
    [SerializeField] private float earlyRhythmBoost = 14f;               // AUMENTADO de 9
    [SerializeField] private float mistakePitchPenalty = 10f;            // REDUCIDO de 14 - menos castigo
    [SerializeField] private float mistakeRhythmPenalty = 12f;           // REDUCIDO de 18

    // Estado
    private float currentPublicScore = 0f;
    private float animationPublicScore = 0f;
    private float targetPublicScore = 0f;
    private float livePitchPulse = 0f;
    private float liveRhythmPulse = 0f;
    private float nextPeriodicSampleTime = 0f;
    private float lastWindowAverage = 0f;

    // Referencias
    private ScoreManager scoreManager;
    private bool isGameActive = false;
    private readonly List<float> performanceWindow = new List<float>();
    private readonly List<float> performanceWindowTimes = new List<float>();

    void Start()
    {
        scoreManager = FindObjectOfType<ScoreManager>();
        EnsureAudienceController();

        currentPublicScore = 0f;
        targetPublicScore = 0f;
    }

    void Update()
    {
        if (!isGameActive) return;

        livePitchPulse = Mathf.MoveTowards(livePitchPulse, 0f, pulseDecayPerSecond * Time.deltaTime);
        liveRhythmPulse = Mathf.MoveTowards(liveRhythmPulse, 0f, pulseDecayPerSecond * Time.deltaTime);

        // Tomar muestras periódicas
        if (Time.time >= nextPeriodicSampleTime)
        {
            float combinedPerformance = GetCurrentPerformance();
            AddPerformanceSample(combinedPerformance);
            nextPeriodicSampleTime = Time.time + Mathf.Max(0.05f, periodicSampleInterval);
        }

        // Podar muestras viejas
        PruneExpiredSamples();

        // Calcular score objetivo
        float windowAverage = CalculateWindowAverage();
        lastWindowAverage = windowAverage;
        float windowConfidence = Mathf.Clamp01(performanceWindow.Count / Mathf.Max(stableWindowSamples, 1f));
        float curvedAverage = Mathf.Pow(Mathf.Clamp01(windowAverage / 100f), reactionCurvePower) * 100f;
        targetPublicScore = curvedAverage * windowConfidence;

        // Suavizar
        float responseSpeed = targetPublicScore >= currentPublicScore ? riseLerpSpeed : fallLerpSpeed;
        currentPublicScore = Mathf.Lerp(currentPublicScore, targetPublicScore, Time.deltaTime * responseSpeed);
        currentPublicScore = Mathf.Clamp(currentPublicScore, 0f, 100f);
        animationPublicScore = Mathf.Lerp(animationPublicScore, currentPublicScore, Time.deltaTime * inertiaLerpSpeed);
    }

    /// <summary>
    /// Inicia el sistema de público para una partida
    /// </summary>
    public void StartGame()
    {
        isGameActive = true;
        currentPublicScore = 0f;
        animationPublicScore = 0f;
        targetPublicScore = 0f;
        livePitchPulse = 0f;
        liveRhythmPulse = 0f;
        lastWindowAverage = 0f;
        nextPeriodicSampleTime = Time.time;
        performanceWindow.Clear();
        performanceWindowTimes.Clear();
    }

    /// <summary>
    /// Termina el sistema de público
    /// </summary>
    public void EndGame()
    {
        isGameActive = false;
    }

    /// <summary>
    /// Se llama cuando el jugador acierta una nota (cualquier nivel)
    /// </summary>
    public void OnNoteHit(float pitchQuality, float rhythmQuality)
    {
        livePitchPulse = Mathf.Clamp(livePitchPulse + (pitchQuality * earlyHitBoost), -100f, 100f);
        liveRhythmPulse = Mathf.Clamp(liveRhythmPulse + (rhythmQuality * earlyRhythmBoost), -100f, 100f);

        // Registrar muestra inmediata
        AddPerformanceSample(GetCurrentPerformance());
    }

    /// <summary>
    /// Se llama cuando el jugador falla una nota
    /// </summary>
    public void OnNoteMiss()
    {
        livePitchPulse = Mathf.Clamp(livePitchPulse - mistakePitchPenalty, -100f, 100f);
        liveRhythmPulse = Mathf.Clamp(liveRhythmPulse - mistakeRhythmPenalty, -100f, 100f);

        PruneExpiredSamples();
    }

    /// <summary>
    /// Obtiene el score actual combinado de público (0-100)
    /// </summary>
    public float GetCurrentPublicScore()
    {
        return currentPublicScore;
    }

    public float GetCurrentPublicScoreForApplause()
    {
        return Mathf.Clamp(currentPublicScore, 0f, 100f);
    }

    public float GetCurrentAudienceAnimationScore()
    {
        return animationPublicScore;
    }

    /// <summary>
    /// Obtiene el performance combinado actual basado en ScoreManager
    /// </summary>
    private float GetCurrentPerformance()
    {
        if (scoreManager == null)
        {
            scoreManager = FindObjectOfType<ScoreManager>();
            if (scoreManager == null) return 0f;
        }

        float baseHarmony = scoreManager.accuracyPercent;
        float baseRhythm = scoreManager.rhythmPercent;

        float liveHarmony = Mathf.Clamp(baseHarmony + livePitchPulse, 0f, 100f);
        float liveRhythm = Mathf.Clamp(baseRhythm + liveRhythmPulse, 0f, 100f);

        return (liveHarmony + liveRhythm) * 0.5f;
    }

    private void AddPerformanceSample(float combinedPerformance)
    {
        performanceWindow.Add(combinedPerformance);
        performanceWindowTimes.Add(Time.time);

        // Limitar tamaño para evitar memory leak
        if (performanceWindow.Count > 1000)
        {
            performanceWindow.RemoveAt(0);
            performanceWindowTimes.RemoveAt(0);
        }
    }

    private void PruneExpiredSamples()
    {
        float oldestAllowedTime = Time.time - Mathf.Max(0.5f, performanceWindowSeconds);
        for (int i = performanceWindow.Count - 1; i >= 0; i--)
        {
            if (performanceWindowTimes[i] < oldestAllowedTime)
            {
                performanceWindow.RemoveAt(i);
                performanceWindowTimes.RemoveAt(i);
            }
        }
    }

    private float CalculateWindowAverage()
    {
        if (performanceWindow.Count == 0)
            return 0f;

        float total = 0f;
        for (int i = 0; i < performanceWindow.Count; i++)
        {
            total += performanceWindow[i];
        }
        return total / performanceWindow.Count;
    }

    private void EnsureAudienceController()
    {
        GameObject gestorAudiencia = GameObject.Find("_GestorAudiencia");
        if (gestorAudiencia == null)
        {
            Debug.LogWarning("[SingPublicSystem] No se encontró _GestorAudiencia en la escena");
            return;
        }

        ControladorAudienciaSing audienceController = gestorAudiencia.GetComponent<ControladorAudienciaSing>();
        if (audienceController == null)
        {
            audienceController = gestorAudiencia.AddComponent<ControladorAudienciaSing>();
        }

        audienceController.sistemaPublico = this;
        if (audienceController.jugador == null && Camera.main != null)
        {
            audienceController.jugador = Camera.main.transform;
        }
    }
}
