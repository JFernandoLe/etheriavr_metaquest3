using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Construye las gráficas de evolución del historial de práctica, agrupadas por mes
/// y separadas en piano y canto. Toda la UI se genera en runtime.
/// El dibujado vive en el archivo parcial .Charts.
/// </summary>
public partial class EvolutionGraphsController : MonoBehaviour
{
    private sealed class SessionViewModel
    {
        public PracticeSessionResponse Session;
        public DateTime PracticeDate;
        public bool IsPiano;
        public float ComponentA;
        public float Rhythm;
        public float Score;
        public string ComponentALabel;
    }

    private sealed class MonthBucket
    {
        public string Key;
        public string DisplayName;
        public DateTime MonthStart;
        public readonly List<SessionViewModel> Sessions = new List<SessionViewModel>();
    }

    // El backend no es consistente con el formato de fecha, así que se prueban varios.
    private static readonly string[] SupportedDateFormats =
    {
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.fff",
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:ssK",
        "yyyy-MM-ddTHH:mm:ss.fffK",
        "yyyy-MM-dd"
    };

    [Header("Datos")]
    [SerializeField] private AuthService authService;
    [SerializeField] private ScrollRect pianoScrollView;
    [SerializeField] private ScrollRect cantoScrollView;
    [SerializeField] private RectTransform pianoContent;
    [SerializeField] private RectTransform cantoContent;

    [Header("Runtime UI")]
    [SerializeField] private Vector2 selectorPosition = new Vector2(0f, -205f);
    [SerializeField] private Vector2 selectorSize = new Vector2(440f, 64f);
    [SerializeField] private Vector2 tooltipPosition = new Vector2(865f, -220f);
    [SerializeField] private Vector2 tooltipSize = new Vector2(620f, 270f);
    [SerializeField] private Vector2 tooltipOffset = new Vector2(38f, 0f);
    [SerializeField] private float tooltipMargin = 28f;
    [SerializeField] private float tooltipTitleFontSize = 30f;
    [SerializeField] private float tooltipBodyFontSize = 27f;
    [SerializeField] private float chartCardHeight = 430f;
    [SerializeField] private float plotHeight = 320f;
    [SerializeField] private float pointSize = 20f;
    [SerializeField] private float pointHitSizeMultiplier = 1.9f;
    [SerializeField] private float pointVisualSizeMultiplier = 1.35f;
    [SerializeField] private float tooltipHideDelay = 0.18f;
    [SerializeField] private float lineThickness = 6f;

    [Header("Colores")]
    [SerializeField] private Color selectorColor = new Color(0.62f, 0.47f, 0.35f, 1f);
    [SerializeField] private Color selectorOptionColor = new Color(0.27f, 0.18f, 0.12f, 0.98f);
    [SerializeField] private Color tooltipColor = new Color(0.15f, 0.1f, 0.07f, 0.96f);
    [SerializeField] private Color cardColor = new Color(0.18f, 0.11f, 0.08f, 0.82f);
    [SerializeField] private Color plotColor = new Color(0.11f, 0.08f, 0.06f, 0.9f);
    [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.12f);
    [SerializeField] private Color axisColor = new Color(1f, 1f, 1f, 0.2f);
    [SerializeField] private Color pianoColor = new Color(0.94f, 0.77f, 0.31f, 1f);
    [SerializeField] private Color cantoColor = new Color(0.33f, 0.81f, 0.74f, 1f);

    private readonly List<MonthBucket> availableMonths = new List<MonthBucket>();
    private readonly List<PracticeSessionResponse> cachedSessions = new List<PracticeSessionResponse>();

    private MonthBucket selectedMonth;
    private string lastLoadError;
    private Coroutine loadRoutine;
    private Coroutine tooltipHideRoutine;
    private CultureInfo spanishCulture;

    private void OnEnable()
    {
        EnsureSceneReferences();
        EnsureRuntimeUi();

        if (loadRoutine != null) StopCoroutine(loadRoutine);

        loadRoutine = StartCoroutine(LoadAndRenderHistory());
    }

    private void OnDisable()
    {
        if (loadRoutine != null)
        {
            StopCoroutine(loadRoutine);
            loadRoutine = null;
        }

        SetSelectorOptionsVisible(false);
        HideTooltip();
    }

    private void EnsureSceneReferences()
    {
        if (rootRect == null) rootRect = transform as RectTransform;
        if (authService == null) authService = FindObjectOfType<AuthService>(true);
        if (pianoScrollView == null) pianoScrollView = FindScrollView("PanelGraficaPiano");
        if (cantoScrollView == null) cantoScrollView = FindScrollView("PanelGraficaCanto");
        if (pianoContent == null && pianoScrollView != null) pianoContent = pianoScrollView.content;
        if (cantoContent == null && cantoScrollView != null) cantoContent = cantoScrollView.content;
        if (spanishCulture == null) spanishCulture = CultureInfo.GetCultureInfo("es-ES");

        // Se reutilizan fuente y sprite de la escena para no romper el estilo visual.
        if (sharedFont == null)
            sharedFont = GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text != null && text.font != null)?.font;

        if (panelSprite == null)
            panelSprite = GetComponentsInChildren<Image>(true).FirstOrDefault(image => image != null && image.sprite != null)?.sprite;

        if (pointSprite == null) pointSprite = BuildCircleSprite();

        ClampRuntimeUiSettings();
    }

    /// <summary>Los mínimos evitan que valores del inspector dejen la UI ilegible en VR.</summary>
    private void ClampRuntimeUiSettings()
    {
        tooltipSize = new Vector2(Mathf.Max(tooltipSize.x, 620f), Mathf.Max(tooltipSize.y, 270f));
        tooltipTitleFontSize = Mathf.Max(tooltipTitleFontSize, 30f);
        tooltipBodyFontSize = Mathf.Max(tooltipBodyFontSize, 27f);
        tooltipMargin = Mathf.Max(tooltipMargin, 18f);
        pointSize = Mathf.Max(pointSize, 24f);
        pointHitSizeMultiplier = Mathf.Max(pointHitSizeMultiplier, 1.5f);
        pointVisualSizeMultiplier = Mathf.Max(pointVisualSizeMultiplier, 1.15f);
        tooltipHideDelay = Mathf.Max(tooltipHideDelay, 0.08f);
    }

    private IEnumerator LoadAndRenderHistory()
    {
        lastLoadError = null;
        selectorValueText.text = "Cargando sesiones...";
        RenderLoadingState();

        if (UserSession.Instance == null || !UserSession.Instance.IsLoggedIn)
        {
            FailLoad("No hay una sesión iniciada.");
            yield break;
        }

        if (authService == null)
        {
            FailLoad("No se encontró AuthService en la escena.");
            yield break;
        }

        string responseJson = null;
        string responseError = null;

        yield return StartCoroutine(authService.GetUserHistory(
            UserSession.Instance.userId,
            onSuccess: json => responseJson = json,
            onError: error => responseError = error));

        cachedSessions.Clear();

        if (!string.IsNullOrWhiteSpace(responseError))
        {
            FailLoad(responseError);
            yield break;
        }

        PracticeSessionListWrapper wrapper = JsonUtility.FromJson<PracticeSessionListWrapper>(responseJson);
        if (wrapper?.sessions != null) cachedSessions.AddRange(wrapper.sessions);

        BuildMonthBuckets();
        RefreshAllCharts();
        loadRoutine = null;
    }

    private void FailLoad(string error)
    {
        lastLoadError = error;
        availableMonths.Clear();
        selectedMonth = null;
        RefreshAllCharts();
        loadRoutine = null;
    }

    /// <summary>Agrupa las sesiones por mes, de más reciente a más antiguo.</summary>
    private void BuildMonthBuckets()
    {
        string previousSelectionKey = selectedMonth?.Key;
        availableMonths.Clear();

        Dictionary<string, MonthBucket> bucketsByKey = new Dictionary<string, MonthBucket>();

        foreach (PracticeSessionResponse session in cachedSessions)
        {
            if (!TryBuildSessionViewModel(session, out SessionViewModel sessionView)) continue;

            DateTime monthStart = new DateTime(sessionView.PracticeDate.Year, sessionView.PracticeDate.Month, 1);
            string key = monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture);

            if (!bucketsByKey.TryGetValue(key, out MonthBucket bucket))
            {
                bucket = new MonthBucket
                {
                    Key = key,
                    MonthStart = monthStart,
                    DisplayName = CapitalizeMonthName(monthStart.ToString("MMMM yyyy", spanishCulture))
                };

                bucketsByKey.Add(key, bucket);
                availableMonths.Add(bucket);
            }

            bucket.Sessions.Add(sessionView);
        }

        availableMonths.Sort((left, right) => right.MonthStart.CompareTo(left.MonthStart));
        foreach (MonthBucket bucket in availableMonths)
            bucket.Sessions.Sort((left, right) => left.PracticeDate.CompareTo(right.PracticeDate));

        if (availableMonths.Count == 0)
        {
            selectedMonth = null;
            return;
        }

        // Se intenta conservar el mes que ya estaba elegido; si no, el actual; si no, el más reciente.
        string currentMonthKey = DateTime.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        selectedMonth = availableMonths.FirstOrDefault(bucket => bucket.Key == previousSelectionKey)
                        ?? availableMonths.FirstOrDefault(bucket => bucket.Key == currentMonthKey)
                        ?? availableMonths[0];
    }

    private bool TryBuildSessionViewModel(PracticeSessionResponse session, out SessionViewModel sessionView)
    {
        sessionView = null;
        if (session == null || !TryParsePracticeDate(session.practice_datetime, out DateTime practiceDate)) return false;

        string practiceMode = session.practice_mode ?? string.Empty;
        bool isPiano = practiceMode.IndexOf("piano", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isCanto = practiceMode.IndexOf("canto", StringComparison.OrdinalIgnoreCase) >= 0;

        // Sesiones antiguas sin modo: se deduce por qué puntuación trae informada.
        if (!isPiano && !isCanto) isPiano = session.harmony_score > 0f || session.tuning_score <= 0f;

        float componentA = Mathf.Clamp(isPiano ? session.harmony_score : session.tuning_score, 0f, 100f);
        float rhythm = Mathf.Clamp(session.rhythm_score, 0f, 100f);

        sessionView = new SessionViewModel
        {
            Session = session,
            PracticeDate = practiceDate,
            IsPiano = isPiano,
            ComponentA = componentA,
            Rhythm = rhythm,
            Score = Mathf.Clamp((componentA + rhythm) * 0.5f, 0f, 100f),
            ComponentALabel = isPiano ? "Armonía" : "Afinación"
        };

        return true;
    }

    private bool TryParsePracticeDate(string rawValue, out DateTime practiceDate)
    {
        practiceDate = default;
        if (string.IsNullOrWhiteSpace(rawValue)) return false;

        string normalizedValue = rawValue.Trim();

        if (DateTimeOffset.TryParse(normalizedValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTimeOffset dateOffset))
        {
            practiceDate = dateOffset.LocalDateTime;
            return true;
        }

        return DateTime.TryParseExact(normalizedValue, SupportedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out practiceDate)
               || DateTime.TryParse(normalizedValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out practiceDate);
    }

    private string CapitalizeMonthName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        return value.Length == 1
            ? value.ToUpper(spanishCulture)
            : char.ToUpper(value[0], spanishCulture) + value.Substring(1);
    }

    private void SelectMonth(MonthBucket month)
    {
        selectedMonth = month;
        SetSelectorOptionsVisible(false);
        RefreshAllCharts();
    }

    private void ToggleSelectorOptions() => SetSelectorOptionsVisible(!selectorOptionsRoot.gameObject.activeSelf);

    private void SetSelectorOptionsVisible(bool visible)
    {
        if (selectorOptionsRoot != null)
            selectorOptionsRoot.gameObject.SetActive(visible && availableMonths.Count > 0);
    }

    public void ShowTooltip(RectTransform pointRect, string title, string body)
    {
        if (tooltipRoot == null) return;

        CancelScheduledTooltipHide();

        tooltipTitleText.text = title;
        tooltipBodyText.text = body;
        PositionTooltipNearPoint(pointRect);
        tooltipRoot.gameObject.SetActive(true);
    }

    /// <summary>Oculta con retardo para que el puntero pueda pasar del punto al tooltip.</summary>
    public void ScheduleHideTooltip()
    {
        CancelScheduledTooltipHide();
        tooltipHideRoutine = StartCoroutine(HideTooltipAfterDelay());
    }

    public void HideTooltip()
    {
        CancelScheduledTooltipHide();
        SetTooltipVisible(false);
    }

    private void CancelScheduledTooltipHide()
    {
        if (tooltipHideRoutine == null) return;

        StopCoroutine(tooltipHideRoutine);
        tooltipHideRoutine = null;
    }

    private IEnumerator HideTooltipAfterDelay()
    {
        yield return new WaitForSecondsRealtime(tooltipHideDelay);
        tooltipHideRoutine = null;
        SetTooltipVisible(false);
    }

    private void SetTooltipVisible(bool visible)
    {
        if (tooltipRoot != null) tooltipRoot.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Coloca el tooltip junto al punto, hacia el lado con espacio, y lo mantiene
    /// dentro de los límites del panel raíz.
    /// </summary>
    private void PositionTooltipNearPoint(RectTransform pointRect)
    {
        if (tooltipRoot == null) return;

        if (pointRect == null || rootRect == null)
        {
            tooltipRoot.anchoredPosition = tooltipPosition;
            return;
        }

        Vector3[] worldCorners = new Vector3[4];
        pointRect.GetWorldCorners(worldCorners);
        Vector3 worldAnchor = (worldCorners[2] + worldCorners[3]) * 0.5f;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldAnchor);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, screenPoint, null, out Vector2 localPoint))
        {
            tooltipRoot.anchoredPosition = tooltipPosition;
            return;
        }

        Vector2 halfTooltip = tooltipRoot.sizeDelta * 0.5f;
        Rect rootBounds = rootRect.rect;
        bool canPlaceRight = localPoint.x + tooltipOffset.x + tooltipRoot.sizeDelta.x <= rootBounds.xMax - tooltipMargin;
        float horizontalOffset = halfTooltip.x + tooltipOffset.x;

        tooltipRoot.anchoredPosition = new Vector2(
            Mathf.Clamp(localPoint.x + (canPlaceRight ? horizontalOffset : -horizontalOffset),
                rootBounds.xMin + halfTooltip.x + tooltipMargin,
                rootBounds.xMax - halfTooltip.x - tooltipMargin),
            Mathf.Clamp(localPoint.y + tooltipOffset.y,
                rootBounds.yMin + halfTooltip.y + tooltipMargin,
                rootBounds.yMax - halfTooltip.y - tooltipMargin));
    }
}

/// <summary>Punto interactivo de la gráfica: muestra el tooltip de su sesión.</summary>
public class EvolutionGraphPoint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private EvolutionGraphsController controller;
    private RectTransform pointRect;
    private string tooltipTitle;
    private string tooltipBody;

    public void Configure(EvolutionGraphsController owner, RectTransform anchorRect, string title, string body)
    {
        controller = owner;
        pointRect = anchorRect;
        tooltipTitle = title;
        tooltipBody = body;
    }

    public void OnPointerEnter(PointerEventData eventData) => controller?.ShowTooltip(pointRect, tooltipTitle, tooltipBody);

    public void OnPointerExit(PointerEventData eventData) => controller?.ScheduleHideTooltip();

    public void OnPointerClick(PointerEventData eventData) => controller?.ShowTooltip(pointRect, tooltipTitle, tooltipBody);
}
