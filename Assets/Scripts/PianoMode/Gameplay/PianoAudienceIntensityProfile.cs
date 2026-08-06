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

    public static Profile ResolveCurrentProfile() => ResolveProfile(
        UserSession.Instance != null ? UserSession.Instance.audienceIntensity : UserSession.DefaultAudienceIntensity);

    public static Profile ResolveProfile(string rawIntensity) => Normalize(rawIntensity) switch
    {
        Low => new Profile(Low, LowScoreForFullReaction),
        High => new Profile(High, HighScoreForFullReaction),
        _ => new Profile(Medium, MediumScoreForFullReaction)
    };

    public static string Normalize(string rawIntensity) =>
        string.IsNullOrWhiteSpace(rawIntensity) ? Medium : rawIntensity.Trim().ToLowerInvariant() switch
        {
            "baja" or "bajo" => Low,
            "alta" or "alto" => High,
            _ => Medium
        };
}
