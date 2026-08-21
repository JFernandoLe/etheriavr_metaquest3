using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Carga de audio local compatible con Android/Quest.
/// StreamingAssets (APK) y persistentDataPath usan rutas distintas.
/// </summary>
public static class StreamingAssetsAudioLoader
{
    public static string BuildRequestUrl(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        path = path.Replace('\\', '/');

        if (path.Contains("://"))
            return path;

        return "file://" + path;
    }

    /// <summary>
    /// Canciones predefinidas embebidas en StreamingAssets (dentro del APK en Android).
    /// No usa File.Exists: en Quest los archivos del APK no son accesibles con System.IO.
    /// </summary>
    public static IEnumerator LoadStreamingAssetsAudioClip(
        string relativePathWithoutExtension,
        System.Action<AudioClip, AudioType> onSuccess,
        System.Action<string> onError)
    {
        string basePath = Application.streamingAssetsPath.TrimEnd('/', '\\');
        string[] extensions = { ".wav", ".mp3" };
        AudioType[] types = { AudioType.WAV, AudioType.MPEG };

        string lastError = null;

        for (int i = 0; i < extensions.Length; i++)
        {
            string absolutePath = basePath + "/" + relativePathWithoutExtension + extensions[i];

#if !UNITY_ANDROID || UNITY_EDITOR
            if (!File.Exists(absolutePath))
                continue;
#endif

            bool loaded = false;
            yield return LoadFromStreamingUrl(absolutePath, types[i],
                clip =>
                {
                    loaded = true;
                    onSuccess?.Invoke(clip, types[i]);
                },
                error => lastError = error);

            if (loaded)
                yield break;
        }

        onError?.Invoke(lastError ??
            $"No se encontró audio para '{relativePathWithoutExtension}' en StreamingAssets.");
    }

    /// <summary>
    /// Alias mantenido por compatibilidad con SongLoader.
    /// </summary>
    public static IEnumerator LoadAudioClip(
        string relativePathWithoutExtension,
        System.Action<AudioClip, AudioType> onSuccess,
        System.Action<string> onError)
    {
        yield return LoadStreamingAssetsAudioClip(relativePathWithoutExtension, onSuccess, onError);
    }

    public static IEnumerator LoadText(
        string relativePath,
        System.Action<string> onSuccess,
        System.Action<string> onError)
    {
        string url = BuildRequestUrl(Application.streamingAssetsPath.TrimEnd('/', '\\') + "/" + relativePath);
        using UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(request.error);
            yield break;
        }

        onSuccess?.Invoke(request.downloadHandler.text);
    }

    /// <summary>
    /// Canciones importadas en persistentDataPath (accesibles con System.IO en Quest).
    /// </summary>
    public static IEnumerator LoadPersistentAudioClip(
        string absolutePath,
        System.Action<AudioClip> onSuccess,
        System.Action<string> onError)
    {
        if (Path.GetExtension(absolutePath).Equals(".wav", System.StringComparison.OrdinalIgnoreCase))
        {
            AudioClip direct = TryLoadWavDirect(absolutePath);
            if (IsValidClip(direct))
            {
                Debug.Log($"[StreamingAssetsAudioLoader] WAV directo: {direct.name}, {direct.length:F1}s");
                onSuccess?.Invoke(direct);
                yield break;
            }
        }

        AudioType primary = GuessAudioType(absolutePath);
        AudioType[] fallbacks = primary == AudioType.WAV
            ? new[] { AudioType.WAV, AudioType.UNKNOWN }
            : new[] { AudioType.MPEG, AudioType.UNKNOWN, AudioType.WAV };

        yield return LoadFromPersistentPath(absolutePath, primary, fallbacks, onSuccess, onError);
    }

    /// <summary>
    /// Lee WAV PCM 16-bit sin UnityWebRequest (fiable en Quest para archivos locales).
    /// </summary>
    public static AudioClip TryLoadWavDirect(string absolutePath)
    {
        try
        {
            if (!File.Exists(absolutePath))
                return null;

            byte[] data = File.ReadAllBytes(absolutePath);
            if (data.Length < 44)
                return null;

            if (data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F')
                return null;

            int channels = data[22] | (data[23] << 8);
            int sampleRate = data[24] | (data[25] << 8) | (data[26] << 16) | (data[27] << 24);
            int bitsPerSample = data[34] | (data[35] << 8);

            if (channels <= 0 || sampleRate <= 0 || bitsPerSample != 16)
                return null;

            int dataOffset = 44;
            for (int i = 12; i + 8 <= data.Length; i++)
            {
                if (data[i] == 'd' && data[i + 1] == 'a' && data[i + 2] == 't' && data[i + 3] == 'a')
                {
                    dataOffset = i + 8;
                    break;
                }
            }

            int sampleCount = (data.Length - dataOffset) / (2 * channels);
            if (sampleCount <= 0)
                return null;

            float[] samples = new float[sampleCount * channels];
            for (int i = 0; i < samples.Length; i++)
            {
                int byteIndex = dataOffset + i * 2;
                if (byteIndex + 1 >= data.Length)
                    break;
                short pcm = (short)(data[byteIndex] | (data[byteIndex + 1] << 8));
                samples[i] = pcm / 32768f;
            }

            string clipName = Path.GetFileNameWithoutExtension(absolutePath);
            AudioClip clip = AudioClip.Create(clipName, sampleCount, channels, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[StreamingAssetsAudioLoader] WAV directo falló: " + ex.Message);
            return null;
        }
    }

    private static IEnumerator LoadFromStreamingUrl(
        string streamingPath,
        AudioType audioType,
        System.Action<AudioClip> onSuccess,
        System.Action<string> onError)
    {
        string url = BuildRequestUrl(streamingPath);
        Debug.Log($"[StreamingAssetsAudioLoader] StreamingAssets: {url} ({audioType})");

        using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
        DownloadHandlerAudioClip handler = request.downloadHandler as DownloadHandlerAudioClip;
        if (handler != null)
        {
            handler.streamAudio = false;
            handler.compressed = false;
        }

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"[{audioType}] {request.error}");
            yield break;
        }

        AudioClip clip = handler != null ? handler.audioClip : DownloadHandlerAudioClip.GetContent(request);
        if (!IsValidClip(clip))
        {
            onError?.Invoke($"[{audioType}] Clip inválido (samples={clip?.samples ?? 0}, length={clip?.length ?? 0f:F2})");
            yield break;
        }

        Debug.Log($"[StreamingAssetsAudioLoader] OK StreamingAssets {audioType}: {clip.name}, {clip.length:F1}s");
        onSuccess?.Invoke(clip);
    }

    private static IEnumerator LoadFromPersistentPath(
        string absolutePath,
        AudioType primaryType,
        AudioType[] fallbackTypes,
        System.Action<AudioClip> onSuccess,
        System.Action<string> onError)
    {
        if (!File.Exists(absolutePath))
        {
            onError?.Invoke("Archivo no encontrado: " + absolutePath);
            yield break;
        }

        long fileSize = new FileInfo(absolutePath).Length;
        if (fileSize < 512)
        {
            onError?.Invoke($"Archivo vacío o corrupto ({fileSize} bytes): {absolutePath}");
            yield break;
        }

        string lastError = null;
        var typesToTry = new System.Collections.Generic.List<AudioType> { primaryType };
        if (fallbackTypes != null)
        {
            foreach (AudioType type in fallbackTypes)
            {
                if (!typesToTry.Contains(type))
                    typesToTry.Add(type);
            }
        }

        string url = BuildRequestUrl(absolutePath);
        Debug.Log($"[StreamingAssetsAudioLoader] Persistent: ({fileSize} bytes) {url}");

        foreach (AudioType audioType in typesToTry)
        {
            using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
            DownloadHandlerAudioClip handler = request.downloadHandler as DownloadHandlerAudioClip;
            if (handler != null)
            {
                handler.streamAudio = false;
                handler.compressed = false;
            }

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                lastError = $"[{audioType}] {request.error}";
                Debug.LogWarning($"[StreamingAssetsAudioLoader] Falló {audioType}: {request.error}");
                continue;
            }

            AudioClip clip = handler != null ? handler.audioClip : DownloadHandlerAudioClip.GetContent(request);
            if (IsValidClip(clip))
            {
                Debug.Log($"[StreamingAssetsAudioLoader] OK persistent {audioType}: {clip.name}, {clip.length:F1}s");
                onSuccess?.Invoke(clip);
                yield break;
            }

            lastError = $"[{audioType}] Clip decodificado inválido (samples={clip?.samples ?? 0}, length={clip?.length ?? 0f:F2})";
            Debug.LogWarning("[StreamingAssetsAudioLoader] " + lastError);
        }

        onError?.Invoke(string.IsNullOrEmpty(lastError)
            ? "No se pudo decodificar el archivo de audio."
            : lastError);
    }

    private static bool IsValidClip(AudioClip clip)
    {
        return clip != null && clip.samples > 0 && clip.length > 0.05f && clip.channels > 0;
    }

    private static AudioType GuessAudioType(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        switch (ext)
        {
            case ".wav":
                return AudioType.WAV;
            case ".mp3":
            case ".mpeg":
                return AudioType.MPEG;
            default:
                return AudioType.UNKNOWN;
        }
    }
}
