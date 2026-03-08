using UnityEngine;
using TMPro;

public class ExpectedNoteDisplay : MonoBehaviour
{
    public SongLoader songLoader;
    public TextMeshPro expectedNoteText;

    void Update()
    {
        if (songLoader == null || expectedNoteText == null)
            return;

        SongNote note = songLoader.GetCurrentExpectedNote();

        if (note != null)
            expectedNoteText.text = note.note;
        else
            expectedNoteText.text = "";
    }
}