using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Sistema de scoring que compara notas MIDI tocadas con las notas esperadas del JSON
/// Maneja: detección de sincronización, feedback visual, score final
/// </summary>
public class GameplayScoring : MonoBehaviour
{
    [Header("Configuración de Timing")]
    [SerializeField] private float hitWindow = 0.15f; // Gracia para evaluar una nota después de terminar
    [SerializeField] private float minimumHoldForHit = 0.10f; // 10% mínimo de duración
    [SerializeField] private float perfectHoldThreshold = 0.80f;
    
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
    private int totalPlayableNoteUnits = 0;
    private int hitPlayableNoteUnits = 0;
    private int perfectPlayableNoteUnits = 0;
    private readonly HashSet<int> currentlyPressedNotes = new HashSet<int>();
    
    // Feedback visual
    private Dictionary<StaffRenderer, float> staffHitFeedbackTime = new Dictionary<StaffRenderer, float>();
    private Color hitLineOriginalColor;
    
    // Eventos
    public delegate void OnNoteHitDelegate(GameNoteData expected, bool perfect);
    public delegate void OnNoteMissedDelegate(GameNoteData expected);
    public delegate void OnGameFinishedDelegate(GameplayResults results);
    
    public event OnNoteHitDelegate OnNoteHit;
    public event OnNoteMissedDelegate OnNoteMissed;
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
        public Dictionary<int, float> heldDurations = new Dictionary<int, float>();
    }

    public int TotalPlayableNoteUnits => totalPlayableNoteUnits;
    public int HitPlayableNoteUnits => hitPlayableNoteUnits;
    public float CurrentAccuracyPercent => totalPlayableNoteUnits > 0 ? (hitPlayableNoteUnits / (float)totalPlayableNoteUnits) * 100f : 0f;
    
    void Awake()
    {
        gameManager = GetComponent<PianoGameManager>();
        // staffHitFeedbackTime[null] = 0f; // Placeholder - ELIMINADO: Los diccionarios no permiten null como clave
    }
    
    void Start()
    {
        // Auto-detectar componentes
        if (midiAudioManager == null)
        {
            midiAudioManager = FindObjectOfType<MidiAudioManager>();
            Debug.Log($"<color=blue>[GameplayScoring]</color> 🔍 Buscando MidiAudioManager: {(midiAudioManager != null ? "✅ ENCONTRADO" : "❌ NO ENCONTRADO")}");
        }
            
        if (trebleStaff == null)
            trebleStaff = FindObjectOfType<StaffRenderer>(true); // Incluir inactivos
            
        if (bassStaff == null)
        {
            StaffRenderer[] staffs = FindObjectsOfType<StaffRenderer>();
            if (staffs.Length > 1)
                bassStaff = staffs[1];
        }
        
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
    
    /// <summary>
    /// Inicializa el sistema para una nueva canción
    /// </summary>
    public void InitializeForSong(PianoSongData song)
    {
        currentSong = song;
        expectedNotes.Clear();
        noteScores.Clear();
        nextExpectedNoteIndex = 0;
        totalPlayableNoteUnits = 0;
        hitPlayableNoteUnits = 0;
        perfectPlayableNoteUnits = 0;
        currentlyPressedNotes.Clear();
        
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
            midiAudioManager.SetApplauseVolume(publicSystem.GetCurrentPublicScore());
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

        currentlyPressedNotes.Add(midiNote);
        
        Debug.Log($"<color=magenta>[GameplayScoring]</color> 🎹 MIDI {midiNote} PRESIONADO @ {currentGameTime:F3}s (vel={velocity})");
    }

    private void AccumulateHeldDurations()
    {
        if (expectedNotes.Count == 0)
        {
            return;
        }

        for (int i = nextExpectedNoteIndex; i < expectedNotes.Count; i++)
        {
            GameNoteData note = expectedNotes[i];
            if (currentGameTime < note.time)
            {
                break;
            }

            if (currentGameTime > note.time + note.duration)
            {
                continue;
            }

            if (!noteScores.TryGetValue(i, out GameNoteScore score) || score.wasEvaluated)
            {
                continue;
            }

            foreach (int midiNote in GetMidiNotes(note))
            {
                if (currentlyPressedNotes.Contains(midiNote))
                {
                    score.heldDurations[midiNote] += Time.deltaTime;
                }
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

        foreach (int midiNote in midiNotes)
        {
            float heldDuration = score.heldDurations.TryGetValue(midiNote, out float value) ? value : 0f;
            float holdRatio = note.duration > 0.0001f ? heldDuration / note.duration : (heldDuration > 0f ? 1f : 0f);

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
        score.wasHit = successfulUnits > 0;
        score.wasPerfect = successfulUnits == midiNotes.Length && perfectUnits == midiNotes.Length;
        score.wasEvaluated = true;

        if (successfulUnits > 0)
        {
            hitPlayableNoteUnits += successfulUnits;
            perfectPlayableNoteUnits += perfectUnits;
            TriggerHitFeedback(note, score.wasPerfect);
            Debug.Log($"[GameplayScoring] ✅ HIT {successfulUnits}/{midiNotes.Length} | MIDI {string.Join(",", midiNotes)} | t={note.time:F3}s");
            OnNoteHit?.Invoke(note, score.wasPerfect);
            return;
        }

        StaffRenderer targetStaff = note.clef == "treble" ? trebleStaff : bassStaff;
        targetStaff?.SetHitLineError();
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
        StaffRenderer targetStaff = note.clef == "treble" ? trebleStaff : bassStaff;
        if (targetStaff == null) return;
        
        // Cambiar color de la línea de hit a verde brevemente
        staffHitFeedbackTime[targetStaff] = Time.time;
        
        Debug.Log($"[GameplayScoring] 💚 Feedback visual activado para {note.clef}");
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
        
        GameplayResults results = new GameplayResults
        {
            song_name = currentSong.song_name ?? currentSong.song_title,
            total_notes = totalPlayableNoteUnits,
            notes_hit = hitPlayableNoteUnits,
            perfect_notes = perfectPlayableNoteUnits,
            notes_missed = Mathf.Max(totalPlayableNoteUnits - hitPlayableNoteUnits, 0),
            accuracy_percentage = accuracy,
            game_duration = currentGameTime,
            timestamp = System.DateTime.Now
        };
        
        return results;
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
        currentlyPressedNotes.Remove(midiNote);
        Debug.Log($"<color=orange>[GameplayScoring DEBUG]</color> 🔲 MIDI {midiNote} soltado @ {currentGameTime:F3}s");
    }

    void OnDestroy()
    {
        if (midiAudioManager != null)
        {
            midiAudioManager.OnMidiNoteOn -= ProcessMidiNoteOn;
            midiAudioManager.OnMidiNoteOff -= ProcessMidiNoteOff;
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
    public int total_notes;
    public int notes_hit;
    public int perfect_notes;
    public int notes_missed;
    public float accuracy_percentage;
    public float game_duration;
    public System.DateTime timestamp;
}
