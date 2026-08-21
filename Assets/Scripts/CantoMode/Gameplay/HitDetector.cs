using UnityEngine;
using TMPro;

public class HitDetector : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public SUDPReceiver receiver;
    public NoteScroller scroller;
    public SongLoader songLoader;
    public TextMeshPro resultText;

    public ParticleSystem perfectEffect;
    public AudioSource sfxSource;
    public AudioClip perfectSound;

    private bool lastWasPerfect;
    private MaterialPropertyBlock propertyBlock;
    private string lastResultText;
    private Color lastResultColor;

    void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (songLoader == null || receiver == null || scroller == null) return;

        float songTime = songLoader.GetSongTime();
        bool foundActiveNote = false;
        bool currentPerfect = false;

        var notes = scroller.GetActiveScrollingNotes();
        var noteObjects = scroller.GetActiveNotes();

        for (int i = 0; i < notes.Count; i++)
        {
            ScrollingNote sn = notes[i];
            if (sn == null) continue;

            float noteStart = sn.startTime;
            float evaluationEnd = noteStart + (sn.duration * 0.7f);

            if (songTime < noteStart || songTime > evaluationEnd) continue;

            foundActiveNote = true;
            GameObject noteObj = i < noteObjects.Count ? noteObjects[i] : sn.gameObject;
            Renderer rend = noteObj != null ? noteObj.GetComponent<Renderer>() : null;

            int playerMidi = receiver.GetCurrentMidi();
            int diff = Mathf.Abs(playerMidi - sn.midi);

            if (diff == 0)
            {
                currentPerfect = true;
                SetNoteColor(rend, Color.green);
                ShowResult("Perfecto", Color.green);
                ScoreManager.Instance.AddScore(10);
                ScoreManager.Instance.RegisterHit(1f);
                ScoreManager.Instance.RegisterRhythm(1f);
            }
            else if (diff == 1)
            {
                SetNoteColor(rend, Color.yellow);
                ShowResult("Regular", Color.yellow);
                ScoreManager.Instance.AddScore(5);
                ScoreManager.Instance.RegisterHit(0.5f);
                ScoreManager.Instance.RegisterRhythm(1f);
            }
            else
            {
                SetNoteColor(rend, Color.red);
                ShowResult("Mal", Color.red);
                ScoreManager.Instance.RegisterHit(0f);
                ScoreManager.Instance.RegisterRhythm(0f);
            }
        }

        if (currentPerfect && !lastWasPerfect)
        {
            if (perfectEffect != null) perfectEffect.Play();
            if (sfxSource != null && perfectSound != null) sfxSource.PlayOneShot(perfectSound);
        }

        lastWasPerfect = currentPerfect;

        if (!foundActiveNote) ShowResult(string.Empty, Color.white);
    }

    void SetNoteColor(Renderer rend, Color color)
    {
        if (rend == null) return;

        propertyBlock ??= new MaterialPropertyBlock();
        rend.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(ColorId, color);
        propertyBlock.SetColor(BaseColorId, color);
        rend.SetPropertyBlock(propertyBlock);
    }

    void ShowResult(string message, Color color)
    {
        if (resultText == null) return;
        if (message == lastResultText && color == lastResultColor) return;

        lastResultText = message;
        lastResultColor = color;
        resultText.text = message;
        resultText.color = color;
    }
}
