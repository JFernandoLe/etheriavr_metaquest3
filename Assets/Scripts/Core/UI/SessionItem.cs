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

        if (cancionTxt != null) cancionTxt.text = sesion.song_title;
        if (modoTxt != null) modoTxt.text = sesion.practice_mode;

        // La fecha llega con hora; en la fila solo interesa el día (yyyy-MM-dd).
        if (fechaTxt != null)
            fechaTxt.text = sesion.practice_datetime.Length >= 10
                ? sesion.practice_datetime.Substring(0, 10)
                : sesion.practice_datetime;

        if (botonDetalle == null) return;

        botonDetalle.onClick.RemoveAllListeners();
        botonDetalle.onClick.AddListener(AlHacerClic);
    }

    private void AlHacerClic() => manager.VerDetallesDeSesion(miSesion);

    /// <summary>Alternativa a <see cref="AlHacerClic"/> enlazable desde el inspector.</summary>
    public void SeleccionarSesion()
    {
        if (manager == null)
        {
            Debug.LogError("Error: El Manager no está asignado en esta fila.");
            return;
        }

        manager.VerDetallesDeSesion(miSesion);
    }
}