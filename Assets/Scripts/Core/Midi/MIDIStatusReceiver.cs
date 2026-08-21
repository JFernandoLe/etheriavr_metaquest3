using UnityEngine;

public class MIDIStatusReceiver : MonoBehaviour
{
    public delegate void StatusReceivedDelegate(bool isConnected);
    public event StatusReceivedDelegate OnStatusReceived;

    private DirectMidiReceiver midiReceiver;
    private bool lastKnownStatus, isSubscribed;
    private float nextSearchTime;
    private int failedAttachAttempts;

    public DirectMidiReceiver CurrentReceiver => midiReceiver;
    public string CurrentDeviceName => midiReceiver ? midiReceiver.CurrentMidiDeviceName : "NO REGISTRADO";

    private void Start() => TryAttachToReceiver();

    private void Update()
    {
        // Una vez suscritos, el estado llega por eventos (sin polling).
        if (isSubscribed && midiReceiver)
        {
            enabled = false;
            return;
        }

        if (failedAttachAttempts > 20)
        {
            enabled = false;
            return;
        }

        if (Time.unscaledTime < nextSearchTime) return;
        nextSearchTime = Time.unscaledTime + 1f;
        TryAttachToReceiver();
    }

    private void HandleMidiStatusChange(bool isConnected)
    {
        if (lastKnownStatus == isConnected) return;

        lastKnownStatus = isConnected;
        OnStatusReceived?.Invoke(isConnected);
    }

    private void TryAttachToReceiver()
    {
        midiReceiver ??= FindObjectOfType<DirectMidiReceiver>();
        if (!midiReceiver)
        {
            failedAttachAttempts++;
            return;
        }

        failedAttachAttempts = 0;
        if (!isSubscribed)
        {
            midiReceiver.OnConnectionStatusChanged += HandleMidiStatusChange;
            isSubscribed = true;
            HandleMidiStatusChange(midiReceiver.IsMidiConnected);
        }
    }

    private void OnDestroy()
    {
        if (midiReceiver && isSubscribed)
            midiReceiver.OnConnectionStatusChanged -= HandleMidiStatusChange;
    }
}
