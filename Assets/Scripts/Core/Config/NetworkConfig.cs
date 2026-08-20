using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Fuente única de la URL del backend.
/// Prioridad: StreamingAssets/.env → (opcional) descubrimiento UDP en LAN.
/// </summary>
[CreateAssetMenu(fileName = "NetworkConfig", menuName = "EtheriaVR/NetworkConfig")]
public class NetworkConfig : ScriptableObject
{
    private const int UdpDiscoveryPort = 8888;
    private const int UdpDiscoveryTimeoutMs = 3000;
    private const string UdpSearchMessage = "ETHERIA_SEARCH";
    private const string UdpReplyPrefix = "ETHERIA_SERVER_HERE";

    private static NetworkConfig _instance;

    public static NetworkConfig Instance
    {
        get
        {
            if (_instance) return _instance;
            _instance = Resources.Load<NetworkConfig>("NetworkConfig") ?? CreateInstance<NetworkConfig>();
            _instance.Initialize();
            return _instance;
        }
    }

    [Header("Conexión al servidor")]
    public string ipAddress = "";
    public string port = "";
    public bool useHttps;

    [Header("Health check")]
    public string healthPath = "/health";

    [Header("Descubrimiento LAN (legacy)")]
    public bool enableUdpDiscovery;

    private bool isInitializing;
    private Task<bool> currentEnsureTask;

    public bool IsDiscoveryInProgress { get; private set; }
    public bool HasConfiguredServer => !string.IsNullOrEmpty(ipAddress) && !string.IsNullOrEmpty(port);

    public string BaseUrl => !HasConfiguredServer
        ? string.Empty
        : $"{(useHttps ? "https" : "http")}://{ipAddress}:{port}";

    public string HealthUrl
    {
        get
        {
            if (!HasConfiguredServer) return string.Empty;

            string path = string.IsNullOrWhiteSpace(healthPath) ? "/health" : healthPath.Trim();
            if (!path.StartsWith("/")) path = "/" + path;
            return BaseUrl + path;
        }
    }

    private void Initialize()
    {
        if (isInitializing) return;
        isInitializing = true;

        LoadFromEnv();
        _ = EnsureReadyAsync();
    }

    /// <summary>
    /// Garantiza una BaseUrl usable. Con .env configurado retorna de inmediato.
    /// Solo hace broadcast UDP si ENABLE_UDP_DISCOVERY=true.
    /// </summary>
    public Task<bool> EnsureReadyAsync() =>
        currentEnsureTask?.IsCompleted == false
            ? currentEnsureTask
            : (currentEnsureTask = EnsureReadyInternalAsync());

    /// <summary>Alias legacy usado por BackendConnectionManager.</summary>
    public Task<bool> DiscoverServerAsync() => EnsureReadyAsync();

    private async Task<bool> EnsureReadyInternalAsync()
    {
        if (!HasConfiguredServer) LoadFromEnv();

        if (HasConfiguredServer && !enableUdpDiscovery)
        {
            Debug.Log($"[NetworkConfig] Backend: {BaseUrl}");
            return true;
        }

        if (!enableUdpDiscovery)
        {
            Debug.LogWarning("[NetworkConfig] Sin SERVER_IP/SERVER_PORT en .env y UDP desactivado.");
            return false;
        }

        IsDiscoveryInProgress = true;
        Debug.Log("[NetworkConfig] Buscando backend en la red local (UDP)...");

        try
        {
            using (var udp = new UdpClient { EnableBroadcast = true })
            {
                byte[] bytes = Encoding.UTF8.GetBytes(UdpSearchMessage);
                await udp.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, UdpDiscoveryPort));

                Task<UdpReceiveResult> receiveTask = udp.ReceiveAsync();
                if (await Task.WhenAny(receiveTask, Task.Delay(UdpDiscoveryTimeoutMs)) == receiveTask)
                {
                    string[] parts = Encoding.UTF8.GetString(receiveTask.Result.Buffer).Split(':');
                    if (parts.Length == 3 && parts[0] == UdpReplyPrefix)
                    {
                        ipAddress = parts[1];
                        port = parts[2];
                        Debug.Log($"[NetworkConfig] Backend autodetectado: {BaseUrl}");
                        return true;
                    }
                }
                else if (HasConfiguredServer)
                {
                    Debug.LogWarning($"[NetworkConfig] UDP sin respuesta. Usando .env: {BaseUrl}");
                }
                else
                {
                    Debug.LogWarning("[NetworkConfig] UDP sin respuesta y .env sin host.");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkConfig] Error en descubrimiento UDP: {e.Message}");
        }
        finally
        {
            IsDiscoveryInProgress = false;
        }

        return HasConfiguredServer;
    }

    public void LoadFromEnv()
    {
        try
        {
            var env = EnvLoader.Load();
            if (env == null || env.Count == 0) return;

            if (env.TryGetValue("SERVER_IP", out string ip) && !string.IsNullOrWhiteSpace(ip))
                ipAddress = ip.Trim();

            if (env.TryGetValue("SERVER_PORT", out string envPort) && !string.IsNullOrWhiteSpace(envPort))
                port = envPort.Trim();

            if (env.TryGetValue("USE_HTTPS", out string https) && bool.TryParse(https, out bool parsedHttps))
                useHttps = parsedHttps;

            if (env.TryGetValue("HEALTH_PATH", out string path) && !string.IsNullOrWhiteSpace(path))
                healthPath = path.Trim();

            enableUdpDiscovery = EnvLoader.GetBool("ENABLE_UDP_DISCOVERY", false);

            if (HasConfiguredServer)
                Debug.Log($"[NetworkConfig] .env cargado → {BaseUrl}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkConfig] Fallo al leer .env: {e.Message}");
        }
    }
}
