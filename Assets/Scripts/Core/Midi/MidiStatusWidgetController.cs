using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Widget global de estado MIDI: insignia flotante frente a la cámara, panel de detalle
/// y aviso de reconexión que puede bloquear la reanudación del gameplay.
/// La construcción de la UI vive en el archivo parcial .Ui.
/// </summary>
public partial class MidiStatusWidgetController : MonoBehaviour
{
    public static MidiStatusWidgetController Instance { get; private set; }

    [Header("Posicionamiento")]
    [SerializeField] private float distanceFromCamera = 1.04f;
    [SerializeField] private float promptDistanceFromCamera = 0.78f;
    [SerializeField] private float horizontalOffset = 0f;
    [SerializeField] private float verticalOffset = -0.02f;
    [SerializeField] private float pulseDuration = 0.18f;

    private Camera targetCamera;
    private MIDIConnectionManager connectionManager;
    private DirectMidiReceiver receiver;
    private float nextLookupTime;
    private float nextVisualRefreshTime;
    private float notePulseUntilTime;
    private bool gameplayPromptActive;
    private string gameplayPromptMessage;
    private string continueActionLabel = DefaultContinueLabel;
    private Action pendingContinueAction;
    private bool widgetVisible = true;

    private const float LookupInterval = 0.5f;
    private const float VisualRefreshInterval = 0.1f;
    private const string DefaultContinueLabel = "Continuar juego";

    private bool IsPanelOpen => infoPanelRect != null && infoPanelRect.gameObject.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildUi();
    }

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;

    private void Start()
    {
        ResolveDependencies(true);
        RefreshView(true);
    }

    private void Update()
    {
        if (!widgetVisible && !gameplayPromptActive) return;

        ResolveDependencies(false);
        UpdateBadgePulse();

        if (Time.unscaledTime < nextVisualRefreshTime) return;

        nextVisualRefreshTime = Time.unscaledTime + VisualRefreshInterval;
        RefreshView(false);
    }

    private void LateUpdate()
    {
        if (!widgetVisible && !gameplayPromptActive) return;
        PositionCanvas();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (connectionManager != null) connectionManager.OnMidiConnectionChanged -= HandleConnectionChanged;
        UnsubscribeFromReceiver();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Pide reconectar el MIDI antes de poder iniciar o continuar la práctica.</summary>
    public void PromptGameplayReconnect(string message, string actionLabel, Action continueAction)
    {
        gameplayPromptActive = true;
        gameplayPromptMessage = string.IsNullOrWhiteSpace(message)
            ? "Reconecta el controlador MIDI para continuar."
            : message;
        continueActionLabel = string.IsNullOrWhiteSpace(actionLabel) ? DefaultContinueLabel : actionLabel;
        pendingContinueAction = continueAction;

        ShowInfoPanel();
        RefreshView(true);
    }

    public void ClearGameplayPrompt()
    {
        gameplayPromptActive = false;
        gameplayPromptMessage = null;
        continueActionLabel = DefaultContinueLabel;
        pendingContinueAction = null;
        RefreshView(true);
    }

    public void ShowInfoPanel()
    {
        if (widgetVisible && infoPanelRect != null) infoPanelRect.gameObject.SetActive(true);
    }

    public void HideInfoPanel()
    {
        // Mientras el aviso siga activo y sin MIDI, el panel no se puede cerrar.
        if (gameplayPromptActive && !IsCurrentlyConnected()) return;

        if (infoPanelRect != null) infoPanelRect.gameObject.SetActive(false);
    }

    public void SetWidgetVisible(bool visible)
    {
        widgetVisible = visible;

        if (canvas != null) canvas.enabled = visible;
        if (trackedDeviceRaycaster != null) trackedDeviceRaycaster.enabled = visible;
        if (!visible && infoPanelRect != null) infoPanelRect.gameObject.SetActive(false);

        RefreshView(true);
    }

    private void ResolveDependencies(bool forceLookup)
    {
        if (!forceLookup && Time.unscaledTime < nextLookupTime) return;

        nextLookupTime = Time.unscaledTime + LookupInterval;

        if (connectionManager == null)
        {
            connectionManager = MIDIConnectionManager.Instance ?? FindObjectOfType<MIDIConnectionManager>();
            if (connectionManager != null)
            {
                connectionManager.OnMidiConnectionChanged -= HandleConnectionChanged;
                connectionManager.OnMidiConnectionChanged += HandleConnectionChanged;
            }
        }

        DirectMidiReceiver newReceiver = connectionManager != null
            ? connectionManager.GetReceiver()
            : receiver;

        if (receiver != newReceiver)
        {
            UnsubscribeFromReceiver();
            receiver = newReceiver;
            if (receiver != null) receiver.OnMidiNoteActivity += HandleMidiNoteActivity;
        }

        if (targetCamera == null || !targetCamera.isActiveAndEnabled) ResolveTargetCamera();
    }

    /// <summary>Ancla el canvas frente a la cámara; se acerca cuando hay un aviso o el panel abierto.</summary>
    private void PositionCanvas()
    {
        if (!widgetVisible || canvasRect == null) return;

        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            ResolveTargetCamera();
            if (targetCamera == null) return;
        }

        Transform cameraTransform = targetCamera.transform;
        float activeDistance = gameplayPromptActive || IsPanelOpen
            ? promptDistanceFromCamera
            : distanceFromCamera;

        canvasRect.position = cameraTransform.position
                              + cameraTransform.forward * activeDistance
                              + cameraTransform.right * horizontalOffset
                              + cameraTransform.up * verticalOffset;

        canvasRect.LookAt(cameraTransform.position);
        canvasRect.Rotate(0f, 180f, 0f);

        if (canvas.worldCamera != targetCamera) canvas.worldCamera = targetCamera;
    }

    private void ResolveTargetCamera()
    {
        targetCamera = Camera.main;
        if (targetCamera != null) return;

        foreach (Camera candidate in FindObjectsOfType<Camera>())
        {
            if (candidate != null && candidate.isActiveAndEnabled)
            {
                targetCamera = candidate;
                return;
            }
        }
    }

    private void RefreshView(bool force)
    {
        if (!force && badgeImage == null) return;

        bool isConnected = IsCurrentlyConnected();
        string registeredDevice = UserSession.Instance != null
            ? UserSession.Instance.midiDeviceName
            : UserSession.UnregisteredMidiDeviceName;

        reconnectButtonText.text = isConnected ? "Reconectar" : "Buscar MIDI";

        disconnectButton.interactable = isConnected;
        ColorBlock disconnectColors = disconnectButton.colors;
        disconnectColors.normalColor = isConnected ? ButtonColor : ButtonDisabledColor;
        disconnectColors.highlightedColor = isConnected ? ButtonColor * 1.05f : ButtonDisabledColor;
        disconnectColors.pressedColor = isConnected ? ButtonColor * 0.95f : ButtonDisabledColor;
        disconnectButton.colors = disconnectColors;

        titleText.text = "Información MIDI";
        statusText.text = isConnected ? "Estado actual: Conectado" : "Estado actual: Desconectado";
        statusText.color = isConnected ? ConnectedColor : DisconnectedColor;
        deviceText.text = isConnected
            ? $"Dispositivo actual: {ResolveCurrentDeviceName()}"
            : "Dispositivo actual: No detectado";
        registeredDeviceText.text = $"Dispositivo registrado: {registeredDevice}";

        string helperMessage = ResolveHelperMessage(isConnected);
        helperText.text = helperMessage;
        helperText.gameObject.SetActive(!string.IsNullOrWhiteSpace(helperMessage));

        continueButtonText.text = continueActionLabel;
        continueButton.gameObject.SetActive(gameplayPromptActive && isConnected);
        closeButton.gameObject.SetActive(!gameplayPromptActive);
        reconnectButton.gameObject.SetActive(true);
        disconnectButton.gameObject.SetActive(true);

        badgeLabel.text = "MIDI";
        badgeGlyph.text = isConnected ? "V" : "X";
        badgeGlyph.color = ButtonTextColor;

        bool pulsing = isConnected && Time.unscaledTime < notePulseUntilTime;
        badgeImage.color = pulsing
            ? NotePulseColor
            : isConnected ? ConnectedColor : DisconnectedColor;
    }

    private string ResolveHelperMessage(bool isConnected)
    {
        if (gameplayPromptActive) return isConnected ? string.Empty : gameplayPromptMessage;

        return isConnected
            ? "Haz clic sobre el indicador para ver información y acciones MIDI."
            : "No hay un teclado MIDI activo. Usa Buscar MIDI para reintentar la conexión.";
    }

    private bool IsCurrentlyConnected() => connectionManager != null
        ? connectionManager.IsMidiConnected
        : receiver != null && receiver.IsMidiConnected;

    private string ResolveCurrentDeviceName()
    {
        if (connectionManager != null) return connectionManager.CurrentDeviceName;

        return receiver != null ? receiver.CurrentMidiDeviceName : UserSession.UnregisteredMidiDeviceName;
    }

    private void UpdateBadgePulse()
    {
        if (badgeRect == null) return;

        float targetScale = Time.unscaledTime < notePulseUntilTime ? 1.1f : 1f;
        badgeRect.localScale = Vector3.Lerp(badgeRect.localScale, Vector3.one * targetScale, Time.unscaledDeltaTime * 14f);
    }

    private void TogglePanelVisibility()
    {
        if (infoPanelRect == null) return;

        if (IsPanelOpen) HideInfoPanel();
        else ShowInfoPanel();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveTargetCamera();
        RefreshView(true);
    }

    private void HandleConnectionChanged(bool isConnected)
    {
        if (!isConnected && gameplayPromptActive) ShowInfoPanel();

        RefreshView(true);
    }

    private void HandleMidiNoteActivity() => notePulseUntilTime = Time.unscaledTime + pulseDuration;

    private void HandleReconnectClicked()
    {
        if (connectionManager != null) connectionManager.RequestReconnect();
        else if (receiver != null) receiver.RequestReconnect();

        ShowInfoPanel();
        RefreshView(true);
    }

    private void HandleDisconnectClicked()
    {
        if (connectionManager != null) connectionManager.DisconnectCurrentDevice();
        else if (receiver != null) receiver.DisconnectCurrentDevice();

        ShowInfoPanel();
        RefreshView(true);
    }

    private void HandleContinueClicked()
    {
        if (!IsCurrentlyConnected())
        {
            HandleReconnectClicked();
            return;
        }

        // Se captura antes de limpiar: ClearGameplayPrompt descarta la acción pendiente.
        Action continueAction = pendingContinueAction;
        ClearGameplayPrompt();
        HideInfoPanel();
        continueAction?.Invoke();
    }

    private void UnsubscribeFromReceiver()
    {
        if (receiver != null) receiver.OnMidiNoteActivity -= HandleMidiNoteActivity;
    }
}
