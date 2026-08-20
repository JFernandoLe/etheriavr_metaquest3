using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Vigila la conexión con el backend y muestra un overlay VR de estado.
/// Se autoinstala en cualquier escena mediante BackendConnectionBootstrap.
/// </summary>
public class BackendConnectionManager : MonoBehaviour
{
    private enum ConnectionState
    {
        Connecting,
        Reconnecting,
        Connected
    }

    private static readonly Color PanelColor = new Color(0.16f, 0.12f, 0.10f, 0.92f);
    private static readonly Color OutlineColor = new Color(0.92f, 0.84f, 0.74f, 0.78f);
    private static readonly Color LoadingColor = new Color(0.98f, 0.78f, 0.43f, 1f);
    private static readonly Color SuccessColor = new Color(0.38f, 0.82f, 0.58f, 1f);
    private static readonly Color SecondaryTextColor = new Color(0.95f, 0.91f, 0.86f, 0.92f);

    private const string SearchingMessage = "Preparando la conexion con el servidor...";
    private const string LostConnectionMessage = "Se perdio la conexion con el servidor. Reintentando...";

    public static BackendConnectionManager Instance { get; private set; }

    [Header("Monitoreo")]
    [SerializeField] private float initialRetryInterval = 1.25f;
    [SerializeField] private float reconnectRetryInterval = 1.5f;
    [SerializeField] private float connectedPollInterval = 5f;
    [SerializeField] private int requestTimeoutSeconds = 5;
    [SerializeField] private float successDisplayDuration = 1.15f;

    [Header("Overlay")]
    [SerializeField] private float distanceFromCamera = 0.88f;
    [SerializeField] private float verticalOffset = -0.03f;
    [SerializeField] private float spinnerDegreesPerSecond = -220f;

    private Camera targetCamera;
    private Canvas canvas;
    private RectTransform canvasRect;
    private RectTransform panelRect;
    private RectTransform spinnerArcRect;
    private Image spinnerArcImage;
    private Text titleText;
    private Text messageText;

    private float nextCheckTime;
    private float successVisibleUntil;
    private bool hasConfirmedConnection;
    private bool isChecking;
    private ConnectionState currentState = ConnectionState.Connecting;

    private bool IsShowingConnectedState => hasConfirmedConnection && currentState == ConnectionState.Connected;
    private ConnectionState PendingState => hasConfirmedConnection ? ConnectionState.Reconnecting : ConnectionState.Connecting;
    private string PendingTitle => hasConfirmedConnection ? "Reconectando al servidor" : "Conectando al servidor";

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
        SetPanelState(ConnectionState.Connecting, "Conectando al servidor", SearchingMessage, true);
    }

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;

    private void Start()
    {
        ResolveTargetCamera();
        nextCheckTime = 0f;
    }

    private void Update()
    {
        RotateSpinner();
        PositionCanvas();

        if (IsShowingConnectedState && Time.unscaledTime >= successVisibleUntil) SetPanelVisible(false);

        if (!isChecking && Time.unscaledTime >= nextCheckTime) StartCoroutine(CheckConnectionRoutine());
    }

    private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Toda petición del proyecto reporta aquí su resultado, de modo que el overlay
    /// refleje el estado real sin necesidad de sondear constantemente.
    /// </summary>
    public static void ReportRequestResult(UnityWebRequest request)
    {
        if (Instance == null || request == null) return;

        // Un error de protocolo igual demuestra que el servidor respondió.
        bool reachable = request.responseCode > 0
                         || request.result == UnityWebRequest.Result.Success
                         || request.result == UnityWebRequest.Result.ProtocolError;

        if (reachable) Instance.MarkConnected("Conexion con el servidor activa.");
        else Instance.MarkDisconnected(LostConnectionMessage);
    }

    private IEnumerator CheckConnectionRoutine()
    {
        isChecking = true;
        bool shouldShowConnectionUi = !IsShowingConnectedState;

        if (!NetworkConfig.Instance.HasConfiguredServer)
        {
            if (shouldShowConnectionUi) SetPanelState(PendingState, PendingTitle, SearchingMessage, true);

            var ensureTask = NetworkConfig.Instance.EnsureReadyAsync();
            while (!ensureTask.IsCompleted) yield return null;
        }

        string healthUrl = NetworkConfig.Instance.HealthUrl;
        if (string.IsNullOrWhiteSpace(healthUrl))
        {
            MarkDisconnected("No hay host configurado en .env. Reintentando...");
            isChecking = false;
            yield break;
        }

        if (shouldShowConnectionUi)
            SetPanelState(PendingState, PendingTitle, "Comprobando la conexion con la API...", true);

        using (UnityWebRequest request = UnityWebRequest.Get(healthUrl))
        {
            request.timeout = Mathf.Max(1, requestTimeoutSeconds);
            yield return request.SendWebRequest();
            ReportRequestResult(request);
        }

        isChecking = false;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveTargetCamera();

        if (!IsShowingConnectedState) SetPanelVisible(true);

        nextCheckTime = Mathf.Min(nextCheckTime, Time.unscaledTime + 0.1f);
    }

    private void MarkConnected(string message)
    {
        bool wasDisconnected = !IsShowingConnectedState;
        hasConfirmedConnection = true;
        currentState = ConnectionState.Connected;
        nextCheckTime = Time.unscaledTime + connectedPollInterval;

        // Solo se anuncia el éxito si venimos de una caída, para no interrumpir el juego.
        if (!wasDisconnected) return;

        successVisibleUntil = Time.unscaledTime + successDisplayDuration;
        SetPanelState(ConnectionState.Connected, "Conexion exitosa", message, true);
    }

    private void MarkDisconnected(string message)
    {
        nextCheckTime = Time.unscaledTime + (hasConfirmedConnection ? reconnectRetryInterval : initialRetryInterval);
        currentState = PendingState;
        SetPanelState(currentState, PendingTitle, message, true);
    }

    private void PositionCanvas()
    {
        if (canvasRect == null) return;

        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            ResolveTargetCamera();
            if (targetCamera == null) return;
        }

        Transform cameraTransform = targetCamera.transform;
        canvasRect.position = cameraTransform.position
                              + cameraTransform.forward * distanceFromCamera
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

    private void RotateSpinner()
    {
        if (spinnerArcRect == null || !panelRect.gameObject.activeSelf) return;

        spinnerArcRect.Rotate(0f, 0f, spinnerDegreesPerSecond * Time.unscaledDeltaTime);
    }

    private void SetPanelState(ConnectionState state, string title, string message, bool visible)
    {
        currentState = state;
        titleText.text = title;
        messageText.text = message;

        bool connected = state == ConnectionState.Connected;
        spinnerArcImage.color = connected ? SuccessColor : LoadingColor;
        titleText.color = connected ? SuccessColor : Color.white;

        SetPanelVisible(visible);
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRect != null) panelRect.gameObject.SetActive(visible);
    }

    private void BuildUi()
    {
        GameObject canvasObject = CreateUiObject("Canvas", transform, typeof(Canvas), typeof(CanvasScaler));

        canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1600f, 900f);
        canvasRect.localScale = Vector3.one * 0.001f;

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.planeDistance = 1f;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 700;

        canvasObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

        GameObject panelObject = CreateUiObject("BackendStatusPanel", canvasRect, typeof(Image));
        panelRect = CenterRect(panelObject.GetComponent<RectTransform>(), new Vector2(540f, 260f), Vector2.zero);
        panelObject.GetComponent<Image>().color = PanelColor;

        Outline outline = panelObject.AddComponent<Outline>();
        outline.effectColor = OutlineColor;
        outline.effectDistance = new Vector2(2f, -2f);

        Vector2 spinnerSize = new Vector2(118f, 118f);
        Vector2 spinnerPosition = new Vector2(0f, 34f);

        Image spinnerTrackImage = CreateImage(panelRect, "SpinnerTrack", spinnerSize, spinnerPosition, CreateRingSprite(192, 20));
        spinnerTrackImage.color = new Color(1f, 1f, 1f, 0.10f);

        spinnerArcImage = CreateImage(panelRect, "SpinnerArc", spinnerSize, spinnerPosition, CreateRingSprite(192, 20));
        spinnerArcImage.type = Image.Type.Filled;
        spinnerArcImage.fillMethod = Image.FillMethod.Radial360;
        spinnerArcImage.fillOrigin = 0;
        spinnerArcImage.fillAmount = 0.76f;
        spinnerArcRect = spinnerArcImage.rectTransform;

        // Un anillo de grosor igual al tamaño equivale a un círculo lleno.
        Image spinnerCenter = CreateImage(panelRect, "SpinnerCenter", new Vector2(28f, 28f), spinnerPosition, CreateRingSprite(128, 128));
        spinnerCenter.color = new Color(0.97f, 0.94f, 0.90f, 0.92f);

        titleText = CreateText(panelRect, "Title", new Vector2(420f, 44f), new Vector2(0f, -44f), 31, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        messageText = CreateText(panelRect, "Message", new Vector2(440f, 54f), new Vector2(0f, -98f), 22, FontStyle.Normal, TextAnchor.MiddleCenter, SecondaryTextColor);
    }

    private static GameObject CreateUiObject(string name, Transform parent, params System.Type[] components)
    {
        System.Type[] allComponents = new System.Type[components.Length + 1];
        allComponents[0] = typeof(RectTransform);
        components.CopyTo(allComponents, 1);

        GameObject uiObject = new GameObject(name, allComponents);
        uiObject.layer = LayerMask.NameToLayer("UI");
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static RectTransform CenterRect(RectTransform rect, Vector2 size, Vector2 anchoredPosition)
    {
        Vector2 center = new Vector2(0.5f, 0.5f);
        rect.anchorMin = center;
        rect.anchorMax = center;
        rect.pivot = center;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        return rect;
    }

    private static Image CreateImage(RectTransform parent, string objectName, Vector2 size, Vector2 anchoredPosition, Sprite sprite)
    {
        GameObject imageObject = CreateUiObject(objectName, parent, typeof(Image));
        CenterRect(imageObject.GetComponent<RectTransform>(), size, anchoredPosition);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        return image;
    }

    private static Text CreateText(RectTransform parent, string objectName, Vector2 size, Vector2 anchoredPosition,
        int fontSize, FontStyle fontStyle, TextAnchor anchor, Color color)
    {
        GameObject textObject = CreateUiObject(objectName, parent, typeof(Text));
        CenterRect(textObject.GetComponent<RectTransform>(), size, anchoredPosition);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    /// <summary>Genera un anillo. Con thickness &gt;= radio queda un círculo lleno.</summary>
    private static Sprite CreateRingSprite(int size, int thickness)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false)
        {
            wrapMode = TextureWrapMode.Clamp
        };

        float outerRadius = (size - 2f) * 0.5f;
        float innerRadius = Mathf.Max(0f, outerRadius - thickness);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                pixels[(y * size) + x] = distance <= outerRadius && distance >= innerRadius
                    ? Color.white
                    : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }
}

/// <summary>Garantiza que el manager exista antes de cargar la primera escena.</summary>
public static class BackendConnectionBootstrap
{
    private const string ManagerName = "Backend Connection Manager";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureManagerExists()
    {
        if (BackendConnectionManager.Instance != null) return;

        GameObject managerObject = GameObject.Find(ManagerName) ?? new GameObject(ManagerName);

        if (managerObject.GetComponent<BackendConnectionManager>() == null)
            managerObject.AddComponent<BackendConnectionManager>();
    }
}
