using UnityEngine;
using UnityEngine.SceneManagement;

public class UserSession : MonoBehaviour
{
    public static UserSession Instance { get; private set; }
    public const string DefaultAudienceIntensity = "Medio";
    public const string UnregisteredMidiDeviceName = "NO REGISTRADO";

    [Header("Datos del Usuario")]
    public string token, username, email, tessitura;
    public int userId;
    public string midiDeviceName = UnregisteredMidiDeviceName;
    public string audienceIntensity = DefaultAudienceIntensity;

    [Header("Estado")]
    public bool IsLoggedIn;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
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

    public void ApplyConfiguration(UserConfigurationData config)
    {
        if (config != null)
        {
            midiDeviceName = string.IsNullOrWhiteSpace(config.midi_device_name) ? UnregisteredMidiDeviceName : config.midi_device_name;
            audienceIntensity = string.IsNullOrWhiteSpace(config.audience_intensity) ? DefaultAudienceIntensity : config.audience_intensity;
        }
        else
        {
            UpdateMidiDeviceName(midiDeviceName);
            audienceIntensity = string.IsNullOrWhiteSpace(audienceIntensity) ? DefaultAudienceIntensity : audienceIntensity;
        }
    }

    public void UpdateMidiDeviceName(string deviceName) => 
        midiDeviceName = string.IsNullOrWhiteSpace(deviceName) ? UnregisteredMidiDeviceName : deviceName;

    public void Logout()
    {
        token = username = email = tessitura = null;
        userId = 0;
        midiDeviceName = UnregisteredMidiDeviceName;
        audienceIntensity = DefaultAudienceIntensity;
        IsLoggedIn = false;
        SceneManager.LoadScene("LoginScene");
    }
}