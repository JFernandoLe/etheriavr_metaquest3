using System;
using UnityEngine;

/// <summary>
/// Conversión entre frecuencia, MIDI y nombres de nota musicales.
/// </summary>
public static class MusicalNoteUtility
{
    private static readonly string[] NoteNames =
    {
        "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"
    };

    public static float HzToMidi(float frequency)
    {
        if (frequency <= 0f)
            return -1f;
        return 69f + 12f * Mathf.Log(frequency / 440f, 2f);
    }

    public static float MidiToHz(int midi)
    {
        return 440f * Mathf.Pow(2f, (midi - 69f) / 12f);
    }

    public static int RoundMidi(float midiFloat)
    {
        return Mathf.RoundToInt(midiFloat);
    }

    public static float FrequencyToCents(float frequency, int nearestMidi)
    {
        float reference = MidiToHz(nearestMidi);
        if (reference <= 0f || frequency <= 0f)
            return 0f;
        return 1200f * Mathf.Log(frequency / reference, 2f);
    }

    public static string MidiToNoteName(int midi)
    {
        if (midi < 0)
            return "---";

        int note = ((midi % 12) + 12) % 12;
        int octave = (midi / 12) - 1;
        return NoteNames[note] + octave;
    }

    public static int NoteNameToMidi(string noteName)
    {
        if (string.IsNullOrEmpty(noteName) || noteName.Length < 2)
            return -1;

        string name = noteName.Trim();
        int octaveStart = name.Length - 1;
        while (octaveStart > 0 && char.IsDigit(name[octaveStart - 1]) == false && name[octaveStart] != '-')
            break;

        for (int i = 1; i < name.Length; i++)
        {
            if (char.IsDigit(name[i]) || name[i] == '-')
            {
                octaveStart = i;
                break;
            }
        }

        string pitch = name.Substring(0, octaveStart);
        if (!int.TryParse(name.Substring(octaveStart), out int octave))
            return -1;

        int semitone = Array.IndexOf(NoteNames, pitch);
        if (semitone < 0)
            return -1;

        return (octave + 1) * 12 + semitone;
    }
}
