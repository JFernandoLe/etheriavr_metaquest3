using UnityEngine;

/// <summary>
/// Representa una nota individual en una canción de piano
/// </summary>
[System.Serializable]
public class PianoNoteData
{
    [Tooltip("Tiempo en segundos desde el inicio de la canción")]
    public float time;
    
    [Tooltip("Número MIDI de la nota (21-108, típicamente 48-84 para piano)")]
    public int midi;
    
    [Tooltip("Duración de la nota en segundos")]
    public float duration;
    
    [Tooltip("Mano que toca la nota: 'left' (clave Fa) o 'right' (clave Sol)")]
    public string hand;
    
    /// <summary>
    /// Propiedad para verificar si es mano derecha (clave de Sol)
    /// </summary>
    public bool IsRightHand => hand?.ToLower() == "right";
    
    /// <summary>
    /// Propiedad para verificar si es mano izquierda (clave de Fa)
    /// </summary>
    public bool IsLeftHand => hand?.ToLower() == "left";
}
