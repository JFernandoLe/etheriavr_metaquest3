using UnityEngine;

/// <summary>
/// Representa una nota individual en una canción de piano.
/// </summary>
[System.Serializable]
public class PianoNoteData
{
    [Tooltip("Tiempo en segundos desde el inicio de la canción")] public float time;
    [Tooltip("Número MIDI de la nota (21-108, típicamente 48-84 para piano)")] public int midi;
    [Tooltip("Duración de la nota en segundos")] public float duration;
    [Tooltip("Mano que toca la nota: 'left' (clave Fa) o 'right' (clave Sol)")] public string hand;

    public bool IsRightHand => hand?.ToLower() == "right";
    public bool IsLeftHand => hand?.ToLower() == "left";
}
