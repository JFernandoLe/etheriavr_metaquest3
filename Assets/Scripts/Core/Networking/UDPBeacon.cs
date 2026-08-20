using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections;

public class UDPBeacon : MonoBehaviour
{
    private UdpClient udpClient;
    public int discoveryPort = 5555;
    public string discoveryMessage = "ETHERIA_VR_DISCOVERY";

    void Start() {
        udpClient = new UdpClient();
        udpClient.EnableBroadcast = true;
        StartCoroutine(BroadcastPresence());
    }

    IEnumerator BroadcastPresence() {
        while (true) {
            try {
                byte[] data = Encoding.UTF8.GetBytes(discoveryMessage);
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, discoveryPort);
                udpClient.Send(data, data.Length, endPoint);
                Debug.Log("Beacon: Gritando presencia en la red...");
            } catch (System.Exception e) {
                Debug.LogError("Error en Beacon: " + e.Message);
            }
            yield return new WaitForSeconds(3f); // Cada 3 segundos
        }
    }

    void OnDisable() { if (udpClient != null) udpClient.Close(); }
}