using UnityEngine;

public class UserSession : MonoBehaviour
{
    public static UserSession Instance;
    public const string DefaultAudienceIntensity = "Medio";
    public const string UnregisteredMidiDeviceName = "NO REGISTRADO";

    [Header("Datos del Usuario")]
    public string token;
    public int userId;
    public string username;
    public string email;
    public string tessitura;
    public string midiDeviceName = UnregisteredMidiDeviceName;
    public string audienceIntensity = DefaultAudienceIntensity;

    [Header("Estado")]
    public bool IsLoggedIn = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public void SetSession(UserLoginResponse data)
    {
        token = data.access_token;
        userId = data.id;
        username = data.username;
        email = data.email;
        tessitura = data.tessitura;
        ApplyConfiguration(data.configuration ?? data.user_configuration);
        IsLoggedIn = true;

        Debug.Log("<color=green>[UserSession] Sesión iniciada.</color>");
    }

    public void ApplyConfiguration(UserConfigurationData configuration)
    {
        if (configuration == null)
        {
            if (string.IsNullOrWhiteSpace(audienceIntensity))
            {
                audienceIntensity = DefaultAudienceIntensity;
            }

            if (string.IsNullOrWhiteSpace(midiDeviceName))
            {
                midiDeviceName = UnregisteredMidiDeviceName;
            }

            return;
        }

        midiDeviceName = string.IsNullOrWhiteSpace(configuration.midi_device_name)
            ? UnregisteredMidiDeviceName
            : configuration.midi_device_name;

        audienceIntensity = string.IsNullOrWhiteSpace(configuration.audience_intensity)
            ? DefaultAudienceIntensity
            : configuration.audience_intensity;
    }

    public void UpdateMidiDeviceName(string deviceName)
    {
        midiDeviceName = string.IsNullOrWhiteSpace(deviceName)
            ? UnregisteredMidiDeviceName
            : deviceName;
    }

    public void Logout()
    {
        token = null;
        userId = 0;
        username = null;
        email = null;
        tessitura = null;
        midiDeviceName = UnregisteredMidiDeviceName;
        audienceIntensity = DefaultAudienceIntensity;
        IsLoggedIn = false;
        UnityEngine.SceneManagement.SceneManager.LoadScene("LoginScene");
    }
}