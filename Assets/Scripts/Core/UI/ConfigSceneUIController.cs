using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Cablea por código la pantalla de configuración (intensidad del público) y el botón
/// que lleva a ella desde el menú. Se autoinstala para no depender del cableado de escena.
/// </summary>
public class ConfigSceneUIController : MonoBehaviour
{
    private const string ConfigSceneName = "ConfigScene";
    private const string HomeSceneName = "HomeScene";
    private const string LowIntensity = "Bajo";
    private const string MediumIntensity = "Medio";
    private const string HighIntensity = "Alto";

    private static readonly Color SelectedToggleColor = new Color(0.12f, 0.47f, 0.95f, 1f);
    private static readonly Color UnselectedToggleColor = new Color(0.78f, 0.78f, 0.78f, 1f);

    private static bool isRegistered;

    private Toggle lowToggle;
    private Toggle mediumToggle;
    private Toggle highToggle;
    private AuthService authService;
    private string persistedSelection;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        if (isRegistered) return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        isRegistered = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeCurrentScene() => AttachControllerIfNeeded(SceneManager.GetActiveScene());

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachControllerIfNeeded(scene);

    private static void AttachControllerIfNeeded(Scene scene)
    {
        if (scene.name != ConfigSceneName && scene.name != HomeSceneName) return;
        if (FindFirstObjectByType<ConfigSceneUIController>() != null) return;

        GameObject controllerObject = new GameObject(nameof(ConfigSceneUIController));
        SceneManager.MoveGameObjectToScene(controllerObject, scene);
        controllerObject.AddComponent<ConfigSceneUIController>();
    }

    private void Start()
    {
        string activeScene = SceneManager.GetActiveScene().name;

        if (activeScene == HomeSceneName)
        {
            WireHomeSceneButton();
            return;
        }

        if (activeScene != ConfigSceneName)
        {
            Destroy(gameObject);
            return;
        }

        WireConfigScene();
    }

    private void WireHomeSceneButton() => WireButton(FindButton("configuration_button"), () => SceneManager.LoadScene(ConfigSceneName));

    private void WireConfigScene()
    {
        lowToggle = FindToggle("Baja");
        mediumToggle = FindToggle("Media");
        highToggle = FindToggle("Alta");

        if (lowToggle == null || mediumToggle == null || highToggle == null)
        {
            Debug.LogWarning("[ConfigSceneUI] No se encontraron todos los toggles de intensidad.");
            return;
        }

        persistedSelection = NormalizeIntensity(UserSession.Instance != null
            ? UserSession.Instance.audienceIntensity
            : UserSession.DefaultAudienceIntensity);

        BindToggleVisuals(lowToggle);
        BindToggleVisuals(mediumToggle);
        BindToggleVisuals(highToggle);
        ApplyPersistedSelection();

        // Los nombres alternativos cubren las dos versiones del layout de la escena.
        WireButton(FindButton("btn_guardar_configuracion") ?? FindButton("configuration_button"), SaveSelection);
        WireButton(FindButton("btn_volver") ?? FindButton("presentation_button"), () => SceneManager.LoadScene(HomeSceneName));
    }

    private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void ApplyPersistedSelection()
    {
        lowToggle.SetIsOnWithoutNotify(persistedSelection == LowIntensity);
        mediumToggle.SetIsOnWithoutNotify(persistedSelection == MediumIntensity);
        highToggle.SetIsOnWithoutNotify(persistedSelection == HighIntensity);

        RefreshToggleVisuals();
    }

    private void BindToggleVisuals(Toggle toggle)
    {
        if (toggle == null) return;

        toggle.onValueChanged.RemoveListener(HandleToggleValueChanged);
        toggle.onValueChanged.AddListener(HandleToggleValueChanged);
        ApplyToggleVisualState(toggle, toggle.isOn);
    }

    private void HandleToggleValueChanged(bool _) => RefreshToggleVisuals();

    private void RefreshToggleVisuals()
    {
        ApplyToggleVisualState(lowToggle, lowToggle != null && lowToggle.isOn);
        ApplyToggleVisualState(mediumToggle, mediumToggle != null && mediumToggle.isOn);
        ApplyToggleVisualState(highToggle, highToggle != null && highToggle.isOn);
    }

    private static void ApplyToggleVisualState(Toggle toggle, bool isSelected)
    {
        if (toggle == null) return;

        if (toggle.graphic != null) toggle.graphic.color = isSelected ? SelectedToggleColor : UnselectedToggleColor;

        if (toggle.targetGraphic == null) return;

        // El fondo se mantiene claro y es la marca interior la que indica la selección.
        ColorBlock colors = toggle.colors;
        colors.normalColor = Color.white;
        colors.selectedColor = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.95f, 1f, 1f);
        colors.pressedColor = new Color(0.82f, 0.9f, 1f, 1f);
        toggle.colors = colors;

        toggle.targetGraphic.color = isSelected ? new Color(0.87f, 0.94f, 1f, 1f) : Color.white;
    }

    private void SaveSelection()
    {
        string selectedIntensity = GetSelectedIntensity();
        persistedSelection = selectedIntensity;

        if (UserSession.Instance != null) UserSession.Instance.audienceIntensity = selectedIntensity;

        bool canPersistToServer = UserSession.Instance != null
                                  && UserSession.Instance.userId > 0
                                  && !string.IsNullOrEmpty(UserSession.Instance.token);

        if (!canPersistToServer)
        {
            ShowInfo("Configuracion guardada solo en memoria.");
            return;
        }

        EnsureAuthService();

        UserConfigurationRequest request = new UserConfigurationRequest
        {
            midi_device_name = UserSession.Instance.midiDeviceName,
            audience_intensity = selectedIntensity
        };

        StartCoroutine(authService.UpdateUserConfiguration(
            UserSession.Instance.userId,
            request,
            onSuccess: _ => ShowInfo("Configuracion guardada."),
            onError: error =>
            {
                Debug.LogWarning($"[ConfigSceneUI] Error guardando configuracion: {error}");
                ShowInfo("No se pudo guardar en servidor. La seleccion se mantuvo localmente.");
            }));
    }

    private void EnsureAuthService()
    {
        if (authService != null) return;

        authService = FindFirstObjectByType<AuthService>(FindObjectsInactive.Include);
        if (authService != null) return;

        GameObject runtimeAuthService = new GameObject("AuthService_Runtime");
        SceneManager.MoveGameObjectToScene(runtimeAuthService, SceneManager.GetActiveScene());
        authService = runtimeAuthService.AddComponent<AuthService>();
    }

    private string GetSelectedIntensity()
    {
        if (lowToggle != null && lowToggle.isOn) return LowIntensity;
        if (highToggle != null && highToggle.isOn) return HighIntensity;

        return MediumIntensity;
    }

    private static void ShowInfo(string message)
    {
        if (AlertManager.Instance != null) AlertManager.Instance.ShowAlert("Configuracion", message, true);
        else Debug.Log($"[ConfigSceneUI] {message}");
    }

    private static Button FindButton(string objectName) => GameObject.Find(objectName)?.GetComponent<Button>();

    private static Toggle FindToggle(string objectName) => GameObject.Find(objectName)?.GetComponent<Toggle>();

    /// <summary>Acepta las variantes femeninas que usan las etiquetas de la escena (Baja/Alta).</summary>
    private static string NormalizeIntensity(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return MediumIntensity;

        switch (rawValue.Trim().ToLowerInvariant())
        {
            case "baja":
            case "bajo":
                return LowIntensity;
            case "alta":
            case "alto":
                return HighIntensity;
            default:
                return MediumIntensity;
        }
    }
}
