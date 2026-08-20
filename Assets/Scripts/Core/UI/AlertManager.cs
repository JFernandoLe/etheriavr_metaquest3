using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class AlertManager : MonoBehaviour
{
    public static AlertManager Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText, messageText;
    [SerializeField] private Button closeButton;

    private Action onClosedCallback;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!panel || !titleText || !messageText || !closeButton)
            Debug.LogError("<color=red>[AlertManager] ¡Faltan referencias en el Inspector!</color>");

        panel?.SetActive(false);
        closeButton?.onClick.AddListener(Close);
    }

    public void ShowAlert(string title, string message, bool isSuccess = false, Action onClose = null)
    {
        panel.SetActive(true);
        titleText.text = title;
        messageText.text = message;
        
        ColorUtility.TryParseHtmlString(isSuccess ? "#6B9080" : "#906B6D", out Color c);
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
                    string m = valError.detail[0].msg;
                    string cleanMsg = m.Contains("at least 8") ? "La contraseña es muy corta (mínimo 8 caracteres)." :
                                      m.Contains("at least 5") ? "El usuario es muy corto (mínimo 5 caracteres)." :
                                      m.Contains("valid email") ? "El correo electrónico no tiene un formato válido." :
                                      m.Contains("field required") ? "Este campo es obligatorio." : m;

                    ShowAlert("Dato no válido", cleanMsg, false);
                    return;
                }
            }

            var simpleError = JsonUtility.FromJson<FastAPIError>(jsonError);
            ShowAlert(defaultTitle, !string.IsNullOrEmpty(simpleError?.detail) ? simpleError.detail : "Error inesperado en el servidor.", false);
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