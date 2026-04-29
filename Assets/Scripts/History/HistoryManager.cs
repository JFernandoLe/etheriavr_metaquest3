using UnityEngine;
using System.Collections.Generic;

public class HistoryManager : MonoBehaviour
{
    public AuthService authService;
    public GameObject sessionItemPrefab;
    public Transform contentArea; // El objeto "Content" dentro del Scroll View
    public MenuNavegacion navegacion;
    public DetallesSesionUI panelDetalles; // El script del panel de la Imagen 2

    void OnEnable()
    {
        CargarDatosDelServidor();
    }

    // Dentro de tu HistoryManager.cs
    public void CargarDatosDelServidor()
    {
        if (UserSession.Instance == null) return;

        StartCoroutine(authService.GetUserHistory(UserSession.Instance.userId,
            onSuccess: (json) => {
                PracticeSessionListWrapper wrapper = JsonUtility.FromJson<PracticeSessionListWrapper>(json);

                if (wrapper.sessions == null || wrapper.sessions.Count == 0)
                    Debug.LogWarning("El JSON se ley� pero la lista de sesiones est� vac�a.");
                else
                {
                    GenerarLista(wrapper.sessions);
                }
            },
            onError: (err) => Debug.LogError("Error al obtener historial: " + err)
        ));
    }

    void GenerarLista(List<PracticeSessionResponse> sesiones)
    {
        foreach (Transform child in contentArea) Destroy(child.gameObject);

        foreach (var s in sesiones)
        {
            GameObject nuevaFila = Instantiate(sessionItemPrefab, contentArea);
            SessionItem scriptFila = nuevaFila.GetComponent<SessionItem>();

            if (scriptFila != null)
            {
                // LE PASAMOS LOS DATOS Y "THIS" (EL MANAGER)
                scriptFila.Configurar(s, this);
            }
            else
            {
                Debug.LogError("El prefab no tiene el script SessionItem asignado.");
            }
        }
    }

    public void VerDetallesDeSesion(PracticeSessionResponse datos)
    {
        if (panelDetalles != null)
        {
            // 1. Llenamos los datos
            panelDetalles.MostrarDatos(datos);

            // 2. Apagamos el panel donde est� este script (PanelHistorial)
            // Usamos la referencia directa al objeto que contiene el historial
            this.transform.parent.gameObject.SetActive(false);

            // 3. Encendemos el panel de detalles
            panelDetalles.gameObject.SetActive(true);

            // Aseguramos que la escala del de detalles sea normal
            panelDetalles.transform.localScale = Vector3.one;
        }
    }
}