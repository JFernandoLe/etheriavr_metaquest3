using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System;

[CreateAssetMenu(fileName = "NetworkConfig", menuName = "EtheriaVR/NetworkConfig")]
public class NetworkConfig : ScriptableObject
{
    private static NetworkConfig _instance;
    public static NetworkConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<NetworkConfig>("NetworkConfig");
                if (_instance == null)
                {
                    _instance = CreateInstance<NetworkConfig>();
                }
                _instance.Initialize();
            }
            return _instance;
        }
    }

    [Header("Conexión al Servidor (Se llena solo)")]
    public string ipAddress = "";
    public string port = "";
    public bool useHttps = false;

    public string BaseUrl
    {
        get
        {
            if (string.IsNullOrEmpty(ipAddress) || string.IsNullOrEmpty(port)) return "";
            return $"{(useHttps ? "https" : "http")}://{ipAddress}:{port}";
        }
    }

    private bool _isInitializing = false;

    private void Initialize()
    {
        if (_isInitializing) return;
        _isInitializing = true;

        // 1. Cargamos el .env por si el broadcast falla (Backup)
        LoadFromEnv();

        // 2. Intentamos buscar el servidor real en la red actual
        _ = DiscoverServerAsync();
    }

    public async Task DiscoverServerAsync()
    {
        int discoveryPort = 8888; // Puerto de escucha UDP
        string magicWord = "ETHERIA_SEARCH";
        
        Debug.Log("<color=cyan>[NetworkConfig]</color> 📡 Buscando servidor EtheriaVR en la red local...");

        using (UdpClient udpClient = new UdpClient())
        {
            udpClient.EnableBroadcast = true;
            byte[] sendBytes = Encoding.UTF8.GetBytes(magicWord);
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, discoveryPort);

            try 
            {
                // Enviamos el mensaje a toda la red
                await udpClient.SendAsync(sendBytes, sendBytes.Length, endPoint);
                
                // Esperamos respuesta 3 segundos máximo
                var receiveTask = udpClient.ReceiveAsync();
                if (await Task.WhenAny(receiveTask, Task.Delay(3000)) == receiveTask)
                {
                    var result = receiveTask.Result;
                    string returnData = Encoding.UTF8.GetString(result.Buffer);

                    // Esperamos algo como: ETHERIA_SERVER_HERE:<ip>:<puerto>
                    if (returnData.StartsWith("ETHERIA_SERVER_HERE"))
                    {
                        string[] parts = returnData.Split(':');
                        if (parts.Length == 3)
                        {
                            this.ipAddress = parts[1];
                            this.port = parts[2];
                            Debug.Log("<color=green>[NetworkConfig]</color> ✅ Servidor autodetectado.");
                        }
                    }
                }
                else 
                {
                    Debug.LogWarning("<color=yellow>[NetworkConfig]</color> ⚠️ El servidor no respondió al broadcast. Usando IP del .env.");
                }
            }
            catch (Exception e) 
            {
                Debug.LogError($"[NetworkConfig] Error en el descubrimiento UDP: {e.Message}");
            }
        }
    }

    public void LoadFromEnv()
    {
        try
        {
            var env = EnvLoader.Load();
            if (env != null && env.Count > 0)
            {
                if (env.ContainsKey("SERVER_IP")) ipAddress = env["SERVER_IP"];
                if (env.ContainsKey("SERVER_PORT")) port = env["SERVER_PORT"];
                if (env.ContainsKey("USE_HTTPS")) useHttps = bool.Parse(env["USE_HTTPS"]);
                Debug.Log("<color=white>[NetworkConfig]</color> 📄 Valores de respaldo cargados.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkConfig] Fallo al leer .env: {e.Message}");
        }
    }
}