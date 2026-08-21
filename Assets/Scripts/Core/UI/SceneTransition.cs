using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Transición VR entre escenas: fade a negro + carga async + fade de entrada.
/// Evita la sensación de "freeze" al abrir PianoGame / SingGame.
/// </summary>
public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    [SerializeField] private float fadeOutDuration = 0.45f;
    [SerializeField] private float fadeInDuration = 0.55f;
    [SerializeField] private float minimumBlackHold = 0.15f;
    [SerializeField] private Color fadeColor = new Color(0.02f, 0.02f, 0.05f, 1f);

    private CanvasGroup canvasGroup;
    private TMP_Text statusText;
    private bool isTransitioning;

    public bool IsTransitioning => isTransitioning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject host = new GameObject("SceneTransition");
        host.AddComponent<SceneTransition>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public static void Load(string sceneName, string statusMessage = null)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;

        if (Instance == null) Bootstrap();
        if (Instance == null) return;

        Instance.StartCoroutine(Instance.TransitionRoutine(sceneName, statusMessage));
    }

    private IEnumerator TransitionRoutine(string sceneName, string statusMessage)
    {
        if (isTransitioning) yield break;

        isTransitioning = true;
        EnsureOverlayAttachedToCamera();
        SetStatus(string.IsNullOrWhiteSpace(statusMessage) ? "Cargando..." : statusMessage);

        yield return Fade(0f, 1f, fadeOutDuration);

        float blackHoldStart = Time.unscaledTime;
        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        if (load == null)
        {
            Debug.LogError($"[SceneTransition] No se pudo iniciar la carga de '{sceneName}'");
            yield return Fade(1f, 0f, fadeInDuration);
            isTransitioning = false;
            yield break;
        }

        load.allowSceneActivation = false;

        while (load.progress < 0.9f)
        {
            float progress = Mathf.Clamp01(load.progress / 0.9f);
            SetStatus($"{(string.IsNullOrWhiteSpace(statusMessage) ? "Cargando" : statusMessage)}  {Mathf.RoundToInt(progress * 100f)}%");
            yield return null;
        }

        float remainingHold = minimumBlackHold - (Time.unscaledTime - blackHoldStart);
        if (remainingHold > 0f) yield return new WaitForSecondsRealtime(remainingHold);

        SetStatus("Listo");
        load.allowSceneActivation = true;

        while (!load.isDone) yield return null;

        // Un frame para que la nueva escena monte cámara/UI antes del fade-in.
        yield return null;
        EnsureOverlayAttachedToCamera();
        yield return Fade(1f, 0f, fadeInDuration);

        SetStatus(string.Empty);
        isTransitioning = false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureOverlayAttachedToCamera();

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (canvasGroup == null) yield break;

        canvasGroup.blocksRaycasts = true;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.05f, duration);

        canvasGroup.alpha = from;
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / safeDuration);
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        canvasGroup.alpha = to;
        canvasGroup.blocksRaycasts = to > 0.01f;
    }

    private void SetStatus(string message)
    {
        if (statusText == null) return;
        statusText.text = message ?? string.Empty;
        statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    private void BuildOverlay()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        GameObject panel = new GameObject("FadePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image image = panel.GetComponent<Image>();
        image.color = fadeColor;
        image.raycastTarget = true;

        GameObject textObj = new GameObject("StatusText", typeof(RectTransform));
        textObj.transform.SetParent(panel.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.42f);
        textRect.anchorMax = new Vector2(0.5f, 0.42f);
        textRect.sizeDelta = new Vector2(900f, 80f);

        statusText = textObj.AddComponent<TextMeshProUGUI>();
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.fontSize = 36f;
        statusText.color = new Color(0.92f, 0.94f, 1f, 0.95f);
        statusText.text = string.Empty;
        statusText.raycastTarget = false;
    }

    private void EnsureOverlayAttachedToCamera()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) return;

        // En Quest, Overlay suele verse bien; si hay cámara XR, anclar como ScreenSpaceCamera mejora el tracking.
        Camera cam = Camera.main;
        if (cam == null) return;

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 0.35f;
    }
}
