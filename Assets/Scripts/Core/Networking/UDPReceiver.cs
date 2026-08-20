using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;

public class UDPReceiver : MonoBehaviour
{
    public int port = 12345;
    public ConcurrentQueue<byte[]> messageQueue = new();
    
    private Thread receiveThread;
    private UdpClient client;
    private volatile bool keepRunning = true;

    private void Start() 
    {
        (receiveThread = new Thread(ReceiveData) { IsBackground = true }).Start();
        Debug.Log($"<color=cyan>[MIDI RX SETUP]</color> 🎹 Escuchando datos binarios MIDI en puerto {port}");
    }

    private void ReceiveData() 
    {
        try
        {
            client = new UdpClient(port);
            client.Client.ReceiveTimeout = 1000;
            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
            
            while (keepRunning)
            {
                try { messageQueue.Enqueue(client.Receive(ref anyIP)); }
                catch (SocketException) { /* Timeout normal */ }
                catch (ObjectDisposedException) { break; }
            }
        }
        catch (Exception e) { Debug.LogError($"<color=red>[UDP]</color> Error: {e.Message}"); }
        finally { client?.Close(); }
    }

    private void OnApplicationQuit() 
    {
        keepRunning = false;
        client?.Close();
        receiveThread?.Join(5000);
    }
}