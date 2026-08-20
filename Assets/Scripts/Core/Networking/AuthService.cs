using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Text;
using System.Globalization;

public class AuthService : MonoBehaviour
{
    private string BaseUrl => NetworkConfig.Instance.BaseUrl;
    private string RegisterUrl => $"{BaseUrl}/api/users";
    private string LoginUrl => $"{BaseUrl}/api/login";
    private string SongsUrl => $"{BaseUrl}/api/songs/listar";
    private string PracticeSessionsUrl => $"{BaseUrl}/api/practice-sessions";

    public IEnumerator UpdateTessitura(int userId, string tessitura, Action<string> onSuccess, Action<string> onError) =>
        SendJsonRequest($"{BaseUrl}/api/users/{userId}/tessitura", "PUT", $"{{\"tessitura\":\"{tessitura.ToUpper()}\"}}", true, onSuccess, onError);

    public IEnumerator Register(UserCreateRequest data, Action<string> onSuccess, Action<string> onError) =>
        SendJsonRequest(RegisterUrl, "POST", JsonUtility.ToJson(data), false, onSuccess, onError);

    public IEnumerator Login(UserLoginRequest data, Action<string> onSuccess, Action<string> onError) =>
        SendJsonRequest(LoginUrl, "POST", JsonUtility.ToJson(data), false, onSuccess, onError);

    public IEnumerator GetUserConfiguration(int userId, Action<string> onSuccess, Action<string> onError) =>
        SendGetRequest($"{BaseUrl}/api/users/{userId}/configuration", onSuccess, onError);

    public IEnumerator UpdateUserConfiguration(int userId, UserConfigurationRequest data, Action<string> onSuccess, Action<string> onError) =>
        SendJsonRequest($"{BaseUrl}/api/users/{userId}/configuration", "PUT", JsonUtility.ToJson(data), true, onSuccess, onError);

    public IEnumerator SavePracticeSession(PracticeSessionRequest data, Action<string> onSuccess, Action<string> onError) =>
        SendJsonRequest(PracticeSessionsUrl, "POST", SerializePracticeSessionRequest(data), true, onSuccess, onError);

    public IEnumerator GetUserHistory(int userId, Action<string> onSuccess, Action<string> onError) =>
        SendGetRequest($"{BaseUrl}/api/practice-sessions/user/{userId}", onSuccess, onError, "sessions");

    public IEnumerator GetSongs(Action<string> onSuccess, Action<string> onError) =>
        SendGetRequest(SongsUrl, onSuccess, onError, "songs");


    private IEnumerator SendGetRequest(string url, Action<string> onSuccess, Action<string> onError, string wrapKey = null)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            ApplyAuthorizationHeader(request);
            request.timeout = 10;
            yield return request.SendWebRequest();
            BackendConnectionManager.ReportRequestResult(request);

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                if (!string.IsNullOrEmpty(wrapKey) && json.StartsWith("[")) json = $"{{\"{wrapKey}\":{json}}}";
                onSuccess?.Invoke(json);
            }
            else
            {
                string err = string.IsNullOrEmpty(request.downloadHandler.text) ? request.error : request.downloadHandler.text;
                onError?.Invoke(string.IsNullOrEmpty(err) ? "Error de conexión" : err);
            }
        }
    }

    private IEnumerator SendJsonRequest(string url, string method, string json, bool includeAuth, Action<string> onSuccess, Action<string> onError)
    {
        using (UnityWebRequest request = new UnityWebRequest(url, method))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            if (includeAuth) ApplyAuthorizationHeader(request);

            yield return request.SendWebRequest();
            BackendConnectionManager.ReportRequestResult(request);

            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(request.downloadHandler.text);
            }
            else
            {
                string err = string.IsNullOrEmpty(request.downloadHandler.text) ? request.error : request.downloadHandler.text;
                onError?.Invoke(string.IsNullOrEmpty(err) ? "Error de conexión" : err);
            }
        }
    }

    private void ApplyAuthorizationHeader(UnityWebRequest request)
    {
        if (request != null && UserSession.Instance != null && !string.IsNullOrEmpty(UserSession.Instance.token))
            request.SetRequestHeader("Authorization", $"Bearer {UserSession.Instance.token}");
    }

    private string SerializePracticeSessionRequest(PracticeSessionRequest data) => data == null ? "{}" :
        $"{{\"user_id\":{data.user_id},\"song_id\":{data.song_id},\"practice_datetime\":\"{EscapeJson(data.practice_datetime)}\",\"practice_mode\":\"{EscapeJson(data.practice_mode)}\",\"rhythm_score\":{SerializeNullableFloat(data.rhythm_score)},\"harmony_score\":{SerializeNullableFloat(data.harmony_score)},\"tuning_score\":{SerializeNullableFloat(data.tuning_score)}}}";

    private string SerializeNullableFloat(float? value) => 
        value?.ToString(CultureInfo.InvariantCulture) ?? "null";

    private string EscapeJson(string value) => 
        string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}