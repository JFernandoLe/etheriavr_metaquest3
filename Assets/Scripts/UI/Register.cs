using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class Registrar : MonoBehaviour
{
    [Header("UI de Registro")]
    [SerializeField] private TMP_InputField usernameField;
    [SerializeField] private TMP_InputField emailField;
    [SerializeField] private TMP_InputField passwordField;
    [SerializeField] private TMP_InputField confirmPasswordField;
    [SerializeField] private Button registerButton;

    [Header("Servicios")]
    [SerializeField] private AuthService authService;

    private DirectMidiReceiver midiReceiver;

    private void Start()
    {
        midiReceiver = FindObjectOfType<DirectMidiReceiver>();

        if (registerButton != null)
            registerButton.onClick.AddListener(OnRegisterClicked);
    }

    private void OnRegisterClicked()
    {
        if (passwordField.text != confirmPasswordField.text)
        {
            AlertManager.Instance.ShowAlert("Error", "Las contraseñas no coinciden.", false);
            return;
        }

        if (string.IsNullOrEmpty(usernameField.text) || string.IsNullOrEmpty(emailField.text))
        {
            AlertManager.Instance.ShowAlert("Campos vacíos", "Por favor llena todos los datos.", false);
            return;
        }

        registerButton.interactable = false;

        UserCreateRequest requestData = new UserCreateRequest
        {
            username = usernameField.text,
            email = emailField.text,
            password = passwordField.text,
            confirm_password = confirmPasswordField.text,
            midi_device_name = ResolveRegistrationMidiDeviceName(),
            audience_intensity = UserSession.DefaultAudienceIntensity
        };

        StartCoroutine(authService.Register(requestData,
            onSuccess: (jsonResponse) =>
            {
                AlertManager.Instance.ShowAlert(
                    "¡Éxito!",
                    "Tu cuenta ha sido creada.",
                    true,
                    onClose: () => UnityEngine.SceneManagement.SceneManager.LoadScene("LoginScene")
                );
            },
            onError: (errorJson) =>
            {
                AlertManager.Instance.ShowApiError(errorJson, "Registro Fallido");
                registerButton.interactable = true;
            }
        ));
    }

    private string ResolveRegistrationMidiDeviceName()
    {
        if (midiReceiver == null)
        {
            midiReceiver = FindObjectOfType<DirectMidiReceiver>();
        }

        return midiReceiver != null
            ? midiReceiver.GetRegistrationDeviceName()
            : UserSession.UnregisteredMidiDeviceName;
    }
}