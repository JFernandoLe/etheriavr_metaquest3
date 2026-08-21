using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utilidades compartidas de análisis de pitch (voz en vivo + transcripción offline).
/// </summary>
public static class PitchAnalysisCore
{
    public static void ApplyHanningInPlace(float[] window)
    {
        if (window == null || window.Length == 0)
            return;

        int n = window.Length;
        for (int i = 0; i < n; i++)
        {
            float w = 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * i / (n - 1)));
            window[i] *= w;
        }
    }

    public static void ApplyPreEmphasisInPlace(float[] samples, float coefficient = 0.97f)
    {
        if (samples == null || samples.Length < 2)
            return;

        for (int i = samples.Length - 1; i >= 1; i--)
            samples[i] -= coefficient * samples[i - 1];
    }

    public static float ValidateFundamentalHz(float[] window, int sampleRate, YinPitchDetector.YinResult primary)
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
            float harmonicBonus = EstimateHarmonicSupport(window, sampleRate, candidate);
            float octavePenalty = mult switch
            {
                1f => 0f,
                0.5f => 0.02f,
                2f => 0.08f,
                _ => 0.15f
            };
            float score = periodScore * 0.65f + harmonicBonus * 0.35f - octavePenalty;
            if (score > bestScore)
            {
                bestScore = score;
                bestHz = candidate;
            }
        }

        return bestHz;
    }

    public static float EstimatePeriodConfidence(float[] signal, int sampleRate, float targetHz)
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

    public static float EstimateHarmonicSupport(float[] signal, int sampleRate, float fundamentalHz)
    {
        float support = 0f;
        int harmonics = 0;
        for (int h = 1; h <= 4; h++)
        {
            float hz = fundamentalHz * h;
            if (hz > YinPitchDetector.MaxFrequency)
                break;

            support += GoertzelMagnitude(signal, sampleRate, hz);
            harmonics++;
        }

        return harmonics > 0 ? support / harmonics : 0f;
    }

    public static float GoertzelMagnitude(float[] samples, int sampleRate, float targetHz)
    {
        int n = samples.Length;
        if (n < 8)
            return 0f;

        float k = 0.5f + n * targetHz / sampleRate;
        float w = 2f * Mathf.PI * k / n;
        float coeff = 2f * Mathf.Cos(w);
        float s1 = 0f;
        float s2 = 0f;

        for (int i = 0; i < n; i++)
        {
            float s0 = samples[i] + coeff * s1 - s2;
            s2 = s1;
            s1 = s0;
        }

        float power = s1 * s1 + s2 * s2 - coeff * s1 * s2;
        return Mathf.Sqrt(Mathf.Max(0f, power / n));
    }

    public static float AutocorrelationPitchHz(float[] window, int sampleRate)
    {
        return AutocorrelationPitchHz(window, sampleRate, YinPitchDetector.MinFrequency, YinPitchDetector.MaxFrequency);
    }

    public static float AutocorrelationPitchHz(float[] window, int sampleRate, float minHz, float maxHz)
    {
        int minLag = Mathf.Max(2, sampleRate / (int)maxHz);
        int maxLag = Mathf.Min(window.Length - 2, sampleRate / (int)minHz);
        if (maxLag <= minLag + 2)
            return -1f;

        float bestCorr = 0f;
        int bestLag = -1;
        for (int lag = minLag; lag <= maxLag; lag++)
        {
            float corr = 0f;
            float normA = 0f;
            float normB = 0f;
            int count = window.Length - lag;
            for (int i = 0; i < count; i++)
            {
                corr += window[i] * window[i + lag];
                normA += window[i] * window[i];
                normB += window[i + lag] * window[i + lag];
            }

            float denom = Mathf.Sqrt(normA * normB);
            corr = denom > 1e-8f ? corr / denom : 0f;
            if (corr > bestCorr)
            {
                bestCorr = corr;
                bestLag = lag;
            }
        }

        if (bestLag < 0 || bestCorr < 0.12f)
            return -1f;

        return sampleRate / (float)bestLag;
    }

    /// <summary>
    /// Producto armónico vía Goertzel: robusto en graves y mezclas densas.
    /// </summary>
    public static float HarmonicSaliencePitchHz(float[] window, int sampleRate, float minHz, float maxHz, out float confidence)
    {
        confidence = 0f;
        if (window == null || window.Length < 64)
            return -1f;

        int minMidi = MusicalNoteUtility.RoundMidi(MusicalNoteUtility.HzToMidi(minHz));
        int maxMidi = MusicalNoteUtility.RoundMidi(MusicalNoteUtility.HzToMidi(maxHz));

        float bestHz = -1f;
        float bestScore = 0f;

        for (int midi = minMidi; midi <= maxMidi; midi++)
        {
            float hz = MusicalNoteUtility.MidiToHz(midi);
            if (hz < minHz || hz > maxHz)
                continue;

            float score = 1f;
            int harmonics = 0;
            for (int h = 1; h <= 5; h++)
            {
                float harmonicHz = hz * h;
                if (harmonicHz > maxHz * 2f)
                    break;

                float mag = GoertzelMagnitude(window, sampleRate, harmonicHz);
                score *= Mathf.Max(mag, 1e-6f);
                harmonics++;
            }

            if (harmonics > 0)
                score = Mathf.Pow(score, 1f / harmonics);

            if (score > bestScore)
            {
                bestScore = score;
                bestHz = hz;
            }
        }

        if (bestHz <= 0f)
            return -1f;

        confidence = Mathf.Clamp01(bestScore * 12f);
        return bestHz;
    }

    public struct RobustPitchResult
    {
        public float PitchHz;
        public float Confidence;
        public bool IsValid;
    }

    /// <summary>
    /// Combina YIN, validación armónica, autocorrelación y saliencia para voz en vivo y rescate offline.
    /// </summary>
    public static RobustPitchResult DetectPitchRobust(float[] window, int sampleRate, float yinThreshold,
        float minHz, float maxHz)
    {
        var invalid = new RobustPitchResult { PitchHz = -1f, Confidence = 0f, IsValid = false };
        if (window == null || window.Length < 64)
            return invalid;

        YinPitchDetector.YinResult yin = YinPitchDetector.DetectPitchDetailed(window, sampleRate, yinThreshold, minHz, maxHz);
        float bestHz = -1f;
        float bestConf = 0f;

        if (yin.IsValid)
        {
            bestHz = ValidateFundamentalHz(window, sampleRate, yin);
            bestConf = Mathf.Max(yin.Confidence, 0.25f);
        }

        if (bestConf < 0.45f)
        {
            float acHz = AutocorrelationPitchHz(window, sampleRate, minHz, maxHz);
            if (acHz > 0f)
            {
                var acYin = new YinPitchDetector.YinResult { PitchHz = acHz, Confidence = 0.38f, IsValid = true };
                float acValidated = ValidateFundamentalHz(window, sampleRate, acYin);
                float acConf = EstimatePeriodConfidence(window, sampleRate, acValidated) * 0.85f;
                if (acConf > bestConf)
                {
                    bestHz = acValidated;
                    bestConf = acConf;
                }
            }
        }

        if (bestConf < 0.42f)
        {
            float hpsHz = HarmonicSaliencePitchHz(window, sampleRate, minHz, maxHz, out float hpsConf);
            if (hpsHz > 0f && hpsConf > bestConf)
            {
                bestHz = hpsHz;
                bestConf = hpsConf;
            }
        }

        if (bestHz <= 0f || bestHz < minHz || bestHz > maxHz)
            return invalid;

        return new RobustPitchResult
        {
            PitchHz = bestHz,
            Confidence = bestConf,
            IsValid = true
        };
    }

    public static void ApplyFirstOrderHighPassInPlace(float[] samples, int sampleRate, float cutoffHz)
    {
        if (samples == null || samples.Length < 2)
            return;

        float rc = 1f / (2f * Mathf.PI * cutoffHz);
        float dt = 1f / sampleRate;
        float alpha = rc / (rc + dt);
        float prevY = samples[0];
        float prevX = samples[0];
        samples[0] = 0f;

        for (int i = 1; i < samples.Length; i++)
        {
            float x = samples[i];
            float y = alpha * (prevY + x - prevX);
            samples[i] = y;
            prevY = y;
            prevX = x;
        }
    }

    public static float ComputeSpectralFlatness(float[] window, int sampleRate)
    {
        if (window == null || window.Length < 16)
            return 1f;

        float[] bands = { 220f, 440f, 880f, 1320f };
        var mags = new float[bands.Length];
        for (int i = 0; i < bands.Length; i++)
            mags[i] = Mathf.Max(GoertzelMagnitude(window, sampleRate, bands[i]), 1e-8f);

        float geo = 1f;
        float arith = 0f;
        for (int i = 0; i < mags.Length; i++)
        {
            geo *= mags[i];
            arith += mags[i];
        }

        geo = Mathf.Pow(geo, 1f / mags.Length);
        arith /= mags.Length;
        return arith > 1e-8f ? geo / arith : 1f;
    }

    public static void ApplyViterbiPitchSmoothing(List<PitchFrameData> frames, float maxJumpSemis = 6f)
    {
        if (frames == null || frames.Count == 0)
            return;

        const int minMidi = 45;
        const int maxMidi = 90;
        const int bins = maxMidi - minMidi + 1;

        var prev = new float[bins];
        var curr = new float[bins];
        var backtrack = new int[frames.Count, bins];

        for (int i = 0; i < bins; i++)
            prev[i] = 1e6f;

        for (int t = 0; t < frames.Count; t++)
        {
            PitchFrameData frame = frames[t];
            for (int m = 0; m < bins; m++)
            {
                float midi = minMidi + m;
                float obsCost = frame.valid
                    ? Mathf.Abs(midi - frame.midiFloat) * (1.15f - frame.confidence * 0.35f)
                    : 8f;

                float best = 1e6f;
                int bestPrev = 0;
                for (int pm = 0; pm < bins; pm++)
                {
                    float jump = Mathf.Abs(m - pm);
                    if (jump > maxJumpSemis)
                        continue;

                    float cost = prev[pm] + jump * jump * 0.16f + obsCost;
                    if (cost < best)
                    {
                        best = cost;
                        bestPrev = pm;
                    }
                }

                curr[m] = best;
                backtrack[t, m] = bestPrev;
            }

            var swap = prev;
            prev = curr;
            curr = swap;
        }

        int state = 0;
        for (int m = 1; m < bins; m++)
        {
            if (prev[m] < prev[state])
                state = m;
        }

        for (int t = frames.Count - 1; t >= 0; t--)
        {
            float midi = minMidi + state;
            PitchFrameData f = frames[t];
            if (f.valid || f.energy > 0f)
            {
                f.valid = true;
                f.midiFloat = f.valid ? f.midiFloat * 0.4f + midi * 0.6f : midi;
                f.frequency = MusicalNoteUtility.MidiToHz(f.midiFloat);
                f.confidence = Mathf.Max(f.confidence, 0.38f);
                frames[t] = f;
            }

            state = backtrack[t, state];
        }
    }
}
