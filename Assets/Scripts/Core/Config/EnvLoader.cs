using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Lee <c>StreamingAssets/.env</c> como pares clave=valor.
/// Cachea el resultado: el archivo solo se lee una vez por sesión.
/// </summary>
public static class EnvLoader
{
    private static Dictionary<string, string> cachedEnv;
    private static bool hasLoaded;

    public static Dictionary<string, string> Load(bool forceReload = false)
    {
        if (hasLoaded && !forceReload) return cachedEnv;

        cachedEnv = Parse(ReadRawContent(Path.Combine(Application.streamingAssetsPath, ".env")));
        hasLoaded = true;
        return cachedEnv;
    }

    public static string Get(string key, string defaultValue = "")
    {
        Dictionary<string, string> env = Load();
        return env != null && env.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
    }

    public static bool GetBool(string key, bool defaultValue = false)
    {
        string raw = Get(key, null);
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        return bool.TryParse(raw, out bool parsed) ? parsed : defaultValue;
    }

    private static Dictionary<string, string> Parse(string content)
    {
        var envData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(content)) return envData;

        foreach (string rawLine in content.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;

            int separator = line.IndexOf('=');
            if (separator <= 0) continue;

            string key = line.Substring(0, separator).Trim();
            string value = StripQuotes(line.Substring(separator + 1).Trim());
            if (key.Length > 0) envData[key] = value;
        }

        return envData;
    }

    private static string StripQuotes(string value)
    {
        if (value.Length >= 2)
        {
            char first = value[0];
            if ((first == '"' || first == '\'') && value[value.Length - 1] == first)
                return value.Substring(1, value.Length - 2);
        }

        return value;
    }

    private static string ReadRawContent(string path)
    {
        if (Application.platform != RuntimePlatform.Android)
        {
            if (File.Exists(path)) return File.ReadAllText(path);

            Debug.LogWarning("[EnvLoader] No se encontró StreamingAssets/.env");
            return "";
        }

        // En Quest el .env vive dentro del APK y solo se puede leer por UnityWebRequest.
        // Se bloquea a propósito: ocurre una única vez durante el arranque.
        using (UnityWebRequest request = UnityWebRequest.Get(path))
        {
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone) { }

            if (request.result == UnityWebRequest.Result.Success) return request.downloadHandler.text;

            Debug.LogError($"[EnvLoader] Error cargando .env en Quest: {request.error}");
            return "";
        }
    }
}
