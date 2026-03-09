using UnityEngine;

/// <summary>
/// Representa un acorde (múltiples notas simultáneas) en una canción de piano
/// </summary>
[System.Serializable]
public class PianoChordData
{
    [Tooltip("Tiempo en segundos desde el inicio de la canción")]
    public float time;
    
    [Tooltip("Nombre del acorde (ej: C, Am, F#m, G7)")]
    public string name;
    
    [Tooltip("Array de números MIDI que componen el acorde")]
    public int[] notes;
    
    [Tooltip("Duración del acorde en segundos")]
    public float duration;
    
    [Tooltip("Mano que toca el acorde: 'left' (clave Fa) o 'right' (clave Sol)")]
    public string hand;
    
    /// <summary>
    /// Propiedad para verificar si es mano derecha (clave de Sol)
    /// </summary>
    public bool IsRightHand => hand?.ToLower() == "right";
    
    /// <summary>
    /// Propiedad para verificar si es mano izquierda (clave de Fa)
    /// </summary>
    public bool IsLeftHand => hand?.ToLower() == "left";
    
    /// <summary>
    /// Cantidad de notas en el acorde
    /// </summary>
    public int NoteCount => notes?.Length ?? 0;
}
