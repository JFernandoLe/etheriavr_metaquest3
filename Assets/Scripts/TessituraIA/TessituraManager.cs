using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using System.Collections;

[System.Serializable]
public class AIResponse
{
    public string voice;
}

public class TessituraManager : MonoBehaviour
{
    public SUDPReceiver receiver;

    public TextMeshPro currentNoteText;
    public TextMeshPro rangeText;
    public TextMeshPro resultText;

    private int minMidi = 999;
    private int maxMidi = 0;

    private float totalMidi = 0f;
    private int count = 0;

    private int stableFrames = 0;
    private int totalFrames = 0;

    private int lastMidi = -1;

    private bool isMeasuring = true;

    void Update()
    {
        if (!isMeasuring) return;
        if (receiver == null) return;

        int midi = receiver.GetCurrentMidi();

        //  FILTRO BÁSICO
        if (midi < 40 || midi > 85) return;

        totalFrames++;

        //  ESTABILIDAD CORRECTA (persistente)
        if (midi == lastMidi)
            stableFrames++;
        else
            stableFrames = 0;

        lastMidi = midi;

        //  SOLO NOTAS ESTABLES
        if (stableFrames < 3) return;

        //  PROMEDIO ACTUAL (dinámico)
        float currentAvg = (count > 0) ? totalMidi / count : midi;

        //  IGNORAR PICOS EXTREMOS
        if (midi > currentAvg + 10) return;

        // rango
        if (midi < minMidi) minMidi = midi;
        if (midi > maxMidi) maxMidi = midi;

        // promedio
        totalMidi += midi;
        count++;

        // UI
        if (currentNoteText != null)
            currentNoteText.text = MidiToNote(midi);

        if (rangeText != null)
            rangeText.text = $"{MidiToNote(minMidi)} - {MidiToNote(maxMidi)}";
    }

    public void FinishMeasurement()
    {
        Debug.Log("TERMINÉ MEDICIÓN");

        isMeasuring = false;

        float avg = totalMidi / Mathf.Max(count, 1);

        //  AJUSTE INTELIGENTE
        float adjustedMax = Mathf.Min(maxMidi, avg + 8);
        float range = adjustedMax - minMidi;

        float stability = (float)stableFrames / Mathf.Max(totalFrames, 1);

        Debug.Log($"{minMidi}-{maxMidi} | AVG:{avg} | STAB:{stability}");

        StartCoroutine(SendToAI(minMidi, adjustedMax, avg, range, stability));
    }

    IEnumerator SendToAI(float min, float max, float avg, float range, float stability)
    {
        if (string.IsNullOrEmpty(AIServerFinder.ServerURL))
        {
            Debug.LogError(" Servidor aún no encontrado");
            yield break;
        }

        string url = AIServerFinder.ServerURL + "/predict";

        TessituraData data = new TessituraData
        {
            min = (int)min,
            max = (int)max,
            avg = avg,
            range = range,
            stability = stability
        };

        string json = JsonUtility.ToJson(data);

        Debug.Log(" Enviando a: " + url);
        Debug.Log(" JSON: " + json);

        UnityWebRequest www = new UnityWebRequest(url, "POST");

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            string response = www.downloadHandler.text;

            Debug.Log(" IA RESPONSE: " + response);

            AIResponse ai = JsonUtility.FromJson<AIResponse>(response);

            if (ai != null && resultText != null)
                resultText.text = "Tipo de voz: " + ai.voice;
        }
        else
        {
            Debug.LogError(" Error IA: " + www.error);
        }
    }

    string MidiToNote(int midi)
    {
        string[] notes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int note = midi % 12;
        int octave = (midi / 12) - 1;

        return notes[note] + octave;
    }
}