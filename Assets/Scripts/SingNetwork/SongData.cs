using System;

[Serializable]
public class SongNote
{
    public string note;
    public int midi;
    public float start;
    public float duration;
}

[Serializable]
public class LyricLine
{
    public float time;
    public string text;
}

[Serializable]
public class SongData
{
    public string songName;
    public float songDuration;

    public SongNote[] notes;

    public LyricLine[] lyrics;
}