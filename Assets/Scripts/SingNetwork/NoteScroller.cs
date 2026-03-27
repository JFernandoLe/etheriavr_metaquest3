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

    private List<GameObject> activeNotes = new List<GameObject>();

    IEnumerator Start()
    {
        while (songLoader == null || songLoader.loadedSong == null)
            yield return null;

        Debug.Log("NOTES COUNT: " + songLoader.loadedSong.notes.Length);

        SpawnAllNotes();

        songLoader.StartSong();
    }

    void Update()
    {
        if (songLoader == null || songLoader.loadedSong == null)
            return;

        float songTime = songLoader.GetSongTime();

        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            GameObject noteObj = activeNotes[i];
            ScrollingNote sn = noteObj.GetComponent<ScrollingNote>();

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
            }
        }
    }

    void SpawnAllNotes()
    {
        if (songLoader.loadedSong.notes == null || songLoader.loadedSong.notes.Length == 0)
        {
            Debug.LogError("NO HAY NOTAS EN EL JSON");
            return;
        }

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
        }

        Debug.Log("Notas generadas: " + activeNotes.Count);
    }

    public List<GameObject> GetActiveNotes()
    {
        return activeNotes;
    }
}