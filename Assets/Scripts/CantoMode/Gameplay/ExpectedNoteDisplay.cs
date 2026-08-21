using UnityEngine;
using TMPro;

public class ExpectedNoteDisplay : MonoBehaviour
{
    public SongLoader songLoader;
    public TextMeshPro expectedNoteText;
    private string lastText;

    void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (songLoader == null || expectedNoteText == null) return;

        SongNote note = songLoader.GetCurrentExpectedNote();
        string text = note != null ? note.note : string.Empty;
        if (text == lastText) return;

        lastText = text;
        expectedNoteText.text = text;
    }
}
