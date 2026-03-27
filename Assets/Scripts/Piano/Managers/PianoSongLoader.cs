using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// WRAPPER para JsonUtility - necesario porque JsonUtility no soporta List<T> nativamente
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
    
    public GameNoteData[] all_notes;  // Array en lugar de List
    public PianoNoteData[] melody;     // Array en lugar de List
    public PianoChordData[] chords;    // Array en lugar de List
}

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
        // 1. Extraer solo el nombre del archivo por si viene con ruta relativa
        // Si fileName es "songs/rocketman.json", extraer "rocketman.json"
        string fileNameOnly = Path.GetFileName(fileName);
        
        // Construir ruta completa del JSON
        string jsonPath = Path.Combine(Application.streamingAssetsPath, SONGS_FOLDER, fileNameOnly);
        
        Debug.Log($"<color=cyan>[PianoLoader]</color> 📂 Intentando cargar: {jsonPath}");
        
        // 2. Leer el archivo JSON
        string jsonContent = null;
        
        #if UNITY_ANDROID && !UNITY_EDITOR
        // En Android, StreamingAssets requiere UnityWebRequest con protocolo jar://
        Debug.Log($"<color=cyan>[PianoLoader]</color> 🔍 Plataforma: Android");
        using (UnityWebRequest www = UnityWebRequest.Get(jsonPath))
        {
            yield return www.SendWebRequest();
            
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"<color=red>[PianoLoader]</color> ❌ Error con ruta: {jsonPath}");
                Debug.LogError($"<color=red>[PianoLoader]</color> ❌ Error exacto: {www.error}");
                onError?.Invoke($"Error leyendo JSON: {www.error}");
                yield break;
            }
            
            jsonContent = www.downloadHandler.text;
            Debug.Log($"<color=green>[PianoLoader]</color> ✅ JSON cargado desde Android");
        }
        #else
        // En PC/Editor, usar File.ReadAllText
        Debug.Log($"<color=cyan>[PianoLoader]</color> 🔍 Plataforma: PC/Editor");
        
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"<color=red>[PianoLoader]</color> ❌ Archivo NO existe: {jsonPath}");
            Debug.LogError($"<color=red>[PianoLoader]</color> ❌ StreamingAssets path: {Application.streamingAssetsPath}");
            Debug.LogError($"<color=red>[PianoLoader]</color> ❌ Nombre archivo: {fileNameOnly}");
            onError?.Invoke($"Archivo no encontrado: {jsonPath}");
            yield break;
        }
        
        try
        {
            jsonContent = File.ReadAllText(jsonPath);
            Debug.Log($"<color=green>[PianoLoader]</color> ✅ JSON cargado desde PC/Editor");
        }
        catch (System.Exception e)
        {
            onError?.Invoke($"Error leyendo archivo: {e.Message}");
            yield break;
        }
        #endif
        
        // 3. Parsear JSON a PianoSongDataWrapper (usando wrapper para soportar arrays/listas)
        PianoSongDataWrapper wrapper;
        try
        {
            wrapper = JsonUtility.FromJson<PianoSongDataWrapper>(jsonContent);
            
            if (wrapper == null)
            {
                Debug.LogError("[PianoLoader] ❌ El JSON no se pudo parsear como wrapper");
                onError?.Invoke("El JSON no se pudo parsear correctamente");
                yield break;
            }
            
            Debug.Log($"<color=green>[PianoLoader]</color> ✅ JSON parseado exitosamente");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PianoLoader] ❌ Error parseando JSON: {e.Message}");
            Debug.LogError($"[PianoLoader] JSON content length: {jsonContent?.Length}");
            onError?.Invoke($"Error parseando JSON: {e.Message}");
            yield break;
        }
        
        // 4. Convertir wrapper a PianoSongData y convertir arrays a listas
        PianoSongData songData = new PianoSongData();
        songData.song_title = wrapper.song_title;
        songData.artist = wrapper.artist;
        songData.tempo = wrapper.tempo;
        songData.background_music = wrapper.background_music;
        songData.audio_file = wrapper.audio_file;
        songData.piano_volume = wrapper.piano_volume;
        songData.audio_file_volume = wrapper.audio_file_volume;
        
        // Convertir arrays a listas
        songData.all_notes = new List<GameNoteData>(wrapper.all_notes ?? new GameNoteData[0]);
        songData.melody = new List<PianoNoteData>(wrapper.melody ?? new PianoNoteData[0]);
        songData.chords = new List<PianoChordData>(wrapper.chords ?? new PianoChordData[0]);
        
        Debug.Log($"<color=green>[PianoLoader]</color> ✅ Datos convertidos:");
        Debug.Log($"<color=green>[PianoLoader]</color>    - all_notes: {songData.all_notes.Count} elementos");
        Debug.Log($"<color=green>[PianoLoader]</color>    - melody: {songData.melody.Count} elementos");
        Debug.Log($"<color=green>[PianoLoader]</color>    - chords: {songData.chords.Count} elementos");
        
        if (songData.all_notes.Count > 0)
        {
            Debug.Log($"<color=green>[PianoLoader]</color> 🟢 USANDO FORMATO NUEVO (all_notes)");
            Debug.Log($"<color=green>[PianoLoader]</color>    Primera nota: MIDI {songData.all_notes[0].GetMidiNote()} a {songData.all_notes[0].time:F2}s");
        }
        else if (songData.melody.Count > 0)
        {
            Debug.Log($"<color=yellow>[PianoLoader]</color> 🟡 USANDO FORMATO ANTIGUO (melody)");
        }
        else
        {
            Debug.LogError($"[PianoLoader] 🔴 ¡¡¡NINGÚN FORMATO DISPONIBLE!!! all_notes = {songData.all_notes.Count} y melody = {songData.melody.Count}");
        }
        
        // 5. Cargar AudioClip del soundtrack
        // Prioridad: audio_file (nuevo formato) > background_music (formato antiguo)
        string audioFileToLoad = null;
        if (!string.IsNullOrEmpty(songData.audio_file))
        {
            audioFileToLoad = songData.audio_file;
            Debug.Log($"<color=cyan>[PianoLoader]</color> 📁 Usando audio_file (formato nuevo): {audioFileToLoad}");
        }
        else if (!string.IsNullOrEmpty(songData.background_music))
        {
            audioFileToLoad = songData.background_music;
            Debug.Log($"<color=cyan>[PianoLoader]</color> 📁 Usando background_music (formato antiguo): {audioFileToLoad}");
        }
        
        if (!string.IsNullOrEmpty(audioFileToLoad))
        {
            // Extraer solo el nombre del archivo por si viene la ruta completa
            // Ejemplo: "PianoSongs/BackgroundMusic/rocketman.mp3" → "rocketman.mp3"
            string audioFileName = Path.GetFileName(audioFileToLoad);
            string audioPath = Path.Combine(Application.streamingAssetsPath, MUSIC_FOLDER, audioFileName);
            
            Debug.Log($"<color=cyan>[PianoLoader]</color> 🔊 Cargando audio: {audioPath}");
            
            using (UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip(audioPath, AudioType.MPEG))
            {
                yield return audioRequest.SendWebRequest();
                
                if (audioRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"<color=yellow>[PianoLoader]</color> ❌ Error cargando audio: {audioRequest.error}");
                    Debug.LogWarning($"<color=yellow>[PianoLoader]</color> Ruta intentada: {audioPath}");
                    // No es error crítico, continuar sin audio
                }
                else
                {
                    songData.backgroundAudioClip = DownloadHandlerAudioClip.GetContent(audioRequest);
                    Debug.Log($"<color=green>[PianoLoader]</color> ✅ Audio cargado: {songData.backgroundAudioClip.length:F1}s");
                }
            }
        }
        else
        {
            Debug.LogWarning("<color=yellow>[PianoLoader]</color> ⚠️ No se encontró ruta de audio (audio_file ni background_music)");
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
