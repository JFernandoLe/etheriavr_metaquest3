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

    void Start() 
    {
        Debug.Log("<color=cyan>[MIDI Status]</color> 🔍 Buscando DirectMidiReceiver...");
        
        // Buscar DirectMidiReceiver
        midiReceiver = FindObjectOfType<DirectMidiReceiver>();
        
        if (midiReceiver != null)
        {
            Debug.Log("<color=green>[MIDI Status]</color> ✅ DirectMidiReceiver encontrado!");
            
            // Suscribirse a eventos de cambio de estado
            midiReceiver.OnConnectionStatusChanged += HandleMidiStatusChange;
            Debug.Log("<color=green>[MIDI Status]</color> ✅ Suscrito a OnConnectionStatusChanged");
        }
        else
        {
            Debug.LogError("<color=red>[MIDI Status]</color> ❌ NO se encontró DirectMidiReceiver en la escena");
        }
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

    void OnDestroy()
    {
        if (midiReceiver != null)
        {
            midiReceiver.OnConnectionStatusChanged -= HandleMidiStatusChange;
        }
    }
}
