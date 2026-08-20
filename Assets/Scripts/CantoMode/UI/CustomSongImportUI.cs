using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI para importar canciones MP3/WAV locales en el repertorio de canto.
/// En Quest: copia archivos a persistentDataPath/CustomSongs/ o usa el selector nativo.
/// </summary>
public class CustomSongImportUI : MonoBehaviour
{
    [SerializeField] private Button btnImportSong;
    [SerializeField] private TMP_InputField inputSongName;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Transform customSongContainer;
    [SerializeField] private GameObject customSongRowPrefab;

    private CustomSongManager songManager;

    void Start()
    {
        if (CustomSongManager.Instance == null)
        {
            var go = new GameObject("CustomSongManager");
            go.AddComponent<CustomSongManager>();
        }

        songManager = CustomSongManager.Instance;
        songManager.OnSongImported += HandleSongImported;
        songManager.OnImportFailed += HandleImportFailed;

        if (btnImportSong != null)
            btnImportSong.onClick.AddListener(BeginImport);

        RefreshCustomSongList();
    }

    void OnDestroy()
    {
        if (songManager == null)
            return;
        songManager.OnSongImported -= HandleSongImported;
        songManager.OnImportFailed -= HandleImportFailed;
    }

    public void BeginImport()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidSongPicker.PickAudioFile(OnPickedFile);
#else
        string samplePath = Path.Combine(Application.streamingAssetsPath, "SingSongs", "Songs");
        if (Directory.Exists(samplePath))
        {
            string[] mp3s = Directory.GetFiles(samplePath, "*.mp3");
            if (mp3s.Length > 0)
            {
                ImportFile(mp3s[0]);
                return;
            }
        }

        SetStatus("En editor: coloca un MP3 en StreamingAssets o usa adb push hacia CustomSongs.");
#endif
    }

    private void OnPickedFile(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            SetStatus("Importación cancelada.");
            return;
        }

        ImportFile(path);
    }

    private void ImportFile(string path)
    {
        SetStatus("Analizando audio...");
        string displayName = inputSongName != null && !string.IsNullOrWhiteSpace(inputSongName.text)
            ? inputSongName.text
            : Path.GetFileNameWithoutExtension(path);

        StartCoroutine(songManager.ImportFromFile(path, displayName, _ => RefreshCustomSongList()));
    }

    private void HandleSongImported(CustomSongEntry entry)
    {
        SetStatus($"Importada: {entry.Title} ({entry.NoteCount} notas, ~{entry.EstimatedBpm:F0} BPM)");
        RefreshCustomSongList();
    }

    private void HandleImportFailed(string error)
    {
        SetStatus("Error: " + error);
    }

    private void RefreshCustomSongList()
    {
        if (customSongContainer == null || customSongRowPrefab == null || songManager == null)
            return;

        foreach (Transform child in customSongContainer)
            Destroy(child.gameObject);

        IReadOnlyList<CustomSongEntry> songs = songManager.ListCustomSongs();
        foreach (CustomSongEntry entry in songs)
        {
            GameObject row = Instantiate(customSongRowPrefab, customSongContainer);
            TMP_Text label = row.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = $"{entry.Title} ({entry.NoteCount} notas)";

            Button playBtn = row.GetComponentInChildren<Button>();
            if (playBtn != null)
            {
                CustomSongEntry captured = entry;
                playBtn.onClick.AddListener(() => LaunchCustomSong(captured));
            }
        }
    }

    private void LaunchCustomSong(CustomSongEntry entry)
    {
        if (SelectedSongManager.Instance == null)
            return;

        SelectedSongManager.Instance.selectedSong = songManager.ToSongListEntry(entry);
        SelectedSongManager.Instance.BeginSongSelectionMeasurement(SelectedSongManager.Instance.selectedSong, "SingGame");
        UnityEngine.SceneManagement.SceneManager.LoadScene("SingGame");
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
        Debug.Log("[CustomSongImportUI] " + message);
    }
}

#if UNITY_ANDROID && !UNITY_EDITOR
public static class AndroidSongPicker
{
    public static void PickAudioFile(System.Action<string> callback)
    {
        var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.GET_CONTENT");
        intent.Call<AndroidJavaObject>("setType", "audio/*");
        intent.Call<AndroidJavaObject>("addCategory", "android.intent.category.OPENABLE");

        var callbackProxy = new SongPickerCallback(callback);
        activity.Call("startActivityForResult", intent, 9001);

        // Fallback: escanea carpeta CustomSongs si el intent no está cableado
        string customPath = Path.Combine(Application.persistentDataPath, CustomSongManager.CustomSongsFolderName);
        if (Directory.Exists(customPath))
        {
            string[] files = Directory.GetFiles(customPath, "*.mp3");
            if (files.Length > 0)
                callback?.Invoke(files[0]);
        }
    }

    private class SongPickerCallback : AndroidJavaProxy
    {
        private readonly System.Action<string> _callback;

        public SongPickerCallback(System.Action<string> callback) : base("java.lang.Object")
        {
            _callback = callback;
        }
    }
}
#endif
