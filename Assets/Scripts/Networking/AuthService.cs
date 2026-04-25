using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Text;
using System.Globalization;

public class AuthService : MonoBehaviour
{
    private string RegisterUrl => NetworkConfig.Instance.BaseUrl + "/api/users";
    private string LoginUrl => NetworkConfig.Instance.BaseUrl + "/api/login";
    private string SongsUrl => NetworkConfig.Instance.BaseUrl + "/api/songs/listar";
    private string PracticeSessionsUrl => NetworkConfig.Instance.BaseUrl + "/api/practice-sessions";

    private string GetUserConfigurationUrl(int userId)
    {
        return NetworkConfig.Instance.BaseUrl + $"/api/users/{userId}/configuration";
    }

    public IEnumerator UpdateTessitura(int userId, string tessitura, Action<string> onSuccess, Action<string> onError)
    {
        // Construimos la URL usando tu NetworkConfig
        string url = NetworkConfig.Instance.BaseUrl + $"/api/users/{userId}/tessitura";

        // Creamos el JSON manualmente o con un objeto anónimo
        // Como tu base de datos usa ENUMs, nos aseguramos de enviarlo en MAYÚSCULAS
        string json = "{\"tessitura\":\"" + tessitura.ToUpper() + "\"}";

        // Usamos tu método SendJsonRequest para heredar toda tu lógica de seguridad
        yield return SendJsonRequest(url, "PUT", json, true, onSuccess, onError);
    }

    public IEnumerator Register(UserCreateRequest data, Action<string> onSuccess, Action<string> onError)
    {
        yield return SendJsonRequest(RegisterUrl, "POST", JsonUtility.ToJson(data), false, onSuccess, onError);
    }

    public IEnumerator Login(UserLoginRequest data, Action<string> onSuccess, Action<string> onError)
    {
        yield return SendJsonRequest(LoginUrl, "POST", JsonUtility.ToJson(data), false, onSuccess, onError);
    }

    public IEnumerator GetUserConfiguration(int userId, Action<string> onSuccess, Action<string> onError)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(GetUserConfigurationUrl(userId)))
        {
            ApplyAuthorizationHeader(request);
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(request.downloadHandler.text);
            }
            else
            {
                string errorResponse = request.downloadHandler.text;
                if (string.IsNullOrEmpty(errorResponse)) errorResponse = request.error;
                onError?.Invoke(string.IsNullOrEmpty(errorResponse) ? "Error de conexión" : errorResponse);
            }
        }
    }

    public IEnumerator UpdateUserConfiguration(int userId, UserConfigurationRequest data, Action<string> onSuccess, Action<string> onError)
    {
        yield return SendJsonRequest(GetUserConfigurationUrl(userId), "PUT", JsonUtility.ToJson(data), true, onSuccess, onError);
    }

    public IEnumerator SavePracticeSession(PracticeSessionRequest data, Action<string> onSuccess, Action<string> onError)
    {
        if (data != null)
        {
            Debug.Log($"[SessionAudit] Registrando sesion | user={data.user_id} | song={data.song_id} | mode={data.practice_mode} | datetime={data.practice_datetime}");
        }

        yield return SendJsonRequest(PracticeSessionsUrl, "POST", SerializePracticeSessionRequest(data), true, onSuccess, onError);
    }
    // Método para obtener el historial de sesiones del usuario
    public IEnumerator GetUserHistory(int userId, Action<string> onSuccess, Action<string> onError)
    {
        // Construimos la URL: BaseUrl + /api/practice-sessions/user/{id}
        string url = NetworkConfig.Instance.BaseUrl + $"/api/practice-sessions/user/{userId}";

        Debug.Log($"[SessionAudit] Consultando historial | user={userId}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            // Aplicamos el Token de seguridad que ya tienes implementado
            ApplyAuthorizationHeader(request);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;

                // Si el backend devuelve una lista plana [], la envolvemos para que JsonUtility la entienda
                if (json.StartsWith("["))
                {
                    json = "{\"sessions\":" + json + "}";
                }

                Debug.Log($"[SessionAudit] Historial recuperado correctamente | user={userId}");
                onSuccess?.Invoke(json);
            }
            else
            {
                string errorResponse = request.downloadHandler.text;
                if (string.IsNullOrEmpty(errorResponse)) errorResponse = request.error;
                Debug.LogError($"[SessionAudit] Error consultando historial | user={userId} | detalle={errorResponse}");
                onError?.Invoke(errorResponse);
            }
        }
    }
    public IEnumerator GetSongs(Action<string> onSuccess, Action<string> onError)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(SongsUrl))
        {
            // Enviamos el Token que guardamos en el UserSession
            ApplyAuthorizationHeader(request);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                // Si el JSON es una lista [{},{}], lo envolvemos para el Wrapper
                if (json.StartsWith("[")) json = "{\"songs\":" + json + "}";
                onSuccess?.Invoke(json);
            }
            else
            {
                onError?.Invoke(request.error);
            }
        }
    }

    private IEnumerator SendJsonRequest(string url, string method, string json, bool includeAuth, Action<string> onSuccess, Action<string> onError)
    {
        using (UnityWebRequest request = new UnityWebRequest(url, method))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            if (includeAuth)
            {
                ApplyAuthorizationHeader(request);
            }

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(request.downloadHandler.text);
            }
            else
            {
                string errorResponse = request.downloadHandler.text;
                if (string.IsNullOrEmpty(errorResponse)) errorResponse = request.error;
                onError?.Invoke(string.IsNullOrEmpty(errorResponse) ? "Error de conexión" : errorResponse);
            }
        }
    }

    private void ApplyAuthorizationHeader(UnityWebRequest request)
    {
        if (request == null)
        {
            return;
        }

        if (UserSession.Instance != null && !string.IsNullOrEmpty(UserSession.Instance.token))
        {
            request.SetRequestHeader("Authorization", "Bearer " + UserSession.Instance.token);
        }
    }

    private string SerializePracticeSessionRequest(PracticeSessionRequest data)
    {
        if (data == null)
        {
            return "{}";
        }

        return "{" +
            $"\"user_id\":{data.user_id}," +
            $"\"song_id\":{data.song_id}," +
            $"\"practice_datetime\":\"{EscapeJson(data.practice_datetime)}\"," +
            $"\"practice_mode\":\"{EscapeJson(data.practice_mode)}\"," +
            $"\"rhythm_score\":{SerializeNullableFloat(data.rhythm_score)}," +
            $"\"harmony_score\":{SerializeNullableFloat(data.harmony_score)}," +
            $"\"tuning_score\":{SerializeNullableFloat(data.tuning_score)}" +
            "}";
    }

    private string SerializeNullableFloat(float? value)
    {
        return value.HasValue
            ? value.Value.ToString(CultureInfo.InvariantCulture)
            : "null";
    }

    private string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }


}