using System;

/// <summary>
/// Representación intermedia de alta resolución antes de convertir a SongNote/esferas.
/// </summary>
[Serializable]
public class NoteEvent
{
    public float pitchMidi;
    public float pitchHz;
    public float startTime;
    public float duration;
    public float confidence;
    public float energy;
    public int midiRounded;
    public string noteName;
}

[Serializable]
public class PitchFrameData
{
    public float time;
    public float midiFloat;
    public float frequency;
    public float confidence;
    public float energy;
    public float spectralFlux;
    public bool valid;
}

[Serializable]
public class MelodyKeyEstimate
{
    public string keyName;
    public float confidence;
    public int rootPc;
    public bool isMinor;

    public static MelodyKeyEstimate Unknown => new MelodyKeyEstimate
    {
        keyName = "Indeterminada",
        confidence = 0f,
        rootPc = -1,
        isMinor = false
    };
}
