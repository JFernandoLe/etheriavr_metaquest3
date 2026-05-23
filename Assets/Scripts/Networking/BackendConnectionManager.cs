using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    public static BackendConnectionManager Instance { get; private set; }

    [Header("Monitoreo")]
    [SerializeField] private float initialRetryInterval = 1.25f;
    [SerializeField] private float reconnectRetryInterval = 1.5f;
    [SerializeField] private float connectedPollInterval = 5f;
    [SerializeField] private int requestTimeoutSeconds = 3;
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
    private Image panelImage;
    private Image spinnerTrackImage;
    private Image spinnerArcImage;
    private Text titleText;
    private Text messageText;

    private float nextCheckTime;
    private float successVisibleUntil;
    private bool hasConfirmedConnection;
    private bool isChecking;
    private ConnectionState currentState = ConnectionState.Connecting;

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
        SetPanelState(ConnectionState.Connecting, "Conectando al servidor", "Buscando el servidor en la red local...", true);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        ResolveTargetCamera();
        nextCheckTime = 0f;
    }

    private void Update()
    {
        RotateSpinner();
        PositionCanvas();

        if (hasConfirmedConnection && currentState == ConnectionState.Connected && Time.unscaledTime >= successVisibleUntil)
        {
            SetPanelVisible(false);
        }

        if (!isChecking && Time.unscaledTime >= nextCheckTime)
        {
            StartCoroutine(CheckConnectionRoutine());
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void ReportRequestResult(UnityWebRequest request)
    {
        if (Instance == null || request == null)
        {
            return;
        }

        bool reachable = request.responseCode > 0 ||
                         request.result == UnityWebRequest.Result.Success ||
                         request.result == UnityWebRequest.Result.ProtocolError;

        if (reachable)
        {
            Instance.MarkConnected(false, "Conexion con el servidor activa.");
            return;
        }

        string errorMessage = string.IsNullOrWhiteSpace(request.error)
            ? "Se perdio la conexion con el servidor. Reintentando..."
            : "Se perdio la conexion con el servidor. Reintentando...";
        Instance.MarkDisconnected(errorMessage);
    }

    private IEnumerator CheckConnectionRoutine()
    {
        isChecking = true;
        bool shouldShowConnectionUi = !hasConfirmedConnection || currentState != ConnectionState.Connected;

        if (string.IsNullOrWhiteSpace(NetworkConfig.Instance.BaseUrl) || !hasConfirmedConnection)
        {
            if (shouldShowConnectionUi)
            {
                SetPanelState(
                    hasConfirmedConnection ? ConnectionState.Reconnecting : ConnectionState.Connecting,
                    hasConfirmedConnection ? "Reconectando al servidor" : "Conectando al servidor",
                    "Buscando el servidor en la red local...",
                    true);
            }

            var discoveryTask = NetworkConfig.Instance.DiscoverServerAsync();
            while (!discoveryTask.IsCompleted)
            {
                yield return null;
            }
        }

        string baseUrl = NetworkConfig.Instance.BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            MarkDisconnected("No se encontro el servidor. Reintentando...");
            isChecking = false;
            yield break;
        }

        if (shouldShowConnectionUi)
        {
            SetPanelState(
                hasConfirmedConnection ? ConnectionState.Reconnecting : ConnectionState.Connecting,
                hasConfirmedConnection ? "Reconectando al servidor" : "Conectando al servidor",
                "Comprobando la conexion con la API...",
                true);
        }

        using (UnityWebRequest request = UnityWebRequest.Get(baseUrl))
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

        if (!hasConfirmedConnection || currentState != ConnectionState.Connected)
        {
            SetPanelVisible(true);
        }

        nextCheckTime = Mathf.Min(nextCheckTime, Time.unscaledTime + 0.1f);
    }

    private void MarkConnected(bool showToast, string message)
    {
        bool wasDisconnected = !hasConfirmedConnection || currentState != ConnectionState.Connected;
        hasConfirmedConnection = true;
        currentState = ConnectionState.Connected;
        nextCheckTime = Time.unscaledTime + connectedPollInterval;

        if (!showToast && !wasDisconnected)
        {
            return;
        }

        successVisibleUntil = Time.unscaledTime + successDisplayDuration;
        SetPanelState(ConnectionState.Connected, "Conexion exitosa", message, true);
    }

    private void MarkDisconnected(string message)
    {
        currentState = hasConfirmedConnection ? ConnectionState.Reconnecting : ConnectionState.Connecting;
        nextCheckTime = Time.unscaledTime + (hasConfirmedConnection ? reconnectRetryInterval : initialRetryInterval);
        SetPanelState(
            currentState,
            currentState == ConnectionState.Reconnecting ? "Reconectando al servidor" : "Conectando al servidor",
            message,
            true);
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.layer = LayerMask.NameToLayer("UI");
        canvasObject.transform.SetParent(transform, false);

        canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1600f, 900f);
        canvasRect.localScale = Vector3.one * 0.001f;

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.planeDistance = 1f;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 700;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        GameObject panelObject = new GameObject("BackendStatusPanel", typeof(RectTransform), typeof(Image));
        panelObject.layer = LayerMask.NameToLayer("UI");
        panelObject.transform.SetParent(canvasRect, false);

        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(540f, 260f);
        panelRect.anchoredPosition = Vector2.zero;

        panelImage = panelObject.GetComponent<Image>();
        panelImage.color = PanelColor;

        Outline outline = panelObject.AddComponent<Outline>();
        outline.effectColor = OutlineColor;
        outline.effectDistance = new Vector2(2f, -2f);

        spinnerTrackImage = CreateImage(panelRect, "SpinnerTrack", new Vector2(118f, 118f), new Vector2(0f, 34f), CreateRingSprite(192, 20));
        spinnerTrackImage.color = new Color(1f, 1f, 1f, 0.10f);

        spinnerArcImage = CreateImage(panelRect, "SpinnerArc", new Vector2(118f, 118f), new Vector2(0f, 34f), CreateRingSprite(192, 20));
        spinnerArcImage.type = Image.Type.Filled;
        spinnerArcImage.fillMethod = Image.FillMethod.Radial360;
        spinnerArcImage.fillOrigin = 0;
        spinnerArcImage.fillAmount = 0.76f;
        spinnerArcRect = spinnerArcImage.rectTransform;

        Image spinnerCenter = CreateImage(panelRect, "SpinnerCenter", new Vector2(28f, 28f), new Vector2(0f, 34f), CreateCircleSprite(128));
        spinnerCenter.color = new Color(0.97f, 0.94f, 0.90f, 0.92f);

        titleText = CreateText(panelRect, "Title", new Vector2(420f, 44f), new Vector2(0f, -44f), 31, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        messageText = CreateText(panelRect, "Message", new Vector2(440f, 54f), new Vector2(0f, -98f), 22, FontStyle.Normal, TextAnchor.MiddleCenter, SecondaryTextColor);
    }

    private void PositionCanvas()
    {
        if (canvasRect == null)
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

        canvasRect.position = targetCamera.transform.position +
                              (targetCamera.transform.forward * distanceFromCamera) +
                              (targetCamera.transform.up * verticalOffset);
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

    private void RotateSpinner()
    {
        if (spinnerArcRect == null || !panelRect.gameObject.activeSelf)
        {
            return;
        }

        spinnerArcRect.Rotate(0f, 0f, spinnerDegreesPerSecond * Time.unscaledDeltaTime);
    }

    private void SetPanelState(ConnectionState state, string title, string message, bool visible)
    {
        currentState = state;
        titleText.text = title;
        messageText.text = message;
        spinnerArcImage.color = state == ConnectionState.Connected ? SuccessColor : LoadingColor;
        titleText.color = state == ConnectionState.Connected ? SuccessColor : Color.white;
        SetPanelVisible(visible);
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRect != null)
        {
            panelRect.gameObject.SetActive(visible);
        }
    }

    private Image CreateImage(RectTransform parent, string objectName, Vector2 size, Vector2 anchoredPosition, Sprite sprite)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.layer = LayerMask.NameToLayer("UI");
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        return image;
    }

    private Text CreateText(RectTransform parent, string objectName, Vector2 size, Vector2 anchoredPosition, int fontSize, FontStyle fontStyle, TextAnchor anchor, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.layer = LayerMask.NameToLayer("UI");
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

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

    private Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        float radius = (size - 2f) * 0.5f;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                pixels[(y * size) + x] = distance <= radius ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private Sprite CreateRingSprite(int size, int thickness)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

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

public static class BackendConnectionBootstrap
{
    private const string ManagerName = "Backend Connection Manager";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureManagerExists()
    {
        if (BackendConnectionManager.Instance != null)
        {
            return;
        }

        GameObject existingObject = GameObject.Find(ManagerName);
        if (existingObject != null)
        {
            if (existingObject.GetComponent<BackendConnectionManager>() == null)
            {
                existingObject.AddComponent<BackendConnectionManager>();
            }
            return;
        }

        GameObject managerObject = new GameObject(ManagerName);
        managerObject.AddComponent<BackendConnectionManager>();
    }
}