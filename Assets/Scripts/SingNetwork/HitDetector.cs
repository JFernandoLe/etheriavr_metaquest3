using UnityEngine;
using TMPro;

public class HitDetector : MonoBehaviour
{
    public SUDPReceiver receiver;
    public NoteScroller scroller;
    public SongLoader songLoader;
    public TextMeshPro resultText;

    public ParticleSystem perfectEffect;
    public AudioSource sfxSource;
    public AudioClip perfectSound;

    bool lastWasPerfect = false;

    void Update()
    {
        if (songLoader == null || receiver == null)
            return;

        float songTime = songLoader.GetSongTime();

        bool foundActiveNote = false;
        bool currentPerfect = false;

        foreach (GameObject noteObj in scroller.GetActiveNotes())
        {
            ScrollingNote sn = noteObj.GetComponent<ScrollingNote>();
            if (sn == null)
                continue;

            float noteStart = sn.startTime;
            float noteEnd = sn.startTime + sn.duration;

            float evaluationEnd = noteStart + (sn.duration * 0.7f);

            if (songTime >= noteStart && songTime <= evaluationEnd)
            {
                foundActiveNote = true;

                int playerMidi = receiver.GetCurrentMidi();
                int diff = Mathf.Abs(playerMidi - sn.midi);

                Renderer rend = noteObj.GetComponent<Renderer>();

                if (diff == 0)
                {
                    currentPerfect = true;

                    rend.material.color = Color.green;
                    ShowResult("Perfecto", Color.green);

                    ScoreManager.Instance.AddScore(10);
                    ScoreManager.Instance.RegisterHit(1f);
                    ScoreManager.Instance.RegisterRhythm(1f);
                }
                else if (diff == 1)
                {
                    rend.material.color = Color.yellow;
                    ShowResult("Regular", Color.yellow);

                    ScoreManager.Instance.AddScore(5);
                    ScoreManager.Instance.RegisterHit(0.5f);
                    ScoreManager.Instance.RegisterRhythm(1f);
                }
                else
                {
                    rend.material.color = Color.red;
                    ShowResult("Mal", Color.red);

                    ScoreManager.Instance.RegisterHit(0f);
                    ScoreManager.Instance.RegisterRhythm(0f);
                }
            }
        }

        if (currentPerfect && !lastWasPerfect)
        {
            if (perfectEffect != null)
                perfectEffect.Play();

            if (sfxSource != null && perfectSound != null)
                sfxSource.PlayOneShot(perfectSound);
        }

        lastWasPerfect = currentPerfect;

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