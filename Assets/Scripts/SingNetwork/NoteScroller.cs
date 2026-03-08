using UnityEngine;
using System.Collections.Generic;

public class NoteScroller : MonoBehaviour
{
    public SongLoader songLoader;
    public GameObject notePrefab;

    public float scrollSpeed = 2f;
    public float midiHeightMultiplier = 0.1f;
    public float destroyX = -20f;

    private List<GameObject> activeNotes = new List<GameObject>();

    void Start()
    {
        SpawnAllNotes();
        songLoader.StartSong();
    }

    void Update()
    {
        float songTime = songLoader.GetSongTime();

        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            GameObject noteObj = activeNotes[i];
            ScrollingNote sn = noteObj.GetComponent<ScrollingNote>();

            float noteLength = sn.duration * scrollSpeed;

            //  INICIO REAL DE NOTA
            float startX = (sn.startTime - songTime) * scrollSpeed;

            // COMPENSACIÓN PARA QUE CREZCA HACIA LA DERECHA
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
        foreach (var note in songLoader.loadedSong.notes)
        {
            GameObject obj = Instantiate(notePrefab);

            float yPos = note.midi * midiHeightMultiplier;
            obj.transform.position = new Vector3(0, yPos, 0);

            float noteLength = note.duration * scrollSpeed;

            // Escala correcta
            obj.transform.localScale = new Vector3(noteLength, 0.3f, 0.3f);

            ScrollingNote sn = obj.AddComponent<ScrollingNote>();
            sn.midi = note.midi;
            sn.startTime = note.start;
            sn.duration = note.duration;

            activeNotes.Add(obj);
        }
    }

    public List<GameObject> GetActiveNotes()
    {
        return activeNotes;
    }
}