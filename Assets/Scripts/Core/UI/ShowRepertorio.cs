using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pide el repertorio al backend e instancia tarjetas por canción.
/// Las canciones importadas localmente aparecen integradas con el mismo prefab.
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

        if (CustomSongManager.Instance != null)
            CustomSongManager.Instance.OnSongDeleted += HandleSongDeleted;

        ReloadRepertory();
    }

    private void OnDestroy()
    {
        if (CustomSongManager.Instance != null)
            CustomSongManager.Instance.OnSongDeleted -= HandleSongDeleted;
    }

    public void ReloadRepertory()
    {
        CargarDatos();
    }

    private void HandleSongDeleted(string songId)
    {
        ReloadRepertory();
    }

    private void CargarDatos()
    {
        foreach (Transform child in songBoxContainer)
            Destroy(child.gameObject);

        SelectedSongManager.Instance?.BeginRepertoryRequestMeasurement();

        StartCoroutine(authService.GetSongs(
            onSuccess: json =>
            {
                SongListWrapper wrapper = JsonUtility.FromJson<SongListWrapper>(json);
                List<SongListarResponse> songs = wrapper?.songs;

                if (songs != null)
                {
                    foreach (SongListarResponse song in songs)
                        CreateSongItem(song, false);
                }

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
            CreateSongItem(song, true, entry.Id);
        }
    }

    private void CreateSongItem(SongListarResponse song, bool isCustom, string customSongId = null)
    {
        GameObject newBox = Instantiate(songBoxPrefab, songBoxContainer);

        SongItem item = newBox.GetComponent<SongItem>();
        if (item == null)
        {
            Debug.LogWarning("[Repertorio] El prefab de canción no tiene componente SongItem");
            return;
        }

        item.Setup(song);
        item.UpdateButtonState(true);

        if (isCustom && !string.IsNullOrEmpty(customSongId))
            item.ConfigureCustomSongActions(customSongId, ConfirmAndDeleteCustomSong);
    }

    private void ConfirmAndDeleteCustomSong(string songId, string songTitle)
    {
        CustomSongConfirmDialog.Show(
            songTitle,
            onConfirm: () =>
            {
                if (CustomSongManager.Instance != null)
                    CustomSongManager.Instance.DeleteSong(songId);
            });
    }
}
