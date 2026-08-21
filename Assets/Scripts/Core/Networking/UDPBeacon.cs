using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections;

/// <summary>
/// Broadcast UDP legacy de descubrimiento. Desactivado por defecto:
/// NetworkConfig ya resuelve el backend vía .env.
/// </summary>
public class UDPBeacon : MonoBehaviour
{
    [SerializeField] private bool enableBeacon = false;
    public int discoveryPort = 5555;
    public string discoveryMessage = "ETHERIA_VR_DISCOVERY";

    private UdpClient udpClient;

    void Start()
    {
        if (!enableBeacon)
        {
            enabled = false;
            return;
        }

        udpClient = new UdpClient();
        udpClient.EnableBroadcast = true;
        StartCoroutine(BroadcastPresence());
    }

    IEnumerator BroadcastPresence()
    {
        var wait = new WaitForSeconds(3f);
        while (true)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(discoveryMessage);
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, discoveryPort);
                udpClient.Send(data, data.Length, endPoint);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error en Beacon: " + e.Message);
            }

            yield return wait;
        }
    }

    void OnDisable()
    {
        if (udpClient != null) udpClient.Close();
    }
}
