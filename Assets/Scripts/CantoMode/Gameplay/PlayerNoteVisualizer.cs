using UnityEngine;
using TMPro;

public class PlayerNoteVisualizer : MonoBehaviour
{
    public SUDPReceiver receiver;

    public float midiHeightMultiplier = 0.1f;
    public float smoothingSpeed = 10f;
    public TextMeshPro playerNoteText;
    private float targetY;

    void Update()
    {
        if (receiver == null)
            return;

        int currentMidi = receiver.GetCurrentMidi();
        if (playerNoteText != null)
        {
            playerNoteText.text = MidiToNoteName(currentMidi);
        }

        if (currentMidi <= 0)
            return;

        targetY = currentMidi * midiHeightMultiplier;

        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, targetY, smoothingSpeed * Time.deltaTime);
        transform.position = pos;
    }
    string MidiToNoteName(int midi)
    {
        if (midi <= 0)
            return "---";

        string[] noteNames = { "C", "C#", "D", "D#", "E", "F",
                           "F#", "G", "G#", "A", "A#", "B" };

        int noteIndex = midi % 12;
        int octave = (midi / 12) - 1;

        return noteNames[noteIndex] + octave;
    }
}