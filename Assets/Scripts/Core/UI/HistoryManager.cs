using UnityEngine;
using System.Collections.Generic;

public class HistoryManager : MonoBehaviour
{
    public AuthService authService;
    public GameObject sessionItemPrefab;
    public Transform contentArea;
    public MenuNavegacion navegacion;
    public DetallesSesionUI panelDetalles;

    private void OnEnable() => CargarDatosDelServidor();

    public void CargarDatosDelServidor()
    {
        if (!UserSession.Instance) return;

        StartCoroutine(authService.GetUserHistory(UserSession.Instance.userId,
            onSuccess: json => 
            {
                var wrapper = JsonUtility.FromJson<PracticeSessionListWrapper>(json);
                if (wrapper?.sessions == null || wrapper.sessions.Count == 0)
                    Debug.LogWarning("El JSON se leyó pero la lista de sesiones está vacía.");
                else
                    GenerarLista(wrapper.sessions);
            },
            onError: err => Debug.LogError($"Error al obtener historial: {err}")
        ));
    }

    private void GenerarLista(List<PracticeSessionResponse> sesiones)
    {
        foreach (Transform child in contentArea) Destroy(child.gameObject);

        foreach (var s in sesiones)
        {
            if (Instantiate(sessionItemPrefab, contentArea).TryGetComponent(out SessionItem script))
                script.Configurar(s, this);
            else
                Debug.LogError("El prefab no tiene el script SessionItem asignado.");
        }
    }

    public void VerDetallesDeSesion(PracticeSessionResponse datos)
    {
        if (!panelDetalles) return;

        panelDetalles.MostrarDatos(datos);
        transform.parent.gameObject.SetActive(false);
        panelDetalles.gameObject.SetActive(true);
        panelDetalles.transform.localScale = Vector3.one;
    }
}