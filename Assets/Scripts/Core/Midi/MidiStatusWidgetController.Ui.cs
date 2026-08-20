using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Construcción por código de la UI del widget MIDI. Se genera en runtime para que
/// el widget funcione en cualquier escena sin necesidad de cablearlo en el inspector.
/// </summary>
public partial class MidiStatusWidgetController
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

    // Se prueban en orden porque el nombre del recurso interno cambia entre versiones de Unity.
    private static readonly string[] BackgroundSpriteCandidates =
    {
        "UI/Skin/Background.psd", "Background.psd", "UI/Skin/UISprite.psd", "UISprite.psd"
    };

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
    private Text continueButtonText;

    private void BuildUi()
    {
        BuildCanvas();
        BuildBadge();
        BuildInfoPanel();
    }

    private void BuildCanvas()
    {
        GameObject canvasObject = CreateUiObject("Canvas", transform,
            typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));

        canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1840f, 1080f);
        canvasRect.localScale = Vector3.one * 0.001f;

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.planeDistance = 1f;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;

        trackedDeviceRaycaster = canvasObject.GetComponent<TrackedDeviceGraphicRaycaster>();
        canvasObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
    }

    private void BuildBadge()
    {
        GameObject badgeObject = CreateUiObject("MidiBadge", canvasRect, typeof(Image), typeof(Button));

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

        AddOutline(badgeObject, Color.white, new Vector2(4f, -4f));

        badgeLabel = CreateText(badgeRect, "MidiLabel", new Vector2(120f, 38f), new Vector2(0f, 24f), 28, FontStyle.Bold, TextAnchor.MiddleCenter, ButtonTextColor);
        badgeLabel.text = "MIDI";

        badgeGlyph = CreateText(badgeRect, "MidiGlyph", new Vector2(90f, 52f), new Vector2(0f, -34f), 44, FontStyle.Bold, TextAnchor.MiddleCenter, ButtonTextColor);

        badgeButton.onClick.AddListener(TogglePanelVisibility);
    }

    private void BuildInfoPanel()
    {
        GameObject panelObject = CreateUiObject("MidiInfoPanel", canvasRect, typeof(Image));

        infoPanelRect = panelObject.GetComponent<RectTransform>();
        infoPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        infoPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        infoPanelRect.pivot = new Vector2(0.5f, 0.5f);
        infoPanelRect.sizeDelta = new Vector2(680f, 470f);
        infoPanelRect.anchoredPosition = new Vector2(0f, 54f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = PanelColor;
        ApplyBackgroundSprite(panelImage);

        AddOutline(panelObject, PanelOutlineColor, new Vector2(2f, -2f));

        titleText = CreateText(infoPanelRect, "Title", new Vector2(560f, 42f), new Vector2(0f, 170f), 30, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        titleText.text = "Información MIDI";

        statusText = CreateText(infoPanelRect, "StatusText", new Vector2(560f, 44f), new Vector2(0f, 104f), 27, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        deviceText = CreateText(infoPanelRect, "DeviceText", new Vector2(560f, 76f), new Vector2(0f, 24f), 22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        registeredDeviceText = CreateText(infoPanelRect, "RegisteredDeviceText", new Vector2(560f, 72f), new Vector2(0f, -62f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, SecondaryTextColor);
        helperText = CreateText(infoPanelRect, "HelperText", new Vector2(580f, 88f), new Vector2(0f, -152f), 20, FontStyle.Italic, TextAnchor.MiddleCenter, SecondaryTextColor);

        reconnectButton = CreateButton(infoPanelRect, "ReconnectButton", new Vector2(170f, 52f), new Vector2(-185f, -206f), "Buscar MIDI", ButtonColor, out reconnectButtonText);
        disconnectButton = CreateButton(infoPanelRect, "DisconnectButton", new Vector2(170f, 52f), new Vector2(0f, -206f), "Desconectar", ButtonColor, out _);
        closeButton = CreateButton(infoPanelRect, "CloseButton", new Vector2(170f, 52f), new Vector2(185f, -206f), "Cerrar", ButtonColor, out _);
        continueButton = CreateButton(infoPanelRect, "ContinueButton", new Vector2(220f, 56f), new Vector2(0f, -206f), "Continuar juego", ConnectedColor, out continueButtonText);

        reconnectButton.onClick.AddListener(HandleReconnectClicked);
        disconnectButton.onClick.AddListener(HandleDisconnectClicked);
        closeButton.onClick.AddListener(HideInfoPanel);
        continueButton.onClick.AddListener(HandleContinueClicked);

        infoPanelRect.gameObject.SetActive(false);
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

    private static void AddOutline(GameObject target, Color effectColor, Vector2 effectDistance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = effectColor;
        outline.effectDistance = effectDistance;
    }

    private Button CreateButton(Transform parent, string name, Vector2 size, Vector2 anchoredPosition,
        string label, Color backgroundColor, out Text labelText)
    {
        GameObject buttonObject = CreateUiObject(name, parent, typeof(Image), typeof(Button));

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        Image image = buttonObject.GetComponent<Image>();
        image.color = backgroundColor;
        ApplyBackgroundSprite(image);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = backgroundColor * 1.08f;
        colors.pressedColor = backgroundColor * 0.92f;
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = ButtonDisabledColor;
        button.colors = colors;

        AddOutline(buttonObject, new Color(0f, 0f, 0f, 0.28f), new Vector2(2f, -2f));

        labelText = CreateText(rectTransform, name + "Label", size - new Vector2(24f, 12f), Vector2.zero, 20, FontStyle.Bold, TextAnchor.MiddleCenter, ButtonTextColor);
        labelText.text = label;

        return button;
    }

    private static Text CreateText(Transform parent, string name, Vector2 size, Vector2 anchoredPosition,
        int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
    {
        GameObject textObject = CreateUiObject(name, parent, typeof(Text));

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

    private static void ApplyBackgroundSprite(Image image)
    {
        foreach (string candidate in BackgroundSpriteCandidates)
        {
            Sprite sprite = Resources.GetBuiltinResource<Sprite>(candidate);
            if (sprite == null) continue;

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            return;
        }
    }

    private static Sprite CreateCircleSprite(int size)
    {
        int safeSize = Mathf.Max(32, size);
        Texture2D texture = new Texture2D(safeSize, safeSize, TextureFormat.ARGB32, false)
        {
            wrapMode = TextureWrapMode.Clamp
        };

        float radius = (safeSize - 2f) * 0.5f;
        Vector2 center = new Vector2(radius, radius);
        Color[] pixels = new Color[safeSize * safeSize];

        for (int y = 0; y < safeSize; y++)
        {
            for (int x = 0; x < safeSize; x++)
            {
                pixels[y * safeSize + x] = Vector2.Distance(new Vector2(x, y), center) <= radius
                    ? Color.white
                    : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, safeSize, safeSize), new Vector2(0.5f, 0.5f), safeSize);
    }
}
