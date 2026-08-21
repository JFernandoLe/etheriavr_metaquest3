using System;
using UnityEngine;

/// <summary>
/// Abre el selector SAF de Android en Quest y devuelve ruta local + nombre original.
/// </summary>
public class QuestSongPicker : MonoBehaviour
{
    private const string PickerObjectName = "QuestSongPicker";
    private const char ResultSeparator = '\t';

    private static QuestSongPicker activeInstance;
    private Action<string, string> pendingCallback;

    public static void PickAudioFile(Action<string, string> onPicked)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        EnsureInstance();
        activeInstance.pendingCallback = onPicked;
        activeInstance.StartAndroidPicker();
#else
        onPicked?.Invoke(null, null);
#endif
    }

    private static void EnsureInstance()
    {
        if (activeInstance != null)
            return;

        var existing = GameObject.Find(PickerObjectName);
        if (existing != null)
        {
            activeInstance = existing.GetComponent<QuestSongPicker>() ?? existing.AddComponent<QuestSongPicker>();
            DontDestroyOnLoad(existing);
            return;
        }

        var go = new GameObject(PickerObjectName);
        DontDestroyOnLoad(go);
        activeInstance = go.AddComponent<QuestSongPicker>();
    }

    private void Awake()
    {
        gameObject.name = PickerObjectName;
    }

    private void StartAndroidPicker()
    {
        try
        {
            using AndroidJavaClass launcher = new AndroidJavaClass("com.etheriavr.audiopicker.AudioPickerLauncher");
            launcher.CallStatic("launch", PickerObjectName, nameof(OnAudioPicked));
        }
        catch (Exception ex)
        {
            Debug.LogError("[QuestSongPicker] No se pudo abrir selector SAF: " + ex);
            pendingCallback?.Invoke(null, null);
            pendingCallback = null;
        }
    }

    public void OnAudioPicked(string payload)
    {
        string path = payload;
        string displayName = null;

        if (!string.IsNullOrEmpty(payload))
        {
            int separatorIndex = payload.IndexOf(ResultSeparator);
            if (separatorIndex >= 0)
            {
                path = payload.Substring(0, separatorIndex);
                displayName = payload.Substring(separatorIndex + 1);
            }
        }

        Debug.Log($"[QuestSongPicker] Archivo: {path} | Nombre: {displayName}");
        pendingCallback?.Invoke(string.IsNullOrEmpty(path) ? null : path,
            string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim());
        pendingCallback = null;
    }
}
