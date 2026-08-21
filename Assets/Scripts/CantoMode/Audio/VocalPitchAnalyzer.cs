using TMPro;
using UnityEngine;

/// <summary>
/// Captura audio del micrófono del Quest y calcula pitch/nota/cents en tiempo real.
/// </summary>
public class VocalPitchAnalyzer : MonoBehaviour
{
    private const float LiveMinFrequency = 65f;
    private const float LiveMaxFrequency = 1350f;

    [Header("Captura")]
    [SerializeField] private int sampleRate = YinPitchDetector.DefaultSampleRate;
    [SerializeField] private int frameSize = 1536;
    [SerializeField] private float yinThreshold = 0.17f;
    [SerializeField] private float energyThreshold = 0.0003f;
    [SerializeField] private int maxDropFrames = 5;
    [SerializeField] private float sendInterval = 0.02f;

    [Header("Suavizado")]
    [SerializeField] private float centsSmoothingFactor = 0.22f;

    [Header("UI opcional")]
    public TextMeshPro centsText;

    private AudioClip micClip;
    private string deviceName;
    private float[] sampleBuffer;
    private float[] analysisBuffer;
    private float previousPitch = -1f;
    private float lastValidPitch = -1f;
    private float lastConfidence;
    private int dropFrames;
    private float lastProcessTime;
    private float smoothedCents;
    private float noiseFloorEma = 0.0002f;

    private readonly float[] recentMidi = new float[5];
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

        if (!TryReadLatestFrame(out _))
            return;

        ProcessFrame(analysisBuffer);
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
        noiseFloorEma = noiseFloorEma * 0.90f + energy * 0.10f;
        float adaptiveThreshold = Mathf.Max(energyThreshold, noiseFloorEma * 1.9f);

        if (energy < adaptiveThreshold)
        {
            dropFrames++;
            if (ShouldHoldPitch() && lastValidPitch > 0f)
                PublishPitch(lastValidPitch, lastConfidence * 0.92f);
            return;
        }

        System.Array.Copy(frame, analysisBuffer, frameSize);
        PitchAnalysisCore.ApplyFirstOrderHighPassInPlace(analysisBuffer, sampleRate, 90f);
        PitchAnalysisCore.ApplyPreEmphasisInPlace(analysisBuffer, 0.95f);
        PitchAnalysisCore.ApplyHanningInPlace(analysisBuffer);

        float flatness = PitchAnalysisCore.ComputeSpectralFlatness(analysisBuffer, sampleRate);
        if (flatness > 0.72f && energy < adaptiveThreshold * 2.5f)
        {
            dropFrames++;
            if (ShouldHoldPitch() && lastValidPitch > 0f)
                PublishPitch(lastValidPitch, lastConfidence * 0.9f);
            return;
        }

        float adaptiveYin = Mathf.Lerp(yinThreshold, yinThreshold * 0.68f,
            Mathf.Clamp01(energy / (adaptiveThreshold * 3.5f)));

        PitchAnalysisCore.RobustPitchResult result = PitchAnalysisCore.DetectPitchRobust(
            analysisBuffer, sampleRate, adaptiveYin, LiveMinFrequency, LiveMaxFrequency);

        if (!result.IsValid || result.Confidence < 0.18f)
        {
            dropFrames++;
            if (ShouldHoldPitch() && lastValidPitch > 0f)
                PublishPitch(lastValidPitch, lastConfidence * 0.88f);
            return;
        }

        float pitch = SmoothPitch(result.PitchHz, result.Confidence);
        pitch = ApplyAdaptiveMedian(pitch, result.Confidence);
        lastValidPitch = pitch;
        lastConfidence = result.Confidence;
        dropFrames = 0;
        PublishPitch(pitch, result.Confidence);
    }

    private bool ShouldHoldPitch() => dropFrames < maxDropFrames + (lastConfidence > 0.55f ? 2 : 0);

    private float ApplyAdaptiveMedian(float pitchHz, float confidence)
    {
        float midi = MusicalNoteUtility.HzToMidi(pitchHz);
        recentMidi[recentMidiCount % recentMidi.Length] = midi;
        recentMidiCount++;

        int count = Mathf.Min(recentMidiCount, recentMidi.Length);
        if (count < 3 || confidence > 0.62f)
            return pitchHz;

        var cluster = new float[count];
        int clusterCount = 0;
        for (int i = 0; i < count; i++)
        {
            if (Mathf.Abs(recentMidi[i] - midi) <= 1.2f)
                cluster[clusterCount++] = recentMidi[i];
        }

        if (clusterCount < 2)
            return pitchHz;

        System.Array.Sort(cluster, 0, clusterCount);
        return MusicalNoteUtility.MidiToHz(cluster[clusterCount / 2]);
    }

    private float SmoothPitch(float currentPitch, float confidence)
    {
        if (previousPitch <= 0f)
        {
            previousPitch = currentPitch;
            return currentPitch;
        }

        float centsDiff = 1200f * Mathf.Log(currentPitch / previousPitch, 2f);
        float absCents = Mathf.Abs(centsDiff);
        float alpha = absCents switch
        {
            > 180f => 0.72f,
            > 100f => 0.52f,
            > 40f => 0.42f,
            _ => 0.28f + confidence * 0.18f
        };

        currentPitch = alpha * currentPitch + (1f - alpha) * previousPitch;
        previousPitch = currentPitch;
        return currentPitch;
    }

    private void PublishPitch(float frequency, float confidence)
    {
        packetCount++;

        float midiFloat = MusicalNoteUtility.HzToMidi(frequency);
        int midi = MusicalNoteUtility.RoundMidi(midiFloat);
        float rawCents = MusicalNoteUtility.FrequencyToCents(frequency, midi);

        float adaptiveSmooth = Mathf.Lerp(centsSmoothingFactor, centsSmoothingFactor * 1.8f, confidence);
        smoothedCents = Mathf.Lerp(smoothedCents, rawCents, adaptiveSmooth);
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
