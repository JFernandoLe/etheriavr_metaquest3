using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema de público virtual que evoluciona según la precisión del jugador
/// El público aumenta gradualmente si lo haces bien y disminuye si fallas
/// </summary>
public class PianoPublicSystem : MonoBehaviour
{
    private const string AudienceSyncTag = "[AudienceSync]";

    private struct PerformanceWindowSample
    {
        public float time;
        public float combinedPerformance;
    }

    private struct AudienceComputationSnapshot
    {
        public float baseRhythm;
        public float baseHarmony;
        public float liveRhythm;
        public float liveHarmony;
        public float combinedPerformance;
        public int windowSamples;
        public float windowAverage;
        public float windowConfidence;
        public float curvedAverage;
        public float targetScore;
        public float currentScore;
        public float animationScore;
        public float responseSpeed;
    }

    [Header("Configuración del Público")]
    [SerializeField] private float performanceWindowSeconds = 5.25f;
    [SerializeField] private float stableWindowSamples = 5f;
    [SerializeField] private float periodicSampleInterval = 0.18f;
    [SerializeField] private float reactionCurvePower = 1.08f;
    [SerializeField] private float riseLerpSpeed = 5f;
    [SerializeField] private float fallLerpSpeed = 8.5f;
    [SerializeField] private float inertiaLerpSpeed = 3.4f;
    [SerializeField] private float pulseDecayPerSecond = 26f;
    [SerializeField] private float earlyRhythmBoost = 18f;
    [SerializeField] private float earlyHarmonyBoost = 9f;
    [SerializeField] private float mistakeRhythmPenalty = 14f;
    [SerializeField] private float mistakeHarmonyPenalty = 18f;
    [SerializeField] private float liveWindowReactionMultiplier = 1.1f;
    
    // Estado
    private float currentPublicScore = 0f;
    private float animationPublicScore = 0f;
    private float targetPublicScore = 0f;
    private float totalNoteCount = 0f;
    private float correctNoteCount = 0f;
    private float liveRhythmPulse = 0f;
    private float liveHarmonyPulse = 0f;
    private float currentLiveRhythm = 0f;
    private float currentLiveHarmony = 0f;
    private float lastWindowAverage = 0f;
    private float nextPeriodicSampleTime = 0f;
    
    // Referencias
    private GameplayScoring gameplayScoring;
    private MidiAudioManager midiAudioManager;
    private bool isGameActive = false;
    private PianoAudienceIntensityProfile.Profile audienceProfile;
    private readonly List<PerformanceWindowSample> performanceWindow = new List<PerformanceWindowSample>();
    
    // Debug
    private List<string> recentEvents = new List<string>();
    private const int MAX_DEBUG_EVENTS = 10;
    [SerializeField] private bool enableAudienceSyncLogs = true;
    
    void Awake()
    {
    }
    
    void Start()
    {
        gameplayScoring = FindObjectOfType<GameplayScoring>();
        midiAudioManager = FindObjectOfType<MidiAudioManager>();
        RefreshAudienceProfile();
        EnsureAudienceController();
        
        if (gameplayScoring != null)
        {
            gameplayScoring.OnNoteEvaluated += OnNoteEvaluatedCallback;
            gameplayScoring.OnGameFinished += OnGameFinishedCallback;
        }
        
        currentPublicScore = 0f;
        targetPublicScore = 0f;
        totalNoteCount = 0f;
        correctNoteCount = 0f;
    }
    
    void Update()
    {
        if (!isGameActive) return;

        liveRhythmPulse = Mathf.MoveTowards(liveRhythmPulse, 0f, pulseDecayPerSecond * Time.deltaTime);
        liveHarmonyPulse = Mathf.MoveTowards(liveHarmonyPulse, 0f, pulseDecayPerSecond * Time.deltaTime);

        float combinedPerformance = UpdateLivePerformanceValues();

        if (Time.time >= nextPeriodicSampleTime)
        {
            AddPerformanceSample(combinedPerformance);
            nextPeriodicSampleTime = Time.time + Mathf.Max(0.05f, periodicSampleInterval);
        }

        PruneExpiredSamples();

        AudienceComputationSnapshot snapshot = BuildAudienceComputationSnapshot();
        lastWindowAverage = snapshot.windowAverage;
        targetPublicScore = snapshot.targetScore;
        currentPublicScore = Mathf.Lerp(currentPublicScore, targetPublicScore, Time.deltaTime * snapshot.responseSpeed);
        currentPublicScore = Mathf.Clamp(currentPublicScore, 0f, 100f);
        animationPublicScore = Mathf.Lerp(animationPublicScore, currentPublicScore, Time.deltaTime * inertiaLerpSpeed);
    }
    
    /// <summary>
    /// Se llama cuando el juego comienza
    /// </summary>
    public void StartGame()
    {
        RefreshAudienceProfile();
        isGameActive = true;
        currentPublicScore = 0f;
        animationPublicScore = 0f;
        targetPublicScore = 0f;
        totalNoteCount = 0f;
        correctNoteCount = 0f;
        liveRhythmPulse = 0f;
        liveHarmonyPulse = 0f;
        currentLiveRhythm = 0f;
        currentLiveHarmony = 0f;
        lastWindowAverage = 0f;
        nextPeriodicSampleTime = Time.time;
        performanceWindow.Clear();
        recentEvents.Clear();

        if (midiAudioManager != null)
        {
            midiAudioManager.InitializeApplauseSystem();
            midiAudioManager.StartApplauseLoop();
            midiAudioManager.SetApplauseVolume(0f);
        }
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
    }
    
    /// <summary>
    /// Callback cuando aciertas una nota
    /// </summary>
    private void OnNoteEvaluatedCallback(GameNoteData note, float normalizedScore, int successfulUnits, int totalUnits)
    {
        totalNoteCount++;
        correctNoteCount += normalizedScore;
        float previousTargetPublic = targetPublicScore;
        CaptureCurrentPerformanceSample();

        AudienceComputationSnapshot snapshot = BuildAudienceComputationSnapshot();
        LogAudienceSync(
            "NoteEvaluated",
            note,
            normalizedScore > 0f,
            previousTargetPublic,
            snapshot);

        float notePercent = normalizedScore * 100f;
        string eventMsg = $"🎹 Ventana +{notePercent:F0}% ({successfulUnits}/{totalUnits})";
        AddDebugEvent(eventMsg);
    }

    public void OnLiveWindowMatched(GameNoteData note, float onsetQuality, int totalUnits)
    {
        float previousTargetPublic = targetPublicScore;
        float weightedQuality = Mathf.Clamp01(onsetQuality * liveWindowReactionMultiplier);
        float chordWeight = totalUnits > 1 ? Mathf.Lerp(0.8f, 1f, 1f / totalUnits) : 1f;

        liveRhythmPulse = Mathf.Clamp(liveRhythmPulse + (weightedQuality * earlyRhythmBoost * chordWeight), -100f, 100f);
        liveHarmonyPulse = Mathf.Clamp(liveHarmonyPulse + (weightedQuality * earlyHarmonyBoost * chordWeight), -100f, 100f);
        CaptureCurrentPerformanceSample();

        AudienceComputationSnapshot snapshot = BuildAudienceComputationSnapshot();
        LogAudienceSync(
            "LiveWindowMatched",
            note,
            true,
            previousTargetPublic,
            snapshot);

        string eventMsg = $"⚡ Reacción temprana +{weightedQuality * 100f:F0}% por acierto en ventana";
        AddDebugEvent(eventMsg);
    }
    
    /// <summary>
    /// Se llama cuando tocas una nota que NO está en la canción
    /// </summary>
    public void OnWrongNoteDetected(int wrongMidiNote)
    {
        float previousTargetPublic = targetPublicScore;
        liveRhythmPulse = Mathf.Clamp(liveRhythmPulse - mistakeRhythmPenalty, -100f, 100f);
        liveHarmonyPulse = Mathf.Clamp(liveHarmonyPulse - mistakeHarmonyPenalty, -100f, 100f);
        CaptureCurrentPerformanceSample();

        AudienceComputationSnapshot snapshot = BuildAudienceComputationSnapshot();
        LogAudienceSync(
            "WrongNote",
            wrongMidiNote,
            false,
            previousTargetPublic,
            snapshot);

        string eventMsg = $"⚠️ NOTA EQUIVOCADA (MIDI {wrongMidiNote})";
        AddDebugEvent(eventMsg);
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

    public float GetCurrentPublicScoreForApplause()
    {
        return Mathf.Clamp(currentPublicScore, 0f, 100f);
    }

    public float GetCurrentAudienceAnimationScore()
    {
        return animationPublicScore;
    }
    
    /// <summary>
    /// Obtener el score objetivo (antes de suavizado)
    /// </summary>
    public float GetTargetPublicScore()
    {
        return targetPublicScore;
    }

    public float GetCurrentAudienceCap()
    {
        return 100f;
    }
    
    /// <summary>
    /// Mostrar estadísticas del público
    /// </summary>
    public void LogStatistics()
    {
    }
    
    private void AddDebugEvent(string eventMsg)
    {
        recentEvents.Add(eventMsg);
        if (recentEvents.Count > MAX_DEBUG_EVENTS)
        {
            recentEvents.RemoveAt(0);
        }
    }

    private float UpdateLivePerformanceValues()
    {
        float baseRhythm = gameplayScoring != null ? gameplayScoring.GetLiveRhythmPercentage() : 0f;
        float baseHarmony = gameplayScoring != null ? gameplayScoring.GetLiveHarmonyPercentage() : 0f;

        currentLiveRhythm = Mathf.Clamp(baseRhythm + liveRhythmPulse, 0f, 100f);
        currentLiveHarmony = Mathf.Clamp(baseHarmony + liveHarmonyPulse, 0f, 100f);

        return (currentLiveHarmony + currentLiveRhythm) * 0.5f;
    }

    private AudienceComputationSnapshot BuildAudienceComputationSnapshot()
    {
        AudienceComputationSnapshot snapshot = new AudienceComputationSnapshot();
        snapshot.baseRhythm = gameplayScoring != null ? gameplayScoring.GetLiveRhythmPercentage() : 0f;
        snapshot.baseHarmony = gameplayScoring != null ? gameplayScoring.GetLiveHarmonyPercentage() : 0f;
        snapshot.liveRhythm = Mathf.Clamp(snapshot.baseRhythm + liveRhythmPulse, 0f, 100f);
        snapshot.liveHarmony = Mathf.Clamp(snapshot.baseHarmony + liveHarmonyPulse, 0f, 100f);
        snapshot.combinedPerformance = (snapshot.liveHarmony + snapshot.liveRhythm) * 0.5f;
        snapshot.windowSamples = performanceWindow.Count;
        snapshot.windowAverage = CalculateWindowAverage();
        snapshot.windowConfidence = Mathf.Clamp01(performanceWindow.Count / Mathf.Max(stableWindowSamples, 1f));
        snapshot.curvedAverage = Mathf.Pow(Mathf.Clamp01(snapshot.windowAverage / 100f), reactionCurvePower) * 100f;
        snapshot.targetScore = Mathf.Clamp01(snapshot.curvedAverage / Mathf.Max(audienceProfile.ScoreForFullReaction, 0.01f)) * 100f * snapshot.windowConfidence;
        snapshot.responseSpeed = snapshot.targetScore >= currentPublicScore ? riseLerpSpeed : fallLerpSpeed;
        snapshot.currentScore = currentPublicScore;
        snapshot.animationScore = animationPublicScore;
        return snapshot;
    }

    private void LogAudienceSync(string eventType, GameNoteData note, bool wasOnTime, float previousPublic, AudienceComputationSnapshot snapshot)
    {
        if (!enableAudienceSyncLogs)
        {
            return;
        }

        string midiInfo = note != null ? $"midi=[{FormatMidiNotes(note)}]" : "midi=[n/a]";

        Debug.Log(
            $"{AudienceSyncTag} event={eventType} {midiInfo} onTime={wasOnTime} " +
            $"public={previousPublic:F1}%->{snapshot.targetScore:F1}%");
    }

    private void LogAudienceSync(string eventType, int wrongMidiNote, bool wasOnTime, float previousPublic, AudienceComputationSnapshot snapshot)
    {
        if (!enableAudienceSyncLogs)
        {
            return;
        }

        Debug.Log(
            $"{AudienceSyncTag} event={eventType} midi={wrongMidiNote} onTime={wasOnTime} " +
            $"public={previousPublic:F1}%->{snapshot.targetScore:F1}%");
    }

    private string FormatMidiNotes(GameNoteData note)
    {
        if (note == null)
        {
            return "n/a";
        }

        int[] midiNotes = note.midi_notes != null && note.midi_notes.Length > 0
            ? note.midi_notes
            : new[] { note.GetMidiNote() };

        return string.Join(",", midiNotes);
    }

    private void CaptureCurrentPerformanceSample()
    {
        if (!isGameActive)
        {
            return;
        }

        AddPerformanceSample(UpdateLivePerformanceValues());
        PruneExpiredSamples();
    }

    private void AddPerformanceSample(float combinedPerformance)
    {
        performanceWindow.Add(new PerformanceWindowSample
        {
            time = Time.time,
            combinedPerformance = Mathf.Clamp(combinedPerformance, 0f, 100f)
        });
    }

    private void PruneExpiredSamples()
    {
        float oldestAllowedTime = Time.time - Mathf.Max(0.5f, performanceWindowSeconds);
        for (int i = performanceWindow.Count - 1; i >= 0; i--)
        {
            if (performanceWindow[i].time < oldestAllowedTime)
            {
                performanceWindow.RemoveAt(i);
            }
        }
    }

    private float CalculateWindowAverage()
    {
        if (performanceWindow.Count == 0)
        {
            return 0f;
        }

        float total = 0f;
        for (int i = 0; i < performanceWindow.Count; i++)
        {
            total += performanceWindow[i].combinedPerformance;
        }

        return total / performanceWindow.Count;
    }

    private void RefreshAudienceProfile()
    {
        audienceProfile = PianoAudienceIntensityProfile.ResolveCurrentProfile();
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
