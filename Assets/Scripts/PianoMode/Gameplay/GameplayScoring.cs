using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Sistema de scoring que compara las notas MIDI tocadas con las esperadas del JSON.
/// Se reparte en varios archivos parciales: Input, Evaluation, Feedback y Diagnostics.
/// </summary>
public partial class GameplayScoring : MonoBehaviour
{
    private class PendingFeedbackLatencyEvent
    {
        public System.DateTimeOffset receivedAt;
        public bool isCorrect;
    }

    private class ActivePressState
    {
        public float pressStartTime;
        public float lastProcessedTime;
    }

    /// <summary>Resultado acumulado de una nota (o acorde) esperada.</summary>
    public class GameNoteScore
    {
        public GameNoteData expectedNote;
        public bool wasHit = false;
        public bool wasPerfect = false;
        public bool wasEvaluated = false;
        public int successfulUnits = 0;
        public int perfectUnits = 0;
        public float weightedUnits = 0f;
        public Dictionary<int, float> heldDurations = new Dictionary<int, float>();
        public Dictionary<int, float> onsetOffsets = new Dictionary<int, float>();
        public HashSet<int> liveReactionAwardedNotes = new HashSet<int>();
    }

    [Header("Configuración de Timing")]
    [Tooltip("Gracia para evaluar una nota después de terminar")]
    [SerializeField] private float hitWindow = 0.18f;
    [Tooltip("Fracción mínima de la duración que debe sostenerse para contar como acierto")]
    [SerializeField] private float minimumHoldForHit = 0.10f;
    [SerializeField] private float perfectHoldThreshold = 0.80f;
    [SerializeField] private float simultaneousChordGrace = 0.045f;
    [SerializeField] private float rhythmScoringWindow = 0.24f;
    [SerializeField] private float perfectTimingWindow = 0.04f;
    [Range(0f, 1f)]
    [SerializeField] private float onsetWeightInRhythm = 0.65f;

    [Header("Debug")]
    [SerializeField] private bool enableHarmonyAnalysisDebugLogs = true;

    private PianoGameManager gameManager;
    private MidiAudioManager midiAudioManager;
    private StaffRenderer trebleStaff;
    private StaffRenderer bassStaff;
    private PianoPublicSystem publicSystem;

    private PianoSongData currentSong;
    private float gameStartTime;
    private float currentGameTime;
    private bool isGameActive = false;

    private List<GameNoteData> expectedNotes = new List<GameNoteData>();
    private Dictionary<int, GameNoteScore> noteScores = new Dictionary<int, GameNoteScore>();
    private int nextExpectedNoteIndex = 0;
    private float totalPlayableNoteUnits = 0f;
    private float evaluatedPlayableNoteUnits = 0f;
    private float weightedHitPlayableNoteUnits = 0f;
    private float totalSuccessfulPlayableNoteUnits = 0f;
    private int perfectPlayableNoteUnits = 0;
    private float totalOnsetQualityUnits = 0f;
    private float totalDurationQualityUnits = 0f;
    private float chordCoverageAccumulated = 0f;
    private int totalChordEvents = 0;

    private readonly HashSet<int> currentlyPressedNotes = new HashSet<int>();
    private readonly Dictionary<int, ActivePressState> activePressStates = new Dictionary<int, ActivePressState>();
    private readonly Dictionary<int, StaffRenderer> activeLiveGuides = new Dictionary<int, StaffRenderer>();
    private readonly Dictionary<int, Queue<PendingFeedbackLatencyEvent>> pendingFeedbackLatencyByMidiNote =
        new Dictionary<int, Queue<PendingFeedbackLatencyEvent>>();
    private readonly Dictionary<StaffRenderer, float> staffHitFeedbackTime = new Dictionary<StaffRenderer, float>();

    public delegate void OnNoteHitDelegate(GameNoteData expected, bool perfect);
    public delegate void OnNoteMissedDelegate(GameNoteData expected);
    public delegate void OnNoteEvaluatedDelegate(GameNoteData expected, float normalizedScore, int successfulUnits, int totalUnits);
    public delegate void OnGameFinishedDelegate(GameplayResults results);

    public event OnNoteHitDelegate OnNoteHit;
    public event OnNoteMissedDelegate OnNoteMissed;
    public event OnNoteEvaluatedDelegate OnNoteEvaluated;
    public event OnGameFinishedDelegate OnGameFinished;

    public float TotalPlayableNoteUnits => totalPlayableNoteUnits;
    public float HitPlayableNoteUnits => weightedHitPlayableNoteUnits;
    public float CurrentAccuracyPercent =>
        totalPlayableNoteUnits > 0f ? (weightedHitPlayableNoteUnits / totalPlayableNoteUnits) * 100f : 0f;

    void Awake() => gameManager = GetComponent<PianoGameManager>();

    void Start()
    {
        // Estos mínimos son deliberados: acotan valores del inspector demasiado estrictos para VR.
        hitWindow = Mathf.Max(hitWindow, 0.18f);
        simultaneousChordGrace = Mathf.Max(simultaneousChordGrace, 0.12f);
        rhythmScoringWindow = Mathf.Max(rhythmScoringWindow, hitWindow);
        perfectTimingWindow = Mathf.Clamp(perfectTimingWindow, 0.01f, rhythmScoringWindow);
        onsetWeightInRhythm = Mathf.Clamp01(onsetWeightInRhythm);

        if (midiAudioManager == null) midiAudioManager = FindObjectOfType<MidiAudioManager>();

        AssignStaffReferences();

        if (publicSystem == null)
        {
            publicSystem = FindObjectOfType<PianoPublicSystem>();
            if (publicSystem == null)
            {
                Debug.LogWarning("[GameplayScoring] PianoPublicSystem no encontrado, creando...");
                publicSystem = new GameObject("PianoPublicSystem").AddComponent<PianoPublicSystem>();
            }
        }

        if (midiAudioManager != null)
        {
            midiAudioManager.OnMidiNoteOn += ProcessMidiNoteOn;
            midiAudioManager.OnMidiNoteOff += ProcessMidiNoteOff;
        }
        else
        {
            Debug.LogWarning("[GameplayScoring] MidiAudioManager no encontrado - Scoring NO detectará notas");
        }
    }

    private void AssignStaffReferences()
    {
        foreach (StaffRenderer staff in FindObjectsOfType<StaffRenderer>(true))
        {
            if (staff == null) continue;

            if (staff.Type == StaffRenderer.StaffType.Treble) trebleStaff = staff;
            else if (staff.Type == StaffRenderer.StaffType.Bass) bassStaff = staff;
        }
    }

    /// <summary>Prepara el sistema para una nueva canción.</summary>
    public void InitializeForSong(PianoSongData song)
    {
        currentSong = song;
        expectedNotes.Clear();
        noteScores.Clear();
        nextExpectedNoteIndex = 0;
        totalPlayableNoteUnits = 0f;
        evaluatedPlayableNoteUnits = 0f;
        weightedHitPlayableNoteUnits = 0f;
        totalSuccessfulPlayableNoteUnits = 0f;
        perfectPlayableNoteUnits = 0;
        totalOnsetQualityUnits = 0f;
        totalDurationQualityUnits = 0f;
        chordCoverageAccumulated = 0f;
        totalChordEvents = 0;
        currentlyPressedNotes.Clear();
        activePressStates.Clear();
        pendingFeedbackLatencyByMidiNote.Clear();
        ClearLiveInputGuides();

        if (song.all_notes == null)
        {
            Debug.LogWarning("[GameplayScoring] No hay all_notes en la canción, scoring desactivado");
            return;
        }

        expectedNotes = new List<GameNoteData>(song.all_notes);
        expectedNotes.Sort((a, b) => a.time.CompareTo(b.time));

        for (int i = 0; i < expectedNotes.Count; i++)
        {
            GameNoteScore score = new GameNoteScore { expectedNote = expectedNotes[i] };
            int[] midiNotes = GetMidiNotes(expectedNotes[i]);

            foreach (int midiNote in midiNotes)
                score.heldDurations[midiNote] = 0f;

            noteScores[i] = score;
            totalPlayableNoteUnits += midiNotes.Length;
        }
    }

    public void StartScoring()
    {
        gameStartTime = Time.time;
        currentGameTime = 0f;
        isGameActive = true;
        nextExpectedNoteIndex = 0;
        currentlyPressedNotes.Clear();
        activePressStates.Clear();
        pendingFeedbackLatencyByMidiNote.Clear();
        ClearLiveInputGuides();

        if (publicSystem != null) publicSystem.StartGame();
    }

    public void PauseScoring() => isGameActive = false;

    public void ResumeScoring() => isGameActive = true;

    void Update()
    {
        if (!isGameActive || currentSong == null) return;

        currentGameTime = GetCurrentSongTime();

        AccumulateHeldDurations();

        if (publicSystem != null && midiAudioManager != null)
            midiAudioManager.SetApplauseVolume(publicSystem.GetCurrentPublicScoreForApplause());

        if (currentGameTime >= currentSong.GetGameDuration())
        {
            FinishGame();
            return;
        }

        DetectMissedNotes();
        UpdateHitLineFeedback();
    }

    private float GetCurrentSongTime()
    {
        if (gameManager == null) gameManager = GetComponent<PianoGameManager>();

        AudioSource source = gameManager != null ? gameManager.BackgroundMusicSource : null;
        if (source != null && (source.isPlaying || source.time > 0f)) return source.time;

        return Time.time - gameStartTime;
    }

    private int[] GetMidiNotes(GameNoteData note) =>
        note.midi_notes is { Length: > 0 } ? note.midi_notes : new[] { note.GetMidiNote() };

    private void FinishGame()
    {
        isGameActive = false;

        if (publicSystem != null)
        {
            publicSystem.EndGame();
            publicSystem.LogStatistics();
        }

        OnGameFinished?.Invoke(CalculateFinalScore());
    }

    void OnDestroy()
    {
        ClearLiveInputGuides();

        if (midiAudioManager != null)
        {
            midiAudioManager.OnMidiNoteOn -= ProcessMidiNoteOn;
            midiAudioManager.OnMidiNoteOff -= ProcessMidiNoteOff;
        }

        OnNoteEvaluated = null;
    }
}
