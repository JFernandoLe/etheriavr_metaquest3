using UnityEngine;

public class SelectedSongManager : MonoBehaviour
{
    public static SelectedSongManager Instance { get; private set; }

    [Header("Datos de la Canción Seleccionada")]
    public SongListarResponse selectedSong;
    public double lastSelectionStartTime { get; private set; }
    public string lastSelectionSceneName { get; private set; }
    public string lastSelectionSongTitle { get; private set; }
    public bool hasPendingSelectionMeasurement { get; private set; }

    private double repertoryRequestStartTime;
    private bool repertoryRequestActive;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void BeginRepertoryRequestMeasurement()
    {
        repertoryRequestStartTime = Time.realtimeSinceStartupAsDouble;
        repertoryRequestActive = true;
    }

    public void LogRepertoryRequestCompleted(int songCount)
    {
        if (!repertoryRequestActive)
        {
            return;
        }

        double elapsedSeconds = Time.realtimeSinceStartupAsDouble - repertoryRequestStartTime;
        repertoryRequestActive = false;
        Debug.Log($"[LoadMetrics] Repertorio cargado | canciones={songCount} | endpoint={elapsedSeconds:F2}s");
    }

    public void LogRepertoryRequestFailed(string error)
    {
        if (!repertoryRequestActive)
        {
            return;
        }

        double elapsedSeconds = Time.realtimeSinceStartupAsDouble - repertoryRequestStartTime;
        repertoryRequestActive = false;
        Debug.LogWarning($"[LoadMetrics] Error cargando repertorio tras {elapsedSeconds:F2}s | detalle={error}");
    }

    public void BeginSongSelectionMeasurement(SongListarResponse song, string targetSceneName)
    {
        lastSelectionStartTime = Time.realtimeSinceStartupAsDouble;
        lastSelectionSceneName = targetSceneName;
        lastSelectionSongTitle = song != null ? song.title : "SIN TITULO";
        hasPendingSelectionMeasurement = true;

        string mode = song != null ? song.mode : "DESCONOCIDO";
        Debug.Log($"[LoadMetrics] Cancion seleccionada | titulo={lastSelectionSongTitle} | modo={mode} | escena={targetSceneName} | t0={lastSelectionStartTime:F2}s");
    }

    public void LogSongSelectionCheckpoint(string checkpoint)
    {
        if (!hasPendingSelectionMeasurement)
        {
            return;
        }

        double elapsedSeconds = Time.realtimeSinceStartupAsDouble - lastSelectionStartTime;
        Debug.Log($"[LoadMetrics] {checkpoint} | titulo={lastSelectionSongTitle} | escena={lastSelectionSceneName} | total={elapsedSeconds:F2}s");
    }

    public void CompleteSongSelectionMeasurement(string checkpoint)
    {
        if (!hasPendingSelectionMeasurement)
        {
            return;
        }

        double elapsedSeconds = Time.realtimeSinceStartupAsDouble - lastSelectionStartTime;
        Debug.Log($"[LoadMetrics] {checkpoint} | titulo={lastSelectionSongTitle} | escena={lastSelectionSceneName} | total={elapsedSeconds:F2}s");
        hasPendingSelectionMeasurement = false;
    }
}