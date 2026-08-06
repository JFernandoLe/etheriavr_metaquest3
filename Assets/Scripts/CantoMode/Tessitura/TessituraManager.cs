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
    [Header("Referencias")]
    public SUDPReceiver receiver;
    public AuthService authServiceManual; // Arrastra el AuthManager aquí en el Inspector

    [Header("UI TextmeshPro")]
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
        if (!isMeasuring || receiver == null) return;

        int midi = receiver.GetCurrentMidi();

        // Filtro de rango vocal humano estándar
        if (midi < 40 || midi > 85) return;

        totalFrames++;

        if (midi == lastMidi)
            stableFrames++;
        else
            stableFrames = 0;

        lastMidi = midi;

        // Solo procesar si la nota es estable
        if (stableFrames < 3) return;

        float currentAvg = (count > 0) ? totalMidi / count : midi;
        if (midi > currentAvg + 10) return; // Ignorar picos absurdos

        if (midi < minMidi) minMidi = midi;
        if (midi > maxMidi) maxMidi = midi;

        totalMidi += midi;
        count++;

        if (currentNoteText != null) currentNoteText.text = MidiToNote(midi);
        if (rangeText != null) rangeText.text = $"{MidiToNote(minMidi)} - {MidiToNote(maxMidi)}";
    }

    public void FinishMeasurement()
    {
        Debug.Log("<color=yellow>TERMINÉ MEDICIÓN</color>");
        isMeasuring = false;

        float avg = totalMidi / Mathf.Max(count, 1);
        float adjustedMax = Mathf.Min(maxMidi, avg + 8);
        float range = adjustedMax - minMidi;
        float stability = (float)stableFrames / Mathf.Max(totalFrames, 1);

        StartCoroutine(SendToAI(minMidi, adjustedMax, avg, range, stability));
    }

    IEnumerator SendToAI(float min, float max, float avg, float range, float stability)
    {
        float timeout = 5f;
        float timer = 0;

        // 1. Espera robusta al buscador de servidor
        while (string.IsNullOrEmpty(AIServerFinder.ServerURL) && timer < timeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (string.IsNullOrEmpty(AIServerFinder.ServerURL))
        {
            Debug.LogError("<color=red>Error:</color> Servidor IA no encontrado en la red local.");
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

        string jsonPayload = JsonUtility.ToJson(data);
        UnityWebRequest www = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            string jsonResponse = www.downloadHandler.text;
            Debug.Log("<color=green>IA RESPONSE:</color> " + jsonResponse);

            AIResponse ai = JsonUtility.FromJson<AIResponse>(jsonResponse);

            if (ai != null && !string.IsNullOrEmpty(ai.voice))
            {
                if (resultText != null) resultText.text = ai.voice;

                if (UserSession.Instance != null)
                {
                    // Limpiamos la respuesta de la IA (quitamos espacios y pasamos a Mayúsculas)
                    string vozIA = ai.voice.Trim();
                    string valorParaDB = "";

                    // Mapeo exacto según tu lista de la IA
                    switch (vozIA)
                    {
                        case "Bajo":
                            valorParaDB = "BASS";
                            break;
                        case "Baritono":
                            valorParaDB = "BARITONE";
                            break;
                        case "Tenor":
                            valorParaDB = "TENOR";
                            break;
                        case "Contralto":
                            valorParaDB = "CONTRALTO";
                            break;
                        case "Mezzosoprano":
                            valorParaDB = "MEZZO_SOPRANO";
                            break;
                        case "Soprano":
                            valorParaDB = "SOPRANO";
                            break;
                        default:
                            valorParaDB = vozIA.ToUpper(); // Fallback por si acaso
                            break;
                    }

                    // Actualizamos la sesión local inmediatamente
                    UserSession.Instance.tessitura = valorParaDB;
                    Debug.Log($"<color=cyan>[Mapeo]</color> IA: {vozIA} -> DB: {valorParaDB}");

                    // Lanzamos el guardado al AuthService
                    if (authServiceManual != null || FindObjectOfType<AuthService>() != null)
                    {
                        AuthService auth = authServiceManual != null ? authServiceManual : FindObjectOfType<AuthService>();
                        StartCoroutine(auth.UpdateTessitura(
                            UserSession.Instance.userId,
                            valorParaDB,
                            (res) => Debug.Log("<color=green>[EXITO]</color> Guardado en MySQL"),
                            (err) => Debug.LogError("Error al guardar: " + err)
                        ));
                    }
                }
            }
        }
        else
        {
            Debug.LogError("Error de conexión con IA: " + www.error);
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