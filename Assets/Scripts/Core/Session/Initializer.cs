using UnityEngine;

/// <summary>
/// Arranca la configuración de red al inicio de la app.
/// </summary>
public class Initializer : MonoBehaviour
{
    private void Awake()
    {
        // Acceder a Instance dispara la carga del .env (y UDP solo si está habilitado).
        string baseUrl = NetworkConfig.Instance.BaseUrl;
        Debug.Log(string.IsNullOrEmpty(baseUrl)
            ? "[App] NetworkConfig sin BaseUrl aún."
            : $"[App] Backend configurado: {baseUrl}");
    }
}
