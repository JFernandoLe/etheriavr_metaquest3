using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Representa una canción completa de piano con todas sus notas, acordes y metadatos
/// </summary>
[System.Serializable]
public class PianoSongData
{
    [Header("Metadatos")]
    [Tooltip("Título de la canción")]
    public string song_title;
    
    [Tooltip("Artista o compositor")]
    public string artist;
    
    [Tooltip("Tempo en BPM (beats por minuto)")]
    public int tempo;
    
    [Tooltip("Duración total en segundos")]
    public float duration;
    
    [Header("Audio")]
    [Tooltip("Ruta relativa al MP3 de fondo (ej: BackgroundMusic/rocketman.mp3)")]
    public string background_music;
    
    [Header("Notas Musicales")]
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
    /// Total de notas en la melodía
    /// </summary>
    public int TotalMelodyNotes => melody?.Count ?? 0;
    
    /// <summary>
    /// Total de acordes
    /// </summary>
    public int TotalChords => chords?.Count ?? 0;
    
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
