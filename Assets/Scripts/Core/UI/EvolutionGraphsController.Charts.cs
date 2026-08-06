using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dibujado de las tarjetas de gráfica, el selector de mes y el tooltip.
/// Todo se construye por código sobre los ScrollView que ya existen en la escena.
/// </summary>
public partial class EvolutionGraphsController
{
    private static readonly Vector2 AnchorTopLeft = new Vector2(0f, 1f);
    private static readonly Vector2 AnchorTopRight = new Vector2(1f, 1f);
    private static readonly Vector2 AnchorTopCenter = new Vector2(0.5f, 1f);
    private static readonly Vector2 AnchorCenter = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 AnchorBottomLeft = new Vector2(0f, 0f);
    private static readonly Vector2 AnchorMidLeft = new Vector2(0f, 0.5f);
    private static readonly Vector2 AnchorMidRight = new Vector2(1f, 0.5f);

    private const float FallbackViewportWidth = 1480f;
    private const float MinimumCardWidth = 960f;
    private const float PlotTopOffset = -86f;

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

    private void EnsureRuntimeUi()
    {
        if (rootRect == null) return;

        if (selectorRoot == null) BuildSelectorUi();
        if (tooltipRoot == null) BuildTooltipUi();
    }

    private void BuildSelectorUi()
    {
        selectorRoot = CreateRectTransform("MonthSelector", rootRect, selectorSize);
        SetRect(selectorRoot, AnchorTopCenter, AnchorTopCenter, selectorPosition, selectorSize);

        TMP_Text label = CreateText("Label", selectorRoot, 22f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.MidlineLeft, "Mes");
        SetRect(label.rectTransform, AnchorMidLeft, AnchorMidLeft, new Vector2(-150f, 0f), new Vector2(80f, 34f));

        RectTransform buttonRect = CreatePanelRect("SelectorButton", selectorRoot, selectorColor, new Vector2(320f, 58f));
        SetRect(buttonRect, AnchorMidRight, AnchorMidRight, Vector2.zero, new Vector2(320f, 58f));

        selectorButton = buttonRect.gameObject.AddComponent<Button>();
        selectorButton.colors = BuildInteractableColors(0.88f);
        selectorButton.onClick.AddListener(ToggleSelectorOptions);

        selectorValueText = CreateText("Value", buttonRect, 23f, Color.white, TextAlignmentOptions.MidlineLeft, "Sin datos");
        SetRect(selectorValueText.rectTransform, AnchorMidLeft, AnchorMidRight, AnchorMidLeft, new Vector2(18f, 0f), new Vector2(-58f, 32f));

        TMP_Text arrowText = CreateText("Arrow", buttonRect, 24f, Color.white, TextAlignmentOptions.Center, "▼");
        SetRect(arrowText.rectTransform, AnchorMidRight, AnchorMidRight, new Vector2(-18f, 0f), new Vector2(30f, 30f));

        selectorOptionsRoot = CreatePanelRect("Options", selectorRoot, selectorOptionColor, new Vector2(320f, 0f));
        SetRect(selectorOptionsRoot, AnchorTopRight, AnchorTopRight, new Vector2(0f, -64f), new Vector2(320f, 0f));

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
        SetRect(tooltipRoot, AnchorCenter, AnchorCenter, Vector2.zero, tooltipSize);

        // El tooltip no debe capturar el puntero o robaría el hover al propio punto.
        Image tooltipImage = tooltipRoot.GetComponent<Image>();
        if (tooltipImage != null) tooltipImage.raycastTarget = false;

        tooltipTitleText = CreateText("TooltipTitle", tooltipRoot, tooltipTitleFontSize, Color.white, TextAlignmentOptions.TopLeft, string.Empty);
        tooltipTitleText.enableWordWrapping = true;
        SetRect(tooltipTitleText.rectTransform, AnchorTopLeft, AnchorTopRight, AnchorTopLeft, new Vector2(18f, -16f), new Vector2(-36f, 72f));

        tooltipBodyText = CreateText("TooltipBody", tooltipRoot, tooltipBodyFontSize, new Color(1f, 1f, 1f, 0.88f), TextAlignmentOptions.TopLeft, "Pasa el puntero sobre un punto para ver la sesión.");
        tooltipBodyText.enableWordWrapping = true;
        SetRect(tooltipBodyText.rectTransform, AnchorBottomLeft, AnchorTopRight, AnchorTopLeft, new Vector2(18f, -92f), new Vector2(-36f, -112f));

        tooltipRoot.gameObject.SetActive(false);
    }

    private void UpdateSelectorUi()
    {
        if (selectorValueText == null) return;

        if (selectedMonth == null)
        {
            selectorValueText.text = string.IsNullOrWhiteSpace(lastLoadError) ? "Sin sesiones" : "Sin datos";
            selectorButton.interactable = false;
        }
        else
        {
            selectorValueText.text = selectedMonth.DisplayName;
            selectorButton.interactable = availableMonths.Count > 0;
        }

        RebuildSelectorOptions();
    }

    private void RebuildSelectorOptions()
    {
        if (selectorOptionsRoot == null) return;

        ClearContent(selectorOptionsRoot);

        foreach (MonthBucket bucket in availableMonths)
        {
            Color optionColor = selectedMonth == bucket ? selectorColor : new Color(1f, 1f, 1f, 0.06f);
            RectTransform option = CreatePanelRect("Option", selectorOptionsRoot, optionColor, new Vector2(0f, 52f));
            option.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;

            Button button = option.gameObject.AddComponent<Button>();
            button.colors = BuildInteractableColors(0.85f);

            MonthBucket capturedBucket = bucket;
            button.onClick.AddListener(() => SelectMonth(capturedBucket));

            TMP_Text optionText = CreateText("OptionText", option, 22f, Color.white, TextAlignmentOptions.Center, bucket.DisplayName);
            optionText.rectTransform.anchorMin = AnchorBottomLeft;
            optionText.rectTransform.anchorMax = AnchorTopRight;
            optionText.rectTransform.offsetMin = new Vector2(12f, 8f);
            optionText.rectTransform.offsetMax = new Vector2(-12f, -8f);
        }
    }

    private static ColorBlock BuildInteractableColors(float pressedAlpha)
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.94f);
        colors.pressedColor = new Color(1f, 1f, 1f, pressedAlpha);
        colors.selectedColor = Color.white;
        return colors;
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

        RenderChart(pianoContent, pianoScrollView, pianoSessions, "Piano", "Promedio por sesión de Armonía y Ritmo", pianoColor, "Armonía");
        RenderChart(cantoContent, cantoScrollView, cantoSessions, "Canto", "Promedio por sesión de Afinación y Ritmo", cantoColor, "Afinación");
    }

    private void RenderLoadingState()
    {
        const string message = "Cargando sesiones del usuario...";
        RenderEmptyChart(pianoContent, pianoScrollView, "Piano", message, pianoColor);
        RenderEmptyChart(cantoContent, cantoScrollView, "Canto", message, cantoColor);
    }

    private void RenderChart(RectTransform content, ScrollRect scrollView, List<SessionViewModel> sessions,
        string title, string subtitle, Color accentColor, string componentALabel)
    {
        if (content == null) return;

        ClearContent(content);

        if (sessions == null || sessions.Count == 0)
        {
            RenderEmptyChart(content, scrollView, title,
                $"No hay sesiones de {title.ToLowerInvariant()} en {selectedMonth.DisplayName}.", accentColor);
            return;
        }

        float cardWidth = ResolveCardWidth(scrollView);
        RectTransform card = CreateCard(content, title, subtitle, accentColor, cardWidth);

        float averageScore = sessions.Average(session => session.Score);
        TMP_Text summaryText = CreateText("Summary", card, 26f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.TopRight,
            $"{sessions.Count} sesiones • promedio {averageScore:F1}%");
        SetRect(summaryText.rectTransform, AnchorTopRight, AnchorTopRight, new Vector2(-36f, -30f), new Vector2(460f, 40f));

        RectTransform plot = CreatePanelRect("Plot", card, plotColor, new Vector2(cardWidth - 56f, plotHeight));
        SetRect(plot, AnchorTopCenter, AnchorTopCenter, new Vector2(0f, PlotTopOffset), new Vector2(cardWidth - 56f, plotHeight));

        BuildPlot(plot, sessions, accentColor, componentALabel);

        ScrollToTop(scrollView);
    }

    private void RenderEmptyChart(RectTransform content, ScrollRect scrollView, string title, string message, Color accentColor)
    {
        if (content == null) return;

        ClearContent(content);

        float cardWidth = ResolveCardWidth(scrollView);
        RectTransform card = CreateCard(content, title, "", accentColor, cardWidth);

        RectTransform emptyBody = CreatePanelRect("EmptyBody", card, plotColor, new Vector2(cardWidth - 56f, plotHeight));
        SetRect(emptyBody, AnchorTopCenter, AnchorTopCenter, new Vector2(0f, PlotTopOffset), new Vector2(cardWidth - 56f, plotHeight));

        TMP_Text messageText = CreateText("Message", emptyBody, 28f, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center, message);
        messageText.enableWordWrapping = true;
        SetRect(messageText.rectTransform, AnchorCenter, AnchorCenter, Vector2.zero, new Vector2(cardWidth - 120f, 120f));

        ScrollToTop(scrollView);
    }

    private static void ScrollToTop(ScrollRect scrollView)
    {
        if (scrollView != null) scrollView.verticalNormalizedPosition = 1f;
    }

    private RectTransform CreateCard(RectTransform content, string title, string subtitle, Color accentColor, float cardWidth)
    {
        RectTransform card = CreatePanelRect("RuntimeChartCard", content, cardColor, new Vector2(cardWidth, chartCardHeight));

        LayoutElement layoutElement = card.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = cardWidth;
        layoutElement.preferredHeight = chartCardHeight;
        layoutElement.minHeight = chartCardHeight;

        TMP_Text titleText = CreateText("Title", card, 34f, accentColor, TextAlignmentOptions.TopLeft, title);
        SetRect(titleText.rectTransform, AnchorTopLeft, AnchorTopLeft, new Vector2(28f, -24f), new Vector2(320f, 42f));

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            TMP_Text subtitleText = CreateText("Subtitle", card, 22f, new Color(1f, 1f, 1f, 0.65f), TextAlignmentOptions.TopLeft, subtitle);
            SetRect(subtitleText.rectTransform, AnchorTopLeft, AnchorTopLeft, new Vector2(28f, -58f), new Vector2(640f, 30f));
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

        BuildYAxis(plot, leftPadding, bottomPadding, usableWidth, usableHeight);
        BuildSessionSeries(plot, sessions, accentColor, componentALabel, leftPadding, bottomPadding, usableWidth, usableHeight);

        TMP_Text footer = CreateText("Footer", plot, 18f, new Color(1f, 1f, 1f, 0.48f), TextAlignmentOptions.BottomLeft,
            $"Detalle al posar el puntero: {componentALabel} + Ritmo");
        SetRect(footer.rectTransform, AnchorBottomLeft, AnchorBottomLeft, new Vector2(leftPadding, 12f), new Vector2(420f, 24f));
    }

    private void BuildYAxis(RectTransform plot, float leftPadding, float bottomPadding, float usableWidth, float usableHeight)
    {
        for (int step = 0; step <= 4; step++)
        {
            float normalized = step / 4f;
            float y = bottomPadding + (normalized * usableHeight);

            // El primer trazo coincide con el eje, así que se omite pasando grosor 0.
            CreateAxisLine(plot, new Vector2(leftPadding, y), new Vector2(leftPadding + usableWidth, y), gridColor, step == 0 ? 0f : 2f);

            TMP_Text label = CreateText($"YAxis{step}", plot, 18f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Right,
                (normalized * 100f).ToString("F0", CultureInfo.InvariantCulture));
            SetRect(label.rectTransform, AnchorBottomLeft, AnchorBottomLeft, AnchorMidRight, new Vector2(leftPadding - 12f, y), new Vector2(52f, 24f));
        }
    }

    private void BuildSessionSeries(RectTransform plot, List<SessionViewModel> sessions, Color accentColor,
        string componentALabel, float leftPadding, float bottomPadding, float usableWidth, float usableHeight)
    {
        // Con muchas sesiones se etiqueta una de cada N para que las fechas no se solapen.
        int labelStep = Mathf.Max(1, Mathf.CeilToInt(sessions.Count / 6f));
        Vector2 previousPoint = Vector2.zero;
        bool hasPreviousPoint = false;

        for (int i = 0; i < sessions.Count; i++)
        {
            SessionViewModel session = sessions[i];
            float xNormalized = sessions.Count == 1 ? 0.5f : i / (float)(sessions.Count - 1);
            float x = leftPadding + (xNormalized * usableWidth);
            float y = bottomPadding + (Mathf.Clamp(session.Score, 0f, 100f) / 100f * usableHeight);
            Vector2 pointPosition = new Vector2(x, y);

            if (hasPreviousPoint) CreateAxisLine(plot, previousPoint, pointPosition, accentColor, lineThickness);

            CreateGraphPoint(plot, session, pointPosition, accentColor, componentALabel);

            if (i % labelStep == 0 || i == sessions.Count - 1)
            {
                TMP_Text label = CreateText($"XAxis{i}", plot, 16f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Top,
                    session.PracticeDate.ToString("dd", spanishCulture));
                SetRect(label.rectTransform, AnchorBottomLeft, AnchorBottomLeft, AnchorTopCenter, new Vector2(x, bottomPadding - 12f), new Vector2(56f, 24f));
            }

            previousPoint = pointPosition;
            hasPreviousPoint = true;
        }
    }

    /// <summary>
    /// El punto tiene un área de colisión mayor que la visual: en VR el puntero
    /// tiembla y un objetivo del tamaño del círculo sería casi imposible de acertar.
    /// </summary>
    private void CreateGraphPoint(RectTransform plot, SessionViewModel session, Vector2 pointPosition, Color accentColor, string componentALabel)
    {
        float hitSize = pointSize * pointHitSizeMultiplier;
        float visualSize = pointSize * pointVisualSizeMultiplier;

        RectTransform pointRect = CreateRectTransform("Point", plot, new Vector2(hitSize, hitSize));
        SetRect(pointRect, AnchorBottomLeft, AnchorBottomLeft, AnchorCenter, pointPosition, new Vector2(hitSize, hitSize));

        RectTransform pointVisualRect = CreateRectTransform("PointVisual", pointRect, new Vector2(visualSize, visualSize));
        SetRect(pointVisualRect, AnchorCenter, AnchorCenter, Vector2.zero, new Vector2(visualSize, visualSize));

        Image pointImage = pointVisualRect.gameObject.AddComponent<Image>();
        pointImage.sprite = pointSprite;
        pointImage.color = accentColor;
        pointImage.raycastTarget = false;

        Image hitAreaImage = pointRect.gameObject.AddComponent<Image>();
        hitAreaImage.color = new Color(1f, 1f, 1f, 0.001f);
        hitAreaImage.raycastTarget = true;

        EvolutionGraphPoint point = pointRect.gameObject.AddComponent<EvolutionGraphPoint>();
        point.Configure(this, pointRect, BuildPointTitle(session), BuildPointBody(session, componentALabel));
    }

    private string BuildPointTitle(SessionViewModel session)
    {
        string songTitle = !string.IsNullOrWhiteSpace(session.Session?.song_title) ? session.Session.song_title : "Sesión";

        return $"{songTitle} • {session.PracticeDate.ToString("dd/MM HH:mm", spanishCulture)}";
    }

    private static string BuildPointBody(SessionViewModel session, string componentALabel)
    {
        string practiceMode = !string.IsNullOrWhiteSpace(session.Session?.practice_mode)
            ? session.Session.practice_mode
            : session.IsPiano ? "PIANO" : "CANTO";

        return $"{componentALabel}: {session.ComponentA.ToString("F0", CultureInfo.InvariantCulture)}%\n" +
               $"Ritmo: {session.Rhythm.ToString("F0", CultureInfo.InvariantCulture)}%\n" +
               $"Promedio: {session.Score.ToString("F0", CultureInfo.InvariantCulture)}%\n" +
               $"Modo: {practiceMode}";
    }

    private static void ClearContent(RectTransform content)
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            // Se desactiva antes de destruir para que no cuente en el layout de este frame.
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    private ScrollRect FindScrollView(string panelName) =>
        transform.Find(panelName)?.GetComponentInChildren<ScrollRect>(true);

    private static float ResolveCardWidth(ScrollRect scrollView) =>
        Mathf.Max(MinimumCardWidth, ResolveViewportWidth(scrollView) - 36f);

    private static float ResolveViewportWidth(ScrollRect scrollView)
    {
        if (scrollView == null || scrollView.viewport == null) return FallbackViewportWidth;

        return scrollView.viewport.rect.width > 0f ? scrollView.viewport.rect.width : FallbackViewportWidth;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size) =>
        SetRect(rect, anchor, anchor, pivot, anchoredPosition, size);

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private RectTransform CreateRectTransform(string name, Transform parent, Vector2 size)
    {
        GameObject createdObject = new GameObject(name, typeof(RectTransform));
        createdObject.layer = gameObject.layer;

        RectTransform rectTransform = createdObject.GetComponent<RectTransform>();
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
        textComponent.raycastTarget = false;
        return textComponent;
    }

    /// <summary>Dibuja un segmento como un Image rotado. Grosor 0 significa "no dibujar".</summary>
    private void CreateAxisLine(RectTransform parent, Vector2 from, Vector2 to, Color color, float thickness)
    {
        if (thickness <= 0f) return;

        Vector2 direction = to - from;
        float length = direction.magnitude;
        if (length <= 0.001f) return;

        RectTransform lineRect = CreateRectTransform("Line", parent, Vector2.zero);

        Image image = lineRect.gameObject.AddComponent<Image>();
        image.sprite = panelSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;

        SetRect(lineRect, AnchorBottomLeft, AnchorBottomLeft, AnchorMidLeft, from, new Vector2(length, thickness));
        lineRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    private static Sprite BuildCircleSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                pixels[(y * size) + x] = Vector2.Distance(new Vector2(x, y), center) <= radius ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
