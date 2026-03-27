using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Datos de una nota individual del juego (compatible con all_notes del JSON)
/// </summary>
[System.Serializable]
public class GameNoteData
{
    public float time;                  // Cuándo comienza
    public float duration;              // Cuánto dura
    public int[] midi_notes;            // Array de MIDI notes (para acordes)
    public string clef;                 // "treble" o "bass"
    public bool is_chord;               // Si es un acorde
    
    public int GetMidiNote()
    {
        return midi_notes != null && midi_notes.Length > 0 ? midi_notes[0] : 60;
    }
}

/// <summary>
/// Representa una canción completa de piano con todas sus notas, acordes y metadatos
/// </summary>
[System.Serializable]
public class PianoSongData
{
    [Header("Metadatos")]
    [Tooltip("Título de la canción")]
    public string song_title;
    
    [Tooltip("Nombre de la canción")]
    public string song_name;
    
    [Tooltip("Artista o compositor")]
    public string artist;
    
    [Tooltip("Tempo en BPM (beats por minuto)")]
    public int tempo;
    
    [Tooltip("Duración total en segundos")]
    public float duration;
    
    [Tooltip("Duración de grabación/juego (cuándo termina el juego)")]
    public float recorded_duration;
    
    [Header("Audio")]
    [Tooltip("Ruta relativa al MP3 de fondo (ej: PianoSongs/BackgroundMusic/rocketman.mp3)")]
    public string background_music;
    
    [Tooltip("Audio file (nuevo formato JSON)")]
    public string audio_file;
    
    [Tooltip("Volumen del piano/MIDI (0.0-1.0)")]
    public float piano_volume = 1.0f;
    
    [Tooltip("Volumen del audio de fondo (0.0-1.0)")]
    public float audio_file_volume = 1.0f;
    
    [Header("Sincronización")]
    [Tooltip("Offset inicial en segundos - Ajusta el tiempo de todas las notas si hay desincronización con la música (ej: 3.5f para 3.5 segundos de espera inicial)")]
    public float beatOffsetTime = -1f;
    
    [Header("Notas Musicales - Nuevo Formato")]
    [Tooltip("Lista completa de notas con todos los datos (all_notes)")]
    public List<GameNoteData> all_notes;
    
    [Header("Notas Musicales - Antiguo Formato")]
    [Tooltip("Lista de notas individuales (melodía)")]
    public List<PianoNoteData> melody;
    
    [Tooltip("Lista de acordes")]
    public List<PianoChordData> chords;
    
    /// <summary>
    /// AudioClip cargado del soundtrack (se asigna en runtime)
    /// </summary>
    [System.NonSerialized]
    public AudioClip backgroundAudioClip;
    
    /// <summary>
    /// Total de notas en la melodía (formato antiguo)
    /// </summary>
    public int TotalMelodyNotes => melody?.Count ?? 0;
    
    /// <summary>
    /// Total de acordes (formato antiguo)
    /// </summary>
    public int TotalChords => chords?.Count ?? 0;
    
    /// <summary>
    /// Total de notas en el nuevo formato
    /// </summary>
    public int TotalGameNotes => all_notes?.Count ?? 0;
    
    /// <summary>
    /// Obtiene el audio file correcto (intenta audio_file primero, luego background_music)
    /// </summary>
    public string GetAudioPath()
    {
        if (!string.IsNullOrEmpty(audio_file)) return audio_file;
        if (!string.IsNullOrEmpty(background_music)) return background_music;
        return null;
    }
    
    /// <summary>
    /// Obtiene la duración correcta del gameplay.
    /// </summary>
    public float GetGameDuration()
    {
        if (duration > 0f) return duration;
        if (backgroundAudioClip != null && backgroundAudioClip.length > 0f) return backgroundAudioClip.length;

        if (all_notes != null && all_notes.Count > 0)
        {
            float latestEnd = 0f;
            for (int i = 0; i < all_notes.Count; i++)
            {
                GameNoteData note = all_notes[i];
                latestEnd = Mathf.Max(latestEnd, note.time + note.duration);
            }

            return latestEnd;
        }

        return 0f;
    }
    
    /// <summary>
    /// Obtener todas las notas de melodía para mano derecha
    /// </summary>
    public List<PianoNoteData> GetRightHandMelody()
    {
        if (melody == null) return new List<PianoNoteData>();
        return melody.FindAll(n => n.IsRightHand);
    }
    
    /// <summary>
    /// Obtener todas las notas de melodía para mano izquierda
    /// </summary>
    public List<PianoNoteData> GetLeftHandMelody()
    {
        if (melody == null) return new List<PianoNoteData>();
        return melody.FindAll(n => n.IsLeftHand);
    }
    
    /// <summary>
    /// Obtener todos los acordes para mano derecha
    /// </summary>
    public List<PianoChordData> GetRightHandChords()
    {
        if (chords == null) return new List<PianoChordData>();
        return chords.FindAll(c => c.IsRightHand);
    }
    
    /// <summary>
    /// Obtener todos los acordes para mano izquierda
    /// </summary>
    public List<PianoChordData> GetLeftHandChords()
    {
        if (chords == null) return new List<PianoChordData>();
        return chords.FindAll(c => c.IsLeftHand);
    }
}
