using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// Sistema de scoring que compara notas MIDI tocadas con las notas esperadas del JSON
/// Maneja: detección de sincronización, feedback visual, score final
/// </summary>
public class GameplayScoring : MonoBehaviour
{
    private static readonly Color LiveGuideColor = new Color(0.45f, 0.26f, 0.12f, 1f);

    private class ActivePressState
    {
        public float pressStartTime;
        public float lastProcessedTime;
    }

    [Header("Configuración de Timing")]
    [SerializeField] private float hitWindow = 0.15f; // Gracia para evaluar una nota después de terminar
    [SerializeField] private float minimumHoldForHit = 0.10f; // 10% mínimo de duración
    [SerializeField] private float perfectHoldThreshold = 0.80f;
    [SerializeField] private float simultaneousChordGrace = 0.045f;
    
    [Header("Referencias")]
    private PianoGameManager gameManager;
    private MidiAudioManager midiAudioManager;
    private StaffRenderer trebleStaff;
    private StaffRenderer bassStaff;
    private PianoPublicSystem publicSystem;
    
    // Estado del juego
    private PianoSongData currentSong;
    private float gameStartTime;
    private float currentGameTime;
    private bool isGameActive = false;
    
    // Sistema de scoring
    private List<GameNoteData> expectedNotes = new List<GameNoteData>();
    private Dictionary<int, GameNoteScore> noteScores = new Dictionary<int, GameNoteScore>(); // index -> score
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
    
    // Feedback visual
    private Dictionary<StaffRenderer, float> staffHitFeedbackTime = new Dictionary<StaffRenderer, float>();
    private Color hitLineOriginalColor;
    
    // Eventos
    public delegate void OnNoteHitDelegate(GameNoteData expected, bool perfect);
    public delegate void OnNoteMissedDelegate(GameNoteData expected);
    public delegate void OnNoteEvaluatedDelegate(GameNoteData expected, float normalizedScore, int successfulUnits, int totalUnits);
    public delegate void OnGameFinishedDelegate(GameplayResults results);
    
    public event OnNoteHitDelegate OnNoteHit;
    public event OnNoteMissedDelegate OnNoteMissed;
    public event OnNoteEvaluatedDelegate OnNoteEvaluated;
    public event OnGameFinishedDelegate OnGameFinished;
    
    /// <summary>
    /// Estructura para almacenar el resultado de una nota
    /// </summary>
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

    public float TotalPlayableNoteUnits => totalPlayableNoteUnits;
    public float HitPlayableNoteUnits => weightedHitPlayableNoteUnits;
    public float CurrentAccuracyPercent => totalPlayableNoteUnits > 0f ? (weightedHitPlayableNoteUnits / totalPlayableNoteUnits) * 100f : 0f;
    
    void Awake()
    {
        gameManager = GetComponent<PianoGameManager>();
        // staffHitFeedbackTime[null] = 0f; // Placeholder - ELIMINADO: Los diccionarios no permiten null como clave
    }
    
    void Start()
    {
        simultaneousChordGrace = Mathf.Max(simultaneousChordGrace, 0.12f);

        // Auto-detectar componentes
        if (midiAudioManager == null)
        {
            midiAudioManager = FindObjectOfType<MidiAudioManager>();
            Debug.Log($"<color=blue>[GameplayScoring]</color> 🔍 Buscando MidiAudioManager: {(midiAudioManager != null ? "✅ ENCONTRADO" : "❌ NO ENCONTRADO")}");
        }

        AssignStaffReferences();
        
        // AUTO-DETECTAR PIANO PUBLIC SYSTEM
        if (publicSystem == null)
        {
            publicSystem = FindObjectOfType<PianoPublicSystem>();
            if (publicSystem == null)
            {
                Debug.LogWarning("<color=yellow>[GameplayScoring]</color> ⚠️ PianoPublicSystem no encontrado, creando...");
                GameObject publicObj = new GameObject("PianoPublicSystem");
                publicSystem = publicObj.AddComponent<PianoPublicSystem>();
            }
            else
            {
                Debug.Log("<color=green>[GameplayScoring]</color> ✅ PianoPublicSystem auto-detectado");
            }
        }
        
        // Suscribirse a eventos MIDI
        if (midiAudioManager != null)
        {
            // ✅ Suscribirse a eventos de nota MIDI
            midiAudioManager.OnMidiNoteOn += ProcessMidiNoteOn;
            midiAudioManager.OnMidiNoteOff += ProcessMidiNoteOff;
            Debug.Log("[GameplayScoring] ✅ Suscrito a eventos MIDI (OnMidiNoteOn/Off)");
        }
        else
        {
            Debug.LogWarning("[GameplayScoring] ⚠️ MidiAudioManager no encontrado - Scoring NO detectará notas");
        }
        
        Debug.Log("[GameplayScoring] ✅ Sistema de scoring inicializado");
    }

    private void AssignStaffReferences()
    {
        StaffRenderer[] staffs = FindObjectsOfType<StaffRenderer>(true);
        for (int i = 0; i < staffs.Length; i++)
        {
            if (staffs[i] == null)
            {
                continue;
            }

            if (staffs[i].Type == StaffRenderer.StaffType.Treble)
            {
                trebleStaff = staffs[i];
                continue;
            }

            if (staffs[i].Type == StaffRenderer.StaffType.Bass)
            {
                bassStaff = staffs[i];
            }
        }
    }
    
    /// <summary>
    /// Inicializa el sistema para una nueva canción
    /// </summary>
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
        ClearLiveInputGuides();
        
        // Cargar notas esperadas desde all_notes
        if (song.all_notes != null)
        {
            expectedNotes = new List<GameNoteData>(song.all_notes);
            expectedNotes.Sort((a, b) => a.time.CompareTo(b.time));
            
            // Inicializar estructura de scoring
            for (int i = 0; i < expectedNotes.Count; i++)
            {
                GameNoteScore score = new GameNoteScore { expectedNote = expectedNotes[i] };
                int[] midiNotes = GetMidiNotes(expectedNotes[i]);
                foreach (int midiNote in midiNotes)
                {
                    score.heldDurations[midiNote] = 0f;
                }

                noteScores[i] = score;

                totalPlayableNoteUnits += midiNotes.Length;
            }
            
            // 🔍 DEBUG: Mostrar todas las notas esperadas
            Debug.Log($"<color=yellow>[GameplayScoring DEBUG]</color> 📋 {expectedNotes.Count} notas cargadas:");
            for (int i = 0; i < Mathf.Min(10, expectedNotes.Count); i++)
            {
                GameNoteData note = expectedNotes[i];
                string midiStr = note.midi_notes != null && note.midi_notes.Length > 0 
                    ? $"MIDI {string.Join(",", note.midi_notes)}" 
                    : "NO MIDI";
                Debug.Log($"  [{i}] {midiStr} @ time={note.time:F2}s, duration={note.duration:F2}s, clef={note.clef}");
            }
            if (expectedNotes.Count > 10)
            {
                Debug.Log($"  ... y {expectedNotes.Count - 10} notas más");
            }
            
            Debug.Log($"[GameplayScoring] 📋 {expectedNotes.Count} notas cargadas para scoring");
        }
        else
        {
            Debug.LogWarning("[GameplayScoring] ⚠️  No hay all_notes en la canción, scoring desactivado");
        }
    }
    
    /// <summary>
    /// Se llama cuando el juego comienza
    /// </summary>
    public void StartScoring()
    {
        gameStartTime = Time.time;
        currentGameTime = 0f;
        isGameActive = true;
        nextExpectedNoteIndex = 0;
        currentlyPressedNotes.Clear();
        activePressStates.Clear();
        ClearLiveInputGuides();
        
        // Iniciar sistema público
        if (publicSystem != null)
        {
            publicSystem.StartGame();
            Debug.Log("[GameplayScoring] 👥 Sistema público iniciado");
        }
        
        Debug.Log("[GameplayScoring] 🎮 Scoring iniciado");
    }
    
    /// <summary>
    /// Se llama cuando el juego pausa
    /// </summary>
    public void PauseScoring()
    {
        isGameActive = false;
        Debug.Log("[GameplayScoring] ⏸️ Scoring pausado");
    }
    
    /// <summary>
    /// Se llama cuando el juego reanuda
    /// </summary>
    public void ResumeScoring()
    {
        isGameActive = true;
        Debug.Log("[GameplayScoring] ▶️ Scoring reanudado");
    }
    
    void Update()
    {
        if (!isGameActive || currentSong == null) return;
        
        currentGameTime = GetCurrentSongTime();

        AccumulateHeldDurations();
        
        // Actualizar volumen de aplausos según público
        if (publicSystem != null && midiAudioManager != null)
        {
            midiAudioManager.SetApplauseVolume(publicSystem.GetCurrentPublicScoreForApplause());
        }
        
        // Verificar si el juego debe terminar
        float gameDuration = currentSong.GetGameDuration();
        if (currentGameTime >= gameDuration)
        {
            FinishGame();
            return;
        }
        
        // Detectar notas que se perdieron (pasó su ventana de hit)
        DetectMissedNotes();
        
        // Actualizar feedback visual
        UpdateHitLineFeedback();
    }

    private float GetCurrentSongTime()
    {
        if (gameManager == null)
        {
            gameManager = GetComponent<PianoGameManager>();
        }

        if (gameManager != null && gameManager.BackgroundMusicSource != null)
        {
            AudioSource source = gameManager.BackgroundMusicSource;
            if (source.isPlaying || source.time > 0f)
            {
                return source.time;
            }
        }

        return Time.time - gameStartTime;
    }
    
    /// <summary>
    /// Procesa cuando se toca una nota MIDI
    /// ARQUITECTURA NUEVA: Esta función SOLO registra el evento.
    /// La detección de HIT visual ocurre en MusicNote.Update() cuando la nota llega a la línea de hit mientras está siendo presionada.
    /// </summary>
    public void ProcessMidiNoteOn(int midiNote, int velocity)
    {
        if (!isGameActive)
        {
            return; // Ignorar MIDI si el juego no está activo
        }

        float noteOnTime = GetCurrentSongTime();
        currentlyPressedNotes.Add(midiNote);
        ShowLiveInputGuide(midiNote);
        bool matchedExpectedWindow = TrackOnsetTiming(midiNote, noteOnTime);

        if (!matchedExpectedWindow)
        {
            publicSystem?.OnWrongNoteDetected(midiNote);
        }

        if (!activePressStates.ContainsKey(midiNote))
        {
            activePressStates[midiNote] = new ActivePressState
            {
                pressStartTime = noteOnTime,
                lastProcessedTime = noteOnTime
            };
        }
        else
        {
            activePressStates[midiNote].pressStartTime = noteOnTime;
            activePressStates[midiNote].lastProcessedTime = noteOnTime;
        }
        
        Debug.Log($"<color=magenta>[GameplayScoring]</color> 🎹 MIDI {midiNote} PRESIONADO @ {currentGameTime:F3}s (vel={velocity})");
    }

    private bool TrackOnsetTiming(int midiNote, float noteOnTime)
    {
        bool matchedExpectedWindow = false;

        for (int i = nextExpectedNoteIndex; i < expectedNotes.Count; i++)
        {
            GameNoteData note = expectedNotes[i];

            if (note.time - hitWindow > noteOnTime)
            {
                break;
            }

            if (noteOnTime > note.time + note.duration + hitWindow)
            {
                continue;
            }

            if (!noteScores.TryGetValue(i, out GameNoteScore score) || score.wasEvaluated)
            {
                continue;
            }

            if (score.onsetOffsets.ContainsKey(midiNote))
            {
                continue;
            }

            int[] midiNotes = GetMidiNotes(note);
            if (!midiNotes.Contains(midiNote))
            {
                continue;
            }

            float earliestAcceptedTime = note.time - hitWindow;
            float latestAcceptedTime = note.time + Mathf.Max(hitWindow, simultaneousChordGrace);

            if (noteOnTime < earliestAcceptedTime || noteOnTime > latestAcceptedTime)
            {
                continue;
            }

            float onsetOffset = Mathf.Abs(noteOnTime - note.time);
            score.onsetOffsets[midiNote] = onsetOffset;
            matchedExpectedWindow = true;

            if (!score.liveReactionAwardedNotes.Contains(midiNote) && publicSystem != null)
            {
                score.liveReactionAwardedNotes.Add(midiNote);

                float onsetQuality = 1f - Mathf.Clamp01(onsetOffset / Mathf.Max(hitWindow, 0.0001f));
                publicSystem.OnLiveWindowMatched(note, onsetQuality, midiNotes.Length);
            }

            break;
        }

        return matchedExpectedWindow;
    }

    private void AccumulateHeldDurations()
    {
        if (expectedNotes.Count == 0)
        {
            return;
        }

        if (activePressStates.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<int, ActivePressState> pressedNote in activePressStates)
        {
            float intervalStart = pressedNote.Value.lastProcessedTime;
            float intervalEnd = currentGameTime;

            if (intervalEnd <= intervalStart)
            {
                continue;
            }

            AccumulateHeldDurationForMidiNote(pressedNote.Key, pressedNote.Value, intervalStart, intervalEnd);
            pressedNote.Value.lastProcessedTime = intervalEnd;
        }
    }

    private void AccumulateHeldDurationForMidiNote(int midiNote, ActivePressState pressState, float intervalStart, float intervalEnd)
    {
        for (int i = nextExpectedNoteIndex; i < expectedNotes.Count; i++)
        {
            GameNoteData note = expectedNotes[i];
            if (intervalEnd < note.time)
            {
                break;
            }

            if (intervalStart > note.time + note.duration)
            {
                continue;
            }

            if (!noteScores.TryGetValue(i, out GameNoteScore score) || score.wasEvaluated)
            {
                continue;
            }

            if (!score.heldDurations.ContainsKey(midiNote))
            {
                continue;
            }

            float effectiveIntervalStart = intervalStart;
            bool qualifiesForSimultaneousGrace =
                score.heldDurations[midiNote] <= 0.0001f &&
                pressState != null &&
                pressState.pressStartTime >= note.time &&
                pressState.pressStartTime - note.time <= simultaneousChordGrace;

            if (qualifiesForSimultaneousGrace)
            {
                effectiveIntervalStart = Mathf.Min(effectiveIntervalStart, note.time);
            }

            float overlapStart = Mathf.Max(effectiveIntervalStart, note.time);
            float overlapEnd = Mathf.Min(intervalEnd, note.time + note.duration);
            float overlap = Mathf.Max(0f, overlapEnd - overlapStart);

            if (overlap > 0f)
            {
                score.heldDurations[midiNote] += overlap;
            }
        }
    }
    
    /// <summary>
    /// Detecta notas que se perdieron (pasó el hit window sin tocarlas)
    /// </summary>
    private void DetectMissedNotes()
    {
        while (nextExpectedNoteIndex < expectedNotes.Count)
        {
            GameNoteData note = expectedNotes[nextExpectedNoteIndex];
            
            if (currentGameTime > note.time + note.duration + hitWindow)
            {
                FinalizeExpectedNote(nextExpectedNoteIndex);
                nextExpectedNoteIndex++;
            }
            else
            {
                break; // No hay más notas para marcar como perdidas por ahora
            }
        }
    }

    private void FinalizeExpectedNote(int noteIndex)
    {
        if (!noteScores.TryGetValue(noteIndex, out GameNoteScore score) || score.wasEvaluated)
        {
            return;
        }

        GameNoteData note = expectedNotes[noteIndex];
        int[] midiNotes = GetMidiNotes(note);
        int successfulUnits = 0;
        int perfectUnits = 0;
        float weightedUnits = 0f;
        float onsetQualityUnits = 0f;

        foreach (int midiNote in midiNotes)
        {
            float heldDuration = score.heldDurations.TryGetValue(midiNote, out float value) ? value : 0f;
            float holdRatio = note.duration > 0.0001f ? heldDuration / note.duration : (heldDuration > 0f ? 1f : 0f);
            holdRatio = Mathf.Clamp01(holdRatio);

            weightedUnits += holdRatio;
            totalDurationQualityUnits += holdRatio;

            float onsetOffset = score.onsetOffsets.TryGetValue(midiNote, out float storedOffset)
                ? storedOffset
                : hitWindow;
            float onsetQuality = 1f - Mathf.Clamp01(onsetOffset / Mathf.Max(hitWindow, 0.0001f));
            onsetQualityUnits += onsetQuality;
            totalOnsetQualityUnits += onsetQuality;

            if (holdRatio >= minimumHoldForHit)
            {
                successfulUnits++;
            }

            if (holdRatio >= perfectHoldThreshold)
            {
                perfectUnits++;
            }
        }

        score.successfulUnits = successfulUnits;
        score.perfectUnits = perfectUnits;
        score.weightedUnits = weightedUnits;
        score.wasHit = weightedUnits > 0f;
        score.wasPerfect = successfulUnits == midiNotes.Length && perfectUnits == midiNotes.Length;
        score.wasEvaluated = true;

        float normalizedScore = midiNotes.Length > 0 ? weightedUnits / midiNotes.Length : 0f;
        evaluatedPlayableNoteUnits += midiNotes.Length;
        weightedHitPlayableNoteUnits += weightedUnits;
        totalSuccessfulPlayableNoteUnits += successfulUnits;

        if (midiNotes.Length > 1)
        {
            chordCoverageAccumulated += normalizedScore;
            totalChordEvents++;
        }

        OnNoteEvaluated?.Invoke(note, normalizedScore, successfulUnits, midiNotes.Length);

        if (weightedUnits > 0f)
        {
            perfectPlayableNoteUnits += perfectUnits;
            TriggerHitFeedback(note, score.wasPerfect);
            Debug.Log($"[GameplayScoring] ✅ HIT ponderado {weightedUnits:F2}/{midiNotes.Length} | MIDI {string.Join(",", midiNotes)} | t={note.time:F3}s");
            OnNoteHit?.Invoke(note, score.wasPerfect);
            return;
        }

        ApplyVisualFeedbackToNoteStaffs(note, staff => staff.SetHitLineError());
        Debug.Log($"[GameplayScoring] ❌ MISS | MIDI {string.Join(",", midiNotes)} | t={note.time:F3}s");
        OnNoteMissed?.Invoke(note);
    }

    private int[] GetMidiNotes(GameNoteData note)
    {
        if (note.midi_notes != null && note.midi_notes.Length > 0)
        {
            return note.midi_notes;
        }

        return new[] { note.GetMidiNote() };
    }
    
    /// <summary>
    /// Feedback visual cuando acierta (la línea amarilla se pone verde)
    /// </summary>
    private void TriggerHitFeedback(GameNoteData note, bool isPerfect)
    {
        ApplyVisualFeedbackToNoteStaffs(note, staff => staffHitFeedbackTime[staff] = Time.time);
        Debug.Log($"[GameplayScoring] 💚 Feedback visual activado para MIDI {string.Join(",", GetMidiNotes(note))}");
    }
    
    /// <summary>
    /// Actualiza el feedback visual (línea de hit cambia de color)
    /// </summary>
    private void UpdateHitLineFeedback()
    {
        if (trebleStaff != null)
        {
            UpdateStaffHitLineFeedback(trebleStaff);
        }
        
        if (bassStaff != null)
        {
            UpdateStaffHitLineFeedback(bassStaff);
        }
    }
    
    private void UpdateStaffHitLineFeedback(StaffRenderer staff)
    {
        if (!staffHitFeedbackTime.ContainsKey(staff)) return;
        
        float feedbackDuration = 0.3f; // Duración del feedback en verde
        float timeSinceFeedback = Time.time - staffHitFeedbackTime[staff];
        
        if (timeSinceFeedback < feedbackDuration)
        {
            // Está en verde, no hacer nada (el color ya se cambió)
            return;
        }
        else
        {
            // Volver a amarillo
            staffHitFeedbackTime[staff] = 0f;
        }
    }
    
    /// <summary>
    /// Genera el resultado final del juego
    /// </summary>
    public GameplayResults CalculateFinalScore()
    {
        float accuracy = expectedNotes.Count > 0 
            ? CurrentAccuracyPercent
            : 0f;

        float noteCoverage = totalPlayableNoteUnits > 0f
            ? (totalSuccessfulPlayableNoteUnits / totalPlayableNoteUnits) * 100f
            : 0f;

        float chordCoverage = totalChordEvents > 0
            ? (chordCoverageAccumulated / totalChordEvents) * 100f
            : noteCoverage;

        float harmony = Mathf.Clamp(0.65f * noteCoverage + 0.35f * chordCoverage, 0f, 100f);

        float onsetTiming = totalPlayableNoteUnits > 0f
            ? (totalOnsetQualityUnits / totalPlayableNoteUnits) * 100f
            : 0f;

        float durationTiming = totalPlayableNoteUnits > 0f
            ? (totalDurationQualityUnits / totalPlayableNoteUnits) * 100f
            : 0f;

        float rhythm = Mathf.Clamp(0.75f * onsetTiming + 0.25f * durationTiming, 0f, 100f);
        float global = Mathf.Clamp(0.6f * harmony + 0.4f * rhythm, 0f, 100f);
        
        GameplayResults results = new GameplayResults
        {
            song_name = currentSong.song_name ?? currentSong.song_title,
            total_notes = totalPlayableNoteUnits,
            notes_hit = weightedHitPlayableNoteUnits,
            perfect_notes = perfectPlayableNoteUnits,
            notes_missed = Mathf.Max(totalPlayableNoteUnits - weightedHitPlayableNoteUnits, 0f),
            accuracy_percentage = accuracy,
            note_coverage_percentage = noteCoverage,
            chord_coverage_percentage = chordCoverage,
            onset_timing_percentage = onsetTiming,
            duration_timing_percentage = durationTiming,
            harmony_percentage = harmony,
            rhythm_percentage = rhythm,
            global_percentage = global,
            game_duration = currentGameTime,
            timestamp = System.DateTime.Now
        };
        
        return results;
    }

    public float GetLiveHarmonyPercentage()
    {
        float noteCoverage = evaluatedPlayableNoteUnits > 0f
            ? (totalSuccessfulPlayableNoteUnits / evaluatedPlayableNoteUnits) * 100f
            : 0f;

        float chordCoverage = totalChordEvents > 0
            ? (chordCoverageAccumulated / totalChordEvents) * 100f
            : noteCoverage;

        return Mathf.Clamp(0.65f * noteCoverage + 0.35f * chordCoverage, 0f, 100f);
    }

    public float GetLiveRhythmPercentage()
    {
        float onsetTiming = evaluatedPlayableNoteUnits > 0f
            ? (totalOnsetQualityUnits / evaluatedPlayableNoteUnits) * 100f
            : 0f;

        float durationTiming = evaluatedPlayableNoteUnits > 0f
            ? (totalDurationQualityUnits / evaluatedPlayableNoteUnits) * 100f
            : 0f;

        return Mathf.Clamp(0.75f * onsetTiming + 0.25f * durationTiming, 0f, 100f);
    }
    
    /// <summary>
    /// Termina el juego
    /// </summary>
    private void FinishGame()
    {
        isGameActive = false;
        
        // Terminar sistema público
        if (publicSystem != null)
        {
            publicSystem.EndGame();
            publicSystem.LogStatistics();
        }
        
        GameplayResults results = CalculateFinalScore();
        
        Debug.Log($"[GameplayScoring] 🏁 JUEGO TERMINADO");
        Debug.Log($"[GameplayScoring] 📊 Resultado: {results.notes_hit}/{results.total_notes} aciertos ({results.accuracy_percentage:F1}%)");
        Debug.Log($"[GameplayScoring] 🟢 Perfectos: {results.perfect_notes} | ❌ Perdidas: {results.notes_missed}");
        
        OnGameFinished?.Invoke(results);
    }
    
    /// <summary>
    /// Se llama cuando el usuario suelta una tecla MIDI (nota off)
    /// </summary>
    private void ProcessMidiNoteOff(int midiNote, int velocity)
    {
        float noteOffTime = GetCurrentSongTime();

        if (activePressStates.TryGetValue(midiNote, out ActivePressState pressState))
        {
            AccumulateHeldDurationForMidiNote(midiNote, pressState, pressState.lastProcessedTime, noteOffTime);
            activePressStates.Remove(midiNote);
        }

        currentlyPressedNotes.Remove(midiNote);
        HideLiveInputGuide(midiNote);
        Debug.Log($"<color=orange>[GameplayScoring DEBUG]</color> 🔲 MIDI {midiNote} soltado @ {currentGameTime:F3}s");
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

    private void ShowLiveInputGuide(int midiNote)
    {
        StaffRenderer targetStaff = GetGuideStaffForMidiNote(midiNote);
        if (targetStaff == null)
        {
            return;
        }

        targetStaff.ShowLiveInputIndicator(midiNote, LiveGuideColor);
        activeLiveGuides[midiNote] = targetStaff;
    }

    private void HideLiveInputGuide(int midiNote)
    {
        if (activeLiveGuides.TryGetValue(midiNote, out StaffRenderer targetStaff))
        {
            targetStaff.HideLiveInputIndicator(midiNote);
            activeLiveGuides.Remove(midiNote);
            return;
        }

        GetGuideStaffForMidiNote(midiNote)?.HideLiveInputIndicator(midiNote);
    }

    private void ClearLiveInputGuides()
    {
        trebleStaff?.ClearLiveInputIndicators();
        bassStaff?.ClearLiveInputIndicators();
        activeLiveGuides.Clear();
    }

    private StaffRenderer GetGuideStaffForMidiNote(int midiNote)
    {
        StaffRenderer targetStaff = FindExpectedGuideStaff(midiNote, currentGameTime);
        if (targetStaff != null)
        {
            return targetStaff;
        }

        if (midiNote >= 60)
        {
            return trebleStaff != null ? trebleStaff : bassStaff;
        }

        return bassStaff != null ? bassStaff : trebleStaff;
    }

    private StaffRenderer FindExpectedGuideStaff(int midiNote, float songTime)
    {
        if (expectedNotes.Count == 0)
        {
            return null;
        }

        float searchBefore = Mathf.Max(hitWindow, simultaneousChordGrace);
        float searchAfter = Mathf.Max(hitWindow * 2f, 0.25f);
        int startIndex = Mathf.Max(0, nextExpectedNoteIndex - 8);

        StaffRenderer bestStaff = null;
        float bestDistance = float.MaxValue;

        for (int i = startIndex; i < expectedNotes.Count; i++)
        {
            GameNoteData note = expectedNotes[i];

            if (note.time > songTime + searchAfter)
            {
                break;
            }

            if (songTime < note.time - searchBefore || songTime > note.time + note.duration + searchAfter)
            {
                continue;
            }

            int[] midiNotes = GetMidiNotes(note);
            if (!midiNotes.Contains(midiNote))
            {
                continue;
            }

            StaffRenderer noteStaff = GetStaffForMidiNote(midiNote);
            if (noteStaff == null)
            {
                continue;
            }

            float distance = Mathf.Abs(note.time - songTime);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestStaff = noteStaff;
            }
        }

        return bestStaff;
    }

    private StaffRenderer GetStaffForMidiNote(int midiNote)
    {
        if (midiNote >= 60)
        {
            return trebleStaff != null ? trebleStaff : bassStaff;
        }

        return bassStaff != null ? bassStaff : trebleStaff;
    }

    private void ApplyVisualFeedbackToNoteStaffs(GameNoteData note, Action<StaffRenderer> feedbackAction)
    {
        int[] midiNotes = GetMidiNotes(note);
        bool appliedTreble = false;
        bool appliedBass = false;

        for (int i = 0; i < midiNotes.Length; i++)
        {
            StaffRenderer targetStaff = GetStaffForMidiNote(midiNotes[i]);
            if (targetStaff == null)
            {
                continue;
            }

            if (targetStaff == trebleStaff)
            {
                if (appliedTreble)
                {
                    continue;
                }

                appliedTreble = true;
            }
            else if (targetStaff == bassStaff)
            {
                if (appliedBass)
                {
                    continue;
                }

                appliedBass = true;
            }

            feedbackAction(targetStaff);
        }
    }
}

/// <summary>
/// Estructura que guarda los resultados finales del juego
/// </summary>
[System.Serializable]
public class GameplayResults
{
    public string song_name;
    public string mode_name;
    public float total_notes;
    public float notes_hit;
    public int perfect_notes;
    public float notes_missed;
    public float accuracy_percentage;
    public float note_coverage_percentage;
    public float chord_coverage_percentage;
    public float onset_timing_percentage;
    public float duration_timing_percentage;
    public float harmony_percentage;
    public float rhythm_percentage;
    public float global_percentage;
    public float game_duration;
    public System.DateTime timestamp;
}
