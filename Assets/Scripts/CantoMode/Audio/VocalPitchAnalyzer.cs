using TMPro;
using UnityEngine;

/// <summary>
/// Captura audio del micrófono del Quest y calcula pitch/nota/cents en tiempo real.
/// Reemplaza la dependencia de etheria_desktop + UDP.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class VocalPitchAnalyzer : MonoBehaviour
{
    [Header("Captura")]
    [SerializeField] private int sampleRate = YinPitchDetector.DefaultSampleRate;
    [SerializeField] private int frameSize = YinPitchDetector.DefaultFrameSize;
    [SerializeField] private float yinThreshold = YinPitchDetector.DefaultThreshold;
    [SerializeField] private float energyThreshold = 0.0005f;
    [SerializeField] private int maxDropFrames = 3;
    [SerializeField] private float sendInterval = 0.033f;

    [Header("Suavizado")]
    [SerializeField] private float smoothingFactor = 0.1f;
    [SerializeField] private float centsSmoothingFactor = 0.1f;

    [Header("UI opcional")]
    public TextMeshPro centsText;

    private AudioSource audioSource;
    private string deviceName;
    private float[] sampleBuffer;
    private float previousPitch = -1f;
    private float lastValidPitch = -1f;
    private int dropFrames;
    private float lastProcessTime;
    private float smoothedCents;

    private int currentMidi = -1;
    private float currentCents;
    private string currentTuningState = "DESAFINADO";
    private bool isStable;
    private int packetCount;

    public bool IsMicrophoneActive => !string.IsNullOrEmpty(deviceName) && Microphone.IsRecording(deviceName);
    public bool IsStable => isStable;
    public int PacketCount => packetCount;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.mute = true;
        audioSource.playOnAwake = false;
        sampleBuffer = new float[frameSize];
    }

    void Start()
    {
        if (receiver == null)
            receiver = FindObjectOfType<VocalPitchAnalyzer>();

        if (!StartMicrophone())
            Debug.LogError("[VocalPitchAnalyzer] No se pudo iniciar el micrófono del dispositivo.");
        else
            Debug.Log("[VocalPitchAnalyzer] Micrófono activo — análisis local en Quest.");
    }

    void Update()
    {
        if (!IsMicrophoneActive)
            return;

        if (Time.unscaledTime - lastProcessTime < sendInterval)
            return;

        lastProcessTime = Time.unscaledTime;

        if (!TryReadLatestFrame(out float[] frame))
            return;

        ProcessFrame(frame);
    }

    public float GetCurrentCents() => currentCents;
    public string GetCurrentTuningState() => currentTuningState;
    public int GetCurrentMidi() => currentMidi;
    public float GetCurrentFrequency() => lastValidPitch;

    private bool StartMicrophone()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
#endif

        if (Microphone.devices == null || Microphone.devices.Length == 0)
            return false;

        deviceName = Microphone.devices[0];
        audioSource.clip = Microphone.Start(deviceName, true, 10, sampleRate);
        audioSource.Play();

        while (Microphone.GetPosition(deviceName) <= 0) { }

        return true;
    }

    private bool TryReadLatestFrame(out float[] frame)
    {
        frame = new float[frameSize];
        int micPosition = Microphone.GetPosition(deviceName);
        if (micPosition < frameSize)
            return false;

        int start = micPosition - frameSize;
        if (start < 0)
            start += audioSource.clip.samples;

        audioSource.clip.GetData(sampleBuffer, start);
        System.Array.Copy(sampleBuffer, frame, frameSize);
        return true;
    }

    private void ProcessFrame(float[] frame)
    {
        float energy = YinPitchDetector.ComputeEnergy(frame);

        if (energy < energyThreshold)
        {
            dropFrames++;
            if (dropFrames < maxDropFrames && lastValidPitch > 0f)
                PublishPitch(lastValidPitch);
            return;
        }

        float pitch = YinPitchDetector.DetectPitch(frame, sampleRate, yinThreshold);
        if (pitch <= 0f)
        {
            dropFrames++;
            if (dropFrames < maxDropFrames && lastValidPitch > 0f)
                PublishPitch(lastValidPitch);
            return;
        }

        pitch = SmoothPitch(pitch);
        lastValidPitch = pitch;
        dropFrames = 0;
        PublishPitch(pitch);
    }

    private float SmoothPitch(float currentPitch)
    {
        if (previousPitch <= 0f)
        {
            previousPitch = currentPitch;
            return currentPitch;
        }

        float centsDiff = 1200f * Mathf.Log(currentPitch / previousPitch, 2f);
        float alpha = Mathf.Abs(centsDiff) > 100f ? 0.6f : 0.25f;
        currentPitch = alpha * currentPitch + (1f - alpha) * previousPitch;
        previousPitch = currentPitch;
        return currentPitch;
    }

    private void PublishPitch(float frequency)
    {
        packetCount++;

        float midiFloat = MusicalNoteUtility.HzToMidi(frequency);
        int midi = MusicalNoteUtility.RoundMidi(midiFloat);
        float rawCents = MusicalNoteUtility.FrequencyToCents(frequency, midi);

        smoothedCents = Mathf.Lerp(smoothedCents, rawCents, centsSmoothingFactor);
        currentMidi = midi;
        currentCents = smoothedCents;
        currentTuningState = GetTuningState(smoothedCents);
        isStable = Mathf.Abs(smoothedCents) <= 40f;

        ShowCents($"{smoothedCents:F1}", currentTuningState == "PERFECTO" ? Color.green : Color.yellow);
    }

    private static string GetTuningState(float cents)
    {
        float abs = Mathf.Abs(cents);
        if (abs <= 5f) return "PERFECTO";
        if (abs <= 15f) return "CASI";
        return "DESAFINADO";
    }

    private void ShowCents(string message, Color color)
    {
        if (centsText == null)
            return;

        centsText.text = message;
        centsText.color = color;
    }

    void OnDestroy()
    {
        if (!string.IsNullOrEmpty(deviceName))
            Microphone.End(deviceName);
    }
}
