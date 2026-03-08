using UnityEngine;

public class SongLoader : MonoBehaviour
{
    public SongData loadedSong;
    private float songStartTime;
    private bool songPlaying = false;
    private SongNote currentNote;

    void Start()
    {
        LoadSong("song_sample");
    }

    void Update()
    {
        if (!songPlaying || loadedSong == null)
            return;

        float songTime = Time.time - songStartTime;

        currentNote = GetCurrentNote(songTime);

        if (currentNote != null)
        {
            Debug.Log($"Tiempo: {songTime:F2} | Nota esperada: {currentNote.note}");
        }
    }
    public float GetSongTime()
    {
        if (!songPlaying)
            return 0f;

        return Time.time - songStartTime;
    }
    void LoadSong(string fileName)
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(fileName);

        if (jsonFile == null)
        {
            Debug.LogError("No se encontró el JSON");
            return;
        }

        loadedSong = JsonUtility.FromJson<SongData>(jsonFile.text);
        Debug.Log("Canción cargada: " + loadedSong.songName);
    }

    public void StartSong()
    {
        songStartTime = Time.time;
        songPlaying = true;
    }

    SongNote GetCurrentNote(float currentTime)
    {
        foreach (var note in loadedSong.notes)
        {
            if (currentTime >= note.start &&
                currentTime <= note.start + note.duration)
            {
                return note;
            }
        }

        return null;
    }
    public SongNote GetCurrentExpectedNote()
    {
        return currentNote;
    }
}