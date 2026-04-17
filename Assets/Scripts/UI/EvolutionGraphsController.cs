using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EvolutionGraphsController : MonoBehaviour
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
    [SerializeField] private Vector2 tooltipSize = new Vector2(540f, 220f);
    [SerializeField] private Vector2 tooltipOffset = new Vector2(38f, 0f);
    [SerializeField] private float tooltipMargin = 28f;
    [SerializeField] private float tooltipTitleFontSize = 30f;
    [SerializeField] private float tooltipBodyFontSize = 27f;
    [SerializeField] private float chartCardHeight = 430f;
    [SerializeField] private float plotHeight = 320f;
    [SerializeField] private float pointSize = 20f;
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

    private RectTransform rootRect;
    private TMP_FontAsset sharedFont;
    private Sprite panelSprite;
    private Sprite pointSprite;
    private RectTransform selectorRoot;
    private Button selectorButton;
    private TMP_Text selectorValueText;
    private RectTransform selectorOptionsRoot;
    private RectTransform tooltipRoot;
    private TMP_Text tooltipTitleText;
    private TMP_Text tooltipBodyText;
    private MonthBucket selectedMonth;
    private string lastLoadError;
    private Coroutine loadRoutine;
    private CultureInfo spanishCulture;

    private void OnEnable()
    {
        EnsureSceneReferences();
        EnsureRuntimeUi();

        if (loadRoutine != null)
        {
            StopCoroutine(loadRoutine);
        }

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
        if (rootRect == null)
        {
            rootRect = transform as RectTransform;
        }

        if (authService == null)
        {
            authService = FindObjectOfType<AuthService>(true);
        }

        if (pianoScrollView == null)
        {
            pianoScrollView = FindScrollView("PanelGraficaPiano");
        }

        if (cantoScrollView == null)
        {
            cantoScrollView = FindScrollView("PanelGraficaCanto");
        }

        if (pianoContent == null && pianoScrollView != null)
        {
            pianoContent = pianoScrollView.content;
        }

        if (cantoContent == null && cantoScrollView != null)
        {
            cantoContent = cantoScrollView.content;
        }

        if (spanishCulture == null)
        {
            spanishCulture = CultureInfo.GetCultureInfo("es-ES");
        }

        if (sharedFont == null)
        {
            TMP_Text anyText = GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text != null && text.font != null);
            if (anyText != null)
            {
                sharedFont = anyText.font;
            }
        }

        if (panelSprite == null)
        {
            Image anyImage = GetComponentsInChildren<Image>(true).FirstOrDefault(image => image != null && image.sprite != null);
            if (anyImage != null)
            {
                panelSprite = anyImage.sprite;
            }
        }

        if (pointSprite == null)
        {
            pointSprite = BuildCircleSprite();
        }

        tooltipSize = new Vector2(Mathf.Max(tooltipSize.x, 540f), Mathf.Max(tooltipSize.y, 220f));
        tooltipTitleFontSize = Mathf.Max(tooltipTitleFontSize, 30f);
        tooltipBodyFontSize = Mathf.Max(tooltipBodyFontSize, 27f);
        tooltipMargin = Mathf.Max(tooltipMargin, 18f);
    }

    private IEnumerator LoadAndRenderHistory()
    {
        lastLoadError = null;
        selectorValueText.text = "Cargando sesiones...";
        RenderLoadingState();

        if (UserSession.Instance == null || !UserSession.Instance.IsLoggedIn)
        {
            lastLoadError = "No hay una sesión iniciada.";
            availableMonths.Clear();
            selectedMonth = null;
            RefreshAllCharts();
            loadRoutine = null;
            yield break;
        }

        if (authService == null)
        {
            lastLoadError = "No se encontró AuthService en la escena.";
            availableMonths.Clear();
            selectedMonth = null;
            RefreshAllCharts();
            loadRoutine = null;
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
            lastLoadError = responseError;
            availableMonths.Clear();
            selectedMonth = null;
            RefreshAllCharts();
            loadRoutine = null;
            yield break;
        }

        PracticeSessionListWrapper wrapper = JsonUtility.FromJson<PracticeSessionListWrapper>(responseJson);
        if (wrapper != null && wrapper.sessions != null)
        {
            cachedSessions.AddRange(wrapper.sessions);
        }

        BuildMonthBuckets();
        RefreshAllCharts();
        loadRoutine = null;
    }

    private void BuildMonthBuckets()
    {
        string previousSelectionKey = selectedMonth != null ? selectedMonth.Key : null;
        availableMonths.Clear();

        Dictionary<string, MonthBucket> bucketsByKey = new Dictionary<string, MonthBucket>();

        for (int i = 0; i < cachedSessions.Count; i++)
        {
            if (!TryBuildSessionViewModel(cachedSessions[i], out SessionViewModel sessionView))
            {
                continue;
            }

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
        for (int i = 0; i < availableMonths.Count; i++)
        {
            availableMonths[i].Sessions.Sort((left, right) => left.PracticeDate.CompareTo(right.PracticeDate));
        }

        if (availableMonths.Count == 0)
        {
            selectedMonth = null;
            return;
        }

        selectedMonth = availableMonths.FirstOrDefault(bucket => bucket.Key == previousSelectionKey);
        if (selectedMonth != null)
        {
            return;
        }

        string currentMonthKey = DateTime.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        selectedMonth = availableMonths.FirstOrDefault(bucket => bucket.Key == currentMonthKey) ?? availableMonths[0];
    }

    private bool TryBuildSessionViewModel(PracticeSessionResponse session, out SessionViewModel sessionView)
    {
        sessionView = null;
        if (session == null || !TryParsePracticeDate(session.practice_datetime, out DateTime practiceDate))
        {
            return false;
        }

        string practiceMode = session.practice_mode ?? string.Empty;
        bool isPiano = practiceMode.IndexOf("piano", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isCanto = practiceMode.IndexOf("canto", StringComparison.OrdinalIgnoreCase) >= 0;

        if (!isPiano && !isCanto)
        {
            if (session.harmony_score > 0f || session.tuning_score <= 0f)
            {
                isPiano = true;
            }
            else
            {
                isCanto = true;
            }
        }

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

    private void RefreshAllCharts()
    {
        UpdateSelectorUi();

        if (selectedMonth == null)
        {
            string message = string.IsNullOrWhiteSpace(lastLoadError)
                ? "Todavía no hay sesiones para mostrar en las gráficas."
                : lastLoadError;

            RenderEmptyChart(pianoContent, pianoScrollView, "Piano", message, pianoColor);
            RenderEmptyChart(cantoContent, cantoScrollView, "Canto", message, cantoColor);
            return;
        }

        List<SessionViewModel> pianoSessions = selectedMonth.Sessions.Where(session => session.IsPiano).ToList();
        List<SessionViewModel> cantoSessions = selectedMonth.Sessions.Where(session => !session.IsPiano).ToList();

        RenderChart(
            pianoContent,
            pianoScrollView,
            pianoSessions,
            "Piano",
            "Promedio por sesión de Armonía y Ritmo",
            pianoColor,
            "Armonía");

        RenderChart(
            cantoContent,
            cantoScrollView,
            cantoSessions,
            "Canto",
            "Promedio por sesión de Afinación y Ritmo",
            cantoColor,
            "Afinación");
    }

    private void RenderLoadingState()
    {
        RenderEmptyChart(pianoContent, pianoScrollView, "Piano", "Cargando sesiones del usuario...", pianoColor);
        RenderEmptyChart(cantoContent, cantoScrollView, "Canto", "Cargando sesiones del usuario...", cantoColor);
    }

    private void RenderChart(
        RectTransform content,
        ScrollRect scrollView,
        List<SessionViewModel> sessions,
        string title,
        string subtitle,
        Color accentColor,
        string componentALabel)
    {
        if (content == null)
        {
            return;
        }

        ClearContent(content);

        if (sessions == null || sessions.Count == 0)
        {
            RenderEmptyChart(content, scrollView, title, $"No hay sesiones de {title.ToLowerInvariant()} en {selectedMonth.DisplayName}.", accentColor);
            return;
        }

        float viewportWidth = ResolveViewportWidth(scrollView);
        float cardWidth = Mathf.Max(960f, viewportWidth - 36f);
        RectTransform card = CreateCard(content, title, subtitle, accentColor, cardWidth);

        float averageScore = sessions.Average(session => session.Score);
        TMP_Text summaryText = CreateText(
            "Summary",
            card,
            26f,
            new Color(1f, 1f, 1f, 0.85f),
            TextAlignmentOptions.TopRight,
            $"{sessions.Count} sesiones • promedio {averageScore:F1}%");
        summaryText.rectTransform.anchorMin = new Vector2(1f, 1f);
        summaryText.rectTransform.anchorMax = new Vector2(1f, 1f);
        summaryText.rectTransform.pivot = new Vector2(1f, 1f);
        summaryText.rectTransform.anchoredPosition = new Vector2(-36f, -30f);
        summaryText.rectTransform.sizeDelta = new Vector2(460f, 40f);

        RectTransform plot = CreatePanelRect("Plot", card, plotColor, new Vector2(cardWidth - 56f, plotHeight));
        plot.anchorMin = new Vector2(0.5f, 1f);
        plot.anchorMax = new Vector2(0.5f, 1f);
        plot.pivot = new Vector2(0.5f, 1f);
        plot.anchoredPosition = new Vector2(0f, -86f);

        BuildPlot(plot, sessions, accentColor, componentALabel);

        if (scrollView != null)
        {
            scrollView.verticalNormalizedPosition = 1f;
        }
    }

    private void RenderEmptyChart(RectTransform content, ScrollRect scrollView, string title, string message, Color accentColor)
    {
        if (content == null)
        {
            return;
        }

        ClearContent(content);

        float viewportWidth = ResolveViewportWidth(scrollView);
        float cardWidth = Mathf.Max(960f, viewportWidth - 36f);
        RectTransform card = CreateCard(content, title, "", accentColor, cardWidth);
        RectTransform emptyBody = CreatePanelRect("EmptyBody", card, plotColor, new Vector2(cardWidth - 56f, plotHeight));
        emptyBody.anchorMin = new Vector2(0.5f, 1f);
        emptyBody.anchorMax = new Vector2(0.5f, 1f);
        emptyBody.pivot = new Vector2(0.5f, 1f);
        emptyBody.anchoredPosition = new Vector2(0f, -86f);

        TMP_Text messageText = CreateText("Message", emptyBody, 28f, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center, message);
        messageText.enableWordWrapping = true;
        messageText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        messageText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        messageText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        messageText.rectTransform.sizeDelta = new Vector2(cardWidth - 120f, 120f);
        messageText.rectTransform.anchoredPosition = Vector2.zero;

        if (scrollView != null)
        {
            scrollView.verticalNormalizedPosition = 1f;
        }
    }

    private RectTransform CreateCard(RectTransform content, string title, string subtitle, Color accentColor, float cardWidth)
    {
        RectTransform card = CreatePanelRect("RuntimeChartCard", content, cardColor, new Vector2(cardWidth, chartCardHeight));
        LayoutElement layoutElement = card.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = cardWidth;
        layoutElement.preferredHeight = chartCardHeight;
        layoutElement.minHeight = chartCardHeight;

        TMP_Text titleText = CreateText("Title", card, 34f, accentColor, TextAlignmentOptions.TopLeft, title);
        titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(0f, 1f);
        titleText.rectTransform.pivot = new Vector2(0f, 1f);
        titleText.rectTransform.anchoredPosition = new Vector2(28f, -24f);
        titleText.rectTransform.sizeDelta = new Vector2(320f, 42f);

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            TMP_Text subtitleText = CreateText("Subtitle", card, 22f, new Color(1f, 1f, 1f, 0.65f), TextAlignmentOptions.TopLeft, subtitle);
            subtitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            subtitleText.rectTransform.anchorMax = new Vector2(0f, 1f);
            subtitleText.rectTransform.pivot = new Vector2(0f, 1f);
            subtitleText.rectTransform.anchoredPosition = new Vector2(28f, -58f);
            subtitleText.rectTransform.sizeDelta = new Vector2(640f, 30f);
        }

        return card;
    }

    private void BuildPlot(RectTransform plot, List<SessionViewModel> sessions, Color accentColor, string componentALabel)
    {
        const float leftPadding = 84f;
        const float rightPadding = 28f;
        const float topPadding = 28f;
        const float bottomPadding = 54f;

        float usableWidth = Mathf.Max(220f, plot.sizeDelta.x - leftPadding - rightPadding);
        float usableHeight = Mathf.Max(120f, plot.sizeDelta.y - topPadding - bottomPadding);

        CreateAxisLine(plot, new Vector2(leftPadding, bottomPadding), new Vector2(leftPadding, bottomPadding + usableHeight), axisColor, 3f);
        CreateAxisLine(plot, new Vector2(leftPadding, bottomPadding), new Vector2(leftPadding + usableWidth, bottomPadding), axisColor, 3f);

        for (int step = 0; step <= 4; step++)
        {
            float normalized = step / 4f;
            float value = normalized * 100f;
            float y = bottomPadding + (normalized * usableHeight);

            CreateAxisLine(plot, new Vector2(leftPadding, y), new Vector2(leftPadding + usableWidth, y), gridColor, step == 0 ? 0f : 2f);

            TMP_Text label = CreateText($"YAxis{step}", plot, 18f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Right, value.ToString("F0", CultureInfo.InvariantCulture));
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(0f, 0f);
            label.rectTransform.pivot = new Vector2(1f, 0.5f);
            label.rectTransform.anchoredPosition = new Vector2(leftPadding - 12f, y);
            label.rectTransform.sizeDelta = new Vector2(52f, 24f);
        }

        int labelStep = Mathf.Max(1, Mathf.CeilToInt(sessions.Count / 6f));
        Vector2 previousPoint = Vector2.zero;
        bool hasPreviousPoint = false;

        for (int i = 0; i < sessions.Count; i++)
        {
            SessionViewModel session = sessions[i];
            float xNormalized = sessions.Count == 1 ? 0.5f : i / (float)(sessions.Count - 1);
            float x = leftPadding + (xNormalized * usableWidth);
            float y = bottomPadding + ((Mathf.Clamp(session.Score, 0f, 100f) / 100f) * usableHeight);
            Vector2 pointPosition = new Vector2(x, y);

            if (hasPreviousPoint)
            {
                CreateAxisLine(plot, previousPoint, pointPosition, accentColor, lineThickness);
            }

            CreateGraphPoint(plot, session, pointPosition, accentColor, componentALabel);

            if (i % labelStep == 0 || i == sessions.Count - 1)
            {
                TMP_Text label = CreateText($"XAxis{i}", plot, 16f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Top, session.PracticeDate.ToString("dd", spanishCulture));
                label.rectTransform.anchorMin = new Vector2(0f, 0f);
                label.rectTransform.anchorMax = new Vector2(0f, 0f);
                label.rectTransform.pivot = new Vector2(0.5f, 1f);
                label.rectTransform.anchoredPosition = new Vector2(x, bottomPadding - 12f);
                label.rectTransform.sizeDelta = new Vector2(56f, 24f);
            }

            previousPoint = pointPosition;
            hasPreviousPoint = true;
        }

        TMP_Text footer = CreateText("Footer", plot, 18f, new Color(1f, 1f, 1f, 0.48f), TextAlignmentOptions.BottomLeft, $"Detalle al posar el puntero: {componentALabel} + Ritmo");
        footer.rectTransform.anchorMin = new Vector2(0f, 0f);
        footer.rectTransform.anchorMax = new Vector2(0f, 0f);
        footer.rectTransform.pivot = new Vector2(0f, 0f);
        footer.rectTransform.anchoredPosition = new Vector2(leftPadding, 12f);
        footer.rectTransform.sizeDelta = new Vector2(420f, 24f);
    }

    private void CreateGraphPoint(RectTransform plot, SessionViewModel session, Vector2 pointPosition, Color accentColor, string componentALabel)
    {
        RectTransform pointRect = CreateRectTransform("Point", plot, new Vector2(pointSize, pointSize));
        pointRect.anchorMin = new Vector2(0f, 0f);
        pointRect.anchorMax = new Vector2(0f, 0f);
        pointRect.pivot = new Vector2(0.5f, 0.5f);
        pointRect.anchoredPosition = pointPosition;

        Image pointImage = pointRect.gameObject.AddComponent<Image>();
        pointImage.sprite = pointSprite;
        pointImage.color = accentColor;
        pointImage.raycastTarget = true;

        EvolutionGraphPoint point = pointRect.gameObject.AddComponent<EvolutionGraphPoint>();
        point.Configure(
            this,
            pointRect,
            BuildPointTitle(session),
            BuildPointBody(session, componentALabel));
    }

    private string BuildPointTitle(SessionViewModel session)
    {
        string songTitle = session.Session != null && !string.IsNullOrWhiteSpace(session.Session.song_title)
            ? session.Session.song_title
            : "Sesión";

        return $"{songTitle} • {session.PracticeDate.ToString("dd/MM HH:mm", spanishCulture)}";
    }

    private string BuildPointBody(SessionViewModel session, string componentALabel)
    {
        string practiceMode = session.Session != null && !string.IsNullOrWhiteSpace(session.Session.practice_mode)
            ? session.Session.practice_mode
            : (session.IsPiano ? "PIANO" : "CANTO");

        return
            componentALabel + ": " + session.ComponentA.ToString("F0", CultureInfo.InvariantCulture) + "%\n" +
            "Ritmo: " + session.Rhythm.ToString("F0", CultureInfo.InvariantCulture) + "%\n" +
            "Promedio: " + session.Score.ToString("F0", CultureInfo.InvariantCulture) + "%\n" +
            "Modo: " + practiceMode;
    }

    private void EnsureRuntimeUi()
    {
        if (rootRect == null)
        {
            return;
        }

        if (selectorRoot == null)
        {
            BuildSelectorUi();
        }

        if (tooltipRoot == null)
        {
            BuildTooltipUi();
        }
    }

    private void BuildSelectorUi()
    {
        selectorRoot = CreateRectTransform("MonthSelector", rootRect, selectorSize);
        selectorRoot.anchorMin = new Vector2(0.5f, 1f);
        selectorRoot.anchorMax = new Vector2(0.5f, 1f);
        selectorRoot.pivot = new Vector2(0.5f, 1f);
        selectorRoot.anchoredPosition = selectorPosition;

        TMP_Text label = CreateText("Label", selectorRoot, 22f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.MidlineLeft, "Mes");
        label.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        label.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        label.rectTransform.pivot = new Vector2(0f, 0.5f);
        label.rectTransform.anchoredPosition = new Vector2(-150f, 0f);
        label.rectTransform.sizeDelta = new Vector2(80f, 34f);

        RectTransform buttonRect = CreatePanelRect("SelectorButton", selectorRoot, selectorColor, new Vector2(320f, 58f));
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, 0f);

        selectorButton = buttonRect.gameObject.AddComponent<Button>();
        ColorBlock colors = selectorButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.94f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.88f);
        colors.selectedColor = Color.white;
        selectorButton.colors = colors;
        selectorButton.onClick.AddListener(ToggleSelectorOptions);

        selectorValueText = CreateText("Value", buttonRect, 23f, Color.white, TextAlignmentOptions.MidlineLeft, "Sin datos");
        selectorValueText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        selectorValueText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        selectorValueText.rectTransform.pivot = new Vector2(0f, 0.5f);
        selectorValueText.rectTransform.anchoredPosition = new Vector2(18f, 0f);
        selectorValueText.rectTransform.sizeDelta = new Vector2(-58f, 32f);

        TMP_Text arrowText = CreateText("Arrow", buttonRect, 24f, Color.white, TextAlignmentOptions.Center, "▼");
        arrowText.rectTransform.anchorMin = new Vector2(1f, 0.5f);
        arrowText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        arrowText.rectTransform.pivot = new Vector2(1f, 0.5f);
        arrowText.rectTransform.anchoredPosition = new Vector2(-18f, 0f);
        arrowText.rectTransform.sizeDelta = new Vector2(30f, 30f);

        selectorOptionsRoot = CreatePanelRect("Options", selectorRoot, selectorOptionColor, new Vector2(320f, 0f));
        selectorOptionsRoot.anchorMin = new Vector2(1f, 1f);
        selectorOptionsRoot.anchorMax = new Vector2(1f, 1f);
        selectorOptionsRoot.pivot = new Vector2(1f, 1f);
        selectorOptionsRoot.anchoredPosition = new Vector2(0f, -64f);
        VerticalLayoutGroup layout = selectorOptionsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 6f;
        layout.padding = new RectOffset(8, 8, 8, 8);

        ContentSizeFitter fitter = selectorOptionsRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        SetSelectorOptionsVisible(false);
    }

    private void BuildTooltipUi()
    {
        tooltipRoot = CreatePanelRect("PointTooltip", rootRect, tooltipColor, tooltipSize);
        tooltipRoot.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRoot.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRoot.pivot = new Vector2(0.5f, 0.5f);
        tooltipRoot.anchoredPosition = Vector2.zero;

        tooltipTitleText = CreateText("TooltipTitle", tooltipRoot, tooltipTitleFontSize, Color.white, TextAlignmentOptions.TopLeft, string.Empty);
        tooltipTitleText.enableWordWrapping = true;
        tooltipTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        tooltipTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        tooltipTitleText.rectTransform.pivot = new Vector2(0f, 1f);
        tooltipTitleText.rectTransform.anchoredPosition = new Vector2(18f, -16f);
        tooltipTitleText.rectTransform.sizeDelta = new Vector2(-36f, 56f);

        tooltipBodyText = CreateText("TooltipBody", tooltipRoot, tooltipBodyFontSize, new Color(1f, 1f, 1f, 0.88f), TextAlignmentOptions.TopLeft, "Pasa el puntero sobre un punto para ver la sesión.");
        tooltipBodyText.enableWordWrapping = true;
        tooltipBodyText.rectTransform.anchorMin = new Vector2(0f, 0f);
        tooltipBodyText.rectTransform.anchorMax = new Vector2(1f, 1f);
        tooltipBodyText.rectTransform.pivot = new Vector2(0f, 1f);
        tooltipBodyText.rectTransform.anchoredPosition = new Vector2(18f, -76f);
        tooltipBodyText.rectTransform.sizeDelta = new Vector2(-36f, -92f);

        tooltipRoot.gameObject.SetActive(false);
    }

    private void UpdateSelectorUi()
    {
        if (selectorValueText == null)
        {
            return;
        }

        if (selectedMonth == null)
        {
            selectorValueText.text = string.IsNullOrWhiteSpace(lastLoadError) ? "Sin sesiones" : "Sin datos";
            selectorButton.interactable = false;
            RebuildSelectorOptions();
            return;
        }

        selectorValueText.text = selectedMonth.DisplayName;
        selectorButton.interactable = availableMonths.Count > 0;
        RebuildSelectorOptions();
    }

    private void RebuildSelectorOptions()
    {
        if (selectorOptionsRoot == null)
        {
            return;
        }

        for (int i = selectorOptionsRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = selectorOptionsRoot.GetChild(i);
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        for (int i = 0; i < availableMonths.Count; i++)
        {
            MonthBucket bucket = availableMonths[i];
            RectTransform option = CreatePanelRect("Option", selectorOptionsRoot, selectedMonth == bucket ? selectorColor : new Color(1f, 1f, 1f, 0.06f), new Vector2(0f, 52f));
            LayoutElement layoutElement = option.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 52f;

            Button button = option.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.94f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.85f);
            colors.selectedColor = Color.white;
            button.colors = colors;

            MonthBucket capturedBucket = bucket;
            button.onClick.AddListener(() => SelectMonth(capturedBucket));

            TMP_Text optionText = CreateText("OptionText", option, 22f, Color.white, TextAlignmentOptions.Center, bucket.DisplayName);
            optionText.rectTransform.anchorMin = new Vector2(0f, 0f);
            optionText.rectTransform.anchorMax = new Vector2(1f, 1f);
            optionText.rectTransform.offsetMin = new Vector2(12f, 8f);
            optionText.rectTransform.offsetMax = new Vector2(-12f, -8f);
        }
    }

    private void ToggleSelectorOptions()
    {
        SetSelectorOptionsVisible(!selectorOptionsRoot.gameObject.activeSelf);
    }

    private void SelectMonth(MonthBucket month)
    {
        selectedMonth = month;
        SetSelectorOptionsVisible(false);
        RefreshAllCharts();
    }

    private void SetSelectorOptionsVisible(bool visible)
    {
        if (selectorOptionsRoot != null)
        {
            selectorOptionsRoot.gameObject.SetActive(visible && availableMonths.Count > 0);
        }
    }

    public void ShowTooltip(RectTransform pointRect, string title, string body)
    {
        if (tooltipRoot == null)
        {
            return;
        }

        tooltipTitleText.text = title;
        tooltipBodyText.text = body;
        PositionTooltipNearPoint(pointRect);
        tooltipRoot.gameObject.SetActive(true);
    }

    public void HideTooltip()
    {
        if (tooltipRoot != null)
        {
            tooltipRoot.gameObject.SetActive(false);
        }
    }

    private void PositionTooltipNearPoint(RectTransform pointRect)
    {
        if (tooltipRoot == null)
        {
            return;
        }

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

        Vector2 desiredPosition = new Vector2(
            localPoint.x + (canPlaceRight ? horizontalOffset : -horizontalOffset),
            localPoint.y + tooltipOffset.y);

        desiredPosition.x = Mathf.Clamp(
            desiredPosition.x,
            rootBounds.xMin + halfTooltip.x + tooltipMargin,
            rootBounds.xMax - halfTooltip.x - tooltipMargin);

        desiredPosition.y = Mathf.Clamp(
            desiredPosition.y,
            rootBounds.yMin + halfTooltip.y + tooltipMargin,
            rootBounds.yMax - halfTooltip.y - tooltipMargin);

        tooltipRoot.anchoredPosition = desiredPosition;
    }

    private void ClearContent(RectTransform content)
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    private ScrollRect FindScrollView(string panelName)
    {
        Transform panel = transform.Find(panelName);
        if (panel == null)
        {
            return null;
        }

        return panel.GetComponentInChildren<ScrollRect>(true);
    }

    private float ResolveViewportWidth(ScrollRect scrollView)
    {
        if (scrollView != null && scrollView.viewport != null)
        {
            return scrollView.viewport.rect.width > 0f ? scrollView.viewport.rect.width : 1480f;
        }

        return 1480f;
    }

    private bool TryParsePracticeDate(string rawValue, out DateTime practiceDate)
    {
        practiceDate = default;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        string normalizedValue = rawValue.Trim();
        if (DateTimeOffset.TryParse(normalizedValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTimeOffset dateOffset))
        {
            practiceDate = dateOffset.LocalDateTime;
            return true;
        }

        if (DateTime.TryParseExact(normalizedValue, SupportedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out practiceDate))
        {
            return true;
        }

        return DateTime.TryParse(normalizedValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out practiceDate);
    }

    private string CapitalizeMonthName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.Length == 1)
        {
            return value.ToUpper(spanishCulture);
        }

        return char.ToUpper(value[0], spanishCulture) + value.Substring(1);
    }

    private RectTransform CreateRectTransform(string name, Transform parent, Vector2 size)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = this.gameObject.layer;

        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.localScale = Vector3.one;
        rectTransform.sizeDelta = size;
        return rectTransform;
    }

    private RectTransform CreatePanelRect(string name, Transform parent, Color color, Vector2 size)
    {
        RectTransform rectTransform = CreateRectTransform(name, parent, size);
        Image image = rectTransform.gameObject.AddComponent<Image>();
        image.sprite = panelSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        return rectTransform;
    }

    private TMP_Text CreateText(string name, Transform parent, float fontSize, Color color, TextAlignmentOptions alignment, string text)
    {
        RectTransform rectTransform = CreateRectTransform(name, parent, Vector2.zero);
        TextMeshProUGUI textComponent = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();
        textComponent.font = sharedFont;
        textComponent.fontSize = fontSize;
        textComponent.color = color;
        textComponent.alignment = alignment;
        textComponent.text = text;
        textComponent.enableWordWrapping = false;
        return textComponent;
    }

    private void CreateAxisLine(RectTransform parent, Vector2 from, Vector2 to, Color color, float thickness)
    {
        if (thickness <= 0f)
        {
            return;
        }

        RectTransform lineRect = CreateRectTransform("Line", parent, Vector2.zero);
        Image image = lineRect.gameObject.AddComponent<Image>();
        image.sprite = panelSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;

        Vector2 direction = to - from;
        float length = direction.magnitude;
        if (length <= 0.001f)
        {
            Destroy(lineRect.gameObject);
            return;
        }

        lineRect.anchorMin = new Vector2(0f, 0f);
        lineRect.anchorMax = new Vector2(0f, 0f);
        lineRect.pivot = new Vector2(0f, 0.5f);
        lineRect.anchoredPosition = from;
        lineRect.sizeDelta = new Vector2(length, thickness);
        lineRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    private Sprite BuildCircleSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                Color color = distance <= radius ? Color.white : Color.clear;
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }
}

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

    public void OnPointerEnter(PointerEventData eventData)
    {
        controller?.ShowTooltip(pointRect, tooltipTitle, tooltipBody);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        controller?.HideTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        controller?.ShowTooltip(pointRect, tooltipTitle, tooltipBody);
    }
}