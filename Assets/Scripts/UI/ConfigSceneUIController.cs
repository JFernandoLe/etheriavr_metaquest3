using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ConfigSceneUIController : MonoBehaviour
{
    private const string ConfigSceneName = "ConfigScene";
    private const string HomeSceneName = "HomeScene";
    private static readonly Color SelectedToggleColor = new Color(0.12f, 0.47f, 0.95f, 1f);
    private static readonly Color UnselectedToggleColor = new Color(0.78f, 0.78f, 0.78f, 1f);

    private static bool isRegistered;

    private Toggle lowToggle;
    private Toggle mediumToggle;
    private Toggle highToggle;
    private Button saveButton;
    private Button backButton;
    private AuthService authService;
    private string persistedSelection;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        if (isRegistered)
        {
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        isRegistered = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeCurrentScene()
    {
        AttachControllerIfNeeded(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachControllerIfNeeded(scene);
    }

    private static void AttachControllerIfNeeded(Scene scene)
    {
        if (scene.name != ConfigSceneName && scene.name != HomeSceneName)
        {
            return;
        }

        if (FindFirstObjectByType<ConfigSceneUIController>() != null)
        {
            return;
        }

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

    private void WireHomeSceneButton()
    {
        Button configurationButton = FindButton("configuration_button");
        if (configurationButton == null)
        {
            return;
        }

        configurationButton.onClick.RemoveAllListeners();
        configurationButton.onClick.AddListener(() => SceneManager.LoadScene(ConfigSceneName));
    }

    private void WireConfigScene()
    {
        lowToggle = FindToggle("Baja");
        mediumToggle = FindToggle("Media");
        highToggle = FindToggle("Alta");
        saveButton = FindButton("btn_guardar_configuracion") ?? FindButton("configuration_button");
        backButton = FindButton("btn_volver") ?? FindButton("presentation_button");

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

        if (saveButton != null)
        {
            saveButton.onClick.RemoveAllListeners();
            saveButton.onClick.AddListener(SaveSelection);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() => SceneManager.LoadScene(HomeSceneName));
        }
    }

    private void ApplyPersistedSelection()
    {
        lowToggle.SetIsOnWithoutNotify(persistedSelection == "Bajo");
        mediumToggle.SetIsOnWithoutNotify(persistedSelection == "Medio");
        highToggle.SetIsOnWithoutNotify(persistedSelection == "Alto");

        RefreshToggleVisuals();
    }

    private void BindToggleVisuals(Toggle toggle)
    {
        if (toggle == null)
        {
            return;
        }

        toggle.onValueChanged.RemoveListener(HandleToggleValueChanged);
        toggle.onValueChanged.AddListener(HandleToggleValueChanged);
        ApplyToggleVisualState(toggle, toggle.isOn);
    }

    private void HandleToggleValueChanged(bool _)
    {
        RefreshToggleVisuals();
    }

    private void RefreshToggleVisuals()
    {
        ApplyToggleVisualState(lowToggle, lowToggle != null && lowToggle.isOn);
        ApplyToggleVisualState(mediumToggle, mediumToggle != null && mediumToggle.isOn);
        ApplyToggleVisualState(highToggle, highToggle != null && highToggle.isOn);
    }

    private void ApplyToggleVisualState(Toggle toggle, bool isSelected)
    {
        if (toggle == null)
        {
            return;
        }

        Color targetColor = isSelected ? SelectedToggleColor : UnselectedToggleColor;

        if (toggle.graphic != null)
        {
            toggle.graphic.color = targetColor;
        }

        if (toggle.targetGraphic != null)
        {
            ColorBlock colors = toggle.colors;
            colors.normalColor = Color.white;
            colors.selectedColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.82f, 0.9f, 1f, 1f);
            toggle.colors = colors;

            toggle.targetGraphic.color = isSelected
                ? new Color(0.87f, 0.94f, 1f, 1f)
                : Color.white;
        }
    }

    private void SaveSelection()
    {
        string selectedIntensity = GetSelectedIntensity();
        persistedSelection = selectedIntensity;

        if (UserSession.Instance != null)
        {
            UserSession.Instance.audienceIntensity = selectedIntensity;
        }

        if (UserSession.Instance == null || UserSession.Instance.userId <= 0 || string.IsNullOrEmpty(UserSession.Instance.token))
        {
            ShowInfo("Configuracion guardada solo en memoria.");
            return;
        }

        if (authService == null)
        {
            AuthService sceneAuthService = FindFirstObjectByType<AuthService>(FindObjectsInactive.Include);
            if (sceneAuthService != null)
            {
                authService = sceneAuthService;
            }
            else
            {
                GameObject runtimeAuthService = new GameObject("AuthService_Runtime");
                SceneManager.MoveGameObjectToScene(runtimeAuthService, SceneManager.GetActiveScene());
                authService = runtimeAuthService.AddComponent<AuthService>();
            }
        }

        UserConfigurationRequest request = new UserConfigurationRequest
        {
            midi_device_name = UserSession.Instance.midiDeviceName,
            audience_intensity = selectedIntensity
        };

        StartCoroutine(authService.UpdateUserConfiguration(
            UserSession.Instance.userId,
            request,
            onSuccess: (_) => ShowInfo("Configuracion guardada."),
            onError: (error) =>
            {
                Debug.LogWarning($"[ConfigSceneUI] Error guardando configuracion: {error}");
                ShowInfo("No se pudo guardar en servidor. La seleccion se mantuvo localmente.");
            }));
    }

    private string GetSelectedIntensity()
    {
        if (lowToggle != null && lowToggle.isOn)
        {
            return "Bajo";
        }

        if (highToggle != null && highToggle.isOn)
        {
            return "Alto";
        }

        return "Medio";
    }

    private void ShowInfo(string message)
    {
        if (AlertManager.Instance != null)
        {
            AlertManager.Instance.ShowAlert("Configuracion", message, true);
            return;
        }

        Debug.Log($"[ConfigSceneUI] {message}");
    }

    private Button FindButton(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private Toggle FindToggle(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        return target != null ? target.GetComponent<Toggle>() : null;
    }

    private string NormalizeIntensity(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return "Medio";
        }

        string normalized = rawValue.Trim().ToLowerInvariant();

        if (normalized == "baja" || normalized == "bajo")
        {
            return "Bajo";
        }

        if (normalized == "alta" || normalized == "alto")
        {
            return "Alto";
        }

        return "Medio";
    }
}