using UnityEngine;
using UnityEngine.SceneManagement; // Obligatorio para cambiar escenas

public class CambiadorEscenas : MonoBehaviour
{
    public void IrAEscena(string nombreEscena)
    {
        SceneManager.LoadScene(nombreEscena);
    }
}