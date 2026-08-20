using System;
using System.Collections.Generic;

[Serializable]
public class SongListarResponse
{
    public int id;
    public string musical_genre;
    public string musical_key;
    public string title;
    public int duration;
    public string mode;
    public int tempo;
    public string file_path;
    public string artist_name;
}

[Serializable]
public class SongListWrapper
{
    public List<SongListarResponse> songs; 
}