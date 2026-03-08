using UnityEngine;
using TMPro;

public class HitDetector : MonoBehaviour
{
    public SUDPReceiver receiver;
    public NoteScroller scroller;
    public SongLoader songLoader;
    public TextMeshPro resultText;

    void Update()
    {
        if (songLoader == null || receiver == null)
            return;

        float songTime = songLoader.GetSongTime();

        bool foundActiveNote = false;

        foreach (GameObject noteObj in scroller.GetActiveNotes())
        {
            ScrollingNote sn = noteObj.GetComponent<ScrollingNote>();
            if (sn == null)
                continue;

            float noteStart = sn.startTime;
            float noteEnd = sn.startTime + sn.duration;

            if (songTime >= noteStart && songTime <= noteEnd)
            {
                foundActiveNote = true;

                int playerMidi = receiver.GetCurrentMidi();
                int diff = Mathf.Abs(playerMidi - sn.midi);

                Renderer rend = noteObj.GetComponent<Renderer>();

                if (diff == 0)
                {
                    //  PERFECTO
                    rend.material.color = Color.green;
                    ShowResult("Perfecto", Color.green);
                }
                else if (diff == 1)
                {
                    //  REGULAR
                    rend.material.color = Color.yellow;
                    ShowResult("Regular", Color.yellow);
                }
                else
                {
                    //  MAL
                    rend.material.color = Color.red;
                    ShowResult("Mal", Color.red);
                }
            }
        }

        // Si no hay nota activa, limpiar texto
        if (!foundActiveNote && resultText != null)
        {
            resultText.text = "";
        }
    }

    void ShowResult(string message, Color color)
    {
        if (resultText == null)
            return;

        resultText.text = message;
        resultText.color = color;
    }
}