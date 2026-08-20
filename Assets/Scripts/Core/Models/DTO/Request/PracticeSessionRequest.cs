using System;

[Serializable]
public class PracticeSessionRequest
{
    public int user_id;
    public int song_id;
    public string practice_datetime;
    public string practice_mode;
    public float? rhythm_score;
    public float? harmony_score;
    public float? tuning_score;
}