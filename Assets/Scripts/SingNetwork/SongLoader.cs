using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.IO;

public class SongLoader : MonoBehaviour
{
    public SongData loadedSong;
    private bool songPlaying = false;
    private SongNote currentNote;

    public AudioSource audioSource;
    public float songOffset = -0.15f;

    public string songName = "song_take_on_me";

    void Start()
    {
        if (SelectedSongManager.Instance != null &&
            SelectedSongManager.Instance.selectedSong != null)
        {
            string path = SelectedSongManager.Instance.selectedSong.file_path;

            Debug.Log("PATH DEL BACKEND: " + path);

            songName = Path.GetFileNameWithoutExtension(path);
        }

        SelectedSongManager.Instance?.LogSongSelectionCheckpoint("Escena SingGame iniciada");

        LoadSong(songName);
    }

    void Update()
    {
        if (!songPlaying || loadedSong == null)
            return;

        float songTime = GetSongTime() + songOffset;

        currentNote = GetCurrentNote(songTime);

        if (currentNote != null)
        {
            //Debug.Log($"Tiempo: {songTime:F2} | Nota esperada: {currentNote.note}");
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
        StartCoroutine(LoadSongCoroutine(fileName));
    }

    IEnumerator LoadSongCoroutine(string fileName)
    {

        loadedSong = null;
        songPlaying = false;

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }

        string basePath = Application.streamingAssetsPath + "/SingSongs/Songs";

        string jsonPath = basePath + "/" + fileName + ".json";

        Debug.Log("JSON PATH: " + jsonPath);

        UnityWebRequest jsonRequest = UnityWebRequest.Get(jsonPath);
        yield return jsonRequest.SendWebRequest();

        if (jsonRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error cargando JSON: " + jsonRequest.error);
            yield break;
        }

        string jsonText = jsonRequest.downloadHandler.text;
        loadedSong = JsonUtility.FromJson<SongData>(jsonText);

        if (loadedSong == null)
        {
            Debug.LogError("JSON invalido o vacio");
            yield break;
        }

        SelectedSongManager.Instance?.LogSongSelectionCheckpoint("Canto JSON listo");


        string audioPath = basePath + "/" + fileName + ".wav";

        Debug.Log("AUDIO PATH: " + audioPath);

        UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip(audioPath, AudioType.WAV);
        yield return audioRequest.SendWebRequest();

        if (audioRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error cargando audio: " + audioRequest.error);
            yield break;
        }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(audioRequest);

        if (clip == null)
        {
            Debug.LogError("Clip nulo");
            yield break;
        }

        if (audioSource != null)
        {
            audioSource.clip = clip;
        }

        SelectedSongManager.Instance?.CompleteSongSelectionMeasurement("Canto listo para iniciar gameplay");

        StartSong();
    }

    public void StartSong()
    {
        if (audioSource != null && audioSource.clip != null)
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