using System;
using UnityEngine;

[Serializable]
public class SongNote
{
    public string note;
    public int midi;
    public float start;
    public float duration;
}

[Serializable]
public class SongData
{
    public string songName;
    public SongNote[] notes;
}