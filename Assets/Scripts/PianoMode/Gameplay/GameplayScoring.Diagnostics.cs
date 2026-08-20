using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Formateo de mensajes del análisis armónico. Los llamantes deben comprobar
/// enableHarmonyAnalysisDebugLogs antes de construir los textos, que no son baratos.
/// </summary>
public partial class GameplayScoring
{
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private void LogHarmony(string message) => Debug.Log($"[HarmonyAnalysis] {message}");

    private string DescribeExpectedNote(GameNoteData note)
    {
        int[] midiNotes = GetMidiNotes(note);
        string chordLabel = midiNotes.Length > 1 ? "Acorde" : "Nota";
        return $"{chordLabel} esperado t={note.time:F3}s dur={note.duration:F3}s notas=[{FormatMidiNotes(midiNotes)}]";
    }

    private string FormatMidiNotes(IEnumerable<int> midiNotes) => midiNotes == null
        ? "-"
        : string.Join(", ", midiNotes.Select(note => $"{FormatMidiNoteName(note)}({note})"));

    private string FormatMidiNoteName(int midiNote)
    {
        int normalized = ((midiNote % 12) + 12) % 12;
        int octave = Mathf.FloorToInt(midiNote / 12f) - 1;
        return $"{NoteNames[normalized]}{octave}";
    }

    private string FormatCurrentlyPressedNotes() => currentlyPressedNotes.Count > 0
        ? FormatMidiNotes(currentlyPressedNotes.OrderBy(note => note))
        : "-";

    /// <summary>Detalle por nota de un acorde: tiempo sostenido y desviación de onset.</summary>
    private string DescribePerNoteBreakdown(GameNoteScore score, int[] midiNotes) => string.Join(" | ",
        midiNotes.Select(midiNote =>
        {
            float heldDuration = score.heldDurations.TryGetValue(midiNote, out float heldValue) ? heldValue : 0f;
            float onsetOffset = score.onsetOffsets.TryGetValue(midiNote, out float offsetValue) ? offsetValue : -1f;
            string onsetLabel = onsetOffset >= 0f ? $"{onsetOffset:F3}s" : "sin-match";
            return $"{FormatMidiNoteName(midiNote)}({midiNote}) hold={heldDuration:F3}s onset={onsetLabel}";
        }));
}
