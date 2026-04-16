using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SessionItem : MonoBehaviour
{
    [Header("Referencias de la Fila")]
    public TextMeshProUGUI cancionTxt;
    public TextMeshProUGUI modoTxt;
    public TextMeshProUGUI fechaTxt;
    public Button botonDetalle;

    private PracticeSessionResponse miSesion;
    private HistoryManager manager;

    public void Configurar(PracticeSessionResponse sesion, HistoryManager historyManager)
    {
        miSesion = sesion;
        manager = historyManager;

        // 1. Asignar los textos
        if (cancionTxt != null) cancionTxt.text = sesion.song_title;
        if (modoTxt != null) modoTxt.text = sesion.practice_mode;

        // 2. Formatear la fecha
        if (fechaTxt != null)
        {
            if (sesion.practice_datetime.Length >= 10)
                fechaTxt.text = sesion.practice_datetime.Substring(0, 10);
            else
                fechaTxt.text = sesion.practice_datetime;
        }

        // 3. Conectar el botón
        if (botonDetalle != null)
        {
            botonDetalle.onClick.RemoveAllListeners();
            botonDetalle.onClick.AddListener(AlHacerClic);
        }
    }

    void AlHacerClic()
    {
        manager.VerDetallesDeSesion(miSesion);
    }
    public void SeleccionarSesion()
    {
        Debug.Log("<color=green>1. Clic detectado en la fila!</color>"); // NUEVO
        if (manager != null)
        {
            Debug.Log("<color=yellow>2. Llamando al HistoryManager...</color>"); // NUEVO
            manager.VerDetallesDeSesion(miSesion);
        }
        else
        {
            Debug.LogError("Error: El Manager no está asignado en esta fila.");
        }
    }
}