using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Orquestador de transcripción musical offline.
/// Delega el análisis a MelodyAnalysisPipeline y produce SongData + diagnósticos.
/// </summary>
public static class MelodyTranscriber
{
    public struct TranscriptionResult
    {
        public SongData Song;
        public float EstimatedBpm;
        public int NoteCount;
        public string SourceAudioPath;
        public MelodyKeyEstimate EstimatedKey;
        public TranscriptionDebugInfo DebugInfo;
        public List<NoteEvent> NoteEvents;
    }

    public struct TranscriptionDebugInfo
    {
        public float DurationSeconds;
        public int SourceSampleRate;
        public int SourceChannels;
        public int TotalSourceSamples;
        public int ResampledSamples;
        public int FramesAnalyzed;
        public int ValidPitchFrames;
        public int InvalidPitchFrames;
        public int NotesDetected;
        public int NotesDiscarded;
        public float GlobalRms;
        public float PeakAmplitude;
        public float AveragePitchMidi;
        public int MinDetectedMidi;
        public int MaxDetectedMidi;
        public string EstimatedKey;
        public float KeyConfidence;
    }

    public static TranscriptionResult Transcribe(AudioClip clip, string songName, string sourcePath = null)
    {
        if (clip == null)
            throw new ArgumentNullException(nameof(clip));

        var debug = new TranscriptionDebugInfo
        {
            DurationSeconds = clip.length,
            SourceSampleRate = clip.frequency,
            SourceChannels = clip.channels,
            TotalSourceSamples = clip.samples
        };

        float[] mono = ExtractMonoSamples(clip, ref debug);
        return TranscribeFromMono(mono, clip.length, songName, sourcePath, ref debug);
    }

    /// <summary>Transcripción sobre buffer mono ya extraído (seguro para hilos en segundo plano).</summary>
    public static TranscriptionResult TranscribeFromMono(float[] mono, float durationSeconds, string songName,
        string sourcePath, ref TranscriptionDebugInfo debug)
    {
        if (mono == null || mono.Length == 0)
            throw new ArgumentException("Buffer mono vacío.", nameof(mono));

        debug.GlobalRms = ComputeRms(mono);

        MelodyAnalysisPipeline.PipelineResult pipeline = MelodyAnalysisPipeline.Analyze(mono, durationSeconds, ref debug);
        debug.InvalidPitchFrames = debug.FramesAnalyzed - debug.ValidPitchFrames;

        var songNotes = new List<SongNote>();
        ComputePitchRange(pipeline.NoteEvents, ref debug);

        foreach (NoteEvent ev in pipeline.NoteEvents)
        {
            songNotes.Add(new SongNote
            {
                note = ev.noteName,
                midi = ev.midiRounded,
                start = ev.startTime,
                duration = ev.duration
            });
        }

        return new TranscriptionResult
        {
            Song = new SongData
            {
                songName = songName,
                songDuration = durationSeconds,
                notes = songNotes.ToArray(),
                lyrics = Array.Empty<LyricLine>()
            },
            EstimatedBpm = pipeline.EstimatedBpm,
            NoteCount = songNotes.Count,
            SourceAudioPath = sourcePath,
            EstimatedKey = pipeline.Key,
            DebugInfo = debug,
            NoteEvents = pipeline.NoteEvents
        };
    }

    public static void LogTranscriptionSummary(string songName, TranscriptionResult result)
    {
        LogTranscriptionSummary(songName, result.DebugInfo, new List<SongNote>(result.Song.notes), result.EstimatedKey);
    }

    public static string SaveTranscription(TranscriptionResult result, string outputDirectory, string fileId = null)
    {
        Directory.CreateDirectory(outputDirectory);
        string baseName = string.IsNullOrWhiteSpace(fileId)
            ? SanitizeFileName(result.Song.songName)
            : fileId;
        string jsonPath = Path.Combine(outputDirectory, baseName + ".json");
        File.WriteAllText(jsonPath, JsonUtility.ToJson(result.Song, true));
        SaveDebugLog(result, outputDirectory, baseName);
        return jsonPath;
    }

    public static void SaveDebugLog(TranscriptionResult result, string outputDirectory, string safeName = null)
    {
        safeName ??= SanitizeFileName(result.Song.songName);
        var sb = new StringBuilder();
        TranscriptionDebugInfo d = result.DebugInfo;

        sb.AppendLine("=== MelodyTranscriber Debug ===");
        sb.AppendLine($"Canción: {result.Song.songName}");
        sb.AppendLine($"Duración: {d.DurationSeconds:F2} s");
        sb.AppendLine($"Sample rate origen: {d.SourceSampleRate} Hz");
        sb.AppendLine($"Canales origen: {d.SourceChannels}");
        sb.AppendLine($"Samples origen: {d.TotalSourceSamples}");
        sb.AppendLine($"Samples resampleados: {d.ResampledSamples}");
        sb.AppendLine($"RMS global: {d.GlobalRms:F6}");
        sb.AppendLine($"Pico: {d.PeakAmplitude:F4}");
        sb.AppendLine($"Frames analizados: {d.FramesAnalyzed}");
        sb.AppendLine($"Frames con pitch válido: {d.ValidPitchFrames}");
        sb.AppendLine($"Frames sin pitch: {d.InvalidPitchFrames}");
        sb.AppendLine($"Notas detectadas: {d.NotesDetected}");
        sb.AppendLine($"Notas descartadas: {d.NotesDiscarded}");
        sb.AppendLine($"Pitch promedio (MIDI): {d.AveragePitchMidi:F2}");
        sb.AppendLine($"Rango melódico: {d.MinDetectedMidi} - {d.MaxDetectedMidi}");
        sb.AppendLine($"Tonalidad estimada: {d.EstimatedKey} (conf={d.KeyConfidence:F2})");
        sb.AppendLine($"BPM estimado: {result.EstimatedBpm:F1}");
        sb.AppendLine();
        sb.AppendLine("Limitación: YIN monofónico sobre mezcla completa; bajo/acordes pueden dominar.");
        sb.AppendLine("--- Notas finales ---");

        if (result.Song.notes != null)
        {
            foreach (SongNote note in result.Song.notes)
                sb.AppendLine($"[{FormatTime(note.start)}] {note.note} (MIDI {note.midi}) dur={note.duration:F3}s");
        }

        string logPath = Path.Combine(outputDirectory, safeName + ".transcription.log");
        File.WriteAllText(logPath, sb.ToString());
        Debug.Log($"[MelodyTranscriber] Log guardado: {logPath}");
    }

    private static void ComputePitchRange(List<NoteEvent> events, ref TranscriptionDebugInfo debug)
    {
        if (events == null || events.Count == 0)
            return;

        float sum = 0f;
        int min = 127;
        int max = 0;
        foreach (NoteEvent ev in events)
        {
            sum += ev.pitchMidi;
            min = Math.Min(min, ev.midiRounded);
            max = Math.Max(max, ev.midiRounded);
        }

        debug.AveragePitchMidi = sum / events.Count;
        debug.MinDetectedMidi = min;
        debug.MaxDetectedMidi = max;
    }

    public static float[] ExtractMonoSamplesPublic(AudioClip clip, ref TranscriptionDebugInfo debug) =>
        ExtractMonoSamples(clip, ref debug);

    private static float[] ExtractMonoSamples(AudioClip clip, ref TranscriptionDebugInfo debug)
    {
        float[] raw = new float[clip.samples * clip.channels];
        clip.GetData(raw, 0);

        int monoLength = clip.samples;
        float[] mono = new float[monoLength];
        if (clip.channels >= 2)
        {
            for (int i = 0; i < monoLength; i++)
            {
                float left = raw[i * clip.channels];
                float right = raw[i * clip.channels + 1];
                float mid = (left + right) * 0.5f;
                float side = (left - right) * 0.5f;
                mono[i] = mid * 1.25f - side * 0.55f;
            }
        }
        else
        {
            for (int i = 0; i < monoLength; i++)
                mono[i] = raw[i];
        }

        if (clip.frequency != MelodyAnalysisPipeline.AnalysisSampleRate)
            mono = Resample(mono, clip.frequency, MelodyAnalysisPipeline.AnalysisSampleRate);

        debug.ResampledSamples = mono.Length;
        return mono;
    }

    private static float[] Resample(float[] input, int sourceRate, int targetRate)
    {
        if (sourceRate == targetRate)
            return input;

        int outputLength = Mathf.RoundToInt(input.Length * (targetRate / (float)sourceRate));
        float[] output = new float[outputLength];
        float ratio = (input.Length - 1f) / Mathf.Max(1, outputLength - 1);

        for (int i = 0; i < outputLength; i++)
        {
            float srcIndex = i * ratio;
            int idx = Mathf.FloorToInt(srcIndex);
            float frac = srcIndex - idx;
            float a = input[idx];
            float b = input[Mathf.Min(idx + 1, input.Length - 1)];
            output[i] = Mathf.Lerp(a, b, frac);
        }

        return output;
    }

    private static float ComputeRms(float[] samples)
    {
        if (samples == null || samples.Length == 0)
            return 0f;

        double sum = 0d;
        for (int i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];
        return (float)Math.Sqrt(sum / samples.Length);
    }

    private static void LogTranscriptionSummary(string songName, TranscriptionDebugInfo debug, List<SongNote> notes, MelodyKeyEstimate key)
    {
        Debug.Log($"[MelodyTranscriber] '{songName}' dur={debug.DurationSeconds:F1}s frames={debug.FramesAnalyzed} " +
                  $"valid={debug.ValidPitchFrames} notes={debug.NotesDetected} discarded={debug.NotesDiscarded} " +
                  $"key={key.keyName}({key.confidence:F2}) bpm-range={debug.MinDetectedMidi}-{debug.MaxDetectedMidi}");

        int previewCount = Mathf.Min(notes.Count, 15);
        for (int i = 0; i < previewCount; i++)
        {
            SongNote n = notes[i];
            Debug.Log($"[MelodyTranscriber] [{FormatTime(n.start)}] {n.note} dur={n.duration:F3}s");
        }
    }

    private static string FormatTime(float seconds)
    {
        int mins = (int)(seconds / 60f);
        float secs = seconds - mins * 60f;
        return $"{mins:00}:{secs:00.00}";
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }
}
