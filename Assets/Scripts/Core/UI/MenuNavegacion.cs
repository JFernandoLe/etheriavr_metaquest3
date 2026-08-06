using UnityEngine;

public class MenuNavegacion : MonoBehaviour
{
    [Header("Paneles")]
    public GameObject panelMenuPrincipal;
    public GameObject panelHistorial;
    public GameObject panelGrafica;
    public GameObject panelDetalles;

    public void AbrirHistorial()
    {
        panelMenuPrincipal.SetActive(false);
        panelHistorial.SetActive(true);
    }

    public void AbrirGrafica()
    {
        panelMenuPrincipal.SetActive(false);
        panelGrafica.SetActive(true);
    }

    public void IrADetalles()
    {
        panelMenuPrincipal.SetActive(false);
        panelHistorial.SetActive(false); 
        panelDetalles.SetActive(true);
    }

    public void VolverAlMenu()
    {
        panelMenuPrincipal.SetActive(true);
        panelHistorial.SetActive(false);
        panelGrafica.SetActive(false);
        panelDetalles.SetActive(false);
    }
}