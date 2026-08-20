using UnityEngine;

public class MIDIStatusReceiver : MonoBehaviour
{
    public delegate void StatusReceivedDelegate(bool isConnected);
    public event StatusReceivedDelegate OnStatusReceived;
    
    private DirectMidiReceiver midiReceiver;
    private bool lastKnownStatus, isSubscribed;
    private float nextSearchTime;

    public DirectMidiReceiver CurrentReceiver => midiReceiver;
    public string CurrentDeviceName => midiReceiver ? midiReceiver.CurrentMidiDeviceName : "NO REGISTRADO";

    private void Start() => TryAttachToReceiver();

    private void Update()
    {
        if (Time.unscaledTime < nextSearchTime) return;
        nextSearchTime = Time.unscaledTime + 0.5f;

        if (isSubscribed && midiReceiver)
            HandleMidiStatusChange(midiReceiver.IsMidiConnected);
        else
            TryAttachToReceiver();
    }

    private void HandleMidiStatusChange(bool isConnected)
    {
        if (lastKnownStatus == isConnected) return;

        lastKnownStatus = isConnected;
        Debug.Log($"<color={(isConnected ? "green" : "red")}>[MIDI Status]</color> 🔔 ESTADO CAMBIÓ: {(isConnected ? "CONECTADO ✅" : "DESCONECTADO ❌")}");
        OnStatusReceived?.Invoke(isConnected);
    }

    private void TryAttachToReceiver()
    {
        midiReceiver ??= FindObjectOfType<DirectMidiReceiver>();
        if (!midiReceiver)
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

    private void OnDestroy()
    {
        if (midiReceiver && isSubscribed)
            midiReceiver.OnConnectionStatusChanged -= HandleMidiStatusChange;
    }
}