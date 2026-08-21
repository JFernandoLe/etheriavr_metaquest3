using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Pipeline de transcripción musical: preprocesado → pitch continuo → segmentación → corrección.
/// </summary>
public static class MelodyAnalysisPipeline
{
    public const int AnalysisSampleRate = 22050;
    public const int FrameSize = 2048;
    public const int HopSize = 256;

    private const float NoteChangeCentsThreshold = 40f;
    private const float VibratoCentsThreshold = 35f;
    private const float MinNoteDuration = 0.06f;
    private const float OnsetFluxMultiplier = 2.5f;
    private const float OffsetEnergyRatio = 0.35f;
    private const int MinMelodyMidi = 40;
    private const int MaxMelodyMidi = 96;

    public struct PipelineResult
    {
        public List<NoteEvent> NoteEvents;
        public MelodyKeyEstimate Key;
        public float EstimatedBpm;
        public MelodyTranscriber.TranscriptionDebugInfo Debug;
        public float[] PitchTimelineMidi;
        public float[] PitchTimelineTimes;
    }

    public static PipelineResult Analyze(float[] mono, float songDuration, ref MelodyTranscriber.TranscriptionDebugInfo debug)
    {
        mono = Preprocess(mono, ref debug);
        mono = EmphasizeVocalBand(mono);
        float noiseFloor = EstimateNoiseFloor(mono);

        List<PitchFrameData> frames = ExtractPitchFrames(mono, noiseFloor, ref debug, strictPass: true);
        FillMissedPitchFrames(mono, noiseFloor, frames, ref debug);
        ApplyTemporalSmoothing(frames);
        ApplyPitchContinuityFilter(frames);
        FillPitchGaps(frames, maxGapFrames: 8, maxMidiJump: 2f);
        RemoveSpuriousOutliers(frames);

        MelodyKeyEstimate key = EstimateKey(frames);
        ApplyWeakKeyCorrection(frames, key);

        List<NoteEvent> events = SegmentNoteEvents(frames, noiseFloor);
        events = SplitLongNotesByContour(events, frames);
        events = MergeNoteEvents(events);
        events = RefineOnsetsAndOffsets(events, frames);
        events = FilterNoteEvents(events, ref debug);
        events = RefineNotePitchesPerRegion(mono, events, noiseFloor);
        events = FillSparseRegions(events, frames, songDuration);
        events = SnapNoteEventsToScale(events, key);
        float estimatedBpm = EstimateBpm(events);
        events = AlignNotesToBeatGrid(events, estimatedBpm);

        debug.NotesDetected = events.Count;
        debug.EstimatedKey = key.keyName;
        debug.KeyConfidence = key.confidence;

        BuildPitchTimeline(frames, out float[] timelineTimes, out float[] timelineMidi);
        debug.ValidPitchFrames = CountValidFrames(frames);

        return new PipelineResult
        {
            NoteEvents = events,
            Key = key,
            EstimatedBpm = estimatedBpm,
            Debug = debug,
            PitchTimelineTimes = timelineTimes,
            PitchTimelineMidi = timelineMidi
        };
    }

    private static float[] EmphasizeVocalBand(float[] mono)
    {
        float[] band = ApplyHighPass(mono, AnalysisSampleRate, 180f);
        band = ApplyLowPass(band, AnalysisSampleRate, 2200f);
        float[] output = new float[mono.Length];
        for (int i = 0; i < mono.Length; i++)
            output[i] = mono[i] * 0.25f + band[i] * 0.75f;
        return output;
    }

    private static float EstimateBpm(List<NoteEvent> events)
    {
        if (events == null || events.Count < 4)
            return 0f;

        var onsets = new List<float>();
        foreach (NoteEvent ev in events)
            onsets.Add(ev.startTime);
        return EstimateBpm(onsets);
    }

    private static List<NoteEvent> SplitLongNotesByContour(List<NoteEvent> events, List<PitchFrameData> frames)
    {
        if (events == null || events.Count == 0 || frames == null || frames.Count == 0)
            return events;

        const float minSplitDuration = 0.32f;
        const float splitCents = 55f;
        var result = new List<NoteEvent>();

        foreach (NoteEvent ev in events)
        {
            if (ev.duration < minSplitDuration)
            {
                result.Add(ev);
                continue;
            }

            float endTime = ev.startTime + ev.duration;
            var segmentMidis = new List<float>();
            var segmentTimes = new List<float>();

            foreach (PitchFrameData frame in frames)
            {
                if (!frame.valid || frame.time < ev.startTime || frame.time > endTime)
                    continue;

                segmentMidis.Add(frame.midiFloat);
                segmentTimes.Add(frame.time);
            }

            if (segmentMidis.Count < 6)
            {
                result.Add(ev);
                continue;
            }

            int splitIndex = -1;
            for (int i = 3; i < segmentMidis.Count - 3; i++)
            {
                float left = Median(segmentMidis, 0, i);
                float right = Median(segmentMidis, i, segmentMidis.Count);
                float cents = Mathf.Abs(1200f * Mathf.Log(right / left, 2f));
                if (cents >= splitCents)
                {
                    splitIndex = i;
                    break;
                }
            }

            if (splitIndex < 0)
            {
                result.Add(ev);
                continue;
            }

            float splitTime = segmentTimes[splitIndex];
            if (splitTime <= ev.startTime + 0.08f || splitTime >= endTime - 0.08f)
            {
                result.Add(ev);
                continue;
            }

            NoteEvent first = CloneNoteEvent(ev);
            first.duration = splitTime - ev.startTime;
            first.pitchMidi = Median(segmentMidis, 0, splitIndex);
            first.pitchHz = MusicalNoteUtility.MidiToHz(first.pitchMidi);
            first.midiRounded = MusicalNoteUtility.RoundMidi(first.pitchMidi);
            first.noteName = MusicalNoteUtility.MidiToNoteName(first.midiRounded);

            NoteEvent second = CloneNoteEvent(ev);
            second.startTime = splitTime;
            second.duration = endTime - splitTime;
            second.pitchMidi = Median(segmentMidis, splitIndex, segmentMidis.Count);
            second.pitchHz = MusicalNoteUtility.MidiToHz(second.pitchMidi);
            second.midiRounded = MusicalNoteUtility.RoundMidi(second.pitchMidi);
            second.noteName = MusicalNoteUtility.MidiToNoteName(second.midiRounded);

            result.Add(first);
            result.Add(second);
        }

        result.Sort((a, b) => a.startTime.CompareTo(b.startTime));
        return result;
    }

    private static NoteEvent CloneNoteEvent(NoteEvent source) => new NoteEvent
    {
        pitchMidi = source.pitchMidi,
        pitchHz = source.pitchHz,
        startTime = source.startTime,
        duration = source.duration,
        confidence = source.confidence,
        energy = source.energy,
        midiRounded = source.midiRounded,
        noteName = source.noteName
    };

    private static float Median(List<float> values, int start, int endExclusive)
    {
        int count = endExclusive - start;
        if (count <= 0)
            return 0f;

        var slice = new float[count];
        for (int i = 0; i < count; i++)
            slice[i] = values[start + i];
        System.Array.Sort(slice);
        return slice[count / 2];
    }

    private static List<NoteEvent> RefineNotePitchesPerRegion(float[] mono, List<NoteEvent> events, float noiseFloor) =>
        RefineNoteRegionsHighResolution(mono, events, noiseFloor);

    private static List<NoteEvent> AlignNotesToBeatGrid(List<NoteEvent> events, float bpm)
    {
        if (events == null || events.Count == 0 || bpm <= 20f)
            return events;

        float beatDuration = 60f / bpm;
        float grid = beatDuration * 0.25f;
        const float maxSnap = 0.045f;

        foreach (NoteEvent ev in events)
        {
            float beatIndex = ev.startTime / grid;
            int nearest = Mathf.RoundToInt(beatIndex);
            float snapped = nearest * grid;
            if (Mathf.Abs(snapped - ev.startTime) <= maxSnap)
                ev.startTime = Mathf.Max(0f, snapped);
        }

        return events;
    }

    private static float[] Preprocess(float[] mono, ref MelodyTranscriber.TranscriptionDebugInfo debug)
    {
        mono = NormalizePeak(mono, 0.9f, ref debug);
        mono = ApplyPreEmphasis(mono, 0.97f);
        mono = ApplyHighPass(mono, AnalysisSampleRate, 100f);
        mono = ApplyLowPass(mono, AnalysisSampleRate, 2400f);
        mono = ApplyBandEmphasis(mono, AnalysisSampleRate, 200f, 1600f);
        return mono;
    }

    private static float[] ApplyPreEmphasis(float[] mono, float coefficient)
    {
        if (mono == null || mono.Length < 2)
            return mono;

        float[] output = new float[mono.Length];
        output[0] = mono[0];
        for (int i = 1; i < mono.Length; i++)
            output[i] = mono[i] - coefficient * mono[i - 1];
        return output;
    }

    private static float EstimateNoiseFloor(float[] mono)
    {
        int frameCount = Math.Max(1, (mono.Length - FrameSize) / HopSize);
        var energies = new List<float>(frameCount);
        float[] window = new float[FrameSize];

        for (int f = 0; f < frameCount; f++)
        {
            Array.Copy(mono, f * HopSize, window, 0, FrameSize);
            energies.Add(YinPitchDetector.ComputeEnergy(window));
        }

        energies.Sort();
        int idx = Mathf.Clamp(Mathf.RoundToInt(energies.Count * 0.12f), 0, energies.Count - 1);
        return Mathf.Max(energies[idx], 1e-8f);
    }

    private static List<PitchFrameData> ExtractPitchFrames(float[] mono, float noiseFloor,
        ref MelodyTranscriber.TranscriptionDebugInfo debug, bool strictPass)
    {
        var frames = new List<PitchFrameData>();
        int totalFrames = Math.Max(0, (mono.Length - FrameSize) / HopSize);
        debug.FramesAnalyzed = totalFrames;

        float[] window = new float[FrameSize];
        float[] prevBandEnergies = null;
        float adaptiveEnergyThreshold = noiseFloor * (strictPass ? 1.5f : 1.15f);

        for (int frame = 0; frame < totalFrames; frame++)
        {
            int offset = frame * HopSize;
            Array.Copy(mono, offset, window, 0, FrameSize);
            ApplyHanning(window);

            float time = offset / (float)AnalysisSampleRate;
            float energy = YinPitchDetector.ComputeEnergy(window);
            float flux = ComputeMultiBandFlux(window, ref prevBandEnergies);

            var data = new PitchFrameData
            {
                time = time,
                energy = energy,
                spectralFlux = flux,
                valid = false
            };

            if (energy < adaptiveEnergyThreshold)
            {
                frames.Add(data);
                continue;
            }

            float yinThreshold = strictPass
                ? Mathf.Lerp(0.20f, 0.12f, Mathf.Clamp01(energy / (noiseFloor * 20f)))
                : Mathf.Lerp(0.26f, 0.16f, Mathf.Clamp01(energy / (noiseFloor * 20f)));
            YinPitchDetector.YinResult yin = YinPitchDetector.DetectPitchDetailed(window, AnalysisSampleRate, yinThreshold);
            if (!yin.IsValid)
            {
                frames.Add(data);
                continue;
            }

            float validatedHz = ValidateFundamental(window, AnalysisSampleRate, yin);
            float midiFloat = MusicalNoteUtility.HzToMidi(validatedHz);
            if (midiFloat < MinMelodyMidi || midiFloat > MaxMelodyMidi)
            {
                frames.Add(data);
                continue;
            }

            float minConfidence = strictPass
                ? Mathf.Lerp(0.32f, 0.14f, Mathf.Clamp01(energy / (noiseFloor * 30f)))
                : Mathf.Lerp(0.22f, 0.12f, Mathf.Clamp01(energy / (noiseFloor * 30f)));
            if (yin.Confidence < minConfidence)
            {
                frames.Add(data);
                continue;
            }

            data.valid = true;
            data.frequency = validatedHz;
            data.midiFloat = midiFloat;
            data.confidence = yin.Confidence;
            frames.Add(data);
        }

        return frames;
    }

    private static void FillMissedPitchFrames(float[] mono, float noiseFloor, List<PitchFrameData> frames,
        ref MelodyTranscriber.TranscriptionDebugInfo debug)
    {
        float[] window = new float[FrameSize];
        float[] prevBandEnergies = null;
        float threshold = noiseFloor * 1.25f;

        for (int frame = 0; frame < frames.Count; frame++)
        {
            PitchFrameData data = frames[frame];
            if (data.valid || data.energy < threshold)
                continue;

            int offset = frame * HopSize;
            if (offset + FrameSize > mono.Length)
                continue;

            Array.Copy(mono, offset, window, 0, FrameSize);
            ApplyHanning(window);
            _ = ComputeMultiBandFlux(window, ref prevBandEnergies);

            YinPitchDetector.YinResult yin = YinPitchDetector.DetectPitchDetailed(window, AnalysisSampleRate, 0.24f);
            if (!yin.IsValid)
                continue;

            float validatedHz = ValidateFundamental(window, AnalysisSampleRate, yin);
            float midiFloat = MusicalNoteUtility.HzToMidi(validatedHz);
            if (midiFloat < MinMelodyMidi || midiFloat > MaxMelodyMidi || yin.Confidence < 0.12f)
                continue;

            data.valid = true;
            data.frequency = validatedHz;
            data.midiFloat = midiFloat;
            data.confidence = yin.Confidence * 0.85f;
            frames[frame] = data;
        }
    }

    private static float ValidateFundamental(float[] window, int sampleRate, YinPitchDetector.YinResult primary)
    {
        float bestHz = primary.PitchHz;
        float bestScore = primary.Confidence;

        float[] multipliers = { 0.5f, 1f, 2f, 3f };
        foreach (float mult in multipliers)
        {
            float candidate = primary.PitchHz * mult;
            if (candidate < YinPitchDetector.MinFrequency || candidate > YinPitchDetector.MaxFrequency)
                continue;

            float periodScore = EstimatePeriodConfidence(window, sampleRate, candidate);
            float harmonicBonus = PitchAnalysisCore.EstimateHarmonicSupport(window, sampleRate, candidate);
            float octavePenalty = mult switch
            {
                1f => 0f,
                0.5f => 0.02f,
                2f => 0.10f,
                _ => 0.18f
            };
            float score = periodScore * 0.6f + harmonicBonus * 0.4f - octavePenalty;
            if (score > bestScore)
            {
                bestScore = score;
                bestHz = candidate;
            }
        }

        return bestHz;
    }

    private static float EstimatePeriodConfidence(float[] signal, int sampleRate, float targetHz)
    {
        int period = Mathf.RoundToInt(sampleRate / targetHz);
        if (period < 2 || period >= signal.Length / 2)
            return 0f;

        double sum = 0d;
        double norm = 0d;
        int count = 0;
        for (int i = 0; i < signal.Length - period; i++)
        {
            sum += signal[i] * signal[i + period];
            norm += signal[i] * signal[i];
            count++;
        }

        if (count == 0 || norm <= 1e-8)
            return 0f;

        return (float)Math.Max(0d, sum / Math.Sqrt(norm * count));
    }

    private static void ApplyTemporalSmoothing(List<PitchFrameData> frames)
    {
        const int radius = 5;
        var midiBuffer = new List<float>();

        for (int i = 0; i < frames.Count; i++)
        {
            if (!frames[i].valid)
                continue;

            midiBuffer.Clear();
            for (int j = Math.Max(0, i - radius); j <= Math.Min(frames.Count - 1, i + radius); j++)
            {
                if (frames[j].valid)
                    midiBuffer.Add(frames[j].midiFloat);
            }

            if (midiBuffer.Count == 0)
                continue;

            midiBuffer.Sort();
            float median = midiBuffer[midiBuffer.Count / 2];
            PitchFrameData f = frames[i];
            f.midiFloat = median;
            f.frequency = MusicalNoteUtility.MidiToHz(median);
            frames[i] = f;
        }

        for (int i = 1; i < frames.Count; i++)
        {
            if (!frames[i].valid || !frames[i - 1].valid)
                continue;

            float jumpCents = Mathf.Abs(1200f * Mathf.Log(frames[i].midiFloat / frames[i - 1].midiFloat, 2f));
            if (jumpCents < VibratoCentsThreshold)
            {
                PitchFrameData curr = frames[i];
                curr.midiFloat = curr.midiFloat * 0.25f + frames[i - 1].midiFloat * 0.75f;
                curr.frequency = MusicalNoteUtility.MidiToHz(curr.midiFloat);
                frames[i] = curr;
            }
        }
    }

    private static void ApplyPitchContinuityFilter(List<PitchFrameData> frames)
    {
        const float maxJumpSemis = 7f;
        const float breakConfidence = 0.52f;

        for (int i = 1; i < frames.Count; i++)
        {
            if (!frames[i].valid || !frames[i - 1].valid)
                continue;

            float jump = Mathf.Abs(frames[i].midiFloat - frames[i - 1].midiFloat);
            if (jump <= maxJumpSemis || frames[i].confidence >= breakConfidence)
                continue;

            PitchFrameData corrected = frames[i];
            corrected.midiFloat = frames[i - 1].midiFloat + Mathf.Clamp(frames[i].midiFloat - frames[i - 1].midiFloat, -maxJumpSemis, maxJumpSemis);
            corrected.frequency = MusicalNoteUtility.MidiToHz(corrected.midiFloat);
            corrected.confidence *= 0.9f;
            frames[i] = corrected;
        }
    }

    private static void FillPitchGaps(List<PitchFrameData> frames, int maxGapFrames, float maxMidiJump)
    {
        int i = 0;
        while (i < frames.Count)
        {
            if (frames[i].valid) { i++; continue; }

            int gapStart = i;
            while (i < frames.Count && !frames[i].valid)
                i++;
            int gapEnd = i;
            int gapLen = gapEnd - gapStart;

            if (gapLen > maxGapFrames || gapStart == 0 || gapEnd >= frames.Count)
                continue;

            PitchFrameData before = frames[gapStart - 1];
            PitchFrameData after = frames[gapEnd];
            if (!before.valid || !after.valid)
                continue;

            if (Mathf.Abs(before.midiFloat - after.midiFloat) > maxMidiJump)
                continue;

            float interpolated = (before.midiFloat + after.midiFloat) * 0.5f;
            for (int g = gapStart; g < gapEnd; g++)
            {
                frames[g] = new PitchFrameData
                {
                    time = frames[g].time,
                    valid = true,
                    midiFloat = interpolated,
                    frequency = MusicalNoteUtility.MidiToHz(interpolated),
                    confidence = (before.confidence + after.confidence) * 0.5f,
                    energy = (before.energy + after.energy) * 0.5f,
                    spectralFlux = frames[g].spectralFlux
                };
            }
        }
    }

    private static void RemoveSpuriousOutliers(List<PitchFrameData> frames)
    {
        for (int i = 1; i < frames.Count - 1; i++)
        {
            if (!frames[i].valid)
                continue;

            int validNeighbors = 0;
            float neighborMidi = 0f;

            if (frames[i - 1].valid)
            {
                validNeighbors++;
                neighborMidi += frames[i - 1].midiFloat;
            }

            if (frames[i + 1].valid)
            {
                validNeighbors++;
                neighborMidi += frames[i + 1].midiFloat;
            }

            if (validNeighbors == 0)
                continue;

            neighborMidi /= validNeighbors;
            float cents = Mathf.Abs(1200f * Mathf.Log(frames[i].midiFloat / neighborMidi, 2f));

            if (cents > 150f && frames[i].confidence < 0.55f)
                frames[i] = new PitchFrameData { time = frames[i].time, valid = false };
        }
    }

    private static MelodyKeyEstimate EstimateKey(List<PitchFrameData> frames)
    {
        var pitchClassWeight = new float[12];
        float totalWeight = 0f;

        foreach (PitchFrameData f in frames)
        {
            if (!f.valid)
                continue;

            int pc = ((MusicalNoteUtility.RoundMidi(f.midiFloat) % 12) + 12) % 12;
            float weight = f.confidence * Mathf.Sqrt(f.energy);
            pitchClassWeight[pc] += weight;
            totalWeight += weight;
        }

        if (totalWeight < 0.01f)
            return MelodyKeyEstimate.Unknown;

        string[] majorNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int[] majorProfile = { 6, 2, 4, 5, 3, 6, 2, 4, 5, 3, 1, 4 };
        int[] minorProfile = { 6, 2, 3, 5, 2, 4, 2, 4, 5, 2, 1, 4 };

        float bestScore = 0f;
        int bestRoot = 0;
        bool bestMinor = false;

        for (int root = 0; root < 12; root++)
        {
            for (int mode = 0; mode < 2; mode++)
            {
                int[] profile = mode == 0 ? majorProfile : minorProfile;
                float score = 0f;
                for (int pc = 0; pc < 12; pc++)
                {
                    int rotated = (pc - root + 12) % 12;
                    score += pitchClassWeight[pc] * profile[rotated];
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestRoot = root;
                    bestMinor = mode == 1;
                }
            }
        }

        float confidence = bestScore / totalWeight;
        if (confidence < 0.35f)
            return MelodyKeyEstimate.Unknown;

        string suffix = bestMinor ? " Minor" : " Major";
        return new MelodyKeyEstimate
        {
            keyName = majorNames[bestRoot] + suffix,
            confidence = confidence,
            rootPc = bestRoot,
            isMinor = bestMinor
        };
    }

    private static void ApplyWeakKeyCorrection(List<PitchFrameData> frames, MelodyKeyEstimate key)
    {
        if (key.confidence < 0.45f || key.rootPc < 0)
            return;

        int[] majorIntervals = { 0, 2, 4, 5, 7, 9, 11 };
        int[] minorIntervals = { 0, 2, 3, 5, 7, 8, 10 };
        int[] scale = key.isMinor ? minorIntervals : majorIntervals;

        for (int i = 0; i < frames.Count; i++)
        {
            PitchFrameData f = frames[i];
            if (!f.valid || f.confidence > 0.65f)
                continue;

            int pc = ((MusicalNoteUtility.RoundMidi(f.midiFloat) % 12) + 12) % 12;
            int rel = (pc - key.rootPc + 12) % 12;
            if (Array.IndexOf(scale, rel) >= 0)
                continue;

            int nearestPc = FindNearestScalePc(pc, key.rootPc, scale);
            int midiRounded = MusicalNoteUtility.RoundMidi(f.midiFloat);
            int octave = midiRounded / 12;
            float correctedMidi = octave * 12 + nearestPc + (f.midiFloat - midiRounded);

            f.midiFloat = correctedMidi;
            f.frequency = MusicalNoteUtility.MidiToHz(correctedMidi);
            frames[i] = f;
        }
    }

    private static int FindNearestScalePc(int pc, int root, int[] scale)
    {
        int bestPc = pc;
        int bestDist = 99;
        foreach (int interval in scale)
        {
            int candidate = (root + interval) % 12;
            int dist = Math.Min(Math.Abs(candidate - pc), 12 - Math.Abs(candidate - pc));
            if (dist < bestDist)
            {
                bestDist = dist;
                bestPc = candidate;
            }
        }

        return bestPc;
    }

    private static List<NoteEvent> SegmentNoteEvents(List<PitchFrameData> frames, float noiseFloor)
    {
        var events = new List<NoteEvent>();
        if (frames.Count == 0)
            return events;

        float hopSeconds = HopSize / (float)AnalysisSampleRate;

        NoteEvent current = null;
        float lastValidTime = -1f;
        int consecutiveSilentFrames = 0;

        foreach (PitchFrameData frame in frames)
        {
            if (frame.valid)
            {
                consecutiveSilentFrames = 0;
                bool pitchChanged = current != null &&
                    Mathf.Abs(1200f * Mathf.Log(frame.midiFloat / current.pitchMidi, 2f)) > NoteChangeCentsThreshold;

                if (current == null)
                {
                    current = CreateNoteEvent(frame, hopSeconds);
                    lastValidTime = frame.time;
                    continue;
                }

                if (pitchChanged)
                {
                    events.Add(current);
                    current = CreateNoteEvent(frame, hopSeconds);
                    lastValidTime = frame.time;
                    continue;
                }

                current.duration += hopSeconds;
                current.pitchMidi = current.pitchMidi * 0.75f + frame.midiFloat * 0.25f;
                current.pitchHz = MusicalNoteUtility.MidiToHz(current.pitchMidi);
                current.confidence = Mathf.Max(current.confidence, frame.confidence);
                current.energy = Mathf.Max(current.energy, frame.energy);
                lastValidTime = frame.time;
            }
            else
            {
                consecutiveSilentFrames++;
                if (current != null && consecutiveSilentFrames >= 3)
                {
                    events.Add(current);
                    current = null;
                }
            }
        }

        if (current != null)
            events.Add(current);

        return events;
    }

    private static NoteEvent CreateNoteEvent(PitchFrameData frame, float hopSeconds)
    {
        int midi = MusicalNoteUtility.RoundMidi(frame.midiFloat);
        return new NoteEvent
        {
            pitchMidi = frame.midiFloat,
            pitchHz = frame.frequency,
            startTime = frame.time,
            duration = hopSeconds,
            confidence = frame.confidence,
            energy = frame.energy,
            midiRounded = midi,
            noteName = MusicalNoteUtility.MidiToNoteName(midi)
        };
    }

    private static List<NoteEvent> MergeNoteEvents(List<NoteEvent> events)
    {
        if (events.Count <= 1)
            return events;

        var merged = new List<NoteEvent> { events[0] };
        for (int i = 1; i < events.Count; i++)
        {
            NoteEvent prev = merged[merged.Count - 1];
            NoteEvent curr = events[i];
            float gap = curr.startTime - (prev.startTime + prev.duration);
            float cents = Mathf.Abs(1200f * Mathf.Log(curr.pitchMidi / prev.pitchMidi, 2f));

            if (gap <= 0.12f && cents <= NoteChangeCentsThreshold)
            {
                prev.duration = (curr.startTime + curr.duration) - prev.startTime;
                prev.pitchMidi = (prev.pitchMidi + curr.pitchMidi) * 0.5f;
                prev.pitchHz = MusicalNoteUtility.MidiToHz(prev.pitchMidi);
                prev.midiRounded = MusicalNoteUtility.RoundMidi(prev.pitchMidi);
                prev.noteName = MusicalNoteUtility.MidiToNoteName(prev.midiRounded);
                prev.confidence = Mathf.Max(prev.confidence, curr.confidence);
                merged[merged.Count - 1] = prev;
            }
            else
            {
                merged.Add(curr);
            }
        }

        return merged;
    }

    private static List<NoteEvent> RefineOnsetsAndOffsets(List<NoteEvent> events, List<PitchFrameData> frames)
    {
        foreach (NoteEvent ev in events)
        {
            float searchStart = Mathf.Max(0f, ev.startTime - 0.08f);
            float searchEnd = ev.startTime + 0.05f;
            foreach (PitchFrameData f in frames)
            {
                if (f.time >= searchStart && f.time <= searchEnd && f.spectralFlux > 0f)
                {
                    ev.startTime = Mathf.Min(ev.startTime, f.time);
                    break;
                }
            }

            float endTime = ev.startTime + ev.duration;
            float tail = endTime;
            foreach (PitchFrameData f in frames)
            {
                if (f.time >= endTime - 0.05f && f.time <= endTime + 0.15f && f.valid &&
                    Mathf.Abs(f.midiFloat - ev.pitchMidi) < 1.2f)
                {
                    tail = Mathf.Max(tail, f.time + HopSize / (float)AnalysisSampleRate);
                }
            }

            ev.duration = Mathf.Max(MinNoteDuration, tail - ev.startTime);
        }

        return events;
    }

    private static List<NoteEvent> FillSparseRegions(List<NoteEvent> events, List<PitchFrameData> frames, float songDuration)
    {
        if (songDuration <= 0f || frames.Count == 0)
            return events;

        float minNotes = songDuration * 0.55f;
        if (events.Count >= minNotes)
            return events;

        float avgFlux = ComputeAverageFlux(frames);
        float fluxThreshold = avgFlux * OnsetFluxMultiplier;
        var augmented = new List<NoteEvent>(events);

        for (int i = 1; i < frames.Count - 1; i++)
        {
            PitchFrameData frame = frames[i];
            if (frame.spectralFlux < fluxThreshold)
                continue;

            if (frames[i - 1].spectralFlux >= frame.spectralFlux || frames[i + 1].spectralFlux > frame.spectralFlux)
                continue;

            if (IsTimeCovered(augmented, frame.time, 0.22f))
                continue;

            float? localPitch = GetLocalMedianPitch(frames, i, 10);
            if (!localPitch.HasValue)
                continue;

            int midi = MusicalNoteUtility.RoundMidi(localPitch.Value);
            if (midi < MinMelodyMidi || midi > MaxMelodyMidi)
                continue;

            augmented.Add(new NoteEvent
            {
                pitchMidi = localPitch.Value,
                pitchHz = MusicalNoteUtility.MidiToHz(localPitch.Value),
                startTime = frame.time,
                duration = 0.2f,
                confidence = 0.34f,
                energy = frame.energy,
                midiRounded = midi,
                noteName = MusicalNoteUtility.MidiToNoteName(midi)
            });
        }

        augmented.Sort((a, b) => a.startTime.CompareTo(b.startTime));
        return MergeNoteEvents(augmented);
    }

    private static bool IsTimeCovered(List<NoteEvent> events, float time, float margin)
    {
        foreach (NoteEvent ev in events)
        {
            if (time >= ev.startTime - margin && time <= ev.startTime + ev.duration + margin)
                return true;
        }

        return false;
    }

    private static float? GetLocalMedianPitch(List<PitchFrameData> frames, int center, int radius)
    {
        var pitches = new List<float>();
        for (int j = Math.Max(0, center - radius); j <= Math.Min(frames.Count - 1, center + radius); j++)
        {
            if (frames[j].valid)
                pitches.Add(frames[j].midiFloat);
        }

        if (pitches.Count < 3)
            return null;

        pitches.Sort();
        return pitches[pitches.Count / 2];
    }

    private static List<NoteEvent> SnapNoteEventsToScale(List<NoteEvent> events, MelodyKeyEstimate key)
    {
        if (events == null || key.confidence < 0.45f || key.rootPc < 0)
            return events;

        int[] majorIntervals = { 0, 2, 4, 5, 7, 9, 11 };
        int[] minorIntervals = { 0, 2, 3, 5, 7, 8, 10 };
        int[] scale = key.isMinor ? minorIntervals : majorIntervals;

        for (int i = 0; i < events.Count; i++)
        {
            NoteEvent ev = events[i];
            int pc = ((ev.midiRounded % 12) + 12) % 12;
            int rel = (pc - key.rootPc + 12) % 12;
            if (Array.IndexOf(scale, rel) >= 0)
                continue;

            int nearestPc = FindNearestScalePc(pc, key.rootPc, scale);
            int octave = ev.midiRounded / 12;
            float corrected = octave * 12 + nearestPc + (ev.pitchMidi - ev.midiRounded);
            ev.pitchMidi = corrected;
            ev.midiRounded = MusicalNoteUtility.RoundMidi(corrected);
            ev.pitchHz = MusicalNoteUtility.MidiToHz(corrected);
            ev.noteName = MusicalNoteUtility.MidiToNoteName(ev.midiRounded);
            events[i] = ev;
        }

        return events;
    }

    private static List<NoteEvent> FilterNoteEvents(List<NoteEvent> events, ref MelodyTranscriber.TranscriptionDebugInfo debug)
    {
        var filtered = new List<NoteEvent>();
        debug.NotesDiscarded = 0;

        foreach (NoteEvent ev in events)
        {
            if (ev.duration < MinNoteDuration)
            {
                debug.NotesDiscarded++;
                continue;
            }

            if (ev.midiRounded < MinMelodyMidi || ev.midiRounded > MaxMelodyMidi)
            {
                debug.NotesDiscarded++;
                continue;
            }

            if (ev.confidence < 0.22f && ev.duration < 0.10f)
            {
                debug.NotesDiscarded++;
                continue;
            }

            ev.midiRounded = MusicalNoteUtility.RoundMidi(ev.pitchMidi);
            ev.noteName = MusicalNoteUtility.MidiToNoteName(ev.midiRounded);
            filtered.Add(ev);
        }

        return filtered;
    }

    private static float ComputeAverageFlux(List<PitchFrameData> frames)
    {
        float sum = 0f;
        int count = 0;
        foreach (PitchFrameData f in frames)
        {
            sum += f.spectralFlux;
            count++;
        }

        return count > 0 ? sum / count : 0f;
    }

    private static float ComputeSpectralFlux(float[] window, ref float[] prevSpectrum) =>
        ComputeMultiBandFlux(window, ref prevSpectrum);

    private static float ComputeMultiBandFlux(float[] window, ref float[] prevBandEnergies)
    {
        float low = PitchAnalysisCore.GoertzelMagnitude(window, AnalysisSampleRate, 220f);
        low *= low;
        float mid = PitchAnalysisCore.GoertzelMagnitude(window, AnalysisSampleRate, 620f);
        mid *= mid;
        float high = PitchAnalysisCore.GoertzelMagnitude(window, AnalysisSampleRate, 1200f);
        high *= high;
        var bands = new[] { low, mid, high };

        float flux = 0f;
        if (prevBandEnergies != null && prevBandEnergies.Length == bands.Length)
        {
            for (int i = 0; i < bands.Length; i++)
            {
                float diff = bands[i] - prevBandEnergies[i];
                if (diff > 0f)
                    flux += diff;
            }
        }

        prevBandEnergies = bands;
        return flux / bands.Length;
    }

    private static List<NoteEvent> RefineNoteRegionsHighResolution(float[] mono, List<NoteEvent> events, float noiseFloor)
    {
        if (events == null || events.Count == 0 || mono == null || mono.Length == 0)
            return events;

        const int refineHop = 128;
        float[] window = new float[FrameSize];

        foreach (NoteEvent ev in events)
        {
            float regionStart = Mathf.Max(0f, ev.startTime - 0.04f);
            float regionEnd = ev.startTime + ev.duration + 0.06f;
            int startSample = Mathf.FloorToInt(regionStart * AnalysisSampleRate);
            int endSample = Mathf.Min(mono.Length, Mathf.CeilToInt(regionEnd * AnalysisSampleRate));
            if (endSample - startSample < FrameSize)
                continue;

            var pitches = new List<float>();
            var weights = new List<float>();

            for (int offset = startSample; offset + FrameSize <= endSample; offset += refineHop)
            {
                Array.Copy(mono, offset, window, 0, FrameSize);
                PitchAnalysisCore.ApplyHanningInPlace(window);
                float energy = YinPitchDetector.ComputeEnergy(window);
                if (energy < noiseFloor * 1.2f)
                    continue;

                YinPitchDetector.YinResult yin = YinPitchDetector.DetectPitchDetailed(window, AnalysisSampleRate, 0.14f);
                if (!yin.IsValid || yin.Confidence < 0.22f)
                {
                    float ac = PitchAnalysisCore.AutocorrelationPitchHz(window, AnalysisSampleRate);
                    if (ac <= 0f)
                        continue;
                    yin = new YinPitchDetector.YinResult { PitchHz = ac, Confidence = 0.28f, IsValid = true };
                }

                float hz = PitchAnalysisCore.ValidateFundamentalHz(window, AnalysisSampleRate, yin);
                float midi = MusicalNoteUtility.HzToMidi(hz);
                if (midi < MinMelodyMidi || midi > MaxMelodyMidi)
                    continue;

                pitches.Add(midi);
                weights.Add(yin.Confidence * Mathf.Sqrt(energy));
            }

            if (pitches.Count == 0)
                continue;

            float weightedSum = 0f;
            float weightTotal = 0f;
            for (int i = 0; i < pitches.Count; i++)
            {
                weightedSum += pitches[i] * weights[i];
                weightTotal += weights[i];
            }

            if (weightTotal <= 0f)
                continue;

            float refinedMidi = weightedSum / weightTotal;
            ev.pitchMidi = refinedMidi;
            ev.midiRounded = MusicalNoteUtility.RoundMidi(refinedMidi);
            ev.pitchHz = MusicalNoteUtility.MidiToHz(refinedMidi);
            ev.noteName = MusicalNoteUtility.MidiToNoteName(ev.midiRounded);
        }

        return events;
    }

    private static List<NoteEvent> QuantizeNotePitches(List<NoteEvent> events)
    {
        foreach (NoteEvent ev in events)
        {
            float rounded = MusicalNoteUtility.RoundMidi(ev.pitchMidi);
            ev.pitchMidi = ev.pitchMidi * 0.35f + rounded * 0.65f;
            ev.midiRounded = MusicalNoteUtility.RoundMidi(ev.pitchMidi);
            ev.pitchHz = MusicalNoteUtility.MidiToHz(ev.pitchMidi);
            ev.noteName = MusicalNoteUtility.MidiToNoteName(ev.midiRounded);
        }

        return events;
    }

    private static void BuildPitchTimeline(List<PitchFrameData> frames, out float[] times, out float[] midi)
    {
        int validCount = 0;
        foreach (PitchFrameData f in frames)
        {
            if (f.valid)
                validCount++;
        }

        times = new float[validCount];
        midi = new float[validCount];
        int idx = 0;
        foreach (PitchFrameData f in frames)
        {
            if (!f.valid)
                continue;
            times[idx] = f.time;
            midi[idx] = f.midiFloat;
            idx++;
        }
    }

    private static int CountValidFrames(List<PitchFrameData> frames)
    {
        int count = 0;
        foreach (PitchFrameData f in frames)
        {
            if (f.valid)
                count++;
        }

        return count;
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

    public static void AppendPitchTimelineToLog(StringBuilder sb, float[] times, float[] midi, int maxLines = 120)
    {
        if (times == null || midi == null)
            return;

        sb.AppendLine();
        sb.AppendLine("--- Pitch timeline (Tiempo → MIDI) ---");
        int step = Mathf.Max(1, times.Length / maxLines);
        for (int i = 0; i < times.Length; i += step)
        {
            string note = MusicalNoteUtility.MidiToNoteName(MusicalNoteUtility.RoundMidi(midi[i]));
            sb.AppendLine($"[{FormatTime(times[i])}] {note} ({midi[i]:F2})");
        }
    }

    private static float[] NormalizePeak(float[] input, float targetPeak, ref MelodyTranscriber.TranscriptionDebugInfo debug)
    {
        float peak = 0f;
        for (int i = 0; i < input.Length; i++)
            peak = Mathf.Max(peak, Mathf.Abs(input[i]));

        debug.PeakAmplitude = peak;
        if (peak < 1e-6f)
            return input;

        float gain = targetPeak / peak;
        float[] output = new float[input.Length];
        for (int i = 0; i < input.Length; i++)
            output[i] = input[i] * gain;
        return output;
    }

    private static float[] ApplyHighPass(float[] input, int sampleRate, float cutoffHz)
    {
        float rc = 1f / (2f * Mathf.PI * cutoffHz);
        float dt = 1f / sampleRate;
        float alpha = rc / (rc + dt);
        float[] output = new float[input.Length];
        if (input.Length == 0)
            return output;
        output[0] = input[0];
        for (int i = 1; i < input.Length; i++)
            output[i] = alpha * (output[i - 1] + input[i] - input[i - 1]);
        return output;
    }

    private static float[] ApplyLowPass(float[] input, int sampleRate, float cutoffHz)
    {
        float rc = 1f / (2f * Mathf.PI * cutoffHz);
        float dt = 1f / sampleRate;
        float alpha = dt / (rc + dt);
        float[] output = new float[input.Length];
        if (input.Length == 0)
            return output;
        output[0] = input[0];
        for (int i = 1; i < input.Length; i++)
            output[i] = output[i - 1] + alpha * (input[i] - output[i - 1]);
        return output;
    }

    private static float[] ApplyBandEmphasis(float[] input, int sampleRate, float lowHz, float highHz)
    {
        float[] band = ApplyHighPass(input, sampleRate, lowHz);
        band = ApplyLowPass(band, sampleRate, highHz);
        float[] output = new float[input.Length];
        for (int i = 0; i < input.Length; i++)
            output[i] = input[i] * 0.35f + band[i] * 0.65f;
        return output;
    }

    private static void ApplyHanning(float[] buffer)
    {
        int n = buffer.Length;
        if (n <= 1)
            return;
        for (int i = 0; i < n; i++)
        {
            float w = 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * i / (n - 1)));
            buffer[i] *= w;
        }
    }

    private static string FormatTime(float seconds)
    {
        int mins = (int)(seconds / 60f);
        float secs = seconds - mins * 60f;
        return $"{mins:00}:{secs:00.00}";
    }
}
