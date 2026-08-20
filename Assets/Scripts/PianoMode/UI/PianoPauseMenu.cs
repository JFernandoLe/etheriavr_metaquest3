using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Menú de pausa del modo piano: se abre con el botón de menú del mando
/// y se coloca flotando frente al jugador.
/// </summary>
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
        if (pianoGameManager == null) pianoGameManager = FindObjectOfType<PianoGameManager>();
        if (cabezaJugador == null && Camera.main != null) cabezaJugador = Camera.main.transform;
        if (menuPausa != null) menuPausa.SetActive(false);
    }

    void Update()
    {
        bool pauseButtonPressed = IsPauseButtonPressed();

        // Solo en el flanco de subida, para no alternar la pausa cada frame.
        if (pauseButtonPressed && !fallbackMenuPressedPrevious) TogglePausa();

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

        if (pianoGameManager.isPaused) TryResumeFromPauseMenu();
        else ShowPauseMenu();
    }

    public void ShowPauseMenu(bool pauseGameplay = true)
    {
        if (menuPausa != null) menuPausa.SetActive(true);

        if (pauseGameplay && pianoGameManager != null && !pianoGameManager.isPaused)
            pianoGameManager.PauseGame();

        ColocarMenuFrenteJugador();
    }

    public void HidePauseMenu()
    {
        if (menuPausa != null) menuPausa.SetActive(false);
    }

    public void ReiniciarCancion() => ExitTo(SceneManager.GetActiveScene().buildIndex);

    public void VolverMenu()
    {
        HidePauseMenu();
        if (pianoGameManager != null) pianoGameManager.PrepareForSceneExit();
        SceneManager.LoadScene(homeSceneName);
    }

    private void ExitTo(int buildIndex)
    {
        HidePauseMenu();
        if (pianoGameManager != null) pianoGameManager.PrepareForSceneExit();
        SceneManager.LoadScene(buildIndex);
    }

    private bool IsPauseButtonPressed()
    {
        if (OVRInput.Get(OVRInput.Button.Start)) return true;

        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        return leftHand.isValid
               && leftHand.TryGetFeatureValue(CommonUsages.menuButton, out bool menuButtonPressed)
               && menuButtonPressed;
    }

    /// <summary>Sitúa el menú a la altura de la vista, mirando al jugador.</summary>
    private void ColocarMenuFrenteJugador()
    {
        if (menuPausa == null || cabezaJugador == null) return;

        Vector3 forward = cabezaJugador.forward;
        forward.y = 0f;

        // Si el jugador mira recto arriba/abajo, la proyección se anula: se usa el forward original.
        if (forward.sqrMagnitude < 0.0001f) forward = cabezaJugador.forward;
        forward.Normalize();

        menuPausa.transform.position = cabezaJugador.position + forward * menuDistance;
        menuPausa.transform.LookAt(cabezaJugador.position);
        menuPausa.transform.Rotate(0f, 180f, 0f);
    }

    private void TryResumeFromPauseMenu()
    {
        HidePauseMenu();
        pianoGameManager.ResumeGame();
    }
}
