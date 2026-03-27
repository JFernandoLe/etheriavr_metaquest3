using UnityEngine;
using System;

/// <summary>
/// Receptor de Estado de Conexión MIDI
/// Se suscribe a los eventos de DirectMidiReceiver
/// Notifica cambios de conexión a MIDIConnectionManager
/// </summary>
public class MIDIStatusReceiver : MonoBehaviour
{
    // Evento para notificar cambios de estado
    public delegate void StatusReceivedDelegate(bool isConnected);
    public event StatusReceivedDelegate OnStatusReceived;
    
    private DirectMidiReceiver midiReceiver;
    private bool lastKnownStatus = false;
    private bool isSubscribed = false;
    private float nextSearchTime = 0f;
    private const float SearchIntervalSeconds = 0.5f;

    void Start() 
    {
        Debug.Log("<color=cyan>[MIDI Status]</color> 🔍 Buscando DirectMidiReceiver...");
        TryAttachToReceiver();
    }

    void Update()
    {
        if (Time.unscaledTime < nextSearchTime)
        {
            return;
        }

        nextSearchTime = Time.unscaledTime + SearchIntervalSeconds;

        if (isSubscribed && midiReceiver != null)
        {
            HandleMidiStatusChange(midiReceiver.IsMidiConnected);
            return;
        }

        TryAttachToReceiver();
    }

    /// <summary>
    /// Callback cuando cambia el estado de conexión MIDI
    /// </summary>
    private void HandleMidiStatusChange(bool isConnected)
    {
        Debug.Log($"<color=cyan>[MIDI Status]</color> 📢 Evento recibido: isConnected={isConnected}, lastKnownStatus={lastKnownStatus}");
        
        if (lastKnownStatus != isConnected)
        {
            lastKnownStatus = isConnected;
            
            string status = isConnected ? "CONECTADO ✅" : "DESCONECTADO ❌";
            string color = isConnected ? "green" : "red";
            Debug.Log($"<color={color}>[MIDI Status]</color> 🔔 ESTADO CAMBIÓ: {status}");
            
            Debug.Log($"<color={color}>[MIDI Status]</color> 📡 Invocando OnStatusReceived({isConnected})");
            OnStatusReceived?.Invoke(isConnected);
        }
        else
        {
            Debug.Log($"<color=yellow>[MIDI Status]</color> ℹ️ Estado no cambió (sigue en {(isConnected ? "CONECTADO" : "DESCONECTADO")})");
        }
    }

    private void TryAttachToReceiver()
    {
        if (midiReceiver == null)
        {
            midiReceiver = FindObjectOfType<DirectMidiReceiver>();
        }

        if (midiReceiver == null)
        {
            Debug.LogWarning("<color=yellow>[MIDI Status]</color> ⏳ DirectMidiReceiver aún no está disponible");
            return;
        }

        if (!isSubscribed)
        {
            midiReceiver.OnConnectionStatusChanged += HandleMidiStatusChange;
            isSubscribed = true;
            Debug.Log("<color=green>[MIDI Status]</color> ✅ Suscrito a OnConnectionStatusChanged");
        }

        HandleMidiStatusChange(midiReceiver.IsMidiConnected);
    }

    void OnDestroy()
    {
        if (midiReceiver != null && isSubscribed)
        {
            midiReceiver.OnConnectionStatusChanged -= HandleMidiStatusChange;
        }
    }
}
