using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Muestra el porcentaje de progreso EN VIVO + notas esperadas en tiempo real
/// Permite sincronización visual comparando las notas esperadas con el pentagrama
/// </summary>
public class ProgressDisplay : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Image progressBar;
    
    private float totalNotesCount = 0f;
    private float correctNotesCount = 0f;
    private float currentPercentage = 0f;
    private bool totalsInitialized = false;
    
    private PianoGameManager gameManager;
    private GameplayScoring gameplayScoring;
    private float gameStartTime = 0f;
    
    void Start()
    {
        //Buscar TextMeshProUGUI automáticamente si no está asignado
        if (progressText == null)
            progressText = GetComponent<TextMeshProUGUI>();
        
        if (progressBar == null)
            progressBar = GetComponentInChildren<Image>();
        
        // Obtener referencia al PianoGameManager
        gameManager = PianoGameManager.Instance;
        gameStartTime = Time.time;
        
        // Conectarse a eventos de GameplayScoring
        gameplayScoring = FindObjectOfType<GameplayScoring>();
        if (gameplayScoring != null)
        {
            gameplayScoring.OnNoteHit += OnNoteHit;
            gameplayScoring.OnNoteMissed += OnNoteMissed;
            Debug.Log("<color=green>[ProgressDisplay]</color> ✅ Conectado a GameplayScoring");
        }
        else
        {
            Debug.LogWarning("<color=yellow>[ProgressDisplay]</color> ⚠️ No se encontró GameplayScoring");
        }
        
        UpdateDisplay();
    }
    
    void Update()
    {
        // Actualizar constantemente para ver las notas esperadas en tiempo real
        UpdateDisplay();
    }
    
    private void OnNoteHit(GameNoteData note, bool isPerfect)
    {
        Debug.Log($"<color=cyan>[Progress]</color> ✅ Nota quemada: {correctNotesCount}/{totalNotesCount} ({currentPercentage:F0}%)");
    }
    
    private void OnNoteMissed(GameNoteData note)
    {
        Debug.Log($"<color=red>[Progress]</color> ❌ Nota fallida: {correctNotesCount}/{totalNotesCount} ({currentPercentage:F0}%)");
    }

    private void EnsureTotalsInitialized()
    {
        if (totalsInitialized || gameManager == null || gameManager.currentSongData == null)
            return;

        totalNotesCount = 0f;

        if (gameManager.currentSongData.all_notes != null)
        {
            foreach (GameNoteData note in gameManager.currentSongData.all_notes)
            {
                if (note.midi_notes != null && note.midi_notes.Length > 0)
                    totalNotesCount += note.midi_notes.Length;
                else
                    totalNotesCount += 1f;
            }
        }

        totalsInitialized = true;
    }
    
    private void UpdateDisplay()
    {
        EnsureTotalsInitialized();

        if (gameplayScoring != null)
        {
            totalNotesCount = gameplayScoring.TotalPlayableNoteUnits;
            correctNotesCount = gameplayScoring.HitPlayableNoteUnits;
            currentPercentage = gameplayScoring.CurrentAccuracyPercent;
        }

        if (gameplayScoring == null && totalNotesCount > 0)
            currentPercentage = (correctNotesCount / totalNotesCount) * 100f;
        else if (gameplayScoring == null)
            currentPercentage = 0f;
        
        // Construir texto con:
        // 1. Porcentaje
        // 2. Notas esperadas AHORA (treble/mano derecha)
        // 3. Tiempo restante de la nota actual
        
        string displayText = $"<size=50>{currentPercentage:F0}%</size>\n\n";
        
        // Obtener notas esperadas en tiempo real
        List<GameNoteData> currentNotes = GetCurrentTrebleNotes();
        
        if (currentNotes.Count > 0)
        {
            displayText += "<color=yellow>ESPERANDO:</color>\n";
            foreach (GameNoteData note in currentNotes)
            {
                // Concatenar MIDI notes del acorde
                string midiString = "";
                if (note.midi_notes != null)
                {
                    foreach (int midi in note.midi_notes)
                    {
                        midiString += MidiNumberToNoteName(midi) + " ";
                    }
                }
                else
                {
                    midiString = "---";
                }
                
                // Calcular tiempo restante de esta nota
                float currentGameTime = 0f;
                if (gameManager != null && gameManager.BackgroundMusicSource != null && gameManager.BackgroundMusicSource.isPlaying)
                {
                    currentGameTime = gameManager.BackgroundMusicSource.time;
                }
                else if (gameManager != null && gameManager.isPlaying)
                {
                    currentGameTime = gameManager.gameTime;
                }
                    
                float noteEndTime = note.time + note.duration;
                float timeRemaining = noteEndTime - currentGameTime;
                
                // Mostrar MIDI notes y countdown
                displayText += $"<color=cyan>{midiString}</color> [<color=red>{timeRemaining:F1}s</color>]\n";
            }
        }
        else
        {
            // Si no hay notas esperadas, mostrar siguiente n notas
            displayText += "<color=gray>--- Esperando notas ---</color>\n";
        }
        
        // Actualizar texto
        if (progressText != null)
            progressText.text = displayText;
        
        // Actualizar barra
        if (progressBar != null)
            progressBar.fillAmount = currentPercentage / 100f;
    }
    
    /// <summary>
    /// Obtiene las notas de TREBLE que están siendo esperadas AHORA
    /// </summary>
    private List<GameNoteData> GetCurrentTrebleNotes()
    {
        List<GameNoteData> result = new List<GameNoteData>();
        
        if (gameManager == null || gameManager.currentSongData == null)
            return result;
        
        // 🎵 Usar AUDIO TIME como fuente de verdad (perfectamente sincronizado)
        float currentGameTime = 0f;
        if (gameManager.BackgroundMusicSource != null && gameManager.BackgroundMusicSource.isPlaying)
        {
            currentGameTime = gameManager.BackgroundMusicSource.time;
        }
        else if (gameManager.isPlaying)
        {
            currentGameTime = gameManager.gameTime;
        }
        
        // Buscar en all_notes las que estén activas AHORA
        if (gameManager.currentSongData.all_notes != null)
        {
            foreach (GameNoteData note in gameManager.currentSongData.all_notes)
            {
                // Solo notas de TREBLE (mano derecha)
                if (note.clef != "treble")
                    continue;
                
                // Nota está activa si: time <= currentTime < time + duration
                if (currentGameTime >= note.time && currentGameTime < (note.time + note.duration))
                {
                    result.Add(note);
                }
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Convierte número MIDI a nombre de nota (ej: 60 = C4, 64 = E4)
    /// </summary>
    private string MidiNumberToNoteName(int midiNumber)
    {
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int octave = (midiNumber / 12) - 1;
        int noteIndex = midiNumber % 12;
        return noteNames[noteIndex] + octave;
    }
}
