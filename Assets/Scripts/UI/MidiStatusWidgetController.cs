using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class MidiStatusWidgetController : MonoBehaviour
{
    private static readonly Color ConnectedColor = new Color(0.35f, 0.67f, 0.50f, 0.96f);
    private static readonly Color DisconnectedColor = new Color(0.78f, 0.25f, 0.24f, 0.96f);
    private static readonly Color NotePulseColor = new Color(0.12f, 0.90f, 0.28f, 1f);
    private static readonly Color PanelColor = new Color(0.29f, 0.17f, 0.12f, 0.94f);
    private static readonly Color PanelOutlineColor = new Color(0.82f, 0.64f, 0.50f, 0.85f);
    private static readonly Color SecondaryTextColor = new Color(0.93f, 0.87f, 0.81f, 0.92f);
    private static readonly Color ButtonColor = new Color(0.66f, 0.50f, 0.39f, 0.96f);
    private static readonly Color ButtonDisabledColor = new Color(0.32f, 0.27f, 0.24f, 0.92f);
    private static readonly Color ButtonTextColor = new Color(0.98f, 0.97f, 0.94f, 1f);

    public static MidiStatusWidgetController Instance { get; private set; }

    [Header("Posicionamiento")]
    [SerializeField] private float distanceFromCamera = 1.04f;
    [SerializeField] private float promptDistanceFromCamera = 0.78f;
    [SerializeField] private float horizontalOffset = 0f;
    [SerializeField] private float verticalOffset = -0.02f;
    [SerializeField] private float pulseDuration = 0.18f;

    private Camera targetCamera;
    private Canvas canvas;
    private TrackedDeviceGraphicRaycaster trackedDeviceRaycaster;
    private RectTransform canvasRect;
    private RectTransform badgeRect;
    private RectTransform infoPanelRect;
    private Image badgeImage;
    private Text badgeLabel;
    private Text badgeGlyph;
    private Text titleText;
    private Text statusText;
    private Text deviceText;
    private Text registeredDeviceText;
    private Text helperText;
    private Button badgeButton;
    private Button reconnectButton;
    private Button disconnectButton;
    private Button closeButton;
    private Button continueButton;
    private Text reconnectButtonText;
    private Text disconnectButtonText;
    private Text closeButtonText;
    private Text continueButtonText;

    private MIDIConnectionManager connectionManager;
    private DirectMidiReceiver receiver;
    private float nextLookupTime;
    private float nextVisualRefreshTime;
    private float notePulseUntilTime;
    private bool gameplayPromptActive;
    private string gameplayPromptMessage;
    private string continueActionLabel = "Continuar juego";
    private Action pendingContinueAction;
    private bool widgetVisible = true;

    private const float LookupInterval = 0.5f;
    private const float VisualRefreshInterval = 0.1f;

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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        ResolveDependencies(true);
        RefreshView(true);
    }

    private void Update()
    {
        ResolveDependencies(false);
        UpdateBadgePulse();

        if (Time.unscaledTime >= nextVisualRefreshTime)
        {
            nextVisualRefreshTime = Time.unscaledTime + VisualRefreshInterval;
            RefreshView(false);
        }
    }

    private void LateUpdate()
    {
        PositionCanvas();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnsubscribeFromConnectionManager();
        UnsubscribeFromReceiver();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void PromptGameplayReconnect(string message, string actionLabel, Action continueAction)
    {
        gameplayPromptActive = true;
        gameplayPromptMessage = string.IsNullOrWhiteSpace(message)
            ? "Reconecta el controlador MIDI para continuar."
            : message;
        continueActionLabel = string.IsNullOrWhiteSpace(actionLabel)
            ? "Continuar juego"
            : actionLabel;
        pendingContinueAction = continueAction;

        ShowInfoPanel();
        RefreshView(true);
    }

    public void ClearGameplayPrompt()
    {
        gameplayPromptActive = false;
        gameplayPromptMessage = null;
        continueActionLabel = "Continuar juego";
        pendingContinueAction = null;
        RefreshView(true);
    }

    public void ShowInfoPanel()
    {
        if (!widgetVisible)
        {
            return;
        }

        if (infoPanelRect != null)
        {
            infoPanelRect.gameObject.SetActive(true);
        }
    }

    public void HideInfoPanel()
    {
        if (gameplayPromptActive && !IsCurrentlyConnected())
        {
            return;
        }

        if (infoPanelRect != null)
        {
            infoPanelRect.gameObject.SetActive(false);
        }
    }

    public void SetWidgetVisible(bool visible)
    {
        widgetVisible = visible;

        if (canvas != null)
        {
            canvas.enabled = visible;
        }

        if (trackedDeviceRaycaster != null)
        {
            trackedDeviceRaycaster.enabled = visible;
        }

        if (!visible && infoPanelRect != null)
        {
            infoPanelRect.gameObject.SetActive(false);
        }

        RefreshView(true);
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
        canvasObject.layer = LayerMask.NameToLayer("UI");
        canvasObject.transform.SetParent(transform, false);

        canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1840f, 1080f);
        canvasRect.localScale = Vector3.one * 0.001f;

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.planeDistance = 1f;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;

        trackedDeviceRaycaster = canvasObject.GetComponent<TrackedDeviceGraphicRaycaster>();

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.dynamicPixelsPerUnit = 10f;

        GameObject badgeObject = new GameObject("MidiBadge", typeof(RectTransform), typeof(Image), typeof(Button));
        badgeObject.layer = LayerMask.NameToLayer("UI");
        badgeObject.transform.SetParent(canvasRect, false);

        badgeRect = badgeObject.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(1f, 0f);
        badgeRect.anchorMax = new Vector2(1f, 0f);
        badgeRect.pivot = new Vector2(1f, 0f);
        badgeRect.sizeDelta = new Vector2(148f, 148f);
        badgeRect.anchoredPosition = new Vector2(-168f, 88f);

        badgeImage = badgeObject.GetComponent<Image>();
        badgeImage.sprite = CreateCircleSprite(256);
        badgeImage.type = Image.Type.Simple;

        badgeButton = badgeObject.GetComponent<Button>();
        badgeButton.targetGraphic = badgeImage;

        ColorBlock badgeColors = badgeButton.colors;
        badgeColors.normalColor = Color.white;
        badgeColors.highlightedColor = new Color(1f, 1f, 1f, 0.96f);
        badgeColors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 0.96f);
        badgeColors.selectedColor = badgeColors.highlightedColor;
        badgeColors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.9f);
        badgeButton.colors = badgeColors;

        Outline badgeOutline = badgeObject.AddComponent<Outline>();
        badgeOutline.effectColor = Color.white;
        badgeOutline.effectDistance = new Vector2(4f, -4f);

        badgeLabel = CreateText(badgeRect, "MidiLabel", new Vector2(120f, 38f), new Vector2(0f, 24f), 28, FontStyle.Bold, TextAnchor.MiddleCenter, ButtonTextColor);
        badgeLabel.text = "MIDI";

        badgeGlyph = CreateText(badgeRect, "MidiGlyph", new Vector2(90f, 52f), new Vector2(0f, -34f), 44, FontStyle.Bold, TextAnchor.MiddleCenter, ButtonTextColor);

        badgeButton.onClick.AddListener(TogglePanelVisibility);

        GameObject panelObject = new GameObject("MidiInfoPanel", typeof(RectTransform), typeof(Image));
        panelObject.layer = LayerMask.NameToLayer("UI");
        panelObject.transform.SetParent(canvasRect, false);

        infoPanelRect = panelObject.GetComponent<RectTransform>();
        infoPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        infoPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        infoPanelRect.pivot = new Vector2(0.5f, 0.5f);
        infoPanelRect.sizeDelta = new Vector2(680f, 470f);
        infoPanelRect.anchoredPosition = new Vector2(0f, 54f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = PanelColor;
        Sprite panelSprite = LoadBuiltinSprite("UI/Skin/Background.psd", "Background.psd", "UI/Skin/UISprite.psd", "UISprite.psd");
        if (panelSprite != null)
        {
            panelImage.sprite = panelSprite;
            panelImage.type = Image.Type.Sliced;
        }

        Outline panelOutline = panelObject.AddComponent<Outline>();
        panelOutline.effectColor = PanelOutlineColor;
        panelOutline.effectDistance = new Vector2(2f, -2f);

        titleText = CreateText(infoPanelRect, "Title", new Vector2(560f, 42f), new Vector2(0f, 170f), 30, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        titleText.text = "Información MIDI";

        statusText = CreateText(infoPanelRect, "StatusText", new Vector2(560f, 44f), new Vector2(0f, 104f), 27, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        deviceText = CreateText(infoPanelRect, "DeviceText", new Vector2(560f, 76f), new Vector2(0f, 24f), 22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        registeredDeviceText = CreateText(infoPanelRect, "RegisteredDeviceText", new Vector2(560f, 72f), new Vector2(0f, -62f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, SecondaryTextColor);
        helperText = CreateText(infoPanelRect, "HelperText", new Vector2(580f, 88f), new Vector2(0f, -152f), 20, FontStyle.Italic, TextAnchor.MiddleCenter, SecondaryTextColor);

        reconnectButton = CreateButton(infoPanelRect, "ReconnectButton", new Vector2(170f, 52f), new Vector2(-185f, -206f), "Buscar MIDI", ButtonColor, out reconnectButtonText);
        disconnectButton = CreateButton(infoPanelRect, "DisconnectButton", new Vector2(170f, 52f), new Vector2(0f, -206f), "Desconectar", ButtonColor, out disconnectButtonText);
        closeButton = CreateButton(infoPanelRect, "CloseButton", new Vector2(170f, 52f), new Vector2(185f, -206f), "Cerrar", ButtonColor, out closeButtonText);
        continueButton = CreateButton(infoPanelRect, "ContinueButton", new Vector2(220f, 56f), new Vector2(0f, -206f), "Continuar juego", ConnectedColor, out continueButtonText);

        reconnectButton.onClick.AddListener(HandleReconnectClicked);
        disconnectButton.onClick.AddListener(HandleDisconnectClicked);
        closeButton.onClick.AddListener(HideInfoPanel);
        continueButton.onClick.AddListener(HandleContinueClicked);

        infoPanelRect.gameObject.SetActive(false);
        }

    private void ResolveDependencies(bool forceLookup)
    {
        if (!forceLookup && Time.unscaledTime < nextLookupTime)
        {
            return;
        }

        nextLookupTime = Time.unscaledTime + LookupInterval;

        if (connectionManager == null)
        {
            connectionManager = FindObjectOfType<MIDIConnectionManager>();
            if (connectionManager != null)
            {
                connectionManager.OnMidiConnectionChanged -= HandleConnectionChanged;
                connectionManager.OnMidiConnectionChanged += HandleConnectionChanged;
            }
        }

        DirectMidiReceiver newReceiver = connectionManager != null
            ? connectionManager.GetReceiver()
            : FindObjectOfType<DirectMidiReceiver>();

        if (receiver != newReceiver)
        {
            UnsubscribeFromReceiver();
            receiver = newReceiver;
            if (receiver != null)
            {
                receiver.OnMidiNoteActivity += HandleMidiNoteActivity;
            }
        }

        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            ResolveTargetCamera();
        }
    }

    private void PositionCanvas()
    {
        if (!widgetVisible || canvasRect == null)
        {
            return;
        }

        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            ResolveTargetCamera();
            if (targetCamera == null)
            {
                return;
            }
        }

        float activeDistance = gameplayPromptActive || (infoPanelRect != null && infoPanelRect.gameObject.activeSelf)
            ? promptDistanceFromCamera
            : distanceFromCamera;

        Vector3 anchorPosition = targetCamera.transform.position
            + targetCamera.transform.forward * activeDistance
            + targetCamera.transform.right * horizontalOffset
            + targetCamera.transform.up * verticalOffset;

        canvasRect.position = anchorPosition;
        canvasRect.LookAt(targetCamera.transform.position);
        canvasRect.Rotate(0f, 180f, 0f);

        if (canvas.worldCamera != targetCamera)
        {
            canvas.worldCamera = targetCamera;
        }
    }

    private void ResolveTargetCamera()
    {
        targetCamera = Camera.main;
        if (targetCamera != null)
        {
            return;
        }

        Camera[] cameras = FindObjectsOfType<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].isActiveAndEnabled)
            {
                targetCamera = cameras[i];
                return;
            }
        }
    }

    private void RefreshView(bool force)
    {
        if (!force && badgeImage == null)
        {
            return;
        }

        bool isConnected = IsCurrentlyConnected();
        string currentDevice = ResolveCurrentDeviceName();
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
            ? $"Dispositivo actual: {currentDevice}"
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

        if (Time.unscaledTime < notePulseUntilTime && isConnected)
        {
            badgeImage.color = NotePulseColor;
        }
        else
        {
            badgeImage.color = isConnected ? ConnectedColor : DisconnectedColor;
        }
    }

    private string ResolveHelperMessage(bool isConnected)
    {
        if (gameplayPromptActive)
        {
            if (isConnected)
            {
                return string.Empty;
            }

            return gameplayPromptMessage;
        }

        return isConnected
            ? "Haz clic sobre el indicador para ver información y acciones MIDI."
            : "No hay un teclado MIDI activo. Usa Buscar MIDI para reintentar la conexión.";
    }

    private bool IsCurrentlyConnected()
    {
        if (connectionManager != null)
        {
            return connectionManager.IsMidiConnected;
        }

        return receiver != null && receiver.IsMidiConnected;
    }

    private string ResolveCurrentDeviceName()
    {
        if (connectionManager != null)
        {
            return connectionManager.CurrentDeviceName;
        }

        return receiver != null ? receiver.CurrentMidiDeviceName : UserSession.UnregisteredMidiDeviceName;
    }

    private void UpdateBadgePulse()
    {
        if (badgeRect == null)
        {
            return;
        }

        float targetScale = Time.unscaledTime < notePulseUntilTime ? 1.1f : 1f;
        Vector3 desiredScale = Vector3.one * targetScale;
        badgeRect.localScale = Vector3.Lerp(badgeRect.localScale, desiredScale, Time.unscaledDeltaTime * 14f);
    }

    private void TogglePanelVisibility()
    {
        if (infoPanelRect == null)
        {
            return;
        }

        if (infoPanelRect.gameObject.activeSelf)
        {
            HideInfoPanel();
        }
        else
        {
            ShowInfoPanel();
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveTargetCamera();
        RefreshView(true);
    }

    private void HandleConnectionChanged(bool isConnected)
    {
        if (!isConnected && gameplayPromptActive)
        {
            ShowInfoPanel();
        }

        RefreshView(true);
    }

    private void HandleMidiNoteActivity()
    {
        notePulseUntilTime = Time.unscaledTime + pulseDuration;
    }

    private void HandleReconnectClicked()
    {
        if (connectionManager != null)
        {
            connectionManager.RequestReconnect();
        }
        else if (receiver != null)
        {
            receiver.RequestReconnect();
        }

        ShowInfoPanel();
        RefreshView(true);
    }

    private void HandleDisconnectClicked()
    {
        if (connectionManager != null)
        {
            connectionManager.DisconnectCurrentDevice();
        }
        else if (receiver != null)
        {
            receiver.DisconnectCurrentDevice();
        }

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

        Action continueAction = pendingContinueAction;
        ClearGameplayPrompt();
        HideInfoPanel();
        continueAction?.Invoke();
    }

    private void UnsubscribeFromConnectionManager()
    {
        if (connectionManager != null)
        {
            connectionManager.OnMidiConnectionChanged -= HandleConnectionChanged;
        }
    }

    private void UnsubscribeFromReceiver()
    {
        if (receiver != null)
        {
            receiver.OnMidiNoteActivity -= HandleMidiNoteActivity;
        }
    }

    private Button CreateButton(Transform parent, string name, Vector2 size, Vector2 anchoredPosition, string label, Color backgroundColor, out Text labelText)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.layer = LayerMask.NameToLayer("UI");
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        Image image = buttonObject.GetComponent<Image>();
        image.color = backgroundColor;
        Sprite backgroundSprite = LoadBuiltinSprite("UI/Skin/Background.psd", "Background.psd", "UI/Skin/UISprite.psd", "UISprite.psd");
        if (backgroundSprite != null)
        {
            image.sprite = backgroundSprite;
            image.type = Image.Type.Sliced;
        }

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = backgroundColor * 1.08f;
        colors.pressedColor = backgroundColor * 0.92f;
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = ButtonDisabledColor;
        button.colors = colors;

        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.28f);
        outline.effectDistance = new Vector2(2f, -2f);

        labelText = CreateText(rectTransform, name + "Label", size - new Vector2(24f, 12f), Vector2.zero, 20, FontStyle.Bold, TextAnchor.MiddleCenter, ButtonTextColor);
        labelText.text = label;

        return button;
    }

    private Text CreateText(Transform parent, string name, Vector2 size, Vector2 anchoredPosition, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.layer = LayerMask.NameToLayer("UI");
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return text;
    }

    private Sprite LoadBuiltinSprite(params string[] candidates)
    {
        for (int i = 0; i < candidates.Length; i++)
        {
            Sprite sprite = Resources.GetBuiltinResource<Sprite>(candidates[i]);
            if (sprite != null)
            {
                return sprite;
            }
        }

        return null;
    }

    private Sprite CreateCircleSprite(int size)
    {
        int safeSize = Mathf.Max(32, size);
        Texture2D texture = new Texture2D(safeSize, safeSize, TextureFormat.ARGB32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        float radius = (safeSize - 2f) * 0.5f;
        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < safeSize; y++)
        {
            for (int x = 0; x < safeSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                Color pixelColor = distance <= radius ? Color.white : Color.clear;
                texture.SetPixel(x, y, pixelColor);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, safeSize, safeSize), new Vector2(0.5f, 0.5f), safeSize);
    }
}