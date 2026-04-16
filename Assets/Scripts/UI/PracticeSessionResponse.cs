using System;
using System.Collections.Generic;

[Serializable]
public class PracticeSessionResponse
{
    public int id;
    public int song_id;
    public string song_title; // Título de la canción
    public string practice_datetime;
    public string practice_mode;
    public float rhythm_score;
    public float harmony_score;
    public float tuning_score;
    public float global_score;
}

// Esta clase es necesaria porque el backend devuelve una lista
[Serializable]
public class PracticeSessionListWrapper
{
    public List<PracticeSessionResponse> sessions;
}