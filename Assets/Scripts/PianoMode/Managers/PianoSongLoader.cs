using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Wrapper para JsonUtility: no soporta List&lt;T&gt; en la raíz, así que se leen arrays.
/// Los nombres de campo son el contrato con el JSON y no deben cambiar.
/// </summary>
[System.Serializable]
public class PianoSongDataWrapper
{
    public string song_title;
    public string artist;
    public int tempo;
    public string background_music;
    public string audio_file;
    public float piano_volume = 1.0f;
    public float audio_file_volume = 1.0f;

    public GameNoteData[] all_notes;
    public PianoNoteData[] melody;
    public PianoChordData[] chords;
}

/// <summary>
/// Carga canciones de piano (JSON + AudioClip) desde StreamingAssets.
/// </summary>
public class PianoSongLoader : MonoBehaviour
{
    private const string SONGS_FOLDER = "PianoSongs/Songs/";
    private const string MUSIC_FOLDER = "PianoSongs/BackgroundMusic/";

    /// <param name="fileName">Nombre del archivo JSON (ej: "rocketman.json"), admite ruta relativa.</param>
    public void LoadSong(string fileName, System.Action<PianoSongData> onSuccess, System.Action<string> onError) =>
        StartCoroutine(LoadSongCoroutine(fileName, onSuccess, onError));

    private IEnumerator LoadSongCoroutine(string fileName, System.Action<PianoSongData> onSuccess, System.Action<string> onError)
    {
        string jsonPath = Path.Combine(Application.streamingAssetsPath, SONGS_FOLDER, Path.GetFileName(fileName));
        string jsonContent = null;

        #if UNITY_ANDROID && !UNITY_EDITOR
        // En Android StreamingAssets vive dentro del APK, hay que leerlo vía jar:// con UnityWebRequest.
        using (UnityWebRequest www = UnityWebRequest.Get(jsonPath))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[PianoLoader] Error leyendo {jsonPath}: {www.error}");
                onError?.Invoke($"Error leyendo JSON: {www.error}");
                yield break;
            }

            jsonContent = www.downloadHandler.text;
        }
        #else
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"[PianoLoader] Archivo no existe: {jsonPath}");
            onError?.Invoke($"Archivo no encontrado: {jsonPath}");
            yield break;
        }

        try
        {
            jsonContent = File.ReadAllText(jsonPath);
        }
        catch (System.Exception e)
        {
            onError?.Invoke($"Error leyendo archivo: {e.Message}");
            yield break;
        }
        #endif

        PianoSongDataWrapper wrapper;
        try
        {
            wrapper = JsonUtility.FromJson<PianoSongDataWrapper>(jsonContent);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PianoLoader] Error parseando JSON ({jsonContent?.Length} chars): {e.Message}");
            onError?.Invoke($"Error parseando JSON: {e.Message}");
            yield break;
        }

        if (wrapper == null)
        {
            Debug.LogError("[PianoLoader] El JSON no se pudo parsear como wrapper");
            onError?.Invoke("El JSON no se pudo parsear correctamente");
            yield break;
        }

        PianoSongData songData = new PianoSongData
        {
            song_title = wrapper.song_title,
            artist = wrapper.artist,
            tempo = wrapper.tempo,
            background_music = wrapper.background_music,
            audio_file = wrapper.audio_file,
            piano_volume = wrapper.piano_volume,
            audio_file_volume = wrapper.audio_file_volume,
            all_notes = new List<GameNoteData>(wrapper.all_notes ?? new GameNoteData[0]),
            melody = new List<PianoNoteData>(wrapper.melody ?? new PianoNoteData[0]),
            chords = new List<PianoChordData>(wrapper.chords ?? new PianoChordData[0])
        };

        if (songData.all_notes.Count <= 0 && songData.melody.Count <= 0)
            Debug.LogError("[PianoLoader] La canción no trae notas: all_notes y melody están vacíos.");

        string audioFileToLoad = songData.GetAudioPath();
        if (string.IsNullOrEmpty(audioFileToLoad))
        {
            Debug.LogWarning("[PianoLoader] No se encontró ruta de audio (audio_file ni background_music)");
            onSuccess?.Invoke(songData);
            yield break;
        }

        string audioPath = Path.Combine(Application.streamingAssetsPath, MUSIC_FOLDER, Path.GetFileName(audioFileToLoad));
        using (UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip(audioPath, AudioType.MPEG))
        {
            yield return audioRequest.SendWebRequest();

            // Quedarse sin audio no es fatal: el gameplay continúa sin pista de fondo.
            if (audioRequest.result != UnityWebRequest.Result.Success)
                Debug.LogWarning($"[PianoLoader] Error cargando audio {audioPath}: {audioRequest.error}");
            else
                songData.backgroundAudioClip = DownloadHandlerAudioClip.GetContent(audioRequest);
        }

        onSuccess?.Invoke(songData);
    }

    public bool SongExists(string fileName)
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        // Dentro del APK no se puede comprobar con File.Exists; se asume presente.
        return true;
        #else
        return File.Exists(Path.Combine(Application.streamingAssetsPath, SONGS_FOLDER, fileName));
        #endif
    }
}
