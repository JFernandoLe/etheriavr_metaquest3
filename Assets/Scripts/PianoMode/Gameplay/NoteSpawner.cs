using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Genera notas musicales en el momento correcto según los datos de la canción.
/// </summary>
public class NoteSpawner : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject notePrefab;
    [SerializeField] private StaffRenderer trebleStaff;
    [SerializeField] private StaffRenderer bassStaff;

    [Header("Configuración")]
    [Tooltip("Tiempo visible antes de tocar la hit line")]
    [SerializeField] private float noteTravelTime = 4.5f;

    private PianoSongData currentSong;
    private List<PianoNoteData> allNotes = new List<PianoNoteData>();
    private int nextNoteIndex = 0;
    private bool isSpawning = false;
    private float songStartTime;
    private float currentSongTime = 0f;
    private readonly List<MusicNote> previewNotes = new List<MusicNote>();
    private readonly List<MusicNote> spawnedNotes = new List<MusicNote>(64);
    private MidiAudioManager cachedMidiAudio;
    private GameplayScoring cachedScoring;

    /// <summary>
    /// Carga la canción y prepara las notas para spawn.
    /// Soporta ambos formatos: all_notes (nuevo) y melody (antiguo).
    /// </summary>
    public void LoadSong(PianoSongData songData)
    {
        currentSong = songData;
        allNotes.Clear();
        nextNoteIndex = 0;

        if (songData.all_notes != null && songData.all_notes.Count > 0)
        {
            // Cada GameNoteData puede contener varias notas MIDI simultáneas (acorde).
            foreach (GameNoteData gameNote in songData.all_notes)
            {
                if (gameNote.midi_notes == null) continue;

                foreach (int midiNote in gameNote.midi_notes)
                {
                    allNotes.Add(new PianoNoteData
                    {
                        midi = midiNote,
                        time = gameNote.time,
                        duration = gameNote.duration,
                        hand = GetHandForMidiNote(midiNote)
                    });
                }
            }
        }
        else if (songData.melody != null && songData.melody.Count > 0)
        {
            allNotes.AddRange(songData.melody);
        }
        else
        {
            Debug.LogError("[NoteSpawner] No hay notas para cargar: all_notes y melody están vacíos.");
            return;
        }

        NormalizeHandsByMidiSplit();
        allNotes.Sort((a, b) => a.time.CompareTo(b.time));
        EnsureSharedManagers();
    }

    private void EnsureSharedManagers()
    {
        if (cachedMidiAudio == null) cachedMidiAudio = FindObjectOfType<MidiAudioManager>();
        if (cachedScoring == null) cachedScoring = FindObjectOfType<GameplayScoring>();
        MusicNote.BindSharedManagers(cachedMidiAudio, cachedScoring);
    }

    private void NormalizeHandsByMidiSplit()
    {
        foreach (PianoNoteData note in allNotes)
            note.hand = GetHandForMidiNote(note.midi);
    }

    private string GetHandForMidiNote(int midiNote) => midiNote >= 60 ? "right" : "left";

    private StaffRenderer GetStaffForHand(string hand) => hand == "right" ? trebleStaff : bassStaff;

    private float SafeTravelTime => Mathf.Max(noteTravelTime, 0.1f);

    public void StartSpawning() => StartSpawningInternal(true);

    public void ResumeSpawning() => StartSpawningInternal(false);

    private void StartSpawningInternal(bool resetProgress)
    {
        if (currentSong == null)
        {
            Debug.LogError("[NoteSpawner] No hay canción cargada!");
            return;
        }

        isSpawning = true;
        if (!resetProgress) return;

        ClearPreviewNotes();
        ClearAllNotes();
        songStartTime = Time.time;
        currentSongTime = 0f;
        nextNoteIndex = 0;
    }

    public void StopSpawning() => isSpawning = false;

    void Update()
    {
        if (!isSpawning) return;

        // Sin pista de fondo, el reloj del juego (gameTime) es la fuente de verdad.
        PianoGameManager gameManager = PianoGameManager.Instance;
        currentSongTime = gameManager != null ? gameManager.GetSongPlaybackTime() : Time.time - songStartTime;

        while (nextNoteIndex < allNotes.Count)
        {
            PianoNoteData note = allNotes[nextNoteIndex];

            if (GetStaffForHand(note.hand) == null)
            {
                nextNoteIndex++;
                continue;
            }

            if (currentSongTime < note.time - SafeTravelTime) break;

            SpawnNoteInternal(note, currentSongTime, false);
            nextNoteIndex++;
        }

        if (nextNoteIndex >= allNotes.Count) StopSpawning();
    }

    public void ShowPreviewNotes(float previewSongTime = 0f)
    {
        if (currentSong == null) return;

        ClearPreviewNotes();

        float previewLookAhead = SafeTravelTime;
        foreach (PianoNoteData note in allNotes)
        {
            if (note.time > previewSongTime + previewLookAhead) break;

            MusicNote previewNote = SpawnNoteInternal(note, previewSongTime, true);
            if (previewNote != null) previewNotes.Add(previewNote);
        }
    }

    /// <summary>Instancia una nota como hija del pentagrama correspondiente.</summary>
    private MusicNote SpawnNoteInternal(PianoNoteData noteData, float referenceSongTime, bool previewMode)
    {
        StaffRenderer targetStaff = GetStaffForHand(noteData.hand);
        if (targetStaff == null)
        {
            Debug.LogError($"[NoteSpawner] No hay pentagrama para mano {noteData.hand}");
            return null;
        }

        GameObject noteObj = Instantiate(notePrefab);
        MusicNote note = noteObj.GetComponent<MusicNote>();
        if (note == null)
        {
            Debug.LogError("[NoteSpawner] El prefab no tiene componente MusicNote!");
            Destroy(noteObj);
            return null;
        }

        // La nota se parenta al pentagrama para desplazarse junto a él.
        noteObj.transform.SetParent(targetStaff.transform, false);

        Vector3 spawnPos = targetStaff.transform.InverseTransformPoint(targetStaff.GetSpawnPoint());
        float noteY = targetStaff.GetNoteYPosition(noteData.midi);
        spawnPos.y = noteY;
        targetStaff.CreateLedgerLinesForNote(noteY);

        Vector3 hitPos = targetStaff.transform.InverseTransformPoint(targetStaff.GetHitPoint());
        hitPos.y = spawnPos.y;

        noteObj.transform.localPosition = spawnPos;

        // La velocidad se deriva de la distancia real spawn->hit para respetar noteTravelTime.
        float distance = Vector3.Distance(targetStaff.GetSpawnPoint(), targetStaff.GetHitPoint());
        EnsureSharedManagers();
        note.Initialize(noteData, spawnPos, hitPos, distance / SafeTravelTime);

        if (previewMode) note.SetPreviewPose(referenceSongTime);
        else spawnedNotes.Add(note);

        return note;
    }

    /// <summary>Destruye todas las notas activas (para reset o salida).</summary>
    public void ClearAllNotes()
    {
        for (int i = spawnedNotes.Count - 1; i >= 0; i--)
        {
            if (spawnedNotes[i] != null) Destroy(spawnedNotes[i].gameObject);
        }

        spawnedNotes.Clear();
    }

    public void ClearPreviewNotes()
    {
        for (int i = previewNotes.Count - 1; i >= 0; i--)
        {
            if (previewNotes[i] != null) Destroy(previewNotes[i].gameObject);
        }

        previewNotes.Clear();
    }

    void OnDisable() => StopSpawning();
}
