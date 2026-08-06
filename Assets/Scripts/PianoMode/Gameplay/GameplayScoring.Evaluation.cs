using UnityEngine;

/// <summary>
/// Evaluación de notas esperadas y cálculo de los porcentajes de armonía y ritmo.
/// </summary>
public partial class GameplayScoring
{
    /// <summary>Cierra las notas cuya ventana de acierto ya expiró.</summary>
    private void DetectMissedNotes()
    {
        while (nextExpectedNoteIndex < expectedNotes.Count)
        {
            GameNoteData note = expectedNotes[nextExpectedNoteIndex];
            if (currentGameTime <= note.time + note.duration + hitWindow) break;

            FinalizeExpectedNote(nextExpectedNoteIndex);
            nextExpectedNoteIndex++;
        }
    }

    /// <summary>
    /// Evalúa definitivamente una nota o acorde: calcula unidades acertadas, perfectas
    /// y ponderadas, actualiza los acumuladores globales y dispara los eventos.
    /// </summary>
    private void FinalizeExpectedNote(int noteIndex)
    {
        if (!noteScores.TryGetValue(noteIndex, out GameNoteScore score) || score.wasEvaluated) return;

        GameNoteData note = expectedNotes[noteIndex];
        int[] midiNotes = GetMidiNotes(note);
        int successfulUnits = 0;
        int perfectUnits = 0;
        float weightedUnits = 0f;

        foreach (int midiNote in midiNotes)
        {
            float heldDuration = score.heldDurations.TryGetValue(midiNote, out float value) ? value : 0f;
            float holdRatio = note.duration > 0.0001f
                ? heldDuration / note.duration
                : (heldDuration > 0f ? 1f : 0f);
            holdRatio = Mathf.Clamp01(holdRatio);

            weightedUnits += holdRatio;
            totalDurationQualityUnits += holdRatio;

            float onsetOffset = score.onsetOffsets.TryGetValue(midiNote, out float storedOffset) ? storedOffset : hitWindow;
            totalOnsetQualityUnits += EvaluateTimingQuality(onsetOffset);

            if (holdRatio >= minimumHoldForHit) successfulUnits++;
            if (holdRatio >= perfectHoldThreshold) perfectUnits++;
        }

        score.successfulUnits = successfulUnits;
        score.perfectUnits = perfectUnits;
        score.weightedUnits = weightedUnits;
        score.wasHit = weightedUnits > 0f;
        score.wasPerfect = successfulUnits == midiNotes.Length && perfectUnits == midiNotes.Length;
        score.wasEvaluated = true;

        float normalizedScore = midiNotes.Length > 0 ? weightedUnits / midiNotes.Length : 0f;
        evaluatedPlayableNoteUnits += midiNotes.Length;
        weightedHitPlayableNoteUnits += weightedUnits;
        totalSuccessfulPlayableNoteUnits += successfulUnits;

        if (midiNotes.Length > 1)
        {
            chordCoverageAccumulated += normalizedScore;
            totalChordEvents++;
        }

        if (enableHarmonyAnalysisDebugLogs)
        {
            float chordCoveragePercent = totalChordEvents > 0 ? (chordCoverageAccumulated / totalChordEvents) * 100f : 0f;
            string outcome = weightedUnits > 0f ? (score.wasPerfect ? "PERFECT" : "HIT") : "MISS";

            LogHarmony($"EvaluacionFinal idx={noteIndex} {DescribeExpectedNote(note)} | " +
                       $"detalle=[{DescribePerNoteBreakdown(score, midiNotes)}] " +
                       $"exitosas={successfulUnits}/{midiNotes.Length} perfectas={perfectUnits}/{midiNotes.Length} " +
                       $"scoreNormalizado={normalizedScore:F3} coberturaAcordes={chordCoveragePercent:F1}% " +
                       $"armoniaLive={GetLiveHarmonyPercentage():F1}% resultado={outcome}");
        }

        OnNoteEvaluated?.Invoke(note, normalizedScore, successfulUnits, midiNotes.Length);

        if (weightedUnits > 0f)
        {
            perfectPlayableNoteUnits += perfectUnits;
            TriggerHitFeedback(note);
            OnNoteHit?.Invoke(note, score.wasPerfect);
            return;
        }

        ApplyVisualFeedbackToNoteStaffs(note, staff => staff.SetHitLineError());
        OnNoteMissed?.Invoke(note);
    }

    /// <summary>Genera el resultado final de la partida.</summary>
    public GameplayResults CalculateFinalScore()
    {
        float accuracy = expectedNotes.Count > 0 ? CurrentAccuracyPercent : 0f;

        float noteCoverage = totalPlayableNoteUnits > 0f
            ? (totalSuccessfulPlayableNoteUnits / totalPlayableNoteUnits) * 100f
            : 0f;

        float chordCoverage = totalChordEvents > 0
            ? (chordCoverageAccumulated / totalChordEvents) * 100f
            : noteCoverage;

        float onsetTiming = totalPlayableNoteUnits > 0f ? (totalOnsetQualityUnits / totalPlayableNoteUnits) * 100f : 0f;
        float durationTiming = totalPlayableNoteUnits > 0f ? (totalDurationQualityUnits / totalPlayableNoteUnits) * 100f : 0f;

        float harmony = CombineHarmony(noteCoverage, chordCoverage);
        float rhythm = CombineRhythm(onsetTiming, durationTiming);

        return new GameplayResults
        {
            song_name = currentSong.song_name ?? currentSong.song_title,
            total_notes = totalPlayableNoteUnits,
            notes_hit = weightedHitPlayableNoteUnits,
            perfect_notes = perfectPlayableNoteUnits,
            notes_missed = Mathf.Max(totalPlayableNoteUnits - weightedHitPlayableNoteUnits, 0f),
            accuracy_percentage = accuracy,
            note_coverage_percentage = noteCoverage,
            chord_coverage_percentage = chordCoverage,
            onset_timing_percentage = onsetTiming,
            duration_timing_percentage = durationTiming,
            harmony_percentage = harmony,
            rhythm_percentage = rhythm,
            global_percentage = Mathf.Clamp(0.6f * harmony + 0.4f * rhythm, 0f, 100f),
            game_duration = currentGameTime,
            timestamp = System.DateTime.Now
        };
    }

    /// <summary>Armonía en vivo, calculada solo sobre las notas ya evaluadas.</summary>
    public float GetLiveHarmonyPercentage()
    {
        float noteCoverage = evaluatedPlayableNoteUnits > 0f
            ? (totalSuccessfulPlayableNoteUnits / evaluatedPlayableNoteUnits) * 100f
            : 0f;

        float chordCoverage = totalChordEvents > 0
            ? (chordCoverageAccumulated / totalChordEvents) * 100f
            : noteCoverage;

        return CombineHarmony(noteCoverage, chordCoverage);
    }

    /// <summary>Ritmo en vivo, calculado solo sobre las notas ya evaluadas.</summary>
    public float GetLiveRhythmPercentage()
    {
        float onsetTiming = evaluatedPlayableNoteUnits > 0f ? (totalOnsetQualityUnits / evaluatedPlayableNoteUnits) * 100f : 0f;
        float durationTiming = evaluatedPlayableNoteUnits > 0f ? (totalDurationQualityUnits / evaluatedPlayableNoteUnits) * 100f : 0f;

        return CombineRhythm(onsetTiming, durationTiming);
    }

    private static float CombineHarmony(float noteCoverage, float chordCoverage) =>
        Mathf.Clamp(0.65f * noteCoverage + 0.35f * chordCoverage, 0f, 100f);

    private float CombineRhythm(float onsetTiming, float durationTiming) =>
        Mathf.Clamp((onsetWeightInRhythm * onsetTiming) + ((1f - onsetWeightInRhythm) * durationTiming), 0f, 100f);

    private float EvaluateTimingQuality(float onsetOffset) => HarmonyEngine.EvaluarCalidadTiming(
        onsetOffset, perfectTimingWindow, Mathf.Max(rhythmScoringWindow, hitWindow, 0.0001f));
}
