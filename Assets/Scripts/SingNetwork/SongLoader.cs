using UnityEngine;

public class SongLoader : MonoBehaviour
{
    public SongData loadedSong;
    private float songStartTime;
    private bool songPlaying = false;
    private SongNote currentNote;
    public AudioSource audioSource;
    public float songOffset = -0.15f;   

    void Start()
    {
        LoadSong("song_take_on_me");
    }
        
    void Update()
    {
        if (!songPlaying || loadedSong == null)
            return;

        float songTime = GetSongTime() + songOffset;

        currentNote = GetCurrentNote(songTime);

        if (currentNote != null)
        {
            Debug.Log($"Tiempo: {songTime:F2} | Nota esperada: {currentNote.note}");
        }
    }
    public float GetSongTime()
    {
        if (audioSource == null)
            return 0f;

        return audioSource.time;
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
        if (audioSource != null)
        {
            audioSource.Play();
        }

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