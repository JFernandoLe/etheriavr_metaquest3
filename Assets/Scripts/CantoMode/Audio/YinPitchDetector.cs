using UnityEngine;

/// <summary>
/// Implementación YIN (de Cheveigné & Kawahara) para detección de pitch en tiempo real.
/// Portado desde el motor vocal de etheria_desktop (Python/NumPy).
/// </summary>
public static class YinPitchDetector
{
    public const int DefaultSampleRate = 48000;
    public const int DefaultFrameSize = 1024;
    public const float DefaultThreshold = 0.15f;
    public const float MinFrequency = 65f;
    public const float MaxFrequency = 1300f;

    public static float DetectPitch(float[] signal, int sampleRate, float threshold = DefaultThreshold)
    {
        return DetectPitch(signal, sampleRate, threshold, MinFrequency, MaxFrequency);
    }

    public static float DetectPitch(float[] signal, int sampleRate, float threshold, float minFrequency, float maxFrequency)
    {
        if (signal == null || signal.Length < 4)
            return -1f;

        int tauMin = Mathf.Max(2, sampleRate / (int)maxFrequency);
        int tauMax = Mathf.Min(signal.Length - 1, sampleRate / (int)minFrequency);
        if (tauMax <= tauMin + 2)
            return -1f;

        float mean = 0f;
        for (int i = 0; i < signal.Length; i++)
            mean += signal[i];
        mean /= signal.Length;

        float[] diff = new float[tauMax];
        for (int tau = tauMin; tau < tauMax; tau++)
        {
            float sum = 0f;
            int limit = signal.Length - tau;
            for (int i = 0; i < limit; i++)
            {
                float delta = (signal[i] - mean) - (signal[i + tau] - mean);
                sum += delta * delta;
            }
            diff[tau] = sum;
        }

        float[] cmnd = new float[tauMax];
        cmnd[0] = 1f;
        float runningSum = 0f;

        for (int tau = 1; tau < tauMax; tau++)
        {
            runningSum += diff[tau];
            cmnd[tau] = runningSum <= 0f ? 1f : diff[tau] * tau / runningSum;
        }

        for (int tau = tauMin; tau < tauMax; tau++)
        {
            if (cmnd[tau] >= threshold)
                continue;

            float refinedTau = tau;
            if (tau > 0 && tau + 1 < tauMax)
            {
                float s0 = cmnd[tau - 1];
                float s1 = cmnd[tau];
                float s2 = cmnd[tau + 1];
                float a = (s0 + s2 - 2f * s1) / 2f;
                float b = (s2 - s0) / 2f;
                if (Mathf.Abs(a) > 1e-6f)
                    refinedTau = tau - b / (2f * a);
            }

            return sampleRate / refinedTau;
        }

        return -1f;
    }

    public struct YinResult
    {
        public float PitchHz;
        public float Confidence;
        public bool IsValid;
    }

    /// <summary>
    /// YIN con valor de confianza (1 - cmnd mínimo). Útil para transcripción offline.
    /// </summary>
    public static YinResult DetectPitchDetailed(float[] signal, int sampleRate, float threshold = DefaultThreshold)
    {
        return DetectPitchDetailed(signal, sampleRate, threshold, MinFrequency, MaxFrequency);
    }

    public static YinResult DetectPitchDetailed(float[] signal, int sampleRate, float threshold, float minFrequency, float maxFrequency)
    {
        var invalid = new YinResult { PitchHz = -1f, Confidence = 0f, IsValid = false };
        if (signal == null || signal.Length < 4)
            return invalid;

        int tauMin = Mathf.Max(2, sampleRate / (int)maxFrequency);
        int tauMax = Mathf.Min(signal.Length - 1, sampleRate / (int)minFrequency);
        if (tauMax <= tauMin + 2)
            return invalid;

        float mean = 0f;
        for (int i = 0; i < signal.Length; i++)
            mean += signal[i];
        mean /= signal.Length;

        float[] diff = new float[tauMax];
        for (int tau = tauMin; tau < tauMax; tau++)
        {
            float sum = 0f;
            int limit = signal.Length - tau;
            for (int i = 0; i < limit; i++)
            {
                float delta = (signal[i] - mean) - (signal[i + tau] - mean);
                sum += delta * delta;
            }
            diff[tau] = sum;
        }

        float[] cmnd = new float[tauMax];
        cmnd[0] = 1f;
        float runningSum = 0f;

        for (int tau = 1; tau < tauMax; tau++)
        {
            runningSum += diff[tau];
            cmnd[tau] = runningSum <= 0f ? 1f : diff[tau] * tau / runningSum;
        }

        float bestCmnd = 1f;
        int bestTau = -1;

        for (int tau = tauMin; tau < tauMax; tau++)
        {
            if (cmnd[tau] >= threshold)
                continue;

            if (bestTau < 0 || cmnd[tau] < bestCmnd)
            {
                bestCmnd = cmnd[tau];
                bestTau = tau;
            }
        }

        if (bestTau < 0)
            return invalid;

        float refinedTau = bestTau;
        if (bestTau > 0 && bestTau + 1 < tauMax)
        {
            float s0 = cmnd[bestTau - 1];
            float s1 = cmnd[bestTau];
            float s2 = cmnd[bestTau + 1];
            float a = (s0 + s2 - 2f * s1) / 2f;
            float b = (s2 - s0) / 2f;
            if (Mathf.Abs(a) > 1e-6f)
                refinedTau = bestTau - b / (2f * a);
        }

        float pitch = sampleRate / refinedTau;
        float confidence = Mathf.Clamp01(1f - bestCmnd);

        return new YinResult
        {
            PitchHz = pitch,
            Confidence = confidence,
            IsValid = pitch > minFrequency && pitch < maxFrequency
        };
    }

    public static float ComputeEnergy(float[] signal)
    {
        if (signal == null || signal.Length == 0)
            return 0f;

        float sum = 0f;
        for (int i = 0; i < signal.Length; i++)
            sum += signal[i] * signal[i];
        return sum / signal.Length;
    }
}
