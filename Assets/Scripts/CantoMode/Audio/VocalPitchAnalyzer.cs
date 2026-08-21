using TMPro;
using UnityEngine;

/// <summary>
/// Captura audio del micrófono del Quest y calcula pitch/nota/cents en tiempo real.
/// </summary>
public class VocalPitchAnalyzer : MonoBehaviour
{
    private const float LiveMinFrequency = 70f;
    private const float LiveMaxFrequency = 1300f;

    [Header("Captura")]
    [SerializeField] private int sampleRate = YinPitchDetector.DefaultSampleRate;
    [SerializeField] private int frameSize = YinPitchDetector.DefaultFrameSize;
    [SerializeField] private float yinThreshold = 0.18f;
    [SerializeField] private float energyThreshold = 0.00035f;
    [SerializeField] private int maxDropFrames = 4;
    [SerializeField] private float sendInterval = 0.025f;

    [Header("Suavizado")]
    [SerializeField] private float centsSmoothingFactor = 0.18f;

    [Header("UI opcional")]
    public TextMeshPro centsText;

    private AudioClip micClip;
    private string deviceName;
    private float[] sampleBuffer;
    private float[] analysisBuffer;
    private float previousPitch = -1f;
    private float lastValidPitch = -1f;
    private int dropFrames;
    private float lastProcessTime;
    private float smoothedCents;
    private float noiseFloorEma = 0.0002f;

    private readonly float[] recentMidi = new float[3];
    private int recentMidiCount;

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
        sampleBuffer = new float[frameSize];
        analysisBuffer = new float[frameSize];
    }

    void Start()
    {
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
        micClip = Microphone.Start(deviceName, true, 10, sampleRate);

        float waitStart = Time.realtimeSinceStartup;
        while (Microphone.GetPosition(deviceName) <= 0)
        {
            if (Time.realtimeSinceStartup - waitStart > 2f)
                return false;
        }

        return micClip != null;
    }

    private bool TryReadLatestFrame(out float[] frame)
    {
        frame = analysisBuffer;

        if (micClip == null)
            return false;

        int micPosition = Microphone.GetPosition(deviceName);
        if (micPosition < frameSize)
            return false;

        int start = micPosition - frameSize;
        if (start < 0)
            start += micClip.samples;

        micClip.GetData(sampleBuffer, start);
        System.Array.Copy(sampleBuffer, frame, frameSize);
        return true;
    }

    private void ProcessFrame(float[] frame)
    {
        float energy = YinPitchDetector.ComputeEnergy(frame);
        noiseFloorEma = noiseFloorEma * 0.92f + energy * 0.08f;
        float adaptiveThreshold = Mathf.Max(energyThreshold, noiseFloorEma * 2.2f);

        if (energy < adaptiveThreshold)
        {
            dropFrames++;
            if (dropFrames < maxDropFrames && lastValidPitch > 0f)
                PublishPitch(lastValidPitch);
            return;
        }

        System.Array.Copy(frame, analysisBuffer, frameSize);
        PitchAnalysisCore.ApplyHanningInPlace(analysisBuffer);

        float adaptiveYin = Mathf.Lerp(yinThreshold, yinThreshold * 0.72f,
            Mathf.Clamp01(energy / (adaptiveThreshold * 4f)));

        YinPitchDetector.YinResult yin = YinPitchDetector.DetectPitchDetailed(
            analysisBuffer, sampleRate, adaptiveYin, LiveMinFrequency, LiveMaxFrequency);

        float pitch = yin.IsValid
            ? PitchAnalysisCore.ValidateFundamentalHz(analysisBuffer, sampleRate, yin)
            : -1f;

        if (pitch <= 0f)
        {
            dropFrames++;
            if (dropFrames < maxDropFrames && lastValidPitch > 0f)
                PublishPitch(lastValidPitch);
            return;
        }

        pitch = SmoothPitch(pitch);
        pitch = ApplyRecentMedian(pitch);
        lastValidPitch = pitch;
        dropFrames = 0;
        PublishPitch(pitch);
    }

    private float ApplyRecentMedian(float pitchHz)
    {
        float midi = MusicalNoteUtility.HzToMidi(pitchHz);
        recentMidi[recentMidiCount % recentMidi.Length] = midi;
        recentMidiCount++;

        int count = Mathf.Min(recentMidiCount, recentMidi.Length);
        if (count < 2)
            return pitchHz;

        float[] sorted = new float[count];
        for (int i = 0; i < count; i++)
            sorted[i] = recentMidi[i];
        System.Array.Sort(sorted);
        return MusicalNoteUtility.MidiToHz(sorted[count / 2]);
    }

    private float SmoothPitch(float currentPitch)
    {
        if (previousPitch <= 0f)
        {
            previousPitch = currentPitch;
            return currentPitch;
        }

        float centsDiff = 1200f * Mathf.Log(currentPitch / previousPitch, 2f);
        float alpha = Mathf.Abs(centsDiff) > 100f ? 0.55f : 0.35f;
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
