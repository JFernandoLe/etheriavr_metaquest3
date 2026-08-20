using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UserSession : MonoBehaviour
{
    public static UserSession Instance { get; private set; }
    public const string DefaultAudienceIntensity = "Medio";
    public const string UnregisteredMidiDeviceName = "NO REGISTRADO";
    private const string SessionPrefsKey = "etheriavr_user_session";

    [Header("Datos del Usuario")]
    public string token, username, email, tessitura;
    public int userId;
    public string midiDeviceName = UnregisteredMidiDeviceName;
    public string audienceIntensity = DefaultAudienceIntensity;

    [Header("Estado")]
    public bool IsLoggedIn;

    [Serializable]
    private class PersistedSessionData
    {
        public string token;
        public int userId;
        public string username;
        public string email;
        public string tessitura;
        public string midiDeviceName;
        public string audienceIntensity;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        TryRestoreFromPrefs();
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
        PersistSession();

        Debug.Log("<color=green>[UserSession] Sesión iniciada.</color>");
    }

    public void PersistSession()
    {
        if (!IsLoggedIn || string.IsNullOrEmpty(token)) return;

        var data = new PersistedSessionData
        {
            token = token,
            userId = userId,
            username = username,
            email = email,
            tessitura = tessitura,
            midiDeviceName = midiDeviceName,
            audienceIntensity = audienceIntensity
        };

        PlayerPrefs.SetString(SessionPrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    private void TryRestoreFromPrefs()
    {
        if (!PlayerPrefs.HasKey(SessionPrefsKey)) return;

        string json = PlayerPrefs.GetString(SessionPrefsKey);
        if (string.IsNullOrEmpty(json)) return;

        PersistedSessionData data = JsonUtility.FromJson<PersistedSessionData>(json);
        if (data == null || string.IsNullOrEmpty(data.token)) return;

        token = data.token;
        userId = data.userId;
        username = data.username;
        email = data.email;
        tessitura = data.tessitura;
        midiDeviceName = string.IsNullOrWhiteSpace(data.midiDeviceName) ? UnregisteredMidiDeviceName : data.midiDeviceName;
        audienceIntensity = string.IsNullOrWhiteSpace(data.audienceIntensity) ? DefaultAudienceIntensity : data.audienceIntensity;
        IsLoggedIn = true;

        Debug.Log("<color=green>[UserSession] Sesión restaurada desde almacenamiento local.</color>");
    }

    public void ClearSession()
    {
        token = username = email = tessitura = null;
        userId = 0;
        midiDeviceName = UnregisteredMidiDeviceName;
        audienceIntensity = DefaultAudienceIntensity;
        IsLoggedIn = false;

        if (PlayerPrefs.HasKey(SessionPrefsKey))
        {
            PlayerPrefs.DeleteKey(SessionPrefsKey);
            PlayerPrefs.Save();
        }
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

    public void UpdateMidiDeviceName(string deviceName)
    {
        midiDeviceName = string.IsNullOrWhiteSpace(deviceName) ? UnregisteredMidiDeviceName : deviceName;
        PersistSession();
    }

    public void Logout()
    {
        ClearSession();
        SceneManager.LoadScene("LoginScene");
    }
}