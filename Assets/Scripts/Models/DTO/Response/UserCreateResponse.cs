using System;
[Serializable]
public class UserCreateResponse
{
    public int id;
    public string username;
    public string email;
    public string tessitura;
    public UserConfigurationData configuration;
    public UserConfigurationData user_configuration;
}