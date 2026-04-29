using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using UnityEngine.SceneManagement;

public class Login : MonoBehaviour
{
    [Header("UI de Inicio de Sesión")]
    [SerializeField] private TMP_InputField emailField;
    [SerializeField] private TMP_InputField passwordField;
    [SerializeField] private Button loginButton;

    [Header("Servicios")]
    [SerializeField] private AuthService authService;

    private DirectMidiReceiver midiReceiver;

    private void Start()
    {
        midiReceiver = FindObjectOfType<DirectMidiReceiver>();

        if (loginButton != null)
            loginButton.onClick.AddListener(OnLoginClicked);
    }

    private void OnLoginClicked()
    {
        string email = emailField.text.Trim();
        string password = passwordField.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            AlertManager.Instance.ShowAlert("Campos vacíos", "Por favor ingresa tus credenciales.", false);
            return;
        }

        loginButton.interactable = false;

        UserLoginRequest loginData = new UserLoginRequest
        {
            email = email,
            password = password,
            midi_device_name = ResolveConnectedMidiDeviceName()
        };

        StartCoroutine(authService.Login(loginData,
            onSuccess: (jsonResponse) =>
            {
                Debug.Log("<color=green>>>> Login Exitoso!</color>");

                UserLoginResponse res = JsonUtility.FromJson<UserLoginResponse>(jsonResponse);

                if (UserSession.Instance != null)
                {
                    UserSession.Instance.SetSession(res);
                }
                else
                {
                    Debug.LogWarning("<color=orange>[Login] No se encontró UserSession.Instance en la escena.</color>");
                }

                string connectedMidiDeviceName = ResolveConnectedMidiDeviceName();
                if (!string.IsNullOrWhiteSpace(connectedMidiDeviceName) && UserSession.Instance != null)
                {
                    UserConfigurationRequest configurationRequest = new UserConfigurationRequest
                    {
                        midi_device_name = connectedMidiDeviceName,
                        audience_intensity = string.IsNullOrWhiteSpace(UserSession.Instance.audienceIntensity)
                            ? UserSession.DefaultAudienceIntensity
                            : UserSession.Instance.audienceIntensity
                    };

                    StartCoroutine(authService.UpdateUserConfiguration(
                        UserSession.Instance.userId,
                        configurationRequest,
                        onSuccess: (_) =>
                        {
                            UserSession.Instance.UpdateMidiDeviceName(connectedMidiDeviceName);
                            ShowLoginSuccess(res.username);
                        },
                        onError: (configError) =>
                        {
                            Debug.LogWarning($"[Login] No se pudo sincronizar la configuración MIDI tras login: {configError}");
                            ShowLoginSuccess(res.username);
                        }
                    ));
                    return;
                }

                ShowLoginSuccess(res.username);
            },
            onError: (errorJson) =>
            {
                AlertManager.Instance.ShowApiError(errorJson, "Login Fallido");

                loginButton.interactable = true;
            }
        ));
    }

    private string ResolveConnectedMidiDeviceName()
    {
        if (!MidiInitializer.ShouldEnableMidiForScene(SceneManager.GetActiveScene().name))
        {
            return null;
        }

        if (midiReceiver == null)
        {
            midiReceiver = FindObjectOfType<DirectMidiReceiver>();
        }

        if (midiReceiver != null && midiReceiver.TryGetConnectedDeviceName(out string deviceName))
        {
            return deviceName;
        }

        return null;
    }

    private void ShowLoginSuccess(string username)
    {
        AlertManager.Instance.ShowAlert(
            "¡Bienvenido!",
            $"Hola de nuevo, {username}.",
            true,
            onClose: () =>
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("HomeScene");
            }
        );
    }
}