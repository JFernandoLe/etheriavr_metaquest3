using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

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

        if (TryGetCustomSongPaths(fileName, out string jsonPath, out string audioPath))
        {
            yield return LoadFromPaths(jsonPath, audioPath);
            yield break;
        }

        string basePath = Application.streamingAssetsPath + "/SingSongs/Songs";
        string streamingJson = basePath + "/" + fileName + ".json";
        yield return LoadFromPaths(streamingJson, null, basePath, fileName);
    }

    private static bool TryGetCustomSongPaths(string fileName, out string jsonPath, out string audioPath)
    {
        jsonPath = null;
        audioPath = null;

        string customDir = Path.Combine(Application.persistentDataPath, CustomSongManager.CustomSongsFolderName);
        string candidateJson = Path.Combine(customDir, fileName + ".json");
        if (!File.Exists(candidateJson))
            return false;

        string mp3 = Path.Combine(customDir, fileName + ".mp3");
        string wav = Path.Combine(customDir, fileName + ".wav");
        if (!File.Exists(mp3) && !File.Exists(wav))
            return false;

        jsonPath = candidateJson;
        audioPath = File.Exists(mp3) ? mp3 : wav;
        return true;
    }

    private IEnumerator LoadFromPaths(string jsonPath, string explicitAudioPath = null, string streamingBasePath = null, string fileName = null)
    {
        string jsonText;

        if (jsonPath.StartsWith("http") || jsonPath.Contains(Application.streamingAssetsPath))
        {
            UnityWebRequest jsonRequest = UnityWebRequest.Get(jsonPath);
            yield return jsonRequest.SendWebRequest();

            if (jsonRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error cargando JSON: " + jsonRequest.error);
                yield break;
            }

            jsonText = jsonRequest.downloadHandler.text;
        }
        else
        {
            if (!File.Exists(jsonPath))
            {
                Debug.LogError("JSON no encontrado: " + jsonPath);
                yield break;
            }

            jsonText = File.ReadAllText(jsonPath);
        }

        loadedSong = JsonUtility.FromJson<SongData>(jsonText);
        if (loadedSong == null)
        {
            Debug.LogError("JSON invalido o vacio");
            yield break;
        }

        SelectedSongManager.Instance?.LogSongSelectionCheckpoint("Canto JSON listo");

        string audioPath = explicitAudioPath;
        AudioType audioType = AudioType.UNKNOWN;

        if (audioPath == null && streamingBasePath != null)
        {
            string wavPath = streamingBasePath + "/" + fileName + ".wav";
            string mp3Path = streamingBasePath + "/" + fileName + ".mp3";

            if (File.Exists(mp3Path))
            {
                audioPath = mp3Path;
                audioType = AudioType.MPEG;
            }
            else
            {
                audioPath = wavPath;
                audioType = AudioType.WAV;
            }
        }
        else if (audioPath != null)
        {
            audioType = audioPath.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase)
                ? AudioType.MPEG
                : AudioType.WAV;
        }

        Debug.Log("AUDIO PATH: " + audioPath);

        string requestUri = audioPath.StartsWith("http") ? audioPath : "file://" + audioPath;
        using UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip(requestUri, audioType);
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
            audioSource.clip = clip;

        SelectedSongManager.Instance?.CompleteSongSelectionMeasurement("Canto listo para iniciar gameplay");
        StartSong();
    }

    public void StartSong()
    {
        if (audioSource != null && audioSource.clip != null)
            audioSource.Play();

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
