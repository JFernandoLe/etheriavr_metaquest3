using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Diálogo de confirmación para eliminar canciones personalizadas.
/// </summary>
public static class CustomSongConfirmDialog
{
    public static void Show(string songTitle, Action onConfirm, Action onCancel = null)
    {
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            onConfirm?.Invoke();
            return;
        }

        Button template = FindButtonTemplate();
        TMP_FontAsset font = FindDialogFont(template);

        var overlay = new GameObject("CustomSongConfirmDialog", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();
        StretchFull(overlay.GetComponent<RectTransform>());
        overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
        overlay.GetComponent<Image>().raycastTarget = true;

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(Image));
        panel.transform.SetParent(overlay.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(900f, 520f);
        panel.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.14f, 0.98f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 20f;
        layout.padding = new RectOffset(36, 36, 36, 36);
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;

        CreateLabel(panel.transform, font, $"¿Eliminar \"{songTitle}\"?", 30f);
        CreateLabel(panel.transform, font, "Se borrará el audio, las notas y la metadata.", 22f);

        var buttonsRow = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttonsRow.transform.SetParent(panel.transform, false);
        HorizontalLayoutGroup rowLayout = buttonsRow.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 24f;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = true;
        rowLayout.childForceExpandWidth = true;

        LayoutElement rowLayoutElement = buttonsRow.AddComponent<LayoutElement>();
        rowLayoutElement.minHeight = 140f;
        rowLayoutElement.preferredHeight = 150f;

        CreateDialogButton(buttonsRow.transform, template, font, "Cancelar", () =>
        {
            onCancel?.Invoke();
            UnityEngine.Object.Destroy(overlay);
        });

        CreateDialogButton(buttonsRow.transform, template, font, "Eliminar", () =>
        {
            onConfirm?.Invoke();
            UnityEngine.Object.Destroy(overlay);
        });
    }

    private static Button FindButtonTemplate()
    {
        foreach (string objectName in new[] { "botonVolver", "btn_JugarReal", "presentation_button", "configuration_button" })
        {
            Button button = GameObject.Find(objectName)?.GetComponent<Button>();
            if (button != null)
                return button;
        }

        return UnityEngine.Object.FindFirstObjectByType<Button>();
    }

    private static TMP_FontAsset FindDialogFont(Button template)
    {
        if (template != null)
        {
            TMP_Text sample = template.GetComponentInChildren<TMP_Text>(true);
            if (sample != null && sample.font != null)
                return sample.font;
        }

        TMP_Text anyText = UnityEngine.Object.FindFirstObjectByType<TMP_Text>();
        return anyText != null ? anyText.font : null;
    }

    private static void CreateLabel(Transform parent, TMP_FontAsset font, string text, float size)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (font != null)
            tmp.font = font;

        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.minHeight = 56f;
    }

    private static void CreateDialogButton(Transform parent, Button template, TMP_FontAsset font, string label, Action onClick)
    {
        Button button;
        if (template != null)
        {
            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, parent);
            clone.name = "Btn_" + label;
            button = clone.GetComponent<Button>();

            TMP_Text[] texts = clone.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
                texts[i].text = i == 0 ? label : string.Empty;
        }
        else
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            button = go.GetComponent<Button>();
            go.GetComponent<Image>().color = new Color(0.38f, 0.27f, 0.19f, 1f);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            TMP_Text tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 26f;
            tmp.color = Color.white;
            if (font != null)
                tmp.font = font;
            StretchFull(textGo.GetComponent<RectTransform>());
        }

        LayoutElement layout = button.gameObject.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 120f;
        layout.preferredHeight = 130f;
        layout.flexibleWidth = 1f;
        layout.minWidth = 180f;

        HomeMenuAddSongsBootstrap.SetButtonClick(button, onClick);
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
