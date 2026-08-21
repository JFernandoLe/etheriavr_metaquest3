using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Entrada MIDI: registro de pulsaciones, emparejado de onsets con la partitura
/// y acumulación del tiempo sostenido por nota.
/// </summary>
public partial class GameplayScoring
{
    private const string FeedbackLatencyTag = "[FeedbackLatency]";

    /// <summary>
    /// Registra la pulsación. La detección del hit visual ocurre en MusicNote.Update(),
    /// cuando la nota alcanza la línea de hit mientras se mantiene presionada.
    /// </summary>
    public void ProcessMidiNoteOn(int midiNote, int velocity)
    {
        if (!isGameActive) return;

        float noteOnTime = GetCurrentSongTime();
        DateTimeOffset noteOnReceivedAt = DateTimeOffset.UtcNow;

        currentlyPressedNotes.Add(midiNote);
        ShowLiveInputGuide(midiNote);

        bool matchedExpectedWindow = TrackOnsetTiming(midiNote, noteOnTime);
        RegisterFeedbackLatencyNoteOn(midiNote, noteOnReceivedAt, matchedExpectedWindow);

        if (enableHarmonyAnalysisDebugLogs)
        {
            LogHarmony($"NoteOn t={noteOnTime:F3}s entrada={FormatMidiNoteName(midiNote)}({midiNote}) vel={velocity} " +
                       $"presionadas=[{FormatCurrentlyPressedNotes()}] coincidenciaVentana={(matchedExpectedWindow ? "SI" : "NO")}");
        }

        if (!matchedExpectedWindow) publicSystem?.OnWrongNoteDetected(midiNote);

        if (activePressStates.TryGetValue(midiNote, out ActivePressState pressState))
        {
            pressState.pressStartTime = noteOnTime;
            pressState.lastProcessedTime = noteOnTime;
        }
        else
        {
            activePressStates[midiNote] = new ActivePressState
            {
                pressStartTime = noteOnTime,
                lastProcessedTime = noteOnTime
            };
        }
    }

    private void ProcessMidiNoteOff(int midiNote, int velocity)
    {
        float noteOffTime = GetCurrentSongTime();

        if (activePressStates.TryGetValue(midiNote, out ActivePressState pressState))
        {
            AccumulateHeldDurationForMidiNote(midiNote, pressState, pressState.lastProcessedTime, noteOffTime);
            activePressStates.Remove(midiNote);
        }

        currentlyPressedNotes.Remove(midiNote);
        HideLiveInputGuide(midiNote);
    }

    private void RegisterFeedbackLatencyNoteOn(int midiNote, DateTimeOffset noteOnReceivedAt, bool isCorrect)
    {
        if (!pendingFeedbackLatencyByMidiNote.TryGetValue(midiNote, out Queue<PendingFeedbackLatencyEvent> pendingEvents))
        {
            pendingEvents = new Queue<PendingFeedbackLatencyEvent>();
            pendingFeedbackLatencyByMidiNote[midiNote] = pendingEvents;
        }

        pendingEvents.Enqueue(new PendingFeedbackLatencyEvent
        {
            receivedAt = noteOnReceivedAt,
            isCorrect = isCorrect
        });

        while (pendingEvents.Count > 12) pendingEvents.Dequeue();
    }

    /// <summary>
    /// Instrumentación: mide el retardo entre recibir el MIDI y dibujar su feedback visual.
    /// </summary>
    public void ReportVisualFeedbackLatency(int midiNote, string visualObjectName)
    {
        if (!enableFeedbackLatencyLogs) return;

        DateTimeOffset visualTriggeredAt = DateTimeOffset.UtcNow;

        if (!pendingFeedbackLatencyByMidiNote.TryGetValue(midiNote, out Queue<PendingFeedbackLatencyEvent> pendingEvents)
            || pendingEvents.Count == 0)
        {
            Debug.Log($"{FeedbackLatencyTag} midi={midiNote} isCorrect=False deltaMs=unavailable visualObject={visualObjectName}");
            return;
        }

        PendingFeedbackLatencyEvent pendingEvent = pendingEvents.Dequeue();
        double latencyMs = (visualTriggeredAt - pendingEvent.receivedAt).TotalMilliseconds;

        Debug.Log($"{FeedbackLatencyTag} midi={midiNote} isCorrect={pendingEvent.isCorrect} deltaMs={latencyMs:F3} " +
                  $"receiveTimestamp={pendingEvent.receivedAt:O} visualTimestamp={visualTriggeredAt:O} visualObject={visualObjectName}");
    }

    /// <summary>
    /// Busca la primera nota esperada compatible con la pulsación y guarda su desviación de onset.
    /// </summary>
    /// <returns>True si la pulsación cae dentro de la ventana de alguna nota esperada.</returns>
    private bool TrackOnsetTiming(int midiNote, float noteOnTime)
    {
        bool matchedExpectedWindow = false;

        for (int i = nextExpectedNoteIndex; i < expectedNotes.Count; i++)
        {
            GameNoteData note = expectedNotes[i];

            if (note.time - hitWindow > noteOnTime) break;
            if (noteOnTime > note.time + note.duration + hitWindow) continue;
            if (!noteScores.TryGetValue(i, out GameNoteScore score) || score.wasEvaluated) continue;
            if (score.onsetOffsets.ContainsKey(midiNote)) continue;

            int[] midiNotes = GetMidiNotes(note);
            if (Array.IndexOf(midiNotes, midiNote) < 0) continue;

            float latestAcceptedTime = note.time + Mathf.Max(hitWindow, simultaneousChordGrace);
            if (noteOnTime < note.time - hitWindow || noteOnTime > latestAcceptedTime) continue;

            float onsetOffset = Mathf.Abs(noteOnTime - note.time);
            score.onsetOffsets[midiNote] = onsetOffset;
            matchedExpectedWindow = true;

            if (enableHarmonyAnalysisDebugLogs)
            {
                LogHarmony($"ComparacionEnVivo idx={i} {DescribeExpectedNote(note)} | " +
                           $"entrada={FormatMidiNoteName(midiNote)}({midiNote}) offset={onsetOffset:F3}s " +
                           $"hitWindow={hitWindow:F3}s graciaAcorde={simultaneousChordGrace:F3}s resultado=MATCH");
            }

            if (!score.liveReactionAwardedNotes.Contains(midiNote) && publicSystem != null)
            {
                score.liveReactionAwardedNotes.Add(midiNote);
                publicSystem.OnLiveWindowMatched(note, EvaluateTimingQuality(onsetOffset), midiNotes.Length);
            }

            break;
        }

        if (!matchedExpectedWindow && enableHarmonyAnalysisDebugLogs)
        {
            LogHarmony($"ComparacionEnVivo entrada={FormatMidiNoteName(midiNote)}({midiNote}) t={noteOnTime:F3}s " +
                       $"resultado=MISS siguienteIdx={nextExpectedNoteIndex}");
        }

        return matchedExpectedWindow;
    }

    private void AccumulateHeldDurations()
    {
        if (expectedNotes.Count == 0 || activePressStates.Count == 0) return;

        foreach (KeyValuePair<int, ActivePressState> pressedNote in activePressStates)
        {
            float intervalStart = pressedNote.Value.lastProcessedTime;
            if (currentGameTime <= intervalStart) continue;

            AccumulateHeldDurationForMidiNote(pressedNote.Key, pressedNote.Value, intervalStart, currentGameTime);
            pressedNote.Value.lastProcessedTime = currentGameTime;
        }
    }

    /// <summary>
    /// Suma a cada nota esperada el solape entre el intervalo pulsado y su ventana temporal.
    /// </summary>
    private void AccumulateHeldDurationForMidiNote(int midiNote, ActivePressState pressState, float intervalStart, float intervalEnd)
    {
        for (int i = nextExpectedNoteIndex; i < expectedNotes.Count; i++)
        {
            GameNoteData note = expectedNotes[i];

            if (intervalEnd < note.time) break;
            if (intervalStart > note.time + note.duration) continue;
            if (!noteScores.TryGetValue(i, out GameNoteScore score) || score.wasEvaluated) continue;
            if (!score.heldDurations.ContainsKey(midiNote)) continue;

            // Gracia de acorde: si la tecla entró justo después del inicio, se cuenta desde el inicio de la nota.
            bool qualifiesForSimultaneousGrace =
                score.heldDurations[midiNote] <= 0.0001f &&
                pressState != null &&
                pressState.pressStartTime >= note.time &&
                pressState.pressStartTime - note.time <= simultaneousChordGrace;

            float effectiveIntervalStart = qualifiesForSimultaneousGrace
                ? Mathf.Min(intervalStart, note.time)
                : intervalStart;

            float overlapStart = Mathf.Max(effectiveIntervalStart, note.time);
            float overlapEnd = Mathf.Min(intervalEnd, note.time + note.duration);
            float overlap = Mathf.Max(0f, overlapEnd - overlapStart);

            if (overlap > 0f) score.heldDurations[midiNote] += overlap;
        }
    }
}
