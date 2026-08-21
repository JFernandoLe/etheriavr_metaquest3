using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Clasificación determinista de tesitura vocal basada en reglas musicales.
/// Reemplaza el Random Forest de etheria_desktop.
/// </summary>
public static class TessituraClassifier
{
    public struct VocalRangeSample
    {
        public int Midi;
        public float Confidence;
        public float DurationSeconds;
    }

    public struct VocalRangeResult
    {
        public int MinMidi;
        public int MaxMidi;
        public float AverageMidi;
        public int RangeSemitones;
        public string Classification;
        public string MinNoteName;
        public string MaxNoteName;
        public int ValidSampleCount;
    }

    private struct TessituraRule
    {
        public string Name;
        public int MinMidi;
        public int MaxMidi;
    }

    private static readonly TessituraRule[] Rules =
    {
        new TessituraRule { Name = "Bajo", MinMidi = 40, MaxMidi = 55 },
        new TessituraRule { Name = "Baritono", MinMidi = 48, MaxMidi = 62 },
        new TessituraRule { Name = "Tenor", MinMidi = 53, MaxMidi = 67 },
        new TessituraRule { Name = "Contralto", MinMidi = 57, MaxMidi = 72 },
        new TessituraRule { Name = "Mezzosoprano", MinMidi = 62, MaxMidi = 77 },
        new TessituraRule { Name = "Soprano", MinMidi = 67, MaxMidi = 85 }
    };

    /// <summary>
    /// Analiza muestras de pitch y devuelve rango vocal + clasificación.
    /// Usa percentiles y frecuencia de aparición para filtrar outliers.
    /// </summary>
    public static VocalRangeResult Analyze(IReadOnlyList<VocalRangeSample> samples, int minimumSamples = 12)
    {
        var result = new VocalRangeResult
        {
            Classification = "Indeterminado",
            MinNoteName = "---",
            MaxNoteName = "---"
        };

        if (samples == null || samples.Count == 0)
            return result;

        var weighted = samples
            .Where(s => s.Midi >= 40 && s.Midi <= 85 && s.Confidence > 0.2f)
            .OrderBy(s => s.Midi)
            .ToList();

        if (weighted.Count < minimumSamples)
            return result;

        int minMidi = PercentileMidi(weighted, 0.10f);
        int maxMidi = PercentileMidi(weighted, 0.90f);

        if (maxMidi - minMidi < 4)
            return result;

        float avg = weighted.Average(s => s.Midi * Mathf.Max(0.25f, s.Confidence));

        result.MinMidi = minMidi;
        result.MaxMidi = maxMidi;
        result.AverageMidi = avg;
        result.RangeSemitones = maxMidi - minMidi;
        result.MinNoteName = MusicalNoteUtility.MidiToNoteName(minMidi);
        result.MaxNoteName = MusicalNoteUtility.MidiToNoteName(maxMidi);
        result.ValidSampleCount = weighted.Count;
        result.Classification = Classify(minMidi, maxMidi, avg);
        return result;
    }

    public static string MapToDatabaseEnum(string classification)
    {
        switch (classification)
        {
            case "Bajo": return "BASS";
            case "Baritono": return "BARITONE";
            case "Tenor": return "TENOR";
            case "Contralto": return "CONTRALTO";
            case "Mezzosoprano": return "MEZZO_SOPRANO";
            case "Soprano": return "SOPRANO";
            default: return classification.ToUpperInvariant();
        }
    }

    private static int PercentileMidi(List<VocalRangeSample> sorted, float percentile)
    {
        float totalWeight = sorted.Sum(s => Mathf.Max(0.1f, s.DurationSeconds * s.Confidence));
        if (totalWeight <= 0f)
            return sorted[Mathf.Clamp(Mathf.RoundToInt(percentile * (sorted.Count - 1)), 0, sorted.Count - 1)].Midi;

        float target = totalWeight * percentile;
        float cumulative = 0f;

        foreach (VocalRangeSample sample in sorted)
        {
            cumulative += Mathf.Max(0.1f, sample.DurationSeconds * sample.Confidence);
            if (cumulative >= target)
                return sample.Midi;
        }

        return sorted[sorted.Count - 1].Midi;
    }

    private static string Classify(int minMidi, int maxMidi, float avgMidi)
    {
        string best = "Indeterminado";
        float bestScore = float.MinValue;

        foreach (TessituraRule rule in Rules)
        {
            float center = (rule.MinMidi + rule.MaxMidi) * 0.5f;
            float width = rule.MaxMidi - rule.MinMidi;

            bool minInside = minMidi >= rule.MinMidi - 2 && minMidi <= rule.MaxMidi + 2;
            bool maxInside = maxMidi >= rule.MinMidi - 2 && maxMidi <= rule.MaxMidi + 2;
            bool avgInside = avgMidi >= rule.MinMidi && avgMidi <= rule.MaxMidi;

            float overlapMin = Mathf.Max(0, Mathf.Min(maxMidi, rule.MaxMidi) - Mathf.Max(minMidi, rule.MinMidi));
            float overlapRatio = overlapMin / Mathf.Max(1, maxMidi - minMidi);

            float score = overlapRatio * 3f;
            if (minInside) score += 1f;
            if (maxInside) score += 1f;
            if (avgInside) score += 1.5f;
            score -= Mathf.Abs(avgMidi - center) / width;

            if (score > bestScore)
            {
                bestScore = score;
                best = rule.Name;
            }
        }

        return best;
    }
}
