using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;

public class SUDPReceiver : MonoBehaviour
{
    Thread receiveThread;
    UdpClient client;
    public int port = 12345;

    private string lastMessage = "";
    private bool messageReceived = false;

    // Datos musicales actuales
    private float currentCents = 0f;
    private string currentTuningState = "DESAFINADO";
    private int currentMidi = -1;

    // Suavizado
    private float smoothedCents = 0f;
    private float smoothingFactor = 0.1f;

    public TextMeshPro centsText;

    void Start()
    {
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();

        Debug.Log("SUDPReceiver iniciado en puerto " + port);
    }

    void ReceiveData()
    {
        client = new UdpClient(port);

        while (true)
        {
            try
            {
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = client.Receive(ref anyIP);
                string text = Encoding.UTF8.GetString(data);

                lastMessage = text;
                messageReceived = true;
            }
            catch
            {
                // ignoramos errores
            }
        }
    }

    void Update()
    {
        if (!messageReceived)
            return;

        messageReceived = false;

        string[] parts = lastMessage.Split('|');

        if (parts.Length >= 7 && parts[0] == "voice")
        {
            try
            {
                float frequency = float.Parse(parts[1], CultureInfo.InvariantCulture);
                int midi = int.Parse(parts[2]);
                currentMidi = midi;
                string note = parts[3];
                float rawCents = float.Parse(parts[4], CultureInfo.InvariantCulture);
                float amplitude = float.Parse(parts[5], CultureInfo.InvariantCulture);
                float time = float.Parse(parts[6], CultureInfo.InvariantCulture);

                // Suavizado
                smoothedCents = Mathf.Lerp(smoothedCents, rawCents, smoothingFactor);
                float cents = smoothedCents;

                string tuningState = GetTuningState(cents);

                // Guardamos valores actuales para otros scripts
                currentCents = cents;
                currentTuningState = tuningState;

                Debug.Log($"Nota: {note} | {frequency} Hz | Cents: {cents:F2} | Estado: {tuningState}");
                
            }
            catch
            {
                Debug.LogWarning("Error parseando mensaje: " + lastMessage);
            }
        }
    }

    string GetTuningState(float cents)
    {
        float absCents = Mathf.Abs(cents);

        if (absCents <= 5f)
        {
            ShowCents($"{cents}", Color.red);
            return "PERFECTO";
        }
        else if (absCents <= 15f)
        {
            ShowCents($"{cents}", Color.yellow);
            return "CASI";
        }
        else
        {
            ShowCents($"{cents}", Color.red);
            return "DESAFINADO";
        }
            

    }

    // Métodos públicos para el visualizador
    public float GetCurrentCents()
    {
        return currentCents;
    }

    public string GetCurrentTuningState()
    {
        return currentTuningState;
    }
    public int GetCurrentMidi()
    {
        return currentMidi;
    }
    void OnApplicationQuit()
    {
        if (receiveThread != null && receiveThread.IsAlive)
            receiveThread.Abort();

        if (client != null)
            client.Close();
    }

    void ShowCents(string message, Color color)
    {
        if (centsText == null)
            return;

        centsText.text = message;
        centsText.color = color;
    }
}