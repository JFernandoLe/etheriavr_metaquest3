using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public class PianoPauseMenu : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject menuPausa;
    [SerializeField] private PianoGameManager pianoGameManager;
    [SerializeField] private Transform cabezaJugador;

    [Header("Configuración")]
    [SerializeField] private string homeSceneName = "HomeScene";
    [SerializeField] private float menuDistance = 1.5f;

    private bool fallbackMenuPressedPrevious = false;

    void Start()
    {
        if (pianoGameManager == null)
        {
            pianoGameManager = FindObjectOfType<PianoGameManager>();
        }

        if (cabezaJugador == null && Camera.main != null)
        {
            cabezaJugador = Camera.main.transform;
        }

        if (menuPausa != null)
        {
            menuPausa.SetActive(false);
        }
    }

    void Update()
    {
        bool pauseButtonPressed = IsPauseButtonPressed();

        if (pauseButtonPressed && !fallbackMenuPressedPrevious)
        {
            TogglePausa();
        }

        fallbackMenuPressedPrevious = pauseButtonPressed;
    }

    public void TogglePausa()
    {
        if (pianoGameManager == null)
        {
            pianoGameManager = FindObjectOfType<PianoGameManager>();
            if (pianoGameManager == null)
            {
                Debug.LogWarning("[PianoPauseMenu] No se encontró PianoGameManager en la escena");
                return;
            }
        }

        if (!pianoGameManager.CanTogglePause)
        {
            Debug.Log("[PianoPauseMenu] Pausa ignorada: el gameplay aún no inicia o ya terminó");
            return;
        }

        bool shouldPause = !pianoGameManager.isPaused;

        if (menuPausa != null)
        {
            menuPausa.SetActive(shouldPause);
        }

        if (shouldPause)
        {
            pianoGameManager.PauseGame();
            ColocarMenuFrenteJugador();
        }
        else
        {
            pianoGameManager.ResumeGame();
        }
    }

    public void ReiniciarCancion()
    {
        CloseMenuAndResumeIfNeeded();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void VolverMenu()
    {
        CloseMenuAndResumeIfNeeded();
        SceneManager.LoadScene(homeSceneName);
    }

    private bool IsPauseButtonPressed()
    {
        if (OVRInput.Get(OVRInput.Button.Start))
        {
            return true;
        }

        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftHand.isValid && leftHand.TryGetFeatureValue(CommonUsages.menuButton, out bool menuButtonPressed))
        {
            return menuButtonPressed;
        }

        return false;
    }

    private void ColocarMenuFrenteJugador()
    {
        if (menuPausa == null || cabezaJugador == null)
        {
            return;
        }

        Vector3 forward = cabezaJugador.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = cabezaJugador.forward;
        }

        forward.Normalize();

        Vector3 posicion = cabezaJugador.position + forward * menuDistance;
        menuPausa.transform.position = posicion;
        menuPausa.transform.LookAt(cabezaJugador.position);
        menuPausa.transform.Rotate(0f, 180f, 0f);
    }

    private void CloseMenuAndResumeIfNeeded()
    {
        if (menuPausa != null)
        {
            menuPausa.SetActive(false);
        }

        if (pianoGameManager != null && pianoGameManager.isPaused)
        {
            pianoGameManager.ResumeGame();
        }
    }
}