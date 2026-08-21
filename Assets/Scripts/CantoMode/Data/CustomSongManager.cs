using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Gestiona canciones personalizadas: audio + JSON de notas + metadata.
/// El análisis se ejecuta una sola vez al importar, no durante el canto.
/// </summary>
public class CustomSongManager : MonoBehaviour
{
    public static CustomSongManager Instance { get; private set; }

    public const string CustomSongsFolderName = "CustomSongs";

    public event Action<CustomSongEntry> OnSongImported;
    public event Action<string> OnImportFailed;
    public event Action<CustomSongImportStage, string> OnImportProgress;
    public event Action<string> OnSongDeleted;

    [SerializeField] private int maxNotesForGameplay = 1200;

    private string customSongsPath;
    private bool cancelImportRequested;

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

    public void CancelCurrentImport() => cancelImportRequested = true;

    public static string FormatDisplayTitle(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return "Canción sin nombre";

        string title = rawName.Trim();
        if (!title.Contains(" "))
            title = title.Replace('_', ' ').Replace('-', ' ');
        while (title.Contains("  "))
            title = title.Replace("  ", " ");
        return title;
    }

    public IReadOnlyList<CustomSongEntry> ListCustomSongs()
    {
        var entries = new List<CustomSongEntry>();
        if (!Directory.Exists(customSongsPath))
            return entries;

        foreach (string jsonFile in Directory.GetFiles(customSongsPath, "*.json"))
        {
            if (jsonFile.EndsWith(".meta.json", StringComparison.OrdinalIgnoreCase))
                continue;

            string baseName = Path.GetFileNameWithoutExtension(jsonFile);
            string mp3Path = Path.Combine(customSongsPath, baseName + ".mp3");
            string wavPath = Path.Combine(customSongsPath, baseName + ".wav");
            string audioPath = File.Exists(wavPath) ? wavPath : mp3Path;

            if (!File.Exists(audioPath))
                continue;

            var entry = new CustomSongEntry
            {
                Id = baseName,
                Title = FormatDisplayTitle(baseName),
                JsonPath = jsonFile,
                AudioPath = audioPath,
                IsCustom = true
            };

            string metaPath = Path.Combine(customSongsPath, baseName + ".meta.json");
            if (File.Exists(metaPath))
            {
                SongImportMetadata meta = JsonUtility.FromJson<SongImportMetadata>(File.ReadAllText(metaPath));
                if (meta != null)
                {
                    entry.Title = string.IsNullOrEmpty(meta.title) ? entry.Title : meta.title;
                    entry.EstimatedBpm = meta.estimatedBpm;
                    entry.NoteCount = meta.noteCount;
                    entry.MusicalKey = meta.estimatedKey;
                    entry.KeyConfidence = meta.keyConfidence;
                    entry.DurationSeconds = meta.durationSeconds;
                }
            }

            entries.Add(entry);
        }

        entries.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));
        return entries;
    }

    public IEnumerator ImportFromFile(string sourceAudioPath, string displayName)
    {
        cancelImportRequested = false;

        if (string.IsNullOrEmpty(sourceAudioPath) || !File.Exists(sourceAudioPath))
        {
            OnImportFailed?.Invoke("Archivo de audio no encontrado.");
            yield break;
        }

        string displayTitle = FormatDisplayTitle(string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileNameWithoutExtension(sourceAudioPath)
            : displayName.Trim());

        string safeName = SanitizeName(displayTitle);
        safeName = EnsureUniqueSongId(safeName);

        string extension = Path.GetExtension(sourceAudioPath).ToLowerInvariant();
        if (!IsSupportedAudioExtension(extension))
        {
            OnImportFailed?.Invoke("Formato no soportado. Usa MP3 o WAV.");
            yield break;
        }

        OnImportProgress?.Invoke(CustomSongImportStage.Loading, "Copiando archivo...");

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

        if (cancelImportRequested) yield break;

        string pathForAnalysis = destAudioPath;
#if UNITY_ANDROID && !UNITY_EDITOR
        if (NeedsNativeDecode(extension))
        {
            OnImportProgress?.Invoke(CustomSongImportStage.Loading, "Decodificando audio...");
            string wavPath = AndroidAudioDecoder.ConvertToWavIfNeeded(destAudioPath);
            if (string.IsNullOrEmpty(wavPath))
            {
                OnImportFailed?.Invoke("No se pudo decodificar el MP3 en Quest. Prueba otro archivo o formato WAV.");
                yield break;
            }

            pathForAnalysis = wavPath;
        }
#endif

        if (cancelImportRequested) yield break;

        OnImportProgress?.Invoke(CustomSongImportStage.Analyzing, "Leyendo audio decodificado...");

        AudioClip clip = null;
        string loadError = null;
        yield return StreamingAssetsAudioLoader.LoadPersistentAudioClip(pathForAnalysis,
            loadedClip => clip = loadedClip,
            error => loadError = error);

        if (clip == null)
        {
            Debug.LogError($"[CustomSongManager] Error decodificando '{pathForAnalysis}': {loadError}");
            OnImportFailed?.Invoke("Error leyendo audio: " + loadError);
            yield break;
        }

        if (cancelImportRequested)
        {
            Destroy(clip);
            yield break;
        }

        yield return null;

        float duration = clip.length;
        OnImportProgress?.Invoke(CustomSongImportStage.GeneratingNotes, "Preparando análisis de notas...");

        var debug = new MelodyTranscriber.TranscriptionDebugInfo
        {
            DurationSeconds = duration,
            SourceSampleRate = clip.frequency,
            SourceChannels = clip.channels,
            TotalSourceSamples = clip.samples
        };

        float[] mono = MelodyTranscriber.ExtractMonoSamplesPublic(clip, ref debug);
        Destroy(clip);
        clip = null;

        if (cancelImportRequested) yield break;

        MelodyTranscriber.TranscriptionResult transcription = default;
        Exception transcriptionError = null;
        bool transcriptionDone = false;

        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                transcription = MelodyTranscriber.TranscribeFromMono(mono, duration, displayTitle, destAudioPath, ref debug);
            }
            catch (Exception ex)
            {
                transcriptionError = ex;
            }
            finally
            {
                transcriptionDone = true;
            }
        });

        float analysisStart = Time.unscaledTime;
        float estimateSeconds = Mathf.Clamp(duration * 0.25f + 5f, 10f, 60f);
        while (!transcriptionDone)
        {
            if (cancelImportRequested)
                yield break;

            float elapsed = Time.unscaledTime - analysisStart;
            int percent = Mathf.Clamp(Mathf.RoundToInt(elapsed / estimateSeconds * 100f), 0, 99);
            OnImportProgress?.Invoke(CustomSongImportStage.GeneratingNotes,
                $"Extrayendo notas... {percent}%");
            yield return null;
        }

        if (cancelImportRequested) yield break;

        if (transcriptionError != null)
        {
            Debug.LogError("[CustomSongManager] Error en transcripción: " + transcriptionError);
            CleanupPartialImport(safeName, extension);
            OnImportFailed?.Invoke("Error analizando la canción: " + transcriptionError.Message);
            yield break;
        }

        MelodyTranscriber.LogTranscriptionSummary(safeName, transcription);

        if (transcription.NoteCount == 0)
        {
            CleanupPartialImport(safeName, extension);
            OnImportFailed?.Invoke("No se detectaron notas. Prueba con una pista más melódica.");
            yield break;
        }

        if (transcription.NoteCount > maxNotesForGameplay)
            Debug.LogWarning($"[CustomSongManager] Canción con {transcription.NoteCount} notas; puede afectar rendimiento.");

        OnImportProgress?.Invoke(CustomSongImportStage.Saving, "Guardando...");
        string jsonPath = MelodyTranscriber.SaveTranscription(transcription, customSongsPath, safeName);
        SaveMetadata(safeName, displayTitle, pathForAnalysis, duration, transcription);

        var entry = new CustomSongEntry
        {
            Id = safeName,
            Title = displayTitle,
            JsonPath = jsonPath,
            AudioPath = pathForAnalysis,
            EstimatedBpm = transcription.EstimatedBpm,
            NoteCount = transcription.NoteCount,
            MusicalKey = transcription.EstimatedKey.keyName,
            KeyConfidence = transcription.EstimatedKey.confidence,
            DurationSeconds = duration,
            IsCustom = true
        };

        OnImportProgress?.Invoke(CustomSongImportStage.Ready, "Canción lista.");
        OnSongImported?.Invoke(entry);
    }

    public bool DeleteSong(string songId)
    {
        if (string.IsNullOrEmpty(songId))
            return false;

        string jsonPath = Path.Combine(customSongsPath, songId + ".json");
        string metaPath = Path.Combine(customSongsPath, songId + ".meta.json");
        string logPath = Path.Combine(customSongsPath, songId + ".transcription.log");
        string mp3Path = Path.Combine(customSongsPath, songId + ".mp3");
        string wavPath = Path.Combine(customSongsPath, songId + ".wav");

        if (!File.Exists(jsonPath))
            return false;

        SafeDelete(jsonPath);
        SafeDelete(metaPath);
        SafeDelete(logPath);
        SafeDelete(mp3Path);
        SafeDelete(wavPath);

        OnSongDeleted?.Invoke(songId);
        Debug.Log($"[CustomSongManager] Canción eliminada: {songId}");
        return true;
    }

    public SongListarResponse ToSongListEntry(CustomSongEntry entry)
    {
        string keyLabel = entry.KeyConfidence >= 0.35f && !string.IsNullOrEmpty(entry.MusicalKey)
            ? entry.MusicalKey
            : "Indeterminada";

        int tempo = entry.EstimatedBpm > 0f ? Mathf.RoundToInt(entry.EstimatedBpm) : 0;

        return new SongListarResponse
        {
            title = entry.Title,
            artist_name = "Mis canciones",
            mode = "CANTO",
            file_path = entry.Id,
            musical_key = keyLabel,
            tempo = tempo,
            duration = Mathf.RoundToInt(entry.DurationSeconds),
            is_custom = true
        };
    }

    private void SaveMetadata(string id, string title, string audioPath, float duration,
        MelodyTranscriber.TranscriptionResult transcription)
    {
        var meta = new SongImportMetadata
        {
            id = id,
            title = title,
            audioFileName = Path.GetFileName(audioPath),
            noteDataFileName = id + ".json",
            noteCount = transcription.NoteCount,
            estimatedBpm = transcription.EstimatedBpm,
            estimatedKey = transcription.EstimatedKey.keyName,
            keyConfidence = transcription.EstimatedKey.confidence,
            durationSeconds = duration,
            importedAtUtc = DateTime.UtcNow.ToString("o"),
            analysisType = "melody_pipeline_v5",
            midiFileGenerated = false
        };

        string metaPath = Path.Combine(customSongsPath, id + ".meta.json");
        File.WriteAllText(metaPath, JsonUtility.ToJson(meta, true));
    }

    private string EnsureUniqueSongId(string baseName)
    {
        string candidate = baseName;
        int suffix = 1;
        while (SongFilesExist(candidate))
        {
            candidate = $"{baseName}_{suffix}";
            suffix++;
        }

        return candidate;
    }

    private bool SongFilesExist(string id)
    {
        return File.Exists(Path.Combine(customSongsPath, id + ".json"))
            || File.Exists(Path.Combine(customSongsPath, id + ".mp3"))
            || File.Exists(Path.Combine(customSongsPath, id + ".wav"));
    }

    private void CleanupPartialImport(string id, string extension)
    {
        SafeDelete(Path.Combine(customSongsPath, id + extension));
        SafeDelete(Path.Combine(customSongsPath, id + ".wav"));
        SafeDelete(Path.Combine(customSongsPath, id + ".json"));
        SafeDelete(Path.Combine(customSongsPath, id + ".meta.json"));
    }

    private static void SafeDelete(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        try { File.Delete(path); }
        catch (Exception ex) { Debug.LogWarning("[CustomSongManager] No se pudo borrar: " + path + " | " + ex.Message); }
    }

    private static bool IsSupportedAudioExtension(string extension)
    {
        switch (extension)
        {
            case ".mp3":
            case ".wav":
            case ".mpeg":
            case ".m4a":
            case ".aac":
                return true;
            default:
                return false;
        }
    }

    private static bool NeedsNativeDecode(string extension)
    {
        switch (extension)
        {
            case ".mp3":
            case ".mpeg":
            case ".m4a":
            case ".aac":
                return true;
            default:
                return false;
        }
    }

    private static string SanitizeName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
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
    public string MusicalKey;
    public float KeyConfidence;
    public float DurationSeconds;
    public bool IsCustom;
}

[Serializable]
public class SongImportMetadata
{
    public string id;
    public string title;
    public string audioFileName;
    public string noteDataFileName;
    public int noteCount;
    public float estimatedBpm;
    public string estimatedKey;
    public float keyConfidence;
    public float durationSeconds;
    public string importedAtUtc;
    public string analysisType;
    public bool midiFileGenerated;
}

public enum CustomSongImportStage
{
    Loading,
    Analyzing,
    GeneratingNotes,
    Saving,
    Ready
}
