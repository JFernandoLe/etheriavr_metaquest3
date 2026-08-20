using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Transcripción monofónica offline: analiza un AudioClip y extrae la melodía principal.
/// Alternativa viable a MP3→MIDI completo en Quest (polifónico es demasiado costoso).
/// </summary>
public static class MelodyTranscriber
{
    private const int AnalysisSampleRate = 22050;
    private const int FrameSize = 2048;
    private const int HopSize = 512;
    private const float MinNoteDuration = 0.12f;
    private const float MinEnergy = 0.00008f;
    private const float YinThreshold = 0.12f;

    public struct TranscriptionResult
    {
        public SongData Song;
        public float EstimatedBpm;
        public int NoteCount;
        public string SourceAudioPath;
    }

    public static TranscriptionResult Transcribe(AudioClip clip, string songName, string sourcePath = null)
    {
        if (clip == null)
            throw new ArgumentNullException(nameof(clip));

        float[] mono = ExtractMonoSamples(clip);
        var notes = new List<SongNote>();
        var onsets = new List<float>();

        int totalFrames = Math.Max(0, (mono.Length - FrameSize) / HopSize);
        int lastMidi = -1;
        float segmentStart = 0f;
        float segmentDuration = 0f;
        float lastValidTime = 0f;

        for (int frame = 0; frame < totalFrames; frame++)
        {
            int offset = frame * HopSize;
            float[] window = new float[FrameSize];
            Array.Copy(mono, offset, window, 0, FrameSize);

            float time = offset / (float)AnalysisSampleRate;
            float energy = YinPitchDetector.ComputeEnergy(window);
            if (energy < MinEnergy)
            {
                FlushSegment(notes, ref lastMidi, ref segmentStart, ref segmentDuration, time);
                continue;
            }

            float pitch = YinPitchDetector.DetectPitch(window, AnalysisSampleRate, YinThreshold);
            if (pitch <= 0f)
            {
                FlushSegment(notes, ref lastMidi, ref segmentStart, ref segmentDuration, time);
                continue;
            }

            int midi = MusicalNoteUtility.RoundMidi(MusicalNoteUtility.HzToMidi(pitch));
            if (midi < 36 || midi > 96)
                continue;

            float hopSeconds = HopSize / (float)AnalysisSampleRate;

            if (lastMidi == -1)
            {
                lastMidi = midi;
                segmentStart = time;
                segmentDuration = hopSeconds;
                lastValidTime = time;
                onsets.Add(time);
                continue;
            }

            if (midi == lastMidi || Math.Abs(midi - lastMidi) <= 1)
            {
                segmentDuration += hopSeconds;
                lastValidTime = time;
                if (midi != lastMidi)
                    lastMidi = midi;
            }
            else
            {
                FlushSegment(notes, ref lastMidi, ref segmentStart, ref segmentDuration, time);
                lastMidi = midi;
                segmentStart = time;
                segmentDuration = hopSeconds;
                onsets.Add(time);
                lastValidTime = time;
            }
        }

        FlushSegment(notes, ref lastMidi, ref segmentStart, ref segmentDuration, clip.length);

        float bpm = EstimateBpm(onsets);

        return new TranscriptionResult
        {
            Song = new SongData
            {
                songName = songName,
                songDuration = clip.length,
                notes = notes.ToArray(),
                lyrics = Array.Empty<LyricLine>()
            },
            EstimatedBpm = bpm,
            NoteCount = notes.Count,
            SourceAudioPath = sourcePath
        };
    }

    public static string SaveTranscription(TranscriptionResult result, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        string safeName = SanitizeFileName(result.Song.songName);
        string jsonPath = Path.Combine(outputDirectory, safeName + ".json");
        string json = JsonUtility.ToJson(result.Song, true);
        File.WriteAllText(jsonPath, json);
        return jsonPath;
    }

    private static float[] ExtractMonoSamples(AudioClip clip)
    {
        float[] raw = new float[clip.samples * clip.channels];
        clip.GetData(raw, 0);

        if (clip.channels == 1 && clip.frequency == AnalysisSampleRate)
            return raw;

        int monoLength = clip.samples;
        float[] mono = new float[monoLength];
        for (int i = 0; i < monoLength; i++)
        {
            float sum = 0f;
            for (int c = 0; c < clip.channels; c++)
                sum += raw[i * clip.channels + c];
            mono[i] = sum / clip.channels;
        }

        if (clip.frequency == AnalysisSampleRate)
            return mono;

        return Resample(mono, clip.frequency, AnalysisSampleRate);
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

    private static void FlushSegment(List<SongNote> notes, ref int lastMidi, ref float segmentStart, ref float segmentDuration, float time)
    {
        if (lastMidi < 0 || segmentDuration < MinNoteDuration)
        {
            lastMidi = -1;
            segmentDuration = 0f;
            return;
        }

        notes.Add(new SongNote
        {
            note = MusicalNoteUtility.MidiToNoteName(lastMidi),
            midi = lastMidi,
            start = segmentStart,
            duration = segmentDuration
        });

        lastMidi = -1;
        segmentDuration = 0f;
    }

    private static float EstimateBpm(List<float> onsets)
    {
        if (onsets.Count < 4)
            return 0f;

        var intervals = new List<float>();
        for (int i = 1; i < onsets.Count; i++)
        {
            float delta = onsets[i] - onsets[i - 1];
            if (delta >= 0.25f && delta <= 2f)
                intervals.Add(delta);
        }

        if (intervals.Count == 0)
            return 0f;

        intervals.Sort();
        float median = intervals[intervals.Count / 2];
        return median > 0f ? 60f / median : 0f;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }
}
