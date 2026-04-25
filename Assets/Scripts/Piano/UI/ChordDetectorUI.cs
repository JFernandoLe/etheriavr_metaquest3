using UnityEngine;
using TMPro;

/// <summary>
/// Muestra el acorde actualmente detectado en pantalla
/// Se actualiza cuando el jugador toca acordes en el piano MIDI
/// </summary>
public class ChordDetectorUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private TextMeshProUGUI chordNameText;
    [SerializeField] private TextMeshProUGUI chordNotesText;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("Configuración")]
    [SerializeField] private float fadeSpeed = 3f;
    [SerializeField] private float displayDuration = 2f; // Cuánto tiempo mostrar el acorde
    
    private string currentChord = "";
    private float displayTimer = 0f;
    private bool isDisplaying = false;

    void Awake()
    {
        // Inicializar oculto
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
        
        if (chordNameText != null)
        {
            chordNameText.text = "---";
        }
        
        if (chordNotesText != null)
        {
            chordNotesText.text = "";
        }
    }

    void Update()
    {
        // Fade in/out automático
        if (isDisplaying)
        {
            displayTimer -= Time.deltaTime;
            
            // Fade in
            if (canvasGroup != null && canvasGroup.alpha < 1f)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, fadeSpeed * Time.deltaTime);
            }
            
            // Si el timer expiró, comenzar fade out
            if (displayTimer <= 0f)
            {
                isDisplaying = false;
            }
        }
        else
        {
            // Fade out
            if (canvasGroup != null && canvasGroup.alpha > 0f)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, fadeSpeed * Time.deltaTime);
            }
        }
    }

    /// <summary>
    /// Muestra un acorde detectado
    /// </summary>
    /// <param name="chordName">Nombre del acorde (ej: "C", "Am", "G7")</param>
    /// <param name="notes">Notas del acorde (ej: "C-E-G")</param>
    public void ShowChord(string chordName, string notes = "")
    {
        currentChord = chordName;
        
        if (chordNameText != null)
        {
            chordNameText.text = chordName;
        }
        
        if (chordNotesText != null && !string.IsNullOrEmpty(notes))
        {
            chordNotesText.text = notes;
        }
        
        // Reiniciar timer y activar display
        displayTimer = displayDuration;
        isDisplaying = true;
    }

    /// <summary>
    /// Muestra un acorde basado en datos de PianoChordData
    /// </summary>
    public void ShowChord(PianoChordData chordData)
    {
        string notesString = NotesToString(chordData.notes);
        ShowChord(chordData.name, notesString);
    }

    /// <summary>
    /// Oculta el acorde inmediatamente
    /// </summary>
    public void HideChord()
    {
        isDisplaying = false;
        displayTimer = 0f;
    }

    /// <summary>
    /// Convierte un array de notas MIDI a string legible
    /// </summary>
    private string NotesToString(int[] midiNotes)
    {
        if (midiNotes == null || midiNotes.Length == 0) return "";
        
        string result = "";
        for (int i = 0; i < midiNotes.Length; i++)
        {
            result += GetNoteName(midiNotes[i]);
            if (i < midiNotes.Length - 1)
            {
                result += " - ";
            }
        }
        return result;
    }

    /// <summary>
    /// Convierte un número MIDI a nombre de nota (ej: 60 -> "C4")
    /// </summary>
    private string GetNoteName(int midiNote)
    {
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int noteIndex = midiNote % 12;
        int octave = (midiNote / 12) - 1;
        return $"{noteNames[noteIndex]}{octave}";
    }

    /// <summary>
    /// Compara el acorde actual con el acorde esperado
    /// </summary>
    /// <param name="expectedChord">Acorde esperado</param>
    /// <returns>True si coincide</returns>
    public bool IsCorrectChord(PianoChordData expectedChord)
    {
        return currentChord == expectedChord.name;
    }
}
