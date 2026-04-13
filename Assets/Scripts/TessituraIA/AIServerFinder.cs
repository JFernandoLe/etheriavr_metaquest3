using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Net;
using System.Net.Sockets;

public class AIServerFinder : MonoBehaviour
{
    public static string ServerURL;

    void Start()
    {
        StartCoroutine(FindServer());
    }

    IEnumerator FindServer()
    {
        string baseIP = GetNetworkBase();

        Debug.Log("Escaneando red: " + baseIP + ".X");

        for (int i = 20; i < 60; i++) //  optimizado
        {
            string url = "http://" + baseIP + "." + i + ":5000/predict";

            UnityWebRequest www = new UnityWebRequest(url, "POST");

            www.timeout = 1; //  CRÍTICO

            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(
                "{\"min\":45,\"max\":65,\"avg\":55,\"range\":20,\"stability\":0.8}"
            );

            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                ServerURL = "http://" + baseIP + "." + i + ":5000";
                Debug.Log(" SERVIDOR ENCONTRADO: " + ServerURL);
                yield break;
            }
        }

        Debug.LogError(" No se encontró servidor");
    }

    string GetNetworkBase()
    {
        foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                string ipStr = ip.ToString();

                if (ipStr.StartsWith("127")) continue;

                Debug.Log(" IP detectada: " + ipStr);

                string[] parts = ipStr.Split('.');
                return parts[0] + "." + parts[1] + "." + parts[2];
            }
        }

        Debug.LogWarning(" No se detectó IP, usando default");
        return "192.168.100";
    }
}