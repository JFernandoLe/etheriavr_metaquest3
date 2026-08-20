using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Datos de una nota individual del juego (compatible con all_notes del JSON).
/// </summary>
[System.Serializable]
public class GameNoteData
{
    public float time;
    public float duration;
    public int[] midi_notes;
    public string clef;
    public bool is_chord;

    public int GetMidiNote() => midi_notes is { Length: > 0 } ? midi_notes[0] : 60;
}

/// <summary>
/// Representa una canción completa de piano con todas sus notas, acordes y metadatos.
/// </summary>
[System.Serializable]
public class PianoSongData
{
    [Header("Metadatos")]
    [Tooltip("Título de la canción")] public string song_title;
    [Tooltip("Nombre de la canción")] public string song_name;
    [Tooltip("Artista o compositor")] public string artist;
    [Tooltip("Tempo en BPM (beats por minuto)")] public int tempo;
    [Tooltip("Duración total en segundos")] public float duration;
    [Tooltip("Duración de grabación/juego (cuándo termina el juego)")] public float recorded_duration;

    [Header("Audio")]
    [Tooltip("Ruta relativa al MP3 de fondo (ej: PianoSongs/BackgroundMusic/rocketman.mp3)")] public string background_music;
    [Tooltip("Audio file (nuevo formato JSON)")] public string audio_file;
    [Tooltip("Volumen del piano/MIDI (0.0-1.0)")] public float piano_volume = 1.0f;
    [Tooltip("Volumen del audio de fondo (0.0-1.0)")] public float audio_file_volume = 1.0f;

    [Header("Sincronización")]
    [Tooltip("Offset inicial en segundos - Ajusta el tiempo de todas las notas si hay desincronización con la música (ej: 3.5f para 3.5 segundos de espera inicial)")]
    public float beatOffsetTime = -1f;

    [Header("Notas Musicales - Nuevo Formato")]
    [Tooltip("Lista completa de notas con todos los datos (all_notes)")] public List<GameNoteData> all_notes;

    [Header("Notas Musicales - Antiguo Formato")]
    [Tooltip("Lista de notas individuales (melodía)")] public List<PianoNoteData> melody;
    [Tooltip("Lista de acordes")] public List<PianoChordData> chords;

    /// <summary>AudioClip del soundtrack, asignado en runtime por el loader.</summary>
    [System.NonSerialized] public AudioClip backgroundAudioClip;

    public int TotalMelodyNotes => melody?.Count ?? 0;
    public int TotalChords => chords?.Count ?? 0;
    public int TotalGameNotes => all_notes?.Count ?? 0;

    /// <summary>Ruta de audio efectiva: prioriza audio_file sobre background_music.</summary>
    public string GetAudioPath() =>
        !string.IsNullOrEmpty(audio_file) ? audio_file :
        !string.IsNullOrEmpty(background_music) ? background_music : null;

    /// <summary>
    /// Duración del gameplay: campo explícito, si no la del clip, si no el final de la última nota.
    /// </summary>
    public float GetGameDuration()
    {
        if (duration > 0f) return duration;
        if (backgroundAudioClip != null && backgroundAudioClip.length > 0f) return backgroundAudioClip.length;

        float latestEnd = 0f;
        if (all_notes != null)
        {
            foreach (GameNoteData note in all_notes)
                latestEnd = Mathf.Max(latestEnd, note.time + note.duration);
        }
        return latestEnd;
    }

    public List<PianoNoteData> GetRightHandMelody() => melody?.FindAll(n => n.IsRightHand) ?? new List<PianoNoteData>();
    public List<PianoNoteData> GetLeftHandMelody() => melody?.FindAll(n => n.IsLeftHand) ?? new List<PianoNoteData>();
    public List<PianoChordData> GetRightHandChords() => chords?.FindAll(c => c.IsRightHand) ?? new List<PianoChordData>();
    public List<PianoChordData> GetLeftHandChords() => chords?.FindAll(c => c.IsLeftHand) ?? new List<PianoChordData>();
}
