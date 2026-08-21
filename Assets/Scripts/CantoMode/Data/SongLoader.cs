using System.Collections;
using System.IO;
using UnityEngine;

public class SongLoader : MonoBehaviour
{
    public SongData loadedSong;
    private bool songPlaying;
    private SongNote currentNote;

    public AudioSource audioSource;
    public float songOffset = -0.15f;

    [SerializeField] private float musicVolume = 1f;

    public string songName = "take_on_me";

    void Start()
    {
        if (SelectedSongManager.Instance != null &&
            SelectedSongManager.Instance.selectedSong != null)
        {
            string path = SelectedSongManager.Instance.selectedSong.file_path;
            Debug.Log("[SongLoader] Canción seleccionada: " + path);
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

    public void LoadSong(string fileName)
    {
        StartCoroutine(LoadSongCoroutine(fileName));
    }

    private IEnumerator LoadSongCoroutine(string fileName)
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
            yield return LoadCustomSong(jsonPath, audioPath);
            yield break;
        }

        yield return LoadBuiltInSong(fileName);
    }

    private IEnumerator LoadBuiltInSong(string fileName)
    {
        string jsonRelative = "SingSongs/Songs/" + fileName + ".json";
        bool jsonLoaded = false;
        string jsonError = null;

        yield return StreamingAssetsAudioLoader.LoadText(jsonRelative,
            text =>
            {
                loadedSong = JsonUtility.FromJson<SongData>(text);
                jsonLoaded = loadedSong != null && loadedSong.notes != null && loadedSong.notes.Length > 0;
            },
            error => jsonError = error);

        if (!jsonLoaded)
        {
            Debug.LogError("[SongLoader] Error cargando JSON: " + jsonError);
            yield break;
        }

        SelectedSongManager.Instance?.LogSongSelectionCheckpoint("Canto JSON listo");

        bool audioLoaded = false;
        string audioError = null;

        yield return StreamingAssetsAudioLoader.LoadAudioClip("SingSongs/Songs/" + fileName,
            (clip, _) =>
            {
                AssignMusicClip(clip);
                audioLoaded = true;
            },
            error => audioError = error);

        if (!audioLoaded)
        {
            Debug.LogError("[SongLoader] Error cargando audio: " + audioError);
            yield break;
        }

        SelectedSongManager.Instance?.CompleteSongSelectionMeasurement("Canto listo para iniciar gameplay");
        StartSong();
    }

    private IEnumerator LoadCustomSong(string jsonPath, string audioPath)
    {
        if (!File.Exists(jsonPath))
        {
            Debug.LogError("[SongLoader] JSON personalizado no encontrado: " + jsonPath);
            yield break;
        }

        loadedSong = JsonUtility.FromJson<SongData>(File.ReadAllText(jsonPath));
        if (loadedSong == null || loadedSong.notes == null || loadedSong.notes.Length == 0)
        {
            Debug.LogError("[SongLoader] JSON personalizado inválido.");
            yield break;
        }

        SelectedSongManager.Instance?.LogSongSelectionCheckpoint("Canto JSON listo");

        bool audioLoaded = false;
        string audioError = null;

        yield return StreamingAssetsAudioLoader.LoadPersistentAudioClip(audioPath,
            clip =>
            {
                AssignMusicClip(clip);
                audioLoaded = true;
            },
            error => audioError = error);

        if (!audioLoaded)
        {
            Debug.LogError("[SongLoader] Error cargando audio personalizado: " + audioError);
            yield break;
        }

        SelectedSongManager.Instance?.CompleteSongSelectionMeasurement("Canto listo para iniciar gameplay");
        StartSong();
    }

    private void AssignMusicClip(AudioClip clip)
    {
        if (audioSource == null)
        {
            Debug.LogError("[SongLoader] AudioSource de música no asignado.");
            return;
        }

        audioSource.clip = clip;
        audioSource.loop = false;
        audioSource.mute = false;
        audioSource.volume = musicVolume;
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
        Debug.Log($"[SongLoader] Clip listo: {clip.name}, duración={clip.length:F1}s, canales={clip.channels}");
    }

    public void StartSong()
    {
        if (audioSource == null || audioSource.clip == null)
        {
            Debug.LogError("[SongLoader] No hay clip de música para reproducir.");
            return;
        }

        audioSource.Play();
        songPlaying = true;
        Debug.Log("[SongLoader] Reproduciendo canción de fondo.");
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
        audioPath = File.Exists(wav) ? wav : mp3;
        return true;
    }

    private SongNote GetCurrentNote(float currentTime)
    {
        foreach (SongNote note in loadedSong.notes)
        {
            if (currentTime >= note.start && currentTime <= note.start + note.duration)
                return note;
        }

        return null;
    }

    public SongNote GetCurrentExpectedNote()
    {
        return currentNote;
    }
}
