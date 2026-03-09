using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;

public class UDPReceiver : MonoBehaviour
{
    Thread receiveThread;
    UdpClient client;
    public int port = 12345;
    
    // ✅ OPTIMIZADO: Ahora recibe bytes directamente (sin conversión a string)
    public ConcurrentQueue<byte[]> messageQueue = new ConcurrentQueue<byte[]>();

    void Start() {
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        
        Debug.Log($"<color=cyan>[UDP]</color> Escuchando datos binarios en puerto {port}");
    }

    private void ReceiveData() {
        client = new UdpClient(port);
        while (true) {
            try {
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = client.Receive(ref anyIP);
                
                messageQueue.Enqueue(data);
            } catch (Exception) { }
        }
    }

    void OnApplicationQuit() {
        if (receiveThread != null) receiveThread.Abort();
        if (client != null) client.Close();
    }
}
