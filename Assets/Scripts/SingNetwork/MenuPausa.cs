using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public class MenuPausa : MonoBehaviour
{
    public GameObject menuPausa;
    public AudioSource musica;
    public Transform cabezaJugador;

    bool pausado = false;

    bool botonPresionadoAnterior = false;

    void Update()
    {
        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        bool boton;
        if (leftHand.TryGetFeatureValue(CommonUsages.menuButton, out boton))
        {
            if (boton && !botonPresionadoAnterior)
            {
                TogglePausa();
            }

            botonPresionadoAnterior = boton;
        }
    }

    public void TogglePausa()
    {
        if (EndGameManager.gameEnded)
        {
            Debug.Log("Pausa bloqueada: juego terminado");
            return;
        }
        pausado = !pausado;

        menuPausa.SetActive(pausado);

        if (pausado)
        {
            Time.timeScale = 0f;

            if (musica != null)
                musica.Pause();

            ColocarMenuFrenteJugador();
        }
        else
        {
            Time.timeScale = 1f;

            if (musica != null)
                musica.Play();
        }
    }

    void ColocarMenuFrenteJugador()
    {
        if (cabezaJugador == null) return;

        Vector3 posicion = cabezaJugador.position + cabezaJugador.forward * 2f;

        menuPausa.transform.position = posicion;
        menuPausa.transform.LookAt(cabezaJugador);
        menuPausa.transform.Rotate(0, 180, 0);
    }

    public void ReiniciarCancion()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void VolverMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("HomeScene");
    }
}