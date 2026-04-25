using UnityEngine;
using System.Collections.Generic;

public class ShowRepertorio : MonoBehaviour
{
    [Header("Configuración UI")]
    [SerializeField] private GameObject songBoxPrefab; // Tu molde café
    [SerializeField] private Transform songBoxContainer; // El 'Content' del ScrollView

    [Header("Servicios")]
    [SerializeField] private AuthService authService;
    
    // Lista para mantener referencia de todos los items creados
    private List<SongItem> songItems = new List<SongItem>();
    private void Start()
    {
        Debug.Log("<color=magenta>[Repertorio]</color> 📋 ===== INICIANDO REPERTORIO =====");
        
        CargarDatos();
        
        Debug.Log("<color=magenta>[Repertorio]</color> 📋 ===== REPERTORIO LISTO =====\n");
    }

    private void CargarDatos()
    {
        // Limpiamos basura previa
        foreach (Transform child in songBoxContainer) Destroy(child.gameObject);
        songItems.Clear();

        SelectedSongManager.Instance?.BeginRepertoryRequestMeasurement();

        StartCoroutine(authService.GetSongs(
            onSuccess: (json) => {
                SongListWrapper wrapper = JsonUtility.FromJson<SongListWrapper>(json);
                int songCount = wrapper != null && wrapper.songs != null ? wrapper.songs.Count : 0;
                
                foreach (var song in wrapper.songs)
                {
                    // Creamos la cajita dinámicamente
                    GameObject newBox = Instantiate(songBoxPrefab, songBoxContainer);
                    
                    // Le pasamos los datos
                    SongItem item = newBox.GetComponent<SongItem>();
                    if (item != null) 
                    {
                        item.Setup(song);
                        songItems.Add(item);
                        UpdateSongItemButton(item);
                    }
                }

                SelectedSongManager.Instance?.LogRepertoryRequestCompleted(songCount);
            },
            onError: (err) => {
                SelectedSongManager.Instance?.LogRepertoryRequestFailed(err);
                Debug.LogError("Error al cargar canciones: " + err);
            }
        ));
    }
    
    /// <summary>
    /// Mantiene todos los botones del repertorio habilitados.
    /// </summary>
    private void UpdateSongItemButton(SongItem item)
    {
        if (item == null)
        {
            Debug.LogWarning("<color=yellow>[Repertorio]</color> ⚠️ SongItem es NULL");
            return;
        }
        item.UpdateButtonState(true);
    }
}