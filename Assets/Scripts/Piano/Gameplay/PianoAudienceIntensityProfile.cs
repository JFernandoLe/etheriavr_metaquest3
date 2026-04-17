using UnityEngine;

/// <summary>
/// Perfil de intensidad del publico virtual para el modo piano.
/// Define la dificultad para llegar al 100% y el clip de aplausos a utilizar.
/// </summary>
public static class PianoAudienceIntensityProfile
{
    public const string Low = "Bajo";
    public const string Medium = "Medio";
    public const string High = "Alto";

    public const float LowScoreForFullReaction = 92f;
    public const float MediumScoreForFullReaction = 82f;
    public const float HighScoreForFullReaction = 72f;

    public struct Profile
    {
        public string NormalizedIntensity;
        public float ScoreForFullReaction;

        public Profile(string normalizedIntensity, float scoreForFullReaction)
        {
            NormalizedIntensity = normalizedIntensity;
            ScoreForFullReaction = scoreForFullReaction;
        }
    }

    public static Profile ResolveCurrentProfile()
    {
        string rawIntensity = UserSession.Instance != null
            ? UserSession.Instance.audienceIntensity
            : UserSession.DefaultAudienceIntensity;

        return ResolveProfile(rawIntensity);
    }

    public static Profile ResolveProfile(string rawIntensity)
    {
        string normalizedIntensity = Normalize(rawIntensity);

        switch (normalizedIntensity)
        {
            case Low:
                return new Profile(Low, LowScoreForFullReaction);

            case High:
                return new Profile(High, HighScoreForFullReaction);

            default:
                return new Profile(Medium, MediumScoreForFullReaction);
        }
    }

    public static string Normalize(string rawIntensity)
    {
        if (string.IsNullOrWhiteSpace(rawIntensity))
        {
            return Medium;
        }

        string normalized = rawIntensity.Trim().ToLowerInvariant();

        if (normalized == "baja" || normalized == "bajo")
        {
            return Low;
        }

        if (normalized == "alta" || normalized == "alto")
        {
            return High;
        }

        return Medium;
    }
}