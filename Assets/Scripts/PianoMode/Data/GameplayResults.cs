/// <summary>
/// Resultados finales de una partida de piano, tal como se envían al backend y al panel de resultados.
/// </summary>
[System.Serializable]
public class GameplayResults
{
    public string song_name;
    public string mode_name;
    public float total_notes;
    public float notes_hit;
    public int perfect_notes;
    public float notes_missed;
    public float accuracy_percentage;
    public float note_coverage_percentage;
    public float chord_coverage_percentage;
    public float onset_timing_percentage;
    public float duration_timing_percentage;
    public float harmony_percentage;
    public float rhythm_percentage;
    public float global_percentage;
    public float game_duration;
    public System.DateTime timestamp;
}
