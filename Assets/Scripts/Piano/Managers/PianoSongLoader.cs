using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.Collections;

/// <summary>
/// Carga datos de canciones de piano desde archivos JSON y AudioClips
/// </summary>
public class PianoSongLoader : MonoBehaviour
{
    private const string SONGS_FOLDER = "PianoSongs/Songs/";
    private const string MUSIC_FOLDER = "PianoSongs/BackgroundMusic/";
    
    /// <summary>
    /// Carga una canción de piano desde JSON y su soundtrack
    /// </summary>
    /// <param name="fileName">Nombre del archivo JSON (ej: "rocketman.json")</param>
    /// <param name="onSuccess">Callback cuando se carga exitosamente</param>
    /// <param name="onError">Callback cuando hay un error</param>
    public void LoadSong(string fileName, System.Action<PianoSongData> onSuccess, System.Action<string> onError)
    {
        StartCoroutine(LoadSongCoroutine(fileName, onSuccess, onError));
    }
    
    private IEnumerator LoadSongCoroutine(string fileName, System.Action<PianoSongData> onSuccess, System.Action<string> onError)
    {
        // 1. Construir ruta completa del JSON
        string jsonPath = Path.Combine(Application.streamingAssetsPath, SONGS_FOLDER, fileName);
        
        Debug.Log($"<color=cyan>[PianoLoader]</color> Cargando: {jsonPath}");
        
        // 2. Leer el archivo JSON
        string jsonContent = null;
        
        #if UNITY_ANDROID && !UNITY_EDITOR
        // En Android, StreamingAssets requiere UnityWebRequest
        using (UnityWebRequest www = UnityWebRequest.Get(jsonPath))
        {
            yield return www.SendWebRequest();
            
            if (www.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Error leyendo JSON: {www.error}");
                yield break;
            }
            
            jsonContent = www.downloadHandler.text;
        }
        #else
        // En PC/Editor, usar File.ReadAllText
        if (!File.Exists(jsonPath))
        {
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
        
        // 3. Parsear JSON a PianoSongData
        PianoSongData songData;
        try
        {
            songData = JsonUtility.FromJson<PianoSongData>(jsonContent);
            
            if (songData == null)
            {
                onError?.Invoke("El JSON no se pudo parsear correctamente");
                yield break;
            }
            
            Debug.Log($"<color=green>[PianoLoader]</color> JSON parseado: {songData.song_title}");
            Debug.Log($"<color=green>[PianoLoader]</color> Melodía: {songData.TotalMelodyNotes} notas | Acordes: {songData.TotalChords}");
        }
        catch (System.Exception e)
        {
            onError?.Invoke($"Error parseando JSON: {e.Message}");
            yield break;
        }
        
        // 4. Cargar AudioClip del soundtrack
        if (!string.IsNullOrEmpty(songData.background_music))
        {
            string audioPath = Path.Combine(Application.streamingAssetsPath, MUSIC_FOLDER, songData.background_music);
            
            Debug.Log($"<color=cyan>[PianoLoader]</color> Cargando audio: {audioPath}");
            
            using (UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip(audioPath, AudioType.MPEG))
            {
                yield return audioRequest.SendWebRequest();
                
                if (audioRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"<color=yellow>[PianoLoader]</color> Error cargando audio: {audioRequest.error}");
                    // No es error crítico, continuar sin audio
                }
                else
                {
                    songData.backgroundAudioClip = DownloadHandlerAudioClip.GetContent(audioRequest);
                    Debug.Log($"<color=green>[PianoLoader]</color> Audio cargado: {songData.backgroundAudioClip.length:F1}s");
                }
            }
        }
        
        // 5. Éxito - retornar datos
        onSuccess?.Invoke(songData);
    }
    
    /// <summary>
    /// Verifica si existe un archivo de canción
    /// </summary>
    public bool SongExists(string fileName)
    {
        string jsonPath = Path.Combine(Application.streamingAssetsPath, SONGS_FOLDER, fileName);
        
        #if UNITY_ANDROID && !UNITY_EDITOR
        // En Android es más complejo verificar, retornar true por defecto
        return true;
        #else
        return File.Exists(jsonPath);
        #endif
    }
}
