using UnityEngine;
using TMPro;
using System.Linq;

/// <summary>
/// Muestra en pantalla el acorde detectado mientras el jugador toca el piano MIDI.
/// </summary>
public class ChordDetectorUI : MonoBehaviour
{
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    [Header("Referencias UI")]
    [SerializeField] private TextMeshProUGUI chordNameText;
    [SerializeField] private TextMeshProUGUI chordNotesText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Configuración")]
    [SerializeField] private float fadeSpeed = 3f;
    [SerializeField] private float displayDuration = 2f;

    private string currentChord = "";
    private float displayTimer = 0f;
    private bool isDisplaying = false;

    void Awake()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (chordNameText != null) chordNameText.text = "---";
        if (chordNotesText != null) chordNotesText.text = "";
    }

    void Update()
    {
        if (isDisplaying)
        {
            displayTimer -= Time.deltaTime;

            if (canvasGroup != null && canvasGroup.alpha < 1f)
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, fadeSpeed * Time.deltaTime);

            if (displayTimer <= 0f) isDisplaying = false;
            return;
        }

        if (canvasGroup != null && canvasGroup.alpha > 0f)
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, fadeSpeed * Time.deltaTime);
    }

    /// <param name="chordName">Nombre del acorde (ej: "C", "Am", "G7")</param>
    /// <param name="notes">Notas del acorde (ej: "C4 - E4 - G4")</param>
    public void ShowChord(string chordName, string notes = "")
    {
        currentChord = chordName;

        if (chordNameText != null) chordNameText.text = chordName;
        if (chordNotesText != null && !string.IsNullOrEmpty(notes)) chordNotesText.text = notes;

        displayTimer = displayDuration;
        isDisplaying = true;
    }

    public void ShowChord(PianoChordData chordData) => ShowChord(chordData.name, NotesToString(chordData.notes));

    public void HideChord()
    {
        isDisplaying = false;
        displayTimer = 0f;
    }

    private string NotesToString(int[] midiNotes) => midiNotes == null || midiNotes.Length == 0
        ? ""
        : string.Join(" - ", midiNotes.Select(GetNoteName));

    /// <summary>Convierte un número MIDI a nombre de nota (60 -> "C4").</summary>
    private string GetNoteName(int midiNote) => $"{NoteNames[midiNote % 12]}{(midiNote / 12) - 1}";

    public bool IsCorrectChord(PianoChordData expectedChord) => currentChord == expectedChord.name;
}
