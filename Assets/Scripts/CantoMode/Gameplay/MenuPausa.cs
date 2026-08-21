using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Menú de pausa del modo canto. En Quest el botón de menú es OVR Start, no solo menuButton del mando.
/// </summary>
public class MenuPausa : MonoBehaviour
{
    public GameObject menuPausa;
    public AudioSource musica;
    public Transform cabezaJugador;

    private bool pausado;
    private bool botonPresionadoAnterior;

    void Start()
    {
        Time.timeScale = 1f;
        if (menuPausa != null)
            menuPausa.SetActive(false);

        if (cabezaJugador == null && Camera.main != null)
            cabezaJugador = Camera.main.transform;
    }

    void Update()
    {
        bool pausePressed = IsPauseButtonPressed();

        if (pausePressed && !botonPresionadoAnterior)
            TogglePausa();

        botonPresionadoAnterior = pausePressed;
    }

    private bool IsPauseButtonPressed()
    {
        if (OVRInput.Get(OVRInput.Button.Start))
            return true;
        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftHand.isValid &&
            leftHand.TryGetFeatureValue(CommonUsages.menuButton, out bool leftMenu) &&
            leftMenu)
            return true;

        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightHand.isValid &&
            rightHand.TryGetFeatureValue(CommonUsages.menuButton, out bool rightMenu) &&
            rightMenu)
            return true;

        return false;
    }

    public void TogglePausa()
    {
        if (EndGameManager.gameEnded)
        {
            Debug.Log("[MenuPausa] Pausa bloqueada: juego terminado");
            return;
        }

        if (menuPausa == null)
        {
            Debug.LogWarning("[MenuPausa] menuPausa no asignado.");
            return;
        }

        pausado = !pausado;
        menuPausa.SetActive(pausado);

        Canvas pauseCanvas = menuPausa.GetComponent<Canvas>();
        if (pauseCanvas != null)
            pauseCanvas.sortingOrder = pausado ? 100 : 0;

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
            if (musica != null && musica.clip != null)
                musica.UnPause();
        }
    }

    void ColocarMenuFrenteJugador()
    {
        if (menuPausa == null || cabezaJugador == null)
            return;

        Vector3 forward = cabezaJugador.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = cabezaJugador.forward;
        forward.Normalize();

        menuPausa.transform.position = cabezaJugador.position + forward * 1.5f;
        menuPausa.transform.LookAt(cabezaJugador.position);
        menuPausa.transform.Rotate(0f, 180f, 0f);
    }

    public void ReiniciarCancion()
    {
        pausado = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void VolverMenu()
    {
        pausado = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene("HomeScene");
    }
}
