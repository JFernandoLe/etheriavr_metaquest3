using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema de público virtual que evoluciona según la precisión del jugador:
/// sube gradualmente cuando el desempeño es bueno y baja cuando se falla.
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
    [SerializeField] private bool enableAudienceSyncLogs = false;

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

    private GameplayScoring gameplayScoring;
    private MidiAudioManager midiAudioManager;
    private bool isGameActive = false;
    private PianoAudienceIntensityProfile.Profile audienceProfile;
    private readonly List<PerformanceWindowSample> performanceWindow = new List<PerformanceWindowSample>();

    private float BaseRhythm => gameplayScoring != null ? gameplayScoring.GetLiveRhythmPercentage() : 0f;
    private float BaseHarmony => gameplayScoring != null ? gameplayScoring.GetLiveHarmonyPercentage() : 0f;

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
            PruneExpiredSamples();
        }

        AudienceComputationSnapshot snapshot = BuildAudienceComputationSnapshot();
        lastWindowAverage = snapshot.windowAverage;
        targetPublicScore = snapshot.targetScore;
        currentPublicScore = Mathf.Clamp(
            Mathf.Lerp(currentPublicScore, targetPublicScore, Time.deltaTime * snapshot.responseSpeed), 0f, 100f);
        animationPublicScore = Mathf.Lerp(animationPublicScore, currentPublicScore, Time.deltaTime * inertiaLerpSpeed);
    }

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

        if (midiAudioManager != null)
        {
            midiAudioManager.InitializeApplauseSystem();
            midiAudioManager.StartApplauseLoop();
            midiAudioManager.SetApplauseVolume(0f);
        }
    }

    public void EndGame()
    {
        isGameActive = false;
        if (midiAudioManager != null) midiAudioManager.StopApplauseLoop();
    }

    private void OnNoteEvaluatedCallback(GameNoteData note, float normalizedScore, int successfulUnits, int totalUnits)
    {
        totalNoteCount++;
        correctNoteCount += normalizedScore;

        float previousTargetPublic = targetPublicScore;
        CaptureCurrentPerformanceSample();
        LogAudienceSync("NoteEvaluated", note, normalizedScore > 0f, previousTargetPublic, BuildAudienceComputationSnapshot());
    }

    public void OnLiveWindowMatched(GameNoteData note, float onsetQuality, int totalUnits)
    {
        float previousTargetPublic = targetPublicScore;
        float weightedQuality = Mathf.Clamp01(onsetQuality * liveWindowReactionMultiplier);
        float chordWeight = totalUnits > 1 ? Mathf.Lerp(0.8f, 1f, 1f / totalUnits) : 1f;

        liveRhythmPulse = Mathf.Clamp(liveRhythmPulse + (weightedQuality * earlyRhythmBoost * chordWeight), -100f, 100f);
        liveHarmonyPulse = Mathf.Clamp(liveHarmonyPulse + (weightedQuality * earlyHarmonyBoost * chordWeight), -100f, 100f);
        CaptureCurrentPerformanceSample();

        LogAudienceSync("LiveWindowMatched", note, true, previousTargetPublic, BuildAudienceComputationSnapshot());
    }

    /// <summary>Se llama cuando el jugador toca una nota que no está en la canción.</summary>
    public void OnWrongNoteDetected(int wrongMidiNote)
    {
        float previousTargetPublic = targetPublicScore;
        liveRhythmPulse = Mathf.Clamp(liveRhythmPulse - mistakeRhythmPenalty, -100f, 100f);
        liveHarmonyPulse = Mathf.Clamp(liveHarmonyPulse - mistakeHarmonyPenalty, -100f, 100f);
        CaptureCurrentPerformanceSample();

        LogAudienceSync("WrongNote", wrongMidiNote, false, previousTargetPublic, BuildAudienceComputationSnapshot());
    }

    private void OnGameFinishedCallback(GameplayResults results) => EndGame();

    public float GetCurrentPublicScore() => currentPublicScore;
    public float GetCurrentPublicScoreForApplause() => Mathf.Clamp(currentPublicScore, 0f, 100f);
    public float GetCurrentAudienceAnimationScore() => animationPublicScore;
    public float GetTargetPublicScore() => targetPublicScore;
    public float GetCurrentAudienceCap() => 100f;

    public void LogStatistics()
    {
    }

    /// <summary>Refresca los valores en vivo (con pulsos) y devuelve el desempeño combinado.</summary>
    private float UpdateLivePerformanceValues()
    {
        currentLiveRhythm = Mathf.Clamp(BaseRhythm + liveRhythmPulse, 0f, 100f);
        currentLiveHarmony = Mathf.Clamp(BaseHarmony + liveHarmonyPulse, 0f, 100f);
        return (currentLiveHarmony + currentLiveRhythm) * 0.5f;
    }

    private AudienceComputationSnapshot BuildAudienceComputationSnapshot()
    {
        float baseRhythm = BaseRhythm;
        float baseHarmony = BaseHarmony;
        float liveRhythm = Mathf.Clamp(baseRhythm + liveRhythmPulse, 0f, 100f);
        float liveHarmony = Mathf.Clamp(baseHarmony + liveHarmonyPulse, 0f, 100f);
        float windowAverage = CalculateWindowAverage();
        float windowConfidence = Mathf.Clamp01(performanceWindow.Count / Mathf.Max(stableWindowSamples, 1f));
        float curvedAverage = Mathf.Pow(Mathf.Clamp01(windowAverage / 100f), reactionCurvePower) * 100f;
        float targetScore = Mathf.Clamp01(curvedAverage / Mathf.Max(audienceProfile.ScoreForFullReaction, 0.01f))
                            * 100f * windowConfidence;

        return new AudienceComputationSnapshot
        {
            baseRhythm = baseRhythm,
            baseHarmony = baseHarmony,
            liveRhythm = liveRhythm,
            liveHarmony = liveHarmony,
            combinedPerformance = (liveHarmony + liveRhythm) * 0.5f,
            windowSamples = performanceWindow.Count,
            windowAverage = windowAverage,
            windowConfidence = windowConfidence,
            curvedAverage = curvedAverage,
            targetScore = targetScore,
            responseSpeed = targetScore >= currentPublicScore ? riseLerpSpeed : fallLerpSpeed,
            currentScore = currentPublicScore,
            animationScore = animationPublicScore
        };
    }

    private void LogAudienceSync(string eventType, GameNoteData note, bool wasOnTime, float previousPublic, AudienceComputationSnapshot snapshot)
    {
        if (!enableAudienceSyncLogs) return;

        Debug.Log($"{AudienceSyncTag} event={eventType} midi=[{FormatMidiNotes(note)}] onTime={wasOnTime} " +
                  $"public={previousPublic:F1}%->{snapshot.targetScore:F1}%");
    }

    private void LogAudienceSync(string eventType, int wrongMidiNote, bool wasOnTime, float previousPublic, AudienceComputationSnapshot snapshot)
    {
        if (!enableAudienceSyncLogs) return;

        Debug.Log($"{AudienceSyncTag} event={eventType} midi={wrongMidiNote} onTime={wasOnTime} " +
                  $"public={previousPublic:F1}%->{snapshot.targetScore:F1}%");
    }

    private string FormatMidiNotes(GameNoteData note) => note == null
        ? "n/a"
        : string.Join(",", note.midi_notes is { Length: > 0 } ? note.midi_notes : new[] { note.GetMidiNote() });

    private void CaptureCurrentPerformanceSample()
    {
        if (!isGameActive) return;

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
        int removeCount = 0;
        while (removeCount < performanceWindow.Count && performanceWindow[removeCount].time < oldestAllowedTime)
            removeCount++;

        if (removeCount > 0) performanceWindow.RemoveRange(0, removeCount);
    }

    private float CalculateWindowAverage()
    {
        if (performanceWindow.Count == 0) return 0f;

        float total = 0f;
        for (int i = 0; i < performanceWindow.Count; i++)
            total += performanceWindow[i].combinedPerformance;

        return total / performanceWindow.Count;
    }

    private void RefreshAudienceProfile() => audienceProfile = PianoAudienceIntensityProfile.ResolveCurrentProfile();

    void OnDestroy()
    {
        if (gameplayScoring != null)
        {
            gameplayScoring.OnNoteEvaluated -= OnNoteEvaluatedCallback;
            gameplayScoring.OnGameFinished -= OnGameFinishedCallback;
        }
    }

    /// <summary>
    /// Engancha el controlador de audiencia de piano al gestor de la escena,
    /// desactivando el controlador heredado del modo canto si está presente.
    /// </summary>
    private void EnsureAudienceController()
    {
        GameObject gestorAudiencia = GameObject.Find("_GestorAudiencia");
        if (gestorAudiencia == null) return;

        ControladorAudiencia oldController = gestorAudiencia.GetComponent<ControladorAudiencia>();
        if (oldController != null) oldController.enabled = false;

        ControladorAudienciaPiano pianoController = gestorAudiencia.GetComponent<ControladorAudienciaPiano>()
                                                   ?? gestorAudiencia.AddComponent<ControladorAudienciaPiano>();

        pianoController.sistemaPublico = this;
        if (pianoController.jugador == null && Camera.main != null)
            pianoController.jugador = Camera.main.transform;
    }
}
