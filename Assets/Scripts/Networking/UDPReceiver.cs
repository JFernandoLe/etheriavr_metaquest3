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
    
    // Flag para salida limpia del thread
    private volatile bool keepRunning = true;

    void Start() {
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        
        Debug.Log($"<color=cyan>[MIDI RX SETUP]</color> 🎹 Escuchando datos binarios MIDI en puerto {port}");
    }

    private void ReceiveData() {
        try
        {
            client = new UdpClient(port);
            client.Client.ReceiveTimeout = 1000; // 1 segundo timeout para no bloquear indefinidamente
            
            while (keepRunning)
            {
                try
                {
                    IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = client.Receive(ref anyIP);
                    messageQueue.Enqueue(data);
                }
                catch (SocketException)
                {
                    // Timeout normal - continuar
                }
                catch (ObjectDisposedException)
                {
                    // Socket cerrado - salir limpiamente
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"<color=red>[UDP]</color> Error: {e.Message}");
        }
        finally
        {
            if (client != null)
            {
                client.Close();
                client.Dispose();
            }
        }
    }

    void OnApplicationQuit() {
        // Señalar que debe terminar
        keepRunning = false;
        
        // Dar tiempo al thread para terminar limpiamente
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(5000); // Esperar máximo 5 segundos
        }
    }
}
