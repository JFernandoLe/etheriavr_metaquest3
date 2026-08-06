using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Muestra el progreso en vivo y las notas esperadas en este instante,
/// para poder comparar visualmente con el pentagrama.
/// </summary>
public class ProgressDisplay : MonoBehaviour
{
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    [Header("Referencias")]
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Image progressBar;

    private float totalNotesCount = 0f;
    private float correctNotesCount = 0f;
    private float currentPercentage = 0f;
    private bool totalsInitialized = false;

    private PianoGameManager gameManager;
    private GameplayScoring gameplayScoring;
    private PianoPublicSystem pianoPublicSystem;

    private readonly StringBuilder displayBuilder = new StringBuilder(256);
    private readonly List<GameNoteData> currentTrebleNotes = new List<GameNoteData>();

    void Start()
    {
        if (progressText == null) progressText = GetComponent<TextMeshProUGUI>();
        if (progressBar == null) progressBar = GetComponentInChildren<Image>();

        gameManager = PianoGameManager.Instance;

        gameplayScoring = FindObjectOfType<GameplayScoring>();
        pianoPublicSystem = FindObjectOfType<PianoPublicSystem>();

        if (gameplayScoring == null)
            Debug.LogWarning("[ProgressDisplay] No se encontró GameplayScoring");

        UpdateDisplay();
    }

    // Se refresca cada frame para que el countdown de la nota actual avance.
    void Update() => UpdateDisplay();

    private void EnsureTotalsInitialized()
    {
        if (totalsInitialized || gameManager == null || gameManager.currentSongData == null) return;

        totalNotesCount = 0f;
        if (gameManager.currentSongData.all_notes != null)
        {
            foreach (GameNoteData note in gameManager.currentSongData.all_notes)
                totalNotesCount += note.midi_notes is { Length: > 0 } ? note.midi_notes.Length : 1f;
        }

        totalsInitialized = true;
    }

    /// <summary>Tiempo de juego: el del audio si está sonando, si no el del manager.</summary>
    private float GetCurrentGameTime()
    {
        if (gameManager == null) return 0f;

        AudioSource music = gameManager.BackgroundMusicSource;
        if (music != null && music.isPlaying) return music.time;

        return gameManager.isPlaying ? gameManager.gameTime : 0f;
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
        else
        {
            currentPercentage = totalNotesCount > 0 ? (correctNotesCount / totalNotesCount) * 100f : 0f;
        }

        displayBuilder.Clear();
        displayBuilder.Append("<size=50>").Append(currentPercentage.ToString("F0")).Append("%</size>");

        if (pianoPublicSystem != null)
        {
            displayBuilder.Append("\n<size=28><color=#FFD966>Publico: ")
                          .Append(pianoPublicSystem.GetCurrentPublicScore().ToString("F0"))
                          .Append("%</color></size>");
        }

        displayBuilder.Append("\n\n");

        CollectCurrentTrebleNotes();

        if (currentTrebleNotes.Count == 0)
        {
            displayBuilder.Append("<color=gray>--- Esperando notas ---</color>\n");
        }
        else
        {
            float currentGameTime = GetCurrentGameTime();
            displayBuilder.Append("<color=yellow>ESPERANDO:</color>\n");

            foreach (GameNoteData note in currentTrebleNotes)
            {
                displayBuilder.Append("<color=cyan>");
                if (note.midi_notes == null)
                {
                    displayBuilder.Append("---");
                }
                else
                {
                    foreach (int midi in note.midi_notes)
                        displayBuilder.Append(MidiNumberToNoteName(midi)).Append(' ');
                }

                float timeRemaining = note.time + note.duration - currentGameTime;
                displayBuilder.Append("</color> [<color=red>")
                              .Append(timeRemaining.ToString("F1"))
                              .Append("s</color>]\n");
            }
        }

        if (progressText != null) progressText.text = displayBuilder.ToString();
        if (progressBar != null) progressBar.fillAmount = currentPercentage / 100f;
    }

    /// <summary>Notas de clave de Sol activas en este instante.</summary>
    private void CollectCurrentTrebleNotes()
    {
        currentTrebleNotes.Clear();

        if (gameManager == null || gameManager.currentSongData?.all_notes == null) return;

        float currentGameTime = GetCurrentGameTime();
        foreach (GameNoteData note in gameManager.currentSongData.all_notes)
        {
            if (note.clef != "treble") continue;

            if (currentGameTime >= note.time && currentGameTime < note.time + note.duration)
                currentTrebleNotes.Add(note);
        }
    }

    /// <summary>Convierte un número MIDI a nombre de nota (60 -> "C4").</summary>
    private string MidiNumberToNoteName(int midiNumber) => NoteNames[midiNumber % 12] + ((midiNumber / 12) - 1);
}
