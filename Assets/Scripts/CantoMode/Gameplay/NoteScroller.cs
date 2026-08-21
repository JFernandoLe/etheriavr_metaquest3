using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NoteScroller : MonoBehaviour
{
    public SongLoader songLoader;
    public GameObject notePrefab;

    public float scrollSpeed = 2f;
    public float midiHeightMultiplier = 0.1f;
    public float destroyX = -20f;

    private readonly List<GameObject> activeNotes = new List<GameObject>();
    private readonly List<ScrollingNote> activeScrollingNotes = new List<ScrollingNote>();

    IEnumerator Start()
    {
        while (songLoader == null || songLoader.loadedSong == null)
            yield return null;

        SpawnAllNotes();
        songLoader.StartSong();
    }

    void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (songLoader == null || songLoader.loadedSong == null) return;

        float songTime = songLoader.GetSongTime();

        for (int i = activeScrollingNotes.Count - 1; i >= 0; i--)
        {
            ScrollingNote sn = activeScrollingNotes[i];
            if (sn == null)
            {
                activeScrollingNotes.RemoveAt(i);
                if (i < activeNotes.Count) activeNotes.RemoveAt(i);
                continue;
            }

            GameObject noteObj = activeNotes[i];
            float noteLength = sn.duration * scrollSpeed;
            float startX = (sn.startTime - songTime) * scrollSpeed;
            float correctedX = startX + noteLength / 2f;

            Vector3 pos = noteObj.transform.position;
            pos.x = correctedX;
            noteObj.transform.position = pos;

            if (pos.x < destroyX)
            {
                Destroy(noteObj);
                activeNotes.RemoveAt(i);
                activeScrollingNotes.RemoveAt(i);
            }
        }
    }

    void SpawnAllNotes()
    {
        if (songLoader.loadedSong.notes == null || songLoader.loadedSong.notes.Length == 0)
            return;

        foreach (var note in songLoader.loadedSong.notes)
        {
            GameObject obj = Instantiate(notePrefab);

            float yPos = note.midi * midiHeightMultiplier;
            obj.transform.position = new Vector3(0, yPos, 0);

            float noteLength = note.duration * scrollSpeed;
            obj.transform.localScale = new Vector3(noteLength, 0.3f, 0.3f);

            ScrollingNote sn = obj.AddComponent<ScrollingNote>();
            sn.midi = note.midi;
            sn.startTime = note.start;
            sn.duration = note.duration;

            activeNotes.Add(obj);
            activeScrollingNotes.Add(sn);
        }
    }

    public List<GameObject> GetActiveNotes() => activeNotes;

    public List<ScrollingNote> GetActiveScrollingNotes() => activeScrollingNotes;
}
