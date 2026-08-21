using UnityEngine;
using TMPro;

public class PlayerNoteVisualizer : MonoBehaviour
{
    public VocalPitchAnalyzer receiver;

    public float midiHeightMultiplier = 0.1f;
    public float smoothingSpeed = 10f;
    public TextMeshPro playerNoteText;
    private float targetY;
    private string lastNoteName;

    void Awake()
    {
        if (receiver == null)
            receiver = FindObjectOfType<VocalPitchAnalyzer>();
    }

    void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (receiver == null) return;

        int currentMidi = receiver.GetCurrentMidi();
        if (playerNoteText != null)
        {
            string noteName = MidiToNoteName(currentMidi);
            if (noteName != lastNoteName)
            {
                lastNoteName = noteName;
                playerNoteText.text = noteName;
            }
        }

        if (currentMidi <= 0) return;

        targetY = currentMidi * midiHeightMultiplier;
        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, targetY, smoothingSpeed * Time.deltaTime);
        transform.position = pos;
    }

    static string MidiToNoteName(int midi)
    {
        if (midi <= 0) return "---";
        return MusicalNoteUtility.MidiToNoteName(midi);
    }
}
