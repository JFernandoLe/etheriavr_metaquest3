using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Gestiona canciones personalizadas importadas por el usuario (MP3 + JSON generado localmente).
/// </summary>
public class CustomSongManager : MonoBehaviour
{
    public static CustomSongManager Instance { get; private set; }

    public const string CustomSongsFolderName = "CustomSongs";

    public event Action<CustomSongEntry> OnSongImported;
    public event Action<string> OnImportFailed;

    [SerializeField] private int maxNotesForGameplay = 800;

    private string customSongsPath;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        customSongsPath = Path.Combine(Application.persistentDataPath, CustomSongsFolderName);
        Directory.CreateDirectory(customSongsPath);
    }

    public string CustomSongsPath => customSongsPath;

    public IReadOnlyList<CustomSongEntry> ListCustomSongs()
    {
        var entries = new List<CustomSongEntry>();
        if (!Directory.Exists(customSongsPath))
            return entries;

        foreach (string jsonFile in Directory.GetFiles(customSongsPath, "*.json"))
        {
            string baseName = Path.GetFileNameWithoutExtension(jsonFile);
            string mp3Path = Path.Combine(customSongsPath, baseName + ".mp3");
            string wavPath = Path.Combine(customSongsPath, baseName + ".wav");
            string audioPath = File.Exists(mp3Path) ? mp3Path : wavPath;

            if (!File.Exists(audioPath))
                continue;

            entries.Add(new CustomSongEntry
            {
                Id = baseName,
                Title = baseName,
                JsonPath = jsonFile,
                AudioPath = audioPath
            });
        }

        return entries;
    }

    public IEnumerator ImportFromFile(string sourceAudioPath, string displayName, Action<CustomSongEntry> onComplete = null)
    {
        if (string.IsNullOrEmpty(sourceAudioPath) || !File.Exists(sourceAudioPath))
        {
            OnImportFailed?.Invoke("Archivo de audio no encontrado.");
            yield break;
        }

        string safeName = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileNameWithoutExtension(sourceAudioPath)
            : displayName.Trim();

        foreach (char c in Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(c, '_');

        string extension = Path.GetExtension(sourceAudioPath).ToLowerInvariant();
        string destAudioPath = Path.Combine(customSongsPath, safeName + extension);

        try
        {
            File.Copy(sourceAudioPath, destAudioPath, true);
        }
        catch (Exception ex)
        {
            OnImportFailed?.Invoke("No se pudo copiar el archivo: " + ex.Message);
            yield break;
        }

        AudioType audioType = extension == ".wav" ? AudioType.WAV : AudioType.MPEG;
        using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip("file://" + destAudioPath, audioType);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            OnImportFailed?.Invoke("Error leyendo audio: " + request.error);
            yield break;
        }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
        if (clip == null)
        {
            OnImportFailed?.Invoke("Clip de audio inválido.");
            yield break;
        }

        MelodyTranscriber.TranscriptionResult transcription = MelodyTranscriber.Transcribe(clip, safeName, destAudioPath);
        if (transcription.NoteCount == 0)
        {
            OnImportFailed?.Invoke("No se detectaron notas en el audio. Prueba con una pista más melodica.");
            yield break;
        }

        if (transcription.NoteCount > maxNotesForGameplay)
        {
            Debug.LogWarning($"[CustomSongManager] Canción con {transcription.NoteCount} notas; puede afectar rendimiento.");
        }

        string jsonPath = MelodyTranscriber.SaveTranscription(transcription, customSongsPath);

        var entry = new CustomSongEntry
        {
            Id = safeName,
            Title = safeName,
            JsonPath = jsonPath,
            AudioPath = destAudioPath,
            EstimatedBpm = transcription.EstimatedBpm,
            NoteCount = transcription.NoteCount
        };

        OnSongImported?.Invoke(entry);
        onComplete?.Invoke(entry);
    }

    public SongListarResponse ToSongListEntry(CustomSongEntry entry)
    {
        return new SongListarResponse
        {
            title = entry.Title,
            artist_name = "Personalizada",
            mode = "CANTO",
            file_path = entry.Id,
            musical_key = "Auto",
            tempo = Mathf.RoundToInt(entry.EstimatedBpm),
            duration = 0f,
            is_custom = true
        };
    }
}

[Serializable]
public class CustomSongEntry
{
    public string Id;
    public string Title;
    public string JsonPath;
    public string AudioPath;
    public float EstimatedBpm;
    public int NoteCount;
}
