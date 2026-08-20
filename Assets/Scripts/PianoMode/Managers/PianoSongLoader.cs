using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

/// <summary>
/// Carga charts de piano desde MIDI en StreamingAssets/PianoSongs/Songs/.
/// El file_path de la BD es solo el nombre del archivo (ej: furelise.mid).
/// </summary>
public class PianoSongLoader : MonoBehaviour
{
    private const float ChordGroupEpsilonSeconds = 0.03f;

    /// <param name="fileName">Nombre del archivo MIDI desde la BD (ej: "furelise.mid").</param>
    public void LoadSong(string fileName, System.Action<PianoSongData> onSuccess, System.Action<string> onError) =>
        StartCoroutine(LoadSongCoroutine(fileName, onSuccess, onError));

    private IEnumerator LoadSongCoroutine(string fileName, System.Action<PianoSongData> onSuccess, System.Action<string> onError)
    {
        string assetName = SongAssetPaths.GetAssetFileName(fileName);
        if (string.IsNullOrWhiteSpace(assetName))
        {
            onError?.Invoke("file_path de canción vacío o inválido");
            yield break;
        }

        string midiPath = SongAssetPaths.GetPianoMidiPath(assetName);
        byte[] midiBytes = null;

#if UNITY_ANDROID && !UNITY_EDITOR
        using (UnityWebRequest www = UnityWebRequest.Get(midiPath))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[PianoLoader] Error leyendo {midiPath}: {www.error}");
                onError?.Invoke($"Error leyendo MIDI: {www.error}");
                yield break;
            }

            midiBytes = www.downloadHandler.data;
        }
#else
        if (!File.Exists(midiPath))
        {
            Debug.LogError($"[PianoLoader] Archivo no existe: {midiPath}");
            onError?.Invoke($"Archivo no encontrado: {midiPath}");
            yield break;
        }

        try
        {
            midiBytes = File.ReadAllBytes(midiPath);
        }
        catch (System.Exception e)
        {
            onError?.Invoke($"Error leyendo MIDI: {e.Message}");
            yield break;
        }
#endif

        PianoSongData songData;
        try
        {
            songData = BuildSongDataFromMidi(midiBytes, assetName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PianoLoader] Error parseando MIDI: {e.Message}");
            onError?.Invoke($"Error parseando MIDI: {e.Message}");
            yield break;
        }

        if (songData.all_notes == null || songData.all_notes.Count <= 0)
        {
            onError?.Invoke("El MIDI no contiene notas reproducibles");
            yield break;
        }

        Debug.Log($"[PianoLoader] MIDI cargado: {assetName} | notas agrupadas={songData.all_notes.Count} | duración={songData.duration:F1}s | tempo={songData.tempo}");
        onSuccess?.Invoke(songData);
    }

    private static PianoSongData BuildSongDataFromMidi(byte[] midiBytes, string assetName)
    {
        using (MemoryStream stream = new MemoryStream(midiBytes))
        {
            MidiFile midiFile = MidiFile.Read(stream);
            TempoMap tempoMap = midiFile.GetTempoMap();
            ICollection<Note> midiNotes = midiFile.GetNotes();

            List<TimedMidiNote> timedNotes = new List<TimedMidiNote>(midiNotes.Count);
            foreach (Note note in midiNotes)
            {
                MetricTimeSpan start = note.TimeAs<MetricTimeSpan>(tempoMap);
                MetricTimeSpan length = note.LengthAs<MetricTimeSpan>(tempoMap);
                float timeSec = (float)start.TotalSeconds;
                float durationSec = Mathf.Max(0.05f, (float)length.TotalSeconds);

                timedNotes.Add(new TimedMidiNote
                {
                    time = timeSec,
                    duration = durationSec,
                    midi = (int)note.NoteNumber
                });
            }

            timedNotes.Sort((a, b) => a.time.CompareTo(b.time));

            List<GameNoteData> allNotes = GroupIntoGameNotes(timedNotes);
            float songDuration = 0f;
            foreach (GameNoteData gameNote in allNotes)
                songDuration = Mathf.Max(songDuration, gameNote.time + gameNote.duration);

            int tempoBpm = 120;
            Tempo tempoAtStart = tempoMap.GetTempoAtTime(new MidiTimeSpan(0));
            if (tempoAtStart != null)
                tempoBpm = Mathf.RoundToInt((float)tempoAtStart.BeatsPerMinute);

            string title = Path.GetFileNameWithoutExtension(assetName);

            return new PianoSongData
            {
                song_title = title,
                song_name = title,
                tempo = tempoBpm,
                duration = songDuration,
                recorded_duration = songDuration,
                piano_volume = 1f,
                audio_file_volume = 1f,
                all_notes = allNotes,
                melody = new List<PianoNoteData>(),
                chords = new List<PianoChordData>()
            };
        }
    }

    private static List<GameNoteData> GroupIntoGameNotes(List<TimedMidiNote> timedNotes)
    {
        List<GameNoteData> result = new List<GameNoteData>();
        if (timedNotes == null || timedNotes.Count == 0) return result;

        int index = 0;
        while (index < timedNotes.Count)
        {
            TimedMidiNote first = timedNotes[index];
            List<int> pitches = new List<int> { first.midi };
            float maxDuration = first.duration;
            int lookAhead = index + 1;

            while (lookAhead < timedNotes.Count &&
                   timedNotes[lookAhead].time - first.time <= ChordGroupEpsilonSeconds)
            {
                pitches.Add(timedNotes[lookAhead].midi);
                maxDuration = Mathf.Max(maxDuration, timedNotes[lookAhead].duration);
                lookAhead++;
            }

            pitches.Sort();
            bool isChord = pitches.Count > 1;
            int representative = pitches[pitches.Count / 2];

            result.Add(new GameNoteData
            {
                time = first.time,
                duration = maxDuration,
                midi_notes = pitches.ToArray(),
                clef = representative >= 60 ? "treble" : "bass",
                is_chord = isChord
            });

            index = lookAhead;
        }

        return result;
    }

    public bool SongExists(string fileName)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return !string.IsNullOrWhiteSpace(fileName);
#else
        string assetName = SongAssetPaths.GetAssetFileName(fileName);
        return !string.IsNullOrWhiteSpace(assetName) && File.Exists(SongAssetPaths.GetPianoMidiPath(assetName));
#endif
    }

    private struct TimedMidiNote
    {
        public float time;
        public float duration;
        public int midi;
    }
}
