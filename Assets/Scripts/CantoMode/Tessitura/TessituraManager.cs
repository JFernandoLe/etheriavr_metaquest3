using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TessituraManager : MonoBehaviour
{
    [Header("Referencias")]
    public VocalPitchAnalyzer pitchAnalyzer;
    public AuthService authServiceManual;

    [Header("Medición")]
    [SerializeField] private int minimumStableFrames = 3;
    [SerializeField] private int minimumSamplesForClassification = 12;
    [SerializeField] private float sampleIntervalSeconds = 0.05f;

    [Header("UI TextmeshPro")]
    public TextMeshPro currentNoteText;
    public TextMeshPro rangeText;
    public TextMeshPro resultText;

    private readonly List<TessituraClassifier.VocalRangeSample> samples = new List<TessituraClassifier.VocalRangeSample>();

    private int minMidi = 999;
    private int maxMidi = 0;
    private int lastMidi = -1;
    private int stableFrames = 0;
    private float nextSampleTime;
    private bool isMeasuring = true;
    private int lastDisplayedMidi = int.MinValue;
    private int lastDisplayedMin = int.MinValue;
    private int lastDisplayedMax = int.MinValue;

    void Awake()
    {
        if (pitchAnalyzer == null)
            pitchAnalyzer = FindObjectOfType<VocalPitchAnalyzer>();
    }

    void Update()
    {
        if (!isMeasuring || pitchAnalyzer == null)
            return;

        int midi = pitchAnalyzer.GetCurrentMidi();
        if (midi < 40 || midi > 85)
            return;

        if (midi == lastMidi)
            stableFrames++;
        else
            stableFrames = 0;

        lastMidi = midi;

        if (stableFrames < minimumStableFrames)
            return;

        if (Time.unscaledTime < nextSampleTime)
            return;

        nextSampleTime = Time.unscaledTime + sampleIntervalSeconds;

        float confidence = pitchAnalyzer.IsStable ? 1f : 0.6f;
        samples.Add(new TessituraClassifier.VocalRangeSample
        {
            Midi = midi,
            Confidence = confidence,
            DurationSeconds = sampleIntervalSeconds
        });

        if (midi < minMidi) minMidi = midi;
        if (midi > maxMidi) maxMidi = midi;

        if (currentNoteText != null && midi != lastDisplayedMidi)
        {
            lastDisplayedMidi = midi;
            currentNoteText.text = MusicalNoteUtility.MidiToNoteName(midi);
        }

        if (rangeText != null && (minMidi != lastDisplayedMin || maxMidi != lastDisplayedMax))
        {
            lastDisplayedMin = minMidi;
            lastDisplayedMax = maxMidi;
            rangeText.text =
                $"{MusicalNoteUtility.MidiToNoteName(minMidi)} - {MusicalNoteUtility.MidiToNoteName(maxMidi)}";
        }
    }

    public void FinishMeasurement()
    {
        Debug.Log("<color=yellow>TERMINÓ MEDICIÓN</color>");
        isMeasuring = false;

        TessituraClassifier.VocalRangeResult result = TessituraClassifier.Analyze(samples, minimumSamplesForClassification);

        if (result.ValidSampleCount < minimumSamplesForClassification)
        {
            if (resultText != null)
                resultText.text = "Muestras insuficientes. Canta más tiempo en distintas notas.";
            return;
        }

        if (resultText != null)
            resultText.text = $"{result.Classification}\n{result.MinNoteName} - {result.MaxNoteName}";

        SaveTessitura(result.Classification);
    }

    private void SaveTessitura(string classification)
    {
        if (UserSession.Instance == null)
            return;

        string valorParaDB = TessituraClassifier.MapToDatabaseEnum(classification);
        UserSession.Instance.tessitura = valorParaDB;
        UserSession.Instance.PersistSession();
        Debug.Log($"<color=cyan>[Tessitura]</color> {classification} -> DB: {valorParaDB}");

        AuthService auth = authServiceManual != null ? authServiceManual : FindObjectOfType<AuthService>();
        if (auth == null)
            return;

        StartCoroutine(auth.UpdateTessitura(
            UserSession.Instance.userId,
            valorParaDB,
            _ => Debug.Log("<color=green>[EXITO]</color> Tesitura guardada"),
            err => Debug.LogError("Error al guardar tesitura: " + err)
        ));
    }
}
