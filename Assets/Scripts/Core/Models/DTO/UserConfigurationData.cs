using System;

[Serializable]
public class UserConfigurationData
{
    public int id;
    public int user_id;
    public string midi_device_name;
    public string audience_intensity;
}

[Serializable]
public class UserConfigurationRequest
{
    public string midi_device_name;
    public string audience_intensity;
}