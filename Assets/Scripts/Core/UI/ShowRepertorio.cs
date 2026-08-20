using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pide el repertorio al backend e instancia una tarjeta por canción en el ScrollView.
/// También muestra canciones personalizadas importadas localmente en Quest.
/// </summary>
public class ShowRepertorio : MonoBehaviour
{
    [Header("Configuración UI")]
    [SerializeField] private GameObject songBoxPrefab;
    [SerializeField] private Transform songBoxContainer;

    [Header("Servicios")]
    [SerializeField] private AuthService authService;

    private void Start()
    {
        if (CustomSongManager.Instance == null)
        {
            var managerObject = new GameObject("CustomSongManager");
            managerObject.AddComponent<CustomSongManager>();
        }

        CargarDatos();
    }

    private void CargarDatos()
    {
        foreach (Transform child in songBoxContainer) Destroy(child.gameObject);

        SelectedSongManager.Instance?.BeginRepertoryRequestMeasurement();

        StartCoroutine(authService.GetSongs(
            onSuccess: json =>
            {
                SongListWrapper wrapper = JsonUtility.FromJson<SongListWrapper>(json);
                List<SongListarResponse> songs = wrapper?.songs;

                if (songs != null) foreach (SongListarResponse song in songs) CreateSongItem(song);

                SelectedSongManager.Instance?.LogRepertoryRequestCompleted(songs != null ? songs.Count : 0);
                CargarCancionesPersonalizadas();
            },
            onError: err =>
            {
                SelectedSongManager.Instance?.LogRepertoryRequestFailed(err);
                Debug.LogError($"Error al cargar canciones: {err}");
                CargarCancionesPersonalizadas();
            }
        ));
    }

    private void CargarCancionesPersonalizadas()
    {
        if (CustomSongManager.Instance == null || songBoxPrefab == null || songBoxContainer == null)
            return;

        IReadOnlyList<CustomSongEntry> customSongs = CustomSongManager.Instance.ListCustomSongs();
        foreach (CustomSongEntry entry in customSongs)
        {
            SongListarResponse song = CustomSongManager.Instance.ToSongListEntry(entry);
            CreateSongItem(song);
        }
    }

    private void CreateSongItem(SongListarResponse song)
    {
        GameObject newBox = Instantiate(songBoxPrefab, songBoxContainer);

        SongItem item = newBox.GetComponent<SongItem>();
        if (item == null)
        {
            Debug.LogWarning("<color=yellow>[Repertorio]</color> El prefab de canción no tiene componente SongItem");
            return;
        }

        item.Setup(song);
        item.UpdateButtonState(true);
    }
}
