using UnityEngine;
using System;

/// <summary>
/// Feedback visual del scoring: destellos de la línea de hit y guías de entrada en vivo.
/// </summary>
public partial class GameplayScoring
{
    private static readonly Color LiveGuideColor = new Color(0.45f, 0.26f, 0.12f, 1f);
    private const float HitFeedbackDuration = 0.3f;

    private void TriggerHitFeedback(GameNoteData note) =>
        ApplyVisualFeedbackToNoteStaffs(note, staff => staffHitFeedbackTime[staff] = Time.time);

    private void UpdateHitLineFeedback()
    {
        if (trebleStaff != null) UpdateStaffHitLineFeedback(trebleStaff);
        if (bassStaff != null) UpdateStaffHitLineFeedback(bassStaff);
    }

    private void UpdateStaffHitLineFeedback(StaffRenderer staff)
    {
        if (!staffHitFeedbackTime.TryGetValue(staff, out float lastFeedbackTime)) return;

        if (Time.time - lastFeedbackTime >= HitFeedbackDuration) staffHitFeedbackTime[staff] = 0f;
    }

    private void ShowLiveInputGuide(int midiNote)
    {
        // Staff por altura MIDI: evita escanear la partitura en el hot-path de Note On.
        StaffRenderer targetStaff = GetStaffForMidiNote(midiNote);
        if (targetStaff == null) return;

        targetStaff.ShowLiveInputIndicator(midiNote, LiveGuideColor);
        activeLiveGuides[midiNote] = targetStaff;
    }

    private void HideLiveInputGuide(int midiNote)
    {
        if (activeLiveGuides.TryGetValue(midiNote, out StaffRenderer targetStaff))
        {
            targetStaff.HideLiveInputIndicator(midiNote);
            activeLiveGuides.Remove(midiNote);
            return;
        }

        GetGuideStaffForMidiNote(midiNote)?.HideLiveInputIndicator(midiNote);
    }

    private void ClearLiveInputGuides()
    {
        trebleStaff?.ClearLiveInputIndicators();
        bassStaff?.ClearLiveInputIndicators();
        activeLiveGuides.Clear();
    }

    /// <summary>Pentagrama donde dibujar la guía: el de la nota esperada más cercana, o el que corresponde por altura.</summary>
    private StaffRenderer GetGuideStaffForMidiNote(int midiNote)
    {
        StaffRenderer expectedStaff = FindExpectedGuideStaff(midiNote, currentGameTime);
        return expectedStaff != null ? expectedStaff : GetStaffForMidiNote(midiNote);
    }

    /// <summary>Busca el pentagrama de la nota esperada más cercana en el tiempo que contenga esta nota MIDI.</summary>
    private StaffRenderer FindExpectedGuideStaff(int midiNote, float songTime)
    {
        if (expectedNotes.Count == 0) return null;

        float searchBefore = Mathf.Max(hitWindow, simultaneousChordGrace);
        float searchAfter = Mathf.Max(hitWindow * 2f, 0.25f);
        int startIndex = Mathf.Max(0, nextExpectedNoteIndex - 8);

        StaffRenderer bestStaff = null;
        float bestDistance = float.MaxValue;

        for (int i = startIndex; i < expectedNotes.Count; i++)
        {
            GameNoteData note = expectedNotes[i];

            if (note.time > songTime + searchAfter) break;
            if (songTime < note.time - searchBefore || songTime > note.time + note.duration + searchAfter) continue;
            if (Array.IndexOf(GetMidiNotes(note), midiNote) < 0) continue;

            StaffRenderer noteStaff = GetStaffForMidiNote(midiNote);
            if (noteStaff == null) continue;

            float distance = Mathf.Abs(note.time - songTime);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestStaff = noteStaff;
            }
        }

        return bestStaff;
    }

    private StaffRenderer GetStaffForMidiNote(int midiNote) => midiNote >= 60
        ? (trebleStaff != null ? trebleStaff : bassStaff)
        : (bassStaff != null ? bassStaff : trebleStaff);

    /// <summary>Aplica el feedback una sola vez por pentagrama, aunque el acorde toque varias notas del mismo.</summary>
    private void ApplyVisualFeedbackToNoteStaffs(GameNoteData note, Action<StaffRenderer> feedbackAction)
    {
        bool appliedTreble = false;
        bool appliedBass = false;

        foreach (int midiNote in GetMidiNotes(note))
        {
            StaffRenderer targetStaff = GetStaffForMidiNote(midiNote);
            if (targetStaff == null) continue;

            if (targetStaff == trebleStaff)
            {
                if (appliedTreble) continue;
                appliedTreble = true;
            }
            else if (targetStaff == bassStaff)
            {
                if (appliedBass) continue;
                appliedBass = true;
            }

            feedbackAction(targetStaff);
        }
    }
}
