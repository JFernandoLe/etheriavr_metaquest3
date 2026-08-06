using UnityEngine;

/// <summary>
/// Representa un acorde (múltiples notas simultáneas) en una canción de piano.
/// </summary>
[System.Serializable]
public class PianoChordData
{
    [Tooltip("Tiempo en segundos desde el inicio de la canción")] public float time;
    [Tooltip("Nombre del acorde (ej: C, Am, F#m, G7)")] public string name;
    [Tooltip("Array de números MIDI que componen el acorde")] public int[] notes;
    [Tooltip("Duración del acorde en segundos")] public float duration;
    [Tooltip("Mano que toca el acorde: 'left' (clave Fa) o 'right' (clave Sol)")] public string hand;

    public bool IsRightHand => hand?.ToLower() == "right";
    public bool IsLeftHand => hand?.ToLower() == "left";
    public int NoteCount => notes?.Length ?? 0;
}
