using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

/// <summary>
/// Receptor UDP exclusivo para recibir heartbeat del estado de conexión MIDI
/// Puerto 12346 - No interfiere con el puerto 12345 de notas MIDI
/// </summary>
public class MIDIStatusReceiver : MonoBehaviour
{
    Thread receiveThread;
    UdpClient client;
    public int statusPort = 12346; // Puerto separado para estado MIDI
    
    private bool lastKnownStatus = false;
    private float lastHeartbeatTime = 0f;
    private const float TIMEOUT_SECONDS = 5f; // Si no recibe heartbeat en 5 seg, asume desconectado
    
    // Evento para notificar cambios de estado
    public delegate void StatusReceivedDelegate(bool isConnected);
    public event StatusReceivedDelegate OnStatusReceived;
    
    private object statusLock = new object();
    private bool pendingStatusUpdate = false;
    private bool pendingStatus = false;
    
    // Flag para detener el hilo de forma segura
    private volatile bool keepRunning = true;

    void Start() 
    {
        receiveThread = new Thread(new ThreadStart(ReceiveStatusData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        
        lastHeartbeatTime = Time.time;
        
        Debug.Log($"<color=magenta>[MIDI Status]</color> Escuchando heartbeat en puerto {statusPort}");
    }

    private void ReceiveStatusData() 
    {
        try 
        {
            client = new UdpClient(statusPort);
            client.Client.ReceiveTimeout = 1000; // Timeout de 1 segundo para no bloquear indefinidamente
            
            while (keepRunning) 
            {
                try 
                {
                    IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = client.Receive(ref anyIP);
                    
                    // Validar tamaño del paquete (12 bytes estándar)
                    if (data.Length == 12) 
                    {
                        // [0] = tipo (3=conectado, 4=desconectado)
                        byte msgType = data[0];
                        
                        bool isConnected = (msgType == 3);
                        
                        lock (statusLock) 
                        {
                            pendingStatusUpdate = true;
                            pendingStatus = isConnected;
                        }
                    }
                } 
                catch (SocketException) 
                {
                    // Timeout normal - continuar
                }
                catch (Exception e) 
                {
                    if (keepRunning) 
                    {
                        Debug.LogWarning($"<color=yellow>[MIDI Status]</color> Error recibiendo: {e.Message}");
                    }
                }
            }
        } 
        finally 
        {
            if (client != null) 
            {
                client.Close();
            }
        }
    }

    void Update() 
    {
        // Procesar actualizaciones de estado en el hilo principal
        lock (statusLock) 
        {
            if (pendingStatusUpdate) 
            {
                pendingStatusUpdate = false;
                lastHeartbeatTime = Time.time;
                
                if (lastKnownStatus != pendingStatus) 
                {
                    lastKnownStatus = pendingStatus;
                    OnStatusReceived?.Invoke(pendingStatus);
                }
            }
        }
        
        // Timeout: Si no recibe heartbeat, asumir desconectado
        if (Time.time - lastHeartbeatTime > TIMEOUT_SECONDS) 
        {
            if (lastKnownStatus) 
            {
                lastKnownStatus = false;
                OnStatusReceived?.Invoke(false);
                Debug.LogWarning($"<color=yellow>[MIDI Status]</color> Timeout - Sin heartbeat por {TIMEOUT_SECONDS}s");
            }
        }
    }

    void OnApplicationQuit() 
    {
        StopReceiving();
    }
    
    void OnDestroy()
    {
        StopReceiving();
    }
    
    private void StopReceiving()
    {
        keepRunning = false;
        
        if (receiveThread != null && receiveThread.IsAlive) 
        {
            // Dar tiempo para que el hilo termine naturalmente
            if (!receiveThread.Join(2000)) 
            {
                // Si no termina en 2 segundos, abortar
                receiveThread.Abort();
            }
        }
        
        if (client != null) 
        {
            try 
            {
                client.Close();
            } 
            catch { }
        }
    }
}
