using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class AlertManager : MonoBehaviour
{
    public static AlertManager Instance;

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button closeButton;

    private Action onClosedCallback;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (panel == null || titleText == null || messageText == null || closeButton == null)
        {
            Debug.LogError("<color=red>[AlertManager] ¡Faltan referencias en el Inspector!</color>");
            return;
        }

        panel.SetActive(false);
        closeButton.onClick.AddListener(Close);
    }

    public void ShowAlert(string title, string message, bool isSuccess = false, Action onClose = null)
    {
        panel.SetActive(true);
        titleText.text = title;
        messageText.text = message;
        Color c;
        if (isSuccess)
            ColorUtility.TryParseHtmlString("#6B9080", out c);
        else
            ColorUtility.TryParseHtmlString("#906B6D", out c);
        titleText.color = c;
        onClosedCallback = onClose;
    }

    public void ShowApiError(string jsonError, string defaultTitle = "Error")
    {
        try
        {
            if (jsonError.Contains("[{"))
            {
                var valError = JsonUtility.FromJson<HTTPValidationError>(jsonError);
                if (valError?.detail != null && valError.detail.Length > 0)
                {
                    string rawMsg = valError.detail[0].msg;

                    string cleanMsg = rawMsg switch
                    {
                        var m when m.Contains("at least 8 characters") => "La contraseña es muy corta (mínimo 8 caracteres).",
                        var m when m.Contains("at least 5 characters") => "El usuario es muy corto (mínimo 5 caracteres).",
                        var m when m.Contains("not a valid email") => "El correo electrónico no tiene un formato válido.",
                        var m when m.Contains("field required") => "Este campo es obligatorio.",
                        _ => rawMsg // Si no lo conocemos, mostramos el original
                    };

                    ShowAlert("Dato no válido", cleanMsg, false);
                    return;
                }
            }

            var simpleError = JsonUtility.FromJson<FastAPIError>(jsonError);
            if (simpleError != null && !string.IsNullOrEmpty(simpleError.detail))
            {
                ShowAlert(defaultTitle, simpleError.detail, false);
                return;
            }

            ShowAlert(defaultTitle, "Error inesperado en el servidor.", false);
        }
        catch
        {
            ShowAlert("Error Crítico", "No se pudo procesar la respuesta del servidor.", false);
        }
    }

    public void Close()
    {
        panel.SetActive(false);
        onClosedCallback?.Invoke();
        onClosedCallback = null;
    }
}