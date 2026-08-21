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
    private volatile bool messageReceived = false;

    private float currentCents = 0f;
    private string currentTuningState = "DESAFINADO";
    private int currentMidi = -1;

    private float smoothedCents = 0f;
    private float smoothingFactor = 0.1f;
    private string lastCentsText;
    private Color lastCentsColor;

    public TextMeshPro centsText;

    void Start()
    {
        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();
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
                lastMessage = Encoding.UTF8.GetString(data);
                messageReceived = true;
            }
            catch
            {
                // ignoramos errores de socket
            }
        }
    }

    void Update()
    {
        if (!messageReceived) return;

        messageReceived = false;
        string message = lastMessage;
        if (string.IsNullOrEmpty(message) || message[0] != 'v') return;

        // Parseo ligero: voice|freq|midi|note|cents|amp|time
        int p0 = message.IndexOf('|');
        if (p0 < 0) return;
        if (!message.StartsWith("voice", System.StringComparison.Ordinal)) return;

        int p1 = message.IndexOf('|', p0 + 1);
        int p2 = message.IndexOf('|', p1 + 1);
        int p3 = message.IndexOf('|', p2 + 1);
        int p4 = message.IndexOf('|', p3 + 1);
        int p5 = message.IndexOf('|', p4 + 1);
        if (p1 < 0 || p2 < 0 || p3 < 0 || p4 < 0 || p5 < 0) return;

        try
        {
            int midi = int.Parse(message.Substring(p1 + 1, p2 - p1 - 1), CultureInfo.InvariantCulture);
            float rawCents = float.Parse(message.Substring(p3 + 1, p4 - p3 - 1), CultureInfo.InvariantCulture);

            currentMidi = midi;
            smoothedCents = Mathf.Lerp(smoothedCents, rawCents, smoothingFactor);
            currentCents = smoothedCents;
            currentTuningState = GetTuningState(smoothedCents);
        }
        catch
        {
            // mensaje malformado
        }
    }

    string GetTuningState(float cents)
    {
        float absCents = Mathf.Abs(cents);

        if (absCents <= 5f)
        {
            ShowCents(cents, Color.red);
            return "PERFECTO";
        }

        if (absCents <= 15f)
        {
            ShowCents(cents, Color.yellow);
            return "CASI";
        }

        ShowCents(cents, Color.red);
        return "DESAFINADO";
    }

    public float GetCurrentCents() => currentCents;
    public string GetCurrentTuningState() => currentTuningState;
    public int GetCurrentMidi() => currentMidi;

    void OnApplicationQuit()
    {
        if (receiveThread != null && receiveThread.IsAlive)
            receiveThread.Abort();

        client?.Close();
    }

    void ShowCents(float cents, Color color)
    {
        if (centsText == null) return;

        string text = cents.ToString("0.##", CultureInfo.InvariantCulture);
        if (text == lastCentsText && color == lastCentsColor) return;

        lastCentsText = text;
        lastCentsColor = color;
        centsText.text = text;
        centsText.color = color;
    }
}
